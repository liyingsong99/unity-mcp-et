using System.Diagnostics;
using System.Text;

namespace UnityMcpManager.Utils
{
    /// <summary>
    /// 进程工具类
    /// </summary>
    public static class ProcessUtils
    {
        /// <summary>
        /// 启动进程并返回进程对象
        /// </summary>
        /// <param name="fileName">可执行文件路径</param>
        /// <param name="arguments">命令行参数</param>
        /// <param name="workingDirectory">工作目录</param>
        /// <param name="redirectOutput">是否重定向输出</param>
        /// <param name="environmentVariables">环境变量</param>
        /// <returns>进程对象</returns>
        public static Process StartProcess(string fileName, string arguments = "", string? workingDirectory = null,
            bool redirectOutput = true, Dictionary<string, string>? environmentVariables = null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = redirectOutput,
                RedirectStandardInput = redirectOutput,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            // 设置环境变量
            if (environmentVariables != null)
            {
                foreach (var kvp in environmentVariables)
                {
                    startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                }
            }

            return Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动进程");
        }

        /// <summary>
        /// 查找指定名称的进程
        /// </summary>
        /// <param name="processName">进程名称</param>
        /// <returns>进程列表</returns>
        public static List<Process> FindProcessesByName(string processName)
        {
            return Process.GetProcessesByName(processName).ToList();
        }

        /// <summary>
        /// 根据进程ID获取进程
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>进程对象，如果不存在则返回null</returns>
        public static Process? GetProcessById(int processId)
        {
            try
            {
                return Process.GetProcessById(processId);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 安全地终止进程
        /// </summary>
        /// <param name="process">进程对象</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>是否成功终止</returns>
        public static async Task<bool> SafeKillProcessAsync(Process process, int timeoutMs = 5000)
        {
            if (process == null || process.HasExited)
                return true;

            try
            {
                // 首先尝试优雅关闭
                process.CloseMainWindow();

                // 等待进程自然退出
                var waitTask = Task.Run(() => process.WaitForExit(timeoutMs));
                await waitTask;

                if (process.HasExited)
                    return true;

                // 如果进程仍在运行，强制终止
                process.Kill();
                await Task.Run(() => process.WaitForExit(timeoutMs));

                return process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查进程是否正在运行
        /// </summary>
        /// <param name="process">进程对象</param>
        /// <returns>是否正在运行</returns>
        public static bool IsProcessRunning(Process? process)
        {
            if (process == null)
                return false;

            try
            {
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取进程的内存使用情况
        /// </summary>
        /// <param name="process">进程对象</param>
        /// <returns>内存使用情况（字节）</returns>
        public static long GetProcessMemoryUsage(Process process)
        {
            try
            {
                return process.WorkingSet64;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 获取进程的CPU使用时间
        /// </summary>
        /// <param name="process">进程对象</param>
        /// <returns>CPU使用时间</returns>
        public static TimeSpan GetProcessCpuTime(Process process)
        {
            try
            {
                return process.TotalProcessorTime;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        /// <summary>
        /// 等待进程启动完成
        /// </summary>
        /// <param name="process">进程对象</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>是否启动成功</returns>
        public static async Task<bool> WaitForProcessStartAsync(Process process, int timeoutMs = 10000)
        {
            var startTime = DateTime.Now;

            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                if (IsProcessRunning(process))
                {
                    // 额外等待一段时间确保进程完全启动
                    await Task.Delay(1000);
                    return true;
                }

                await Task.Delay(100);
            }

            return false;
        }

        /// <summary>
        /// 检查可执行文件是否存在
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns>是否存在</returns>
        public static bool IsExecutableAvailable(string fileName)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit(5000);

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}