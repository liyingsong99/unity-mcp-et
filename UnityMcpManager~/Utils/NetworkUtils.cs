using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityMcpManager.Models;

namespace UnityMcpManager.Utils
{
    /// <summary>
    /// 网络工具类
    /// </summary>
    public static class NetworkUtils
    {
        /// <summary>
        /// 检查端口是否可用
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>端口信息</returns>
        public static PortInfo CheckPortAvailability(int port)
        {
            try
            {
                // 检查TCP端口
                var tcpListener = new TcpListener(IPAddress.Any, port);
                try
                {
                    tcpListener.Start();
                    tcpListener.Stop();
                }
                finally
                {
                    tcpListener.Stop();
                }

                // 检查UDP端口
                var udpClient = new UdpClient(port);
                try
                {
                    // UDP客户端创建成功表示端口可用
                }
                finally
                {
                    udpClient.Close();
                }

                return new PortInfo(port, true);
            }
            catch (SocketException)
            {
                // 端口被占用，尝试获取占用进程信息
                var processInfo = GetProcessUsingPort(port);
                return new PortInfo(port, false, processInfo.processId, processInfo.processName);
            }
        }

        /// <summary>
        /// 获取占用指定端口的进程信息
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>进程信息元组</returns>
        private static (int? processId, string? processName) GetProcessUsingPort(int port)
        {
            try
            {
                var tcpConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
                var connection = tcpConnections.FirstOrDefault(c => c.LocalEndPoint.Port == port);

                if (connection != null)
                {
                    // 在Windows上，我们无法直接从IPGlobalProperties获取进程ID
                    // 这里返回基本信息，实际实现可能需要使用WMI或其他方法
                    return (null, "Unknown Process");
                }

                return (null, null);
            }
            catch
            {
                return (null, null);
            }
        }

        /// <summary>
        /// 在指定范围内查找可用端口
        /// </summary>
        /// <param name="startPort">起始端口</param>
        /// <param name="endPort">结束端口</param>
        /// <param name="count">需要找到的端口数量</param>
        /// <returns>可用端口列表</returns>
        public static List<int> FindAvailablePorts(int startPort, int endPort, int count = 1)
        {
            var availablePorts = new List<int>();

            for (int port = startPort; port <= endPort && availablePorts.Count < count; port++)
            {
                var portInfo = CheckPortAvailability(port);
                if (portInfo.IsAvailable)
                {
                    availablePorts.Add(port);
                }
            }

            return availablePorts;
        }

        /// <summary>
        /// 测试与指定主机和端口的连接
        /// </summary>
        /// <param name="host">主机地址</param>
        /// <param name="port">端口号</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>连接是否成功</returns>
        public static async Task<bool> TestConnectionAsync(string host, int port, int timeoutMs = 5000)
        {
            try
            {
                using var tcpClient = new TcpClient();
                var connectTask = tcpClient.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(timeoutMs);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == connectTask && tcpClient.Connected)
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取本机IP地址
        /// </summary>
        /// <returns>IP地址字符串</returns>
        public static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ip = host.AddressList.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
                return ip?.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        /// <summary>
        /// 检查多个端口的可用性
        /// </summary>
        /// <param name="ports">端口列表</param>
        /// <returns>端口信息列表</returns>
        public static List<PortInfo> CheckMultiplePortsAvailability(IEnumerable<int> ports)
        {
            return ports.Select(CheckPortAvailability).ToList();
        }
    }
}