namespace UnityMcpManager.Models
{
    /// <summary>
    /// MCP服务器配置类
    /// </summary>
    public class McpConfig
    {
        /// <summary>
        /// Python可执行文件路径
        /// </summary>
        public string PythonExecutable { get; set; } = "python";

        /// <summary>
        /// 服务器脚本路径
        /// </summary>
        public string ServerScriptPath { get; set; } = "../UnityMcpServer~/src/server.py";

        /// <summary>
        /// 工作目录
        /// </summary>
        public string WorkingDirectory { get; set; } = "../UnityMcpServer~/src";

        /// <summary>
        /// 默认Unity端口
        /// </summary>
        public int DefaultUnityPort { get; set; } = 6400;

        /// <summary>
        /// 默认MCP端口
        /// </summary>
        public int DefaultMcpPort { get; set; } = 6500;

        /// <summary>
        /// 端口范围配置
        /// </summary>
        public PortRangeConfig PortRange { get; set; } = new();

        /// <summary>
        /// 健康检查配置
        /// </summary>
        public HealthCheckConfig HealthCheck { get; set; } = new();

        /// <summary>
        /// 进程管理配置
        /// </summary>
        public ProcessManagementConfig ProcessManagement { get; set; } = new();
    }

    /// <summary>
    /// 端口范围配置
    /// </summary>
    public class PortRangeConfig
    {
        /// <summary>
        /// 最小端口号
        /// </summary>
        public int Min { get; set; } = 6400;

        /// <summary>
        /// 最大端口号
        /// </summary>
        public int Max { get; set; } = 6599;
    }

    /// <summary>
    /// 健康检查配置
    /// </summary>
    public class HealthCheckConfig
    {
        /// <summary>
        /// 检查间隔（秒）
        /// </summary>
        public int IntervalSeconds { get; set; } = 30;

        /// <summary>
        /// 超时时间（秒）
        /// </summary>
        public int TimeoutSeconds { get; set; } = 5;

        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetries { get; set; } = 3;
    }

    /// <summary>
    /// 进程管理配置
    /// </summary>
    public class ProcessManagementConfig
    {
        /// <summary>
        /// 启动超时时间（秒）
        /// </summary>
        public int StartupTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// 关闭超时时间（秒）
        /// </summary>
        public int ShutdownTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// 重启延迟时间（秒）
        /// </summary>
        public int RestartDelaySeconds { get; set; } = 5;
    }
}