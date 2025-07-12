using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UnityMcpManager.Models;
using UnityMcpManager.Services;

namespace UnityMcpManager
{
    /// <summary>
    /// Unity MCP服务器管理器主程序
    /// </summary>
    public class Program
    {
        private static IHost? _host;
        private static bool _isRunning = true;
        private static readonly string StatusFilePath = Path.Combine(Path.GetTempPath(), "unity_mcp_status.json");

        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== Unity MCP Server Manager ===");
            Console.WriteLine("Unity MCP服务器管理器 v1.0.0");
            Console.WriteLine();

            try
            {
                // 创建主机
                _host = CreateHostBuilder(args).Build();

                // 解析命令行参数
                var command = ParseCommandLine(args);

                // 执行命令
                await ExecuteCommand(command);

                // 如果是以服务模式运行，保持程序运行
                if (command == Command.Service)
                {
                    await RunAsService();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"程序启动失败：{ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// 创建主机构建器
        /// </summary>
        /// <param name="args">命令行参数</param>
        /// <returns>主机构建器</returns>
        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    // 注册服务
                    services.AddSingleton<ConfigManager>();

                    // 注册McpConfig，通过ConfigManager提供
                    services.AddSingleton<McpConfig>(provider =>
                    {
                        var configManager = provider.GetRequiredService<ConfigManager>();
                        return configManager.GetMcpConfig();
                    });

                    services.AddSingleton<PortManager>();
                    services.AddSingleton<ProcessManager>();
                    services.AddSingleton<HealthChecker>();
                })
                .ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Information);
                });

        /// <summary>
        /// 解析命令行参数
        /// </summary>
        /// <param name="args">命令行参数</param>
        /// <returns>命令类型</returns>
        private static Command ParseCommandLine(string[] args)
        {
            if (args.Length == 0)
            {
                return Command.Interactive;
            }

            var command = args[0].ToLower();
            return command switch
            {
                "start" => Command.Start,
                "stop" => Command.Stop,
                "restart" => Command.Restart,
                "status" => Command.Status,
                "health" => Command.Health,
                "config" => Command.Config,
                "ports" => Command.Ports,
                "service" => Command.Service,
                "help" => Command.Help,
                _ => Command.Interactive
            };
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="command">命令类型</param>
        private static async Task ExecuteCommand(Command command)
        {
            var serviceProvider = _host!.Services;
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                switch (command)
                {
                    case Command.Start:
                        await StartServer(serviceProvider);
                        break;
                    case Command.Stop:
                        await StopServer(serviceProvider);
                        break;
                    case Command.Restart:
                        await RestartServer(serviceProvider);
                        break;
                    case Command.Status:
                        ShowStatus(serviceProvider);
                        break;
                    case Command.Health:
                        await ShowHealth(serviceProvider);
                        break;
                    case Command.Config:
                        ShowConfig(serviceProvider);
                        break;
                    case Command.Ports:
                        ShowPorts(serviceProvider);
                        break;
                    case Command.Service:
                        logger.LogInformation("以服务模式运行");
                        break;
                    case Command.Help:
                        ShowHelp();
                        break;
                    case Command.Interactive:
                        await RunInteractive(serviceProvider);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "执行命令时发生错误");
                Console.WriteLine($"错误：{ex.Message}");
            }
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        private static async Task StartServer(IServiceProvider serviceProvider)
        {
            var processManager = serviceProvider.GetRequiredService<ProcessManager>();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            Console.WriteLine("正在启动MCP服务器...");

            var success = await processManager.StartMcpServerAsync();
            if (success)
            {
                Console.WriteLine("MCP服务器启动成功");
            }
            else
            {
                Console.WriteLine("MCP服务器启动失败");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        private static async Task StopServer(IServiceProvider serviceProvider)
        {
            var processManager = serviceProvider.GetRequiredService<ProcessManager>();

            Console.WriteLine("正在停止MCP服务器...");

            var success = await processManager.StopMcpServerAsync();
            if (success)
            {
                Console.WriteLine("MCP服务器已停止");
            }
            else
            {
                Console.WriteLine("MCP服务器停止失败");
            }
        }

        /// <summary>
        /// 重启服务器
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        private static async Task RestartServer(IServiceProvider serviceProvider)
        {
            var processManager = serviceProvider.GetRequiredService<ProcessManager>();

            Console.WriteLine("正在重启MCP服务器...");

            var success = await processManager.RestartMcpServerAsync();
            if (success)
            {
                Console.WriteLine("MCP服务器重启成功");
            }
            else
            {
                Console.WriteLine("MCP服务器重启失败");
            }
        }

        /// <summary>
        /// 显示状态
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        private static void ShowStatus(IServiceProvider serviceProvider)
        {
            var processManager = serviceProvider.GetRequiredService<ProcessManager>();
            var processInfo = processManager.GetMcpServerProcessInfo();

            Console.WriteLine("=== MCP服务器状态 ===");
            Console.WriteLine($"状态：{processManager.CurrentStatus}");
            Console.WriteLine($"正在运行：{processManager.IsMcpServerRunning()}");

            if (processInfo != null)
            {
                Console.WriteLine($"进程ID：{processInfo.ProcessId}");
                Console.WriteLine($"进程名称：{processInfo.ProcessName}");
                Console.WriteLine($"启动时间：{processInfo.StartTime}");
                Console.WriteLine($"内存使用：{processInfo.MemoryUsage / (1024 * 1024)}MB");
                Console.WriteLine($"CPU时间：{processInfo.CpuTime}");
            }
        }

        /// <summary>
        /// 显示健康状态
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        private static async Task ShowHealth(IServiceProvider serviceProvider)
        {
            var healthChecker = serviceProvider.GetRequiredService<HealthChecker>();
            var healthStatus = await healthChecker.CheckMcpServerHealthAsync();
            var stats = healthChecker.GetHealthCheckStats();

            Console.WriteLine("=== 健康检查状态 ===");
            Console.WriteLine($"检查时间：{healthStatus.CheckTime}");
            Console.WriteLine($"是否健康：{(healthStatus.IsHealthy ? "是" : "否")}");
            Console.WriteLine($"Unity端口：{healthStatus.UnityPort}");
            Console.WriteLine($"MCP端口：{healthStatus.McpPort}");
            Console.WriteLine($"内存使用：{healthStatus.MemoryUsageMB:F1}MB");
            Console.WriteLine($"运行时间：{healthStatus.Uptime}");
            Console.WriteLine($"连续失败次数：{stats.ConsecutiveFailures}");
            Console.WriteLine($"健康检查运行中：{(stats.IsHealthCheckRunning ? "是" : "否")}");

            if (healthStatus.Details.Count > 0)
            {
                Console.WriteLine("详细信息：");
                foreach (var detail in healthStatus.Details)
                {
                    Console.WriteLine($"  - {detail}");
                }
            }
        }

        /// <summary>
        /// 显示配置
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        private static void ShowConfig(IServiceProvider serviceProvider)
        {
            var configManager = serviceProvider.GetRequiredService<ConfigManager>();
            var configSummary = configManager.GetConfigSummary();
            var validation = configManager.ValidateConfig();

            Console.WriteLine("=== 配置信息 ===");
            Console.WriteLine($"Python可执行文件：{configSummary.PythonExecutable}");
            Console.WriteLine($"服务器脚本路径：{configSummary.ServerScriptPath}");
            Console.WriteLine($"工作目录：{configSummary.WorkingDirectory}");
            Console.WriteLine($"默认Unity端口：{configSummary.DefaultUnityPort}");
            Console.WriteLine($"默认MCP端口：{configSummary.DefaultMcpPort}");
            Console.WriteLine($"端口范围：{configSummary.PortRange}");
            Console.WriteLine($"健康检查间隔：{configSummary.HealthCheckInterval}秒");
            Console.WriteLine($"健康检查超时：{configSummary.HealthCheckTimeout}秒");
            Console.WriteLine($"最大重试次数：{configSummary.MaxRetries}");
            Console.WriteLine($"启动超时：{configSummary.StartupTimeout}秒");
            Console.WriteLine($"关闭超时：{configSummary.ShutdownTimeout}秒");
            Console.WriteLine($"重启延迟：{configSummary.RestartDelay}秒");

            Console.WriteLine();
            Console.WriteLine("=== 配置验证 ===");
            Console.WriteLine($"配置有效：{(validation.IsValid ? "是" : "否")}");

            if (validation.Errors.Count > 0)
            {
                Console.WriteLine("错误：");
                foreach (var error in validation.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }

            if (validation.Warnings.Count > 0)
            {
                Console.WriteLine("警告：");
                foreach (var warning in validation.Warnings)
                {
                    Console.WriteLine($"  - {warning}");
                }
            }
        }

        /// <summary>
        /// 显示端口信息
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        private static void ShowPorts(IServiceProvider serviceProvider)
        {
            var portManager = serviceProvider.GetRequiredService<PortManager>();
            var portReport = portManager.GetPortUsageReport();

            Console.WriteLine("=== 端口使用情况 ===");
            Console.WriteLine($"总端口数：{portReport.TotalPorts}");
            Console.WriteLine($"可用端口数：{portReport.AvailablePorts}");
            Console.WriteLine($"占用端口数：{portReport.OccupiedPorts}");

            Console.WriteLine();
            Console.WriteLine("已分配的服务：");
            foreach (var (service, port) in portReport.AllocatedServices)
            {
                Console.WriteLine($"  - {service}: {port}");
            }
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        private static void ShowHelp()
        {
            Console.WriteLine("=== 使用帮助 ===");
            Console.WriteLine("命令：");
            Console.WriteLine("  start     - 启动MCP服务器");
            Console.WriteLine("  stop      - 停止MCP服务器");
            Console.WriteLine("  restart   - 重启MCP服务器");
            Console.WriteLine("  status    - 显示服务器状态");
            Console.WriteLine("  health    - 显示健康检查状态");
            Console.WriteLine("  config    - 显示配置信息");
            Console.WriteLine("  ports     - 显示端口使用情况");
            Console.WriteLine("  service   - 以服务模式运行");
            Console.WriteLine("  help      - 显示此帮助信息");
            Console.WriteLine();
            Console.WriteLine("示例：");
            Console.WriteLine("  UnityMcpManager~.exe start");
            Console.WriteLine("  UnityMcpManager~.exe status");
            Console.WriteLine("  UnityMcpManager~.exe service");
        }

        /// <summary>
        /// 运行交互模式
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        private static async Task RunInteractive(IServiceProvider serviceProvider)
        {
            Console.WriteLine("=== 交互模式 ===");
            Console.WriteLine("输入 'help' 查看可用命令，输入 'quit' 退出");
            Console.WriteLine();

            while (_isRunning)
            {
                Console.Write("> ");
                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input))
                    continue;

                var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var command = parts[0].ToLower();

                try
                {
                    switch (command)
                    {
                        case "start":
                            await StartServer(serviceProvider);
                            break;
                        case "stop":
                            await StopServer(serviceProvider);
                            break;
                        case "restart":
                            await RestartServer(serviceProvider);
                            break;
                        case "status":
                            ShowStatus(serviceProvider);
                            break;
                        case "health":
                            await ShowHealth(serviceProvider);
                            break;
                        case "config":
                            ShowConfig(serviceProvider);
                            break;
                        case "ports":
                            ShowPorts(serviceProvider);
                            break;
                        case "help":
                            ShowHelp();
                            break;
                        case "quit":
                        case "exit":
                            _isRunning = false;
                            break;
                        default:
                            Console.WriteLine($"未知命令：{command}。输入 'help' 查看可用命令。");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"执行命令时发生错误：{ex.Message}");
                }

                Console.WriteLine();
            }
        }

        /// <summary>
        /// 以服务模式运行
        /// </summary>
        private static async Task RunAsService()
        {
            var serviceProvider = _host!.Services;
            var processManager = serviceProvider.GetRequiredService<ProcessManager>();
            var healthChecker = serviceProvider.GetRequiredService<HealthChecker>();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            // 启动健康检查
            healthChecker.StartHealthCheck();

            // 启动MCP服务器
            await StartServer(serviceProvider);

            Console.WriteLine("服务模式已启动，按 Ctrl+C 停止");

            // 等待用户中断
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _isRunning = false;
            };

            // 定期更新状态文件
            var statusUpdateTask = Task.Run(async () =>
            {
                while (_isRunning)
                {
                    UpdateStatusFile();
                    await Task.Delay(5000); // 每5秒更新一次状态
                }
            });

            // 保持程序运行
            while (_isRunning)
            {
                await Task.Delay(1000);
            }

            // 清理资源
            healthChecker.StopHealthCheck();
            await StopServer(serviceProvider);

            Console.WriteLine("服务已停止");
        }

        /// <summary>
        /// 命令类型枚举
        /// </summary>
        private enum Command
        {
            Start,
            Stop,
            Restart,
            Status,
            Health,
            Config,
            Ports,
            Service,
            Help,
            Interactive
        }

        /// <summary>
        /// 检查程序是否正在运行
        /// </summary>
        /// <returns>是否正在运行</returns>
        public static bool IsRunning()
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName("UnityMcpManager~");
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取当前状态信息
        /// </summary>
        /// <returns>状态信息JSON字符串</returns>
        public static string GetStatusInfo()
        {
            try
            {
                if (_host == null)
                {
                    return "{\"isRunning\":false,\"error\":\"Host not initialized\"}";
                }

                var serviceProvider = _host.Services;
                var processManager = serviceProvider.GetRequiredService<ProcessManager>();
                var portManager = serviceProvider.GetRequiredService<PortManager>();

                var status = new
                {
                    isRunning = processManager.IsMcpServerRunning(),
                    unityPort = portManager.GetAllocatedPort("Unity"),
                    mcpPort = portManager.GetAllocatedPort("MCP"),
                    startupMode = "ConsoleManager",
                    consoleManagerRunning = true,
                    lastHealthCheck = DateTime.Now,
                    isHealthy = processManager.CurrentStatus == ServerStatus.Running,
                    errorMessage = ""
                };

                return System.Text.Json.JsonSerializer.Serialize(status);
            }
            catch (Exception ex)
            {
                var errorStatus = new
                {
                    isRunning = false,
                    unityPort = 0,
                    mcpPort = 0,
                    startupMode = "ConsoleManager",
                    consoleManagerRunning = true,
                    lastHealthCheck = DateTime.Now,
                    isHealthy = false,
                    errorMessage = ex.Message
                };

                return System.Text.Json.JsonSerializer.Serialize(errorStatus);
            }
        }

        /// <summary>
        /// 更新状态文件
        /// </summary>
        private static void UpdateStatusFile()
        {
            try
            {
                var statusInfo = GetStatusInfo();
                File.WriteAllText(StatusFilePath, statusInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新状态文件时出错: {ex.Message}");
            }
        }
    }
}