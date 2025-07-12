namespace UnityMcpManager.Models
{
    /// <summary>
    /// 端口信息结构
    /// </summary>
    public struct PortInfo
    {
        /// <summary>
        /// 端口号
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 是否可用
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// 占用该端口的进程ID
        /// </summary>
        public int? ProcessId { get; set; }

        /// <summary>
        /// 占用该端口的进程名称
        /// </summary>
        public string? ProcessName { get; set; }

        /// <summary>
        /// 检查时间
        /// </summary>
        public DateTime CheckTime { get; set; }

        public PortInfo(int port, bool isAvailable, int? processId = null, string? processName = null)
        {
            Port = port;
            IsAvailable = isAvailable;
            ProcessId = processId;
            ProcessName = processName;
            CheckTime = DateTime.Now;
        }

        public override string ToString()
        {
            return $"Port {Port}: {(IsAvailable ? "Available" : $"Occupied by {ProcessName} (PID: {ProcessId})")}";
        }
    }
}