using Microsoft.Extensions.Logging;
using UnityMcpManager.Models;
using UnityMcpManager.Utils;

namespace UnityMcpManager.Services
{
    /// <summary>
    /// 健康检查服务
    /// </summary>
    public class HealthChecker
    {
        private readonly ILogger<HealthChecker> _logger;
        private readonly McpConfig _config;
        private readonly ProcessManager _processManager;
        private readonly PortManager _portManager;
        private Timer? _healthCheckTimer;
        private int _consecutiveFailures = 0;
        private readonly object _lock = new object();

        public HealthChecker(ILogger<HealthChecker> logger, McpConfig config, ProcessManager processManager, PortManager portManager)
        {
            _logger = logger;
            _config = config;
            _processManager = processManager;
            _portManager = portManager;
        }

        /// <summary>
        /// 启动健康检查
        /// </summary>
        public void StartHealthCheck()
        {
            lock (_lock)
            {
                if (_healthCheckTimer != null)
                {
                    _logger.LogInformation("健康检查已在运行");
                    return;
                }

                _healthCheckTimer = new Timer(PerformHealthCheck, null, TimeSpan.Zero, TimeSpan.FromSeconds(_config.HealthCheck.IntervalSeconds));
                _logger.LogInformation($"健康检查已启动，检查间隔：{_config.HealthCheck.IntervalSeconds}秒");
            }
        }

        /// <summary>
        /// 停止健康检查
        /// </summary>
        public void StopHealthCheck()
        {
            lock (_lock)
            {
                _healthCheckTimer?.Dispose();
                _healthCheckTimer = null;
                _logger.LogInformation("健康检查已停止");
            }
        }

        /// <summary>
        /// 执行健康检查
        /// </summary>
        /// <param name="state">定时器状态</param>
        private async void PerformHealthCheck(object? state)
        {
            try
            {
                var healthStatus = await CheckMcpServerHealthAsync();

                if (healthStatus.IsHealthy)
                {
                    if (_consecutiveFailures > 0)
                    {
                        _logger.LogInformation($"MCP服务器健康检查通过，连续失败次数重置为0");
                        _consecutiveFailures = 0;
                    }
                }
                else
                {
                    _consecutiveFailures++;
                    _logger.LogWarning($"MCP服务器健康检查失败 (连续失败次数: {_consecutiveFailures})");

                    // 如果连续失败次数超过阈值，尝试自动恢复
                    if (_consecutiveFailures >= _config.HealthCheck.MaxRetries)
                    {
                        _logger.LogError($"MCP服务器连续失败 {_consecutiveFailures} 次，尝试自动恢复");
                        await AttemptRecoveryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行健康检查时发生错误");
            }
        }

        /// <summary>
        /// 检查MCP服务器健康状态
        /// </summary>
        /// <returns>健康状态</returns>
        public async Task<HealthStatus> CheckMcpServerHealthAsync()
        {
            var healthStatus = new HealthStatus
            {
                CheckTime = DateTime.Now,
                IsHealthy = false,
                Details = new List<string>()
            };

            try
            {
                // 检查进程状态
                var processInfo = _processManager.GetMcpServerProcessInfo();
                if (processInfo == null)
                {
                    healthStatus.Details.Add("MCP服务器进程不存在");
                    return healthStatus;
                }

                if (processInfo.HasExited)
                {
                    healthStatus.Details.Add($"MCP服务器进程已退出，退出代码：{processInfo.ExitCode}");
                    return healthStatus;
                }

                // 检查端口可用性
                var unityPort = _portManager.GetAllocatedPort("Unity");
                var mcpPort = _portManager.GetAllocatedPort("MCP");

                if (!unityPort.HasValue || !mcpPort.HasValue)
                {
                    healthStatus.Details.Add("端口分配信息缺失");
                    return healthStatus;
                }

                // 测试端口连接
                var unityPortHealthy = await NetworkUtils.TestConnectionAsync("localhost", unityPort.Value, _config.HealthCheck.TimeoutSeconds * 1000);
                var mcpPortHealthy = await NetworkUtils.TestConnectionAsync("localhost", mcpPort.Value, _config.HealthCheck.TimeoutSeconds * 1000);

                if (!unityPortHealthy)
                {
                    healthStatus.Details.Add($"Unity端口 {unityPort.Value} 连接失败");
                }

                if (!mcpPortHealthy)
                {
                    healthStatus.Details.Add($"MCP端口 {mcpPort.Value} 连接失败");
                }

                // 检查内存使用情况
                var memoryUsageMB = processInfo.MemoryUsage / (1024 * 1024);
                if (memoryUsageMB > 500) // 如果内存使用超过500MB，记录警告
                {
                    healthStatus.Details.Add($"内存使用量较高：{memoryUsageMB}MB");
                }

                // 检查运行时间
                var uptime = DateTime.Now - processInfo.StartTime;
                if (uptime.TotalHours > 24)
                {
                    healthStatus.Details.Add($"服务器运行时间较长：{uptime.TotalHours:F1}小时");
                }

                // 如果所有检查都通过，则认为是健康的
                if (unityPortHealthy && mcpPortHealthy)
                {
                    healthStatus.IsHealthy = true;
                    healthStatus.Details.Add("所有健康检查项目通过");
                }

                healthStatus.ProcessInfo = processInfo;
                healthStatus.UnityPort = unityPort.Value;
                healthStatus.McpPort = mcpPort.Value;
                healthStatus.MemoryUsageMB = memoryUsageMB;
                healthStatus.Uptime = uptime;

                return healthStatus;
            }
            catch (Exception ex)
            {
                healthStatus.Details.Add($"健康检查异常：{ex.Message}");
                return healthStatus;
            }
        }

        /// <summary>
        /// 尝试自动恢复
        /// </summary>
        /// <returns>是否恢复成功</returns>
        private async Task<bool> AttemptRecoveryAsync()
        {
            try
            {
                _logger.LogInformation("开始自动恢复MCP服务器");

                // 停止当前服务器
                var stopSuccess = await _processManager.StopMcpServerAsync();
                if (!stopSuccess)
                {
                    _logger.LogWarning("停止MCP服务器失败，但仍尝试重新启动");
                }

                // 等待一段时间
                await Task.Delay(_config.ProcessManagement.RestartDelaySeconds * 1000);

                // 重新启动服务器
                var startSuccess = await _processManager.StartMcpServerAsync();
                if (startSuccess)
                {
                    _logger.LogInformation("MCP服务器自动恢复成功");
                    _consecutiveFailures = 0;
                    return true;
                }
                else
                {
                    _logger.LogError("MCP服务器自动恢复失败");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自动恢复过程中发生错误");
                return false;
            }
        }

        /// <summary>
        /// 获取健康检查统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        public HealthCheckStats GetHealthCheckStats()
        {
            return new HealthCheckStats
            {
                ConsecutiveFailures = _consecutiveFailures,
                MaxRetries = _config.HealthCheck.MaxRetries,
                CheckIntervalSeconds = _config.HealthCheck.IntervalSeconds,
                IsHealthCheckRunning = _healthCheckTimer != null
            };
        }
    }

    /// <summary>
    /// 健康状态类
    /// </summary>
    public class HealthStatus
    {
        /// <summary>
        /// 检查时间
        /// </summary>
        public DateTime CheckTime { get; set; }

        /// <summary>
        /// 是否健康
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// 详细信息
        /// </summary>
        public List<string> Details { get; set; } = new();

        /// <summary>
        /// 进程信息
        /// </summary>
        public ProcessInfo? ProcessInfo { get; set; }

        /// <summary>
        /// Unity端口
        /// </summary>
        public int UnityPort { get; set; }

        /// <summary>
        /// MCP端口
        /// </summary>
        public int McpPort { get; set; }

        /// <summary>
        /// 内存使用量（MB）
        /// </summary>
        public double MemoryUsageMB { get; set; }

        /// <summary>
        /// 运行时间
        /// </summary>
        public TimeSpan Uptime { get; set; }
    }

    /// <summary>
    /// 健康检查统计信息
    /// </summary>
    public class HealthCheckStats
    {
        /// <summary>
        /// 连续失败次数
        /// </summary>
        public int ConsecutiveFailures { get; set; }

        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetries { get; set; }

        /// <summary>
        /// 检查间隔（秒）
        /// </summary>
        public int CheckIntervalSeconds { get; set; }

        /// <summary>
        /// 健康检查是否正在运行
        /// </summary>
        public bool IsHealthCheckRunning { get; set; }
    }
}