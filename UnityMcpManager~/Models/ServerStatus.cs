namespace UnityMcpManager.Models
{
    /// <summary>
    /// MCP服务器状态枚举
    /// </summary>
    public enum ServerStatus
    {
        /// <summary>
        /// 已停止
        /// </summary>
        Stopped,

        /// <summary>
        /// 正在启动
        /// </summary>
        Starting,

        /// <summary>
        /// 正在运行
        /// </summary>
        Running,

        /// <summary>
        /// 正在停止
        /// </summary>
        Stopping,

        /// <summary>
        /// 错误状态
        /// </summary>
        Error,

        /// <summary>
        /// 未知状态
        /// </summary>
        Unknown
    }
}