using Microsoft.Extensions.Logging;
using UnityMcpManager.Models;
using UnityMcpManager.Utils;

namespace UnityMcpManager.Services
{
    /// <summary>
    /// 端口管理服务
    /// </summary>
    public class PortManager
    {
        private readonly ILogger<PortManager> _logger;
        private readonly McpConfig _config;
        private readonly Dictionary<string, int> _allocatedPorts;
        private readonly object _lock = new object();

        public PortManager(ILogger<PortManager> logger, McpConfig config)
        {
            _logger = logger;
            _config = config;
            _allocatedPorts = new Dictionary<string, int>();
        }

        /// <summary>
        /// 为指定服务分配端口
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <param name="preferredPort">首选端口</param>
        /// <returns>分配的端口号</returns>
        public int AllocatePort(string serviceName, int? preferredPort = null)
        {
            lock (_lock)
            {
                // 检查是否已经为该服务分配了端口
                if (_allocatedPorts.ContainsKey(serviceName))
                {
                    var existingPort = _allocatedPorts[serviceName];
                    var portInfo = NetworkUtils.CheckPortAvailability(existingPort);

                    if (portInfo.IsAvailable)
                    {
                        _logger.LogInformation($"服务 {serviceName} 重用已分配的端口 {existingPort}");
                        return existingPort;
                    }
                    else
                    {
                        _logger.LogWarning($"服务 {serviceName} 的已分配端口 {existingPort} 被占用，需要重新分配");
                        _allocatedPorts.Remove(serviceName);
                    }
                }

                // 尝试使用首选端口
                if (preferredPort.HasValue)
                {
                    var preferredPortInfo = NetworkUtils.CheckPortAvailability(preferredPort.Value);
                    if (preferredPortInfo.IsAvailable)
                    {
                        _allocatedPorts[serviceName] = preferredPort.Value;
                        _logger.LogInformation($"为服务 {serviceName} 分配首选端口 {preferredPort.Value}");
                        return preferredPort.Value;
                    }
                    else
                    {
                        _logger.LogWarning($"首选端口 {preferredPort.Value} 不可用：{preferredPortInfo}");
                    }
                }

                // 在配置的端口范围内查找可用端口
                var availablePorts = NetworkUtils.FindAvailablePorts(_config.PortRange.Min, _config.PortRange.Max, 1);

                if (availablePorts.Count == 0)
                {
                    throw new InvalidOperationException($"在端口范围 {_config.PortRange.Min}-{_config.PortRange.Max} 内未找到可用端口");
                }

                var allocatedPort = availablePorts[0];
                _allocatedPorts[serviceName] = allocatedPort;
                _logger.LogInformation($"为服务 {serviceName} 分配端口 {allocatedPort}");

                return allocatedPort;
            }
        }

        /// <summary>
        /// 释放指定服务的端口
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        public void ReleasePort(string serviceName)
        {
            lock (_lock)
            {
                if (_allocatedPorts.TryGetValue(serviceName, out var port))
                {
                    _allocatedPorts.Remove(serviceName);
                    _logger.LogInformation($"释放服务 {serviceName} 的端口 {port}");
                }
            }
        }

        /// <summary>
        /// 获取指定服务的已分配端口
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <returns>端口号，如果未分配则返回null</returns>
        public int? GetAllocatedPort(string serviceName)
        {
            lock (_lock)
            {
                return _allocatedPorts.TryGetValue(serviceName, out var port) ? port : null;
            }
        }

        /// <summary>
        /// 检查端口冲突并尝试解决
        /// </summary>
        /// <returns>冲突解决报告</returns>
        public Task<PortConflictReport> ResolvePortConflictsAsync()
        {
            var report = new PortConflictReport();

            lock (_lock)
            {
                var servicesToCheck = _allocatedPorts.ToList();

                foreach (var (serviceName, port) in servicesToCheck)
                {
                    var portInfo = NetworkUtils.CheckPortAvailability(port);

                    if (!portInfo.IsAvailable)
                    {
                        report.ConflictedPorts.Add(new PortConflictInfo
                        {
                            ServiceName = serviceName,
                            Port = port,
                            ConflictProcess = portInfo.ProcessName,
                            ConflictProcessId = portInfo.ProcessId
                        });

                        _logger.LogWarning($"检测到端口冲突：服务 {serviceName} 的端口 {port} 被进程 {portInfo.ProcessName} 占用");

                        // 尝试重新分配端口
                        try
                        {
                            var newPort = AllocatePort(serviceName);
                            report.ResolvedPorts.Add(new PortResolutionInfo
                            {
                                ServiceName = serviceName,
                                OldPort = port,
                                NewPort = newPort
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"无法为服务 {serviceName} 重新分配端口");
                            report.UnresolvedConflicts.Add(serviceName);
                        }
                    }
                }
            }

            return Task.FromResult(report);
        }

        /// <summary>
        /// 获取端口使用情况报告
        /// </summary>
        /// <returns>端口使用情况</returns>
        public PortUsageReport GetPortUsageReport()
        {
            var report = new PortUsageReport();

            // 检查配置范围内的所有端口
            var portsToCheck = Enumerable.Range(_config.PortRange.Min, _config.PortRange.Max - _config.PortRange.Min + 1);
            var portInfos = NetworkUtils.CheckMultiplePortsAvailability(portsToCheck);

            report.TotalPorts = portInfos.Count;
            report.AvailablePorts = portInfos.Count(p => p.IsAvailable);
            report.OccupiedPorts = portInfos.Count(p => !p.IsAvailable);
            report.PortDetails = portInfos;

            lock (_lock)
            {
                report.AllocatedServices = _allocatedPorts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            return report;
        }
    }

    /// <summary>
    /// 端口冲突报告
    /// </summary>
    public class PortConflictReport
    {
        public List<PortConflictInfo> ConflictedPorts { get; set; } = new();
        public List<PortResolutionInfo> ResolvedPorts { get; set; } = new();
        public List<string> UnresolvedConflicts { get; set; } = new();
    }

    /// <summary>
    /// 端口冲突信息
    /// </summary>
    public class PortConflictInfo
    {
        public string ServiceName { get; set; } = string.Empty;
        public int Port { get; set; }
        public string? ConflictProcess { get; set; }
        public int? ConflictProcessId { get; set; }
    }

    /// <summary>
    /// 端口解决信息
    /// </summary>
    public class PortResolutionInfo
    {
        public string ServiceName { get; set; } = string.Empty;
        public int OldPort { get; set; }
        public int NewPort { get; set; }
    }

    /// <summary>
    /// 端口使用情况报告
    /// </summary>
    public class PortUsageReport
    {
        public int TotalPorts { get; set; }
        public int AvailablePorts { get; set; }
        public int OccupiedPorts { get; set; }
        public List<PortInfo> PortDetails { get; set; } = new();
        public Dictionary<string, int> AllocatedServices { get; set; } = new();
    }
}