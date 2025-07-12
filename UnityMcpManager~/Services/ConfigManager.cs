using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using UnityMcpManager.Models;

namespace UnityMcpManager.Services
{
    /// <summary>
    /// 配置管理服务
    /// </summary>
    public class ConfigManager
    {
        private readonly ILogger<ConfigManager> _logger;
        private readonly IConfiguration _configuration;
        private McpConfig _mcpConfig;

        public ConfigManager(ILogger<ConfigManager> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _mcpConfig = LoadMcpConfig();
        }

        /// <summary>
        /// 获取MCP配置
        /// </summary>
        /// <returns>MCP配置对象</returns>
        public McpConfig GetMcpConfig()
        {
            return _mcpConfig;
        }

        /// <summary>
        /// 重新加载配置
        /// </summary>
        public void ReloadConfig()
        {
            _mcpConfig = LoadMcpConfig();
            _logger.LogInformation("配置已重新加载");
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        /// <param name="config">配置对象</param>
        /// <param name="filePath">文件路径</param>
        public void SaveConfigToFile(McpConfig config, string filePath = "appsettings.json")
        {
            try
            {
                var json = JsonConvert.SerializeObject(new { UnityMcpServer = config }, Formatting.Indented);
                File.WriteAllText(filePath, json);
                _logger.LogInformation($"配置已保存到文件：{filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"保存配置到文件失败：{filePath}");
                throw;
            }
        }

        /// <summary>
        /// 验证配置的有效性
        /// </summary>
        /// <returns>验证结果</returns>
        public ConfigValidationResult ValidateConfig()
        {
            var result = new ConfigValidationResult
            {
                IsValid = true,
                Errors = new List<string>(),
                Warnings = new List<string>()
            };

            // 验证Python可执行文件
            if (string.IsNullOrEmpty(_mcpConfig.PythonExecutable))
            {
                result.IsValid = false;
                result.Errors.Add("Python可执行文件路径不能为空");
            }

            // 验证服务器脚本路径
            if (string.IsNullOrEmpty(_mcpConfig.ServerScriptPath))
            {
                result.IsValid = false;
                result.Errors.Add("服务器脚本路径不能为空");
            }
            else if (!File.Exists(_mcpConfig.ServerScriptPath))
            {
                result.Warnings.Add($"服务器脚本文件不存在：{_mcpConfig.ServerScriptPath}");
            }

            // 验证工作目录
            if (!string.IsNullOrEmpty(_mcpConfig.WorkingDirectory) && !Directory.Exists(_mcpConfig.WorkingDirectory))
            {
                result.Warnings.Add($"工作目录不存在：{_mcpConfig.WorkingDirectory}");
            }

            // 验证端口范围
            if (_mcpConfig.PortRange.Min < 1 || _mcpConfig.PortRange.Max > 65535)
            {
                result.IsValid = false;
                result.Errors.Add("端口范围必须在1-65535之间");
            }

            if (_mcpConfig.PortRange.Min > _mcpConfig.PortRange.Max)
            {
                result.IsValid = false;
                result.Errors.Add("端口范围的最小值不能大于最大值");
            }

            // 验证默认端口
            if (_mcpConfig.DefaultUnityPort < 1 || _mcpConfig.DefaultUnityPort > 65535)
            {
                result.IsValid = false;
                result.Errors.Add("默认Unity端口必须在1-65535之间");
            }

            if (_mcpConfig.DefaultMcpPort < 1 || _mcpConfig.DefaultMcpPort > 65535)
            {
                result.IsValid = false;
                result.Errors.Add("默认MCP端口必须在1-65535之间");
            }

            // 验证健康检查配置
            if (_mcpConfig.HealthCheck.IntervalSeconds < 1)
            {
                result.IsValid = false;
                result.Errors.Add("健康检查间隔必须大于0");
            }

            if (_mcpConfig.HealthCheck.TimeoutSeconds < 1)
            {
                result.IsValid = false;
                result.Errors.Add("健康检查超时时间必须大于0");
            }

            if (_mcpConfig.HealthCheck.MaxRetries < 1)
            {
                result.IsValid = false;
                result.Errors.Add("最大重试次数必须大于0");
            }

            // 验证进程管理配置
            if (_mcpConfig.ProcessManagement.StartupTimeoutSeconds < 1)
            {
                result.IsValid = false;
                result.Errors.Add("启动超时时间必须大于0");
            }

            if (_mcpConfig.ProcessManagement.ShutdownTimeoutSeconds < 1)
            {
                result.IsValid = false;
                result.Errors.Add("关闭超时时间必须大于0");
            }

            if (_mcpConfig.ProcessManagement.RestartDelaySeconds < 0)
            {
                result.IsValid = false;
                result.Errors.Add("重启延迟时间不能为负数");
            }

            return result;
        }

        /// <summary>
        /// 获取配置摘要信息
        /// </summary>
        /// <returns>配置摘要</returns>
        public ConfigSummary GetConfigSummary()
        {
            return new ConfigSummary
            {
                PythonExecutable = _mcpConfig.PythonExecutable,
                ServerScriptPath = _mcpConfig.ServerScriptPath,
                WorkingDirectory = _mcpConfig.WorkingDirectory,
                DefaultUnityPort = _mcpConfig.DefaultUnityPort,
                DefaultMcpPort = _mcpConfig.DefaultMcpPort,
                PortRange = $"{_mcpConfig.PortRange.Min}-{_mcpConfig.PortRange.Max}",
                HealthCheckInterval = _mcpConfig.HealthCheck.IntervalSeconds,
                HealthCheckTimeout = _mcpConfig.HealthCheck.TimeoutSeconds,
                MaxRetries = _mcpConfig.HealthCheck.MaxRetries,
                StartupTimeout = _mcpConfig.ProcessManagement.StartupTimeoutSeconds,
                ShutdownTimeout = _mcpConfig.ProcessManagement.ShutdownTimeoutSeconds,
                RestartDelay = _mcpConfig.ProcessManagement.RestartDelaySeconds
            };
        }

        /// <summary>
        /// 从配置文件加载MCP配置
        /// </summary>
        /// <returns>MCP配置对象</returns>
        private McpConfig LoadMcpConfig()
        {
            try
            {
                var config = new McpConfig();

                // 从配置文件加载
                _configuration.GetSection("UnityMcpServer").Bind(config);

                _logger.LogInformation("MCP配置加载成功");
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载MCP配置失败，使用默认配置");
                return new McpConfig();
            }
        }
    }

    /// <summary>
    /// 配置验证结果
    /// </summary>
    public class ConfigValidationResult
    {
        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 错误信息列表
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// 警告信息列表
        /// </summary>
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// 配置摘要信息
    /// </summary>
    public class ConfigSummary
    {
        /// <summary>
        /// Python可执行文件
        /// </summary>
        public string PythonExecutable { get; set; } = string.Empty;

        /// <summary>
        /// 服务器脚本路径
        /// </summary>
        public string ServerScriptPath { get; set; } = string.Empty;

        /// <summary>
        /// 工作目录
        /// </summary>
        public string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// 默认Unity端口
        /// </summary>
        public int DefaultUnityPort { get; set; }

        /// <summary>
        /// 默认MCP端口
        /// </summary>
        public int DefaultMcpPort { get; set; }

        /// <summary>
        /// 端口范围
        /// </summary>
        public string PortRange { get; set; } = string.Empty;

        /// <summary>
        /// 健康检查间隔
        /// </summary>
        public int HealthCheckInterval { get; set; }

        /// <summary>
        /// 健康检查超时
        /// </summary>
        public int HealthCheckTimeout { get; set; }

        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetries { get; set; }

        /// <summary>
        /// 启动超时时间
        /// </summary>
        public int StartupTimeout { get; set; }

        /// <summary>
        /// 关闭超时时间
        /// </summary>
        public int ShutdownTimeout { get; set; }

        /// <summary>
        /// 重启延迟时间
        /// </summary>
        public int RestartDelay { get; set; }
    }
}