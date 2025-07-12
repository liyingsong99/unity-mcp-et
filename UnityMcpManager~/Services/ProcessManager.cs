using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UnityMcpManager.Models;
using UnityMcpManager.Utils;

namespace UnityMcpManager.Services
{
    /// <summary>
    /// 进程管理服务
    /// </summary>
    public class ProcessManager
    {
        private readonly ILogger<ProcessManager> _logger;
        private readonly McpConfig _config;
        private readonly PortManager _portManager;
        private Process? _mcpServerProcess;
        private readonly object _lock = new object();
        private ServerStatus _currentStatus = ServerStatus.Stopped;

        public ProcessManager(ILogger<ProcessManager> logger, McpConfig config, PortManager portManager)
        {
            _logger = logger;
            _config = config;
            _portManager = portManager;
        }

        /// <summary>
        /// 当前服务器状态
        /// </summary>
        public ServerStatus CurrentStatus
        {
            get
            {
                lock (_lock)
                {
                    return _currentStatus;
                }
            }
        }

        /// <summary>
        /// 启动MCP服务器
        /// </summary>
        /// <returns>是否启动成功</returns>
        public async Task<bool> StartMcpServerAsync()
        {
            lock (_lock)
            {
                if (_currentStatus == ServerStatus.Running || _currentStatus == ServerStatus.Starting)
                {
                    _logger.LogInformation("MCP服务器已在运行或正在启动中");
                    return true;
                }

                _currentStatus = ServerStatus.Starting;
            }

            try
            {
                // 检查Python是否可用
                if (!ProcessUtils.IsExecutableAvailable(_config.PythonExecutable))
                {
                    _logger.LogError($"Python可执行文件 {_config.PythonExecutable} 不可用");
                    SetStatus(ServerStatus.Error);
                    return false;
                }

                // 检查服务器脚本是否存在
                if (!File.Exists(_config.ServerScriptPath))
                {
                    _logger.LogError($"MCP服务器脚本不存在：{_config.ServerScriptPath}");
                    SetStatus(ServerStatus.Error);
                    return false;
                }

                // 分配端口
                var unityPort = _portManager.AllocatePort("Unity", _config.DefaultUnityPort);
                var mcpPort = _portManager.AllocatePort("MCP", _config.DefaultMcpPort);

                _logger.LogInformation($"为MCP服务器分配端口 - Unity: {unityPort}, MCP: {mcpPort}");

                // 构建启动参数 - 将相对路径转换为绝对路径
                var scriptPath = Path.GetFullPath(_config.ServerScriptPath);
                var arguments = $"\"{scriptPath}\"";

                // 设置环境变量
                var environmentVariables = new Dictionary<string, string>
                {
                    ["UNITY_PORT"] = unityPort.ToString(),
                    ["MCP_PORT"] = mcpPort.ToString()
                };

                // 启动进程
                _mcpServerProcess = ProcessUtils.StartProcess(
                    fileName: _config.PythonExecutable,
                    arguments: arguments,
                    workingDirectory: _config.WorkingDirectory,
                    redirectOutput: true,
                    environmentVariables: environmentVariables
                );

                _logger.LogInformation($"MCP服务器进程已启动，PID: {_mcpServerProcess.Id}");

                // 等待进程启动
                var startSuccess = await ProcessUtils.WaitForProcessStartAsync(_mcpServerProcess, _config.ProcessManagement.StartupTimeoutSeconds * 1000);

                if (!startSuccess)
                {
                    _logger.LogError("MCP服务器启动超时");
                    await StopMcpServerAsync();
                    SetStatus(ServerStatus.Error);
                    return false;
                }

                // 设置输出重定向事件
                _mcpServerProcess.OutputDataReceived += OnOutputDataReceived;
                _mcpServerProcess.ErrorDataReceived += OnErrorDataReceived;
                _mcpServerProcess.Exited += OnProcessExited;

                _mcpServerProcess.BeginOutputReadLine();
                _mcpServerProcess.BeginErrorReadLine();

                SetStatus(ServerStatus.Running);
                _logger.LogInformation("MCP服务器启动成功");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动MCP服务器时发生错误");
                SetStatus(ServerStatus.Error);
                return false;
            }
        }

        /// <summary>
        /// 停止MCP服务器
        /// </summary>
        /// <returns>是否停止成功</returns>
        public async Task<bool> StopMcpServerAsync()
        {
            lock (_lock)
            {
                if (_currentStatus == ServerStatus.Stopped || _currentStatus == ServerStatus.Stopping)
                {
                    _logger.LogInformation("MCP服务器已停止或正在停止中");
                    return true;
                }

                _currentStatus = ServerStatus.Stopping;
            }

            try
            {
                if (_mcpServerProcess != null && ProcessUtils.IsProcessRunning(_mcpServerProcess))
                {
                    _logger.LogInformation($"正在停止MCP服务器进程 (PID: {_mcpServerProcess.Id})");

                    var stopSuccess = await ProcessUtils.SafeKillProcessAsync(_mcpServerProcess, _config.ProcessManagement.ShutdownTimeoutSeconds * 1000);

                    if (!stopSuccess)
                    {
                        _logger.LogWarning("MCP服务器进程未能优雅停止，强制终止");
                        _mcpServerProcess.Kill();
                    }
                }

                // 释放端口
                _portManager.ReleasePort("Unity");
                _portManager.ReleasePort("MCP");

                _mcpServerProcess = null;
                SetStatus(ServerStatus.Stopped);
                _logger.LogInformation("MCP服务器已停止");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止MCP服务器时发生错误");
                SetStatus(ServerStatus.Error);
                return false;
            }
        }

        /// <summary>
        /// 重启MCP服务器
        /// </summary>
        /// <returns>是否重启成功</returns>
        public async Task<bool> RestartMcpServerAsync()
        {
            _logger.LogInformation("正在重启MCP服务器");

            var stopSuccess = await StopMcpServerAsync();
            if (!stopSuccess)
            {
                _logger.LogWarning("停止MCP服务器失败，但仍尝试重新启动");
            }

            // 等待一段时间再重新启动
            await Task.Delay(_config.ProcessManagement.RestartDelaySeconds * 1000);

            var startSuccess = await StartMcpServerAsync();
            if (startSuccess)
            {
                _logger.LogInformation("MCP服务器重启成功");
            }
            else
            {
                _logger.LogError("MCP服务器重启失败");
            }

            return startSuccess;
        }

        /// <summary>
        /// 检查MCP服务器是否正在运行
        /// </summary>
        /// <returns>是否正在运行</returns>
        public bool IsMcpServerRunning()
        {
            if (_mcpServerProcess == null)
                return false;

            return ProcessUtils.IsProcessRunning(_mcpServerProcess);
        }

        /// <summary>
        /// 获取MCP服务器进程信息
        /// </summary>
        /// <returns>进程信息</returns>
        public ProcessInfo? GetMcpServerProcessInfo()
        {
            if (_mcpServerProcess == null || !ProcessUtils.IsProcessRunning(_mcpServerProcess))
                return null;

            try
            {
                return new ProcessInfo
                {
                    ProcessId = _mcpServerProcess.Id,
                    ProcessName = _mcpServerProcess.ProcessName,
                    StartTime = _mcpServerProcess.StartTime,
                    MemoryUsage = ProcessUtils.GetProcessMemoryUsage(_mcpServerProcess),
                    CpuTime = ProcessUtils.GetProcessCpuTime(_mcpServerProcess),
                    HasExited = _mcpServerProcess.HasExited,
                    ExitCode = _mcpServerProcess.HasExited ? _mcpServerProcess.ExitCode : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取MCP服务器进程信息时发生错误");
                return null;
            }
        }

        /// <summary>
        /// 设置服务器状态
        /// </summary>
        /// <param name="status">新状态</param>
        private void SetStatus(ServerStatus status)
        {
            lock (_lock)
            {
                _currentStatus = status;
            }
        }

        /// <summary>
        /// 输出数据接收事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.LogInformation($"[MCP Server] {e.Data}");
            }
        }

        /// <summary>
        /// 错误数据接收事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.LogError($"[MCP Server Error] {e.Data}");
            }
        }

        /// <summary>
        /// 进程退出事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnProcessExited(object? sender, EventArgs e)
        {
            _logger.LogWarning("MCP服务器进程意外退出");
            SetStatus(ServerStatus.Error);

            // 释放端口
            _portManager.ReleasePort("Unity");
            _portManager.ReleasePort("MCP");
        }
    }

    /// <summary>
    /// 进程信息类
    /// </summary>
    public class ProcessInfo
    {
        /// <summary>
        /// 进程ID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// 进程名称
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// 启动时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 内存使用量（字节）
        /// </summary>
        public long MemoryUsage { get; set; }

        /// <summary>
        /// CPU使用时间
        /// </summary>
        public TimeSpan CpuTime { get; set; }

        /// <summary>
        /// 是否已退出
        /// </summary>
        public bool HasExited { get; set; }

        /// <summary>
        /// 退出代码
        /// </summary>
        public int? ExitCode { get; set; }
    }
}