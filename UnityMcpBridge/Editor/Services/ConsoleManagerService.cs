using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityMcpBridge.Editor.Models;

namespace UnityMcpBridge.Editor.Services
{
    /// <summary>
    /// 控制台管理器服务类
    /// 负责与控制台管理器的进程间通信
    /// </summary>
    public static class ConsoleManagerService
    {
        private const string ConsoleManagerProcessName = "UnityMcpManager";
        private const string StatusFileName = "unity_mcp_status.json";
        private static readonly string StatusFilePath = Path.Combine(Application.temporaryCachePath, StatusFileName);

        /// <summary>
        /// 检查控制台管理器是否正在运行
        /// </summary>
        /// <returns>是否正在运行</returns>
        public static bool IsConsoleManagerRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName(ConsoleManagerProcessName);
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"检查控制台管理器状态时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 启动控制台管理器（兼容旧接口，无需指定工作目录）
        /// </summary>
        /// <param name="config">服务器管理配置</param>
        /// <returns>是否启动成功</returns>
        public static async Task<bool> StartConsoleManagerAsync(ServerManagementConfig config)
        {
            // 兼容旧接口，默认不指定工作目录
            return await StartConsoleManagerAsync(config, null);
        }

        /// <summary>
        /// 启动控制台管理器（支持自定义工作目录）
        /// </summary>
        /// <param name="config">服务器管理配置</param>
        /// <param name="workingDirectory">工作目录（可选）</param>
        /// <returns>是否启动成功</returns>
        public static async Task<bool> StartConsoleManagerAsync(ServerManagementConfig config, string workingDirectory)
        {
            try
            {
                // 检查是否已经在运行
                if (IsConsoleManagerRunning())
                {
                    UnityEngine.Debug.Log("控制台管理器已在运行");
                    return true;
                }

                // 构建可执行文件路径
                string exePath = GetConsoleManagerPath(config);
                if (!File.Exists(exePath))
                {
                    UnityEngine.Debug.LogError($"控制台管理器可执行文件不存在: {exePath}");
                    return false;
                }

                // 启动进程，设置工作目录（如未指定则不设置）
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "service", // 以服务模式启动
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    startInfo.WorkingDirectory = workingDirectory; // 设置工作目录
                }

                var process = Process.Start(startInfo);
                if (process == null)
                {
                    UnityEngine.Debug.LogError("无法启动控制台管理器进程");
                    return false;
                }

                // 等待启动完成
                var startupSuccess = await WaitForConsoleManagerStartupAsync(config.consoleManagerStartupTimeout);
                if (!startupSuccess)
                {
                    UnityEngine.Debug.LogError("控制台管理器启动超时");
                    try
                    {
                        process.Kill();
                    }
                    catch { }
                    return false;
                }

                UnityEngine.Debug.Log("控制台管理器启动成功");
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"启动控制台管理器时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 停止控制台管理器
        /// </summary>
        /// <returns>是否停止成功</returns>
        public static async Task<bool> StopConsoleManagerAsync()
        {
            try
            {
                var processes = Process.GetProcessesByName(ConsoleManagerProcessName);
                if (processes.Length == 0)
                {
                    UnityEngine.Debug.Log("控制台管理器未在运行");
                    return true;
                }

                foreach (var process in processes)
                {
                    try
                    {
                        // 尝试优雅关闭
                        process.CloseMainWindow();

                        // 等待进程退出
                        var exited = await Task.Run(() => process.WaitForExit(5000));
                        if (!exited)
                        {
                            // 强制终止
                            process.Kill();
                        }
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"停止进程 {process.Id} 时出错: {ex.Message}");
                    }
                }

                UnityEngine.Debug.Log("控制台管理器已停止");
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"停止控制台管理器时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取服务器状态信息
        /// </summary>
        /// <returns>服务器状态信息</returns>
        public static ServerStatusInfo GetServerStatus()
        {
            var status = new ServerStatusInfo
            {
                consoleManagerRunning = IsConsoleManagerRunning(),
                lastHealthCheck = DateTime.Now
            };

            try
            {
                // 尝试从状态文件读取信息
                if (File.Exists(StatusFilePath))
                {
                    var statusJson = File.ReadAllText(StatusFilePath);
                    var statusData = JsonUtility.FromJson<ServerStatusInfo>(statusJson);
                    if (statusData != null)
                    {
                        status.isRunning = statusData.isRunning;
                        status.unityPort = statusData.unityPort;
                        status.mcpPort = statusData.mcpPort;
                        status.isHealthy = statusData.isHealthy;
                        status.errorMessage = statusData.errorMessage;
                    }
                }

                // 如果控制台管理器运行，尝试通过TCP连接获取状态
                if (status.consoleManagerRunning)
                {
                    var tcpStatus = GetStatusViaTcp();
                    if (tcpStatus != null)
                    {
                        status.isRunning = tcpStatus.isRunning;
                        status.unityPort = tcpStatus.unityPort;
                        status.mcpPort = tcpStatus.mcpPort;
                        status.isHealthy = tcpStatus.isHealthy;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"获取服务器状态时出错: {ex.Message}");
                status.errorMessage = ex.Message;
            }

            return status;
        }

        /// <summary>
        /// 检查服务器健康状态
        /// </summary>
        /// <returns>是否健康</returns>
        public static async Task<bool> CheckServerHealthAsync()
        {
            try
            {
                var status = GetServerStatus();
                if (!status.consoleManagerRunning)
                {
                    return false;
                }

                // 检查Unity端口连接
                if (status.unityPort > 0)
                {
                    var unityHealthy = await TestPortConnectionAsync("localhost", status.unityPort);
                    if (!unityHealthy)
                    {
                        return false;
                    }
                }

                // 检查MCP端口连接
                if (status.mcpPort > 0)
                {
                    var mcpHealthy = await TestPortConnectionAsync("localhost", status.mcpPort);
                    if (!mcpHealthy)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"健康检查时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取控制台管理器可执行文件路径
        /// </summary>
        /// <param name="config">配置</param>
        /// <returns>可执行文件路径</returns>
        private static string GetConsoleManagerPath(ServerManagementConfig config)
        {
            // 如果是相对路径，转换为绝对路径
            if (!Path.IsPathRooted(config.consoleManagerPath))
            {
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (!string.IsNullOrEmpty(projectRoot))
                {
                    return Path.Combine(projectRoot, config.consoleManagerPath);
                }
            }

            return config.consoleManagerPath;
        }

        /// <summary>
        /// 等待控制台管理器启动
        /// </summary>
        /// <param name="timeoutSeconds">超时时间（秒）</param>
        /// <returns>是否启动成功</returns>
        private static async Task<bool> WaitForConsoleManagerStartupAsync(int timeoutSeconds)
        {
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);

            while (DateTime.Now - startTime < timeout)
            {
                if (IsConsoleManagerRunning())
                {
                    // 等待一段时间确保完全启动
                    await Task.Delay(1000);
                    return true;
                }

                await Task.Delay(500);
            }

            return false;
        }

        /// <summary>
        /// 通过TCP连接获取状态信息
        /// </summary>
        /// <returns>状态信息</returns>
        private static ServerStatusInfo? GetStatusViaTcp()
        {
            try
            {
                // 这里可以实现TCP通信获取状态
                // 暂时返回null，使用文件方式
                return null;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"通过TCP获取状态时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 测试端口连接
        /// </summary>
        /// <param name="host">主机</param>
        /// <param name="port">端口</param>
        /// <returns>是否连接成功</returns>
        private static async Task<bool> TestPortConnectionAsync(string host, int port)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(3000); // 3秒超时

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                return completedTask == connectTask && client.Connected;
            }
            catch
            {
                return false;
            }
        }
    }
}