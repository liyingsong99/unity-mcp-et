using System;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcpBridge.Editor.Helpers;
using UnityMcpBridge.Editor.Models;
using UnityMcpBridge.Editor.Tools;

namespace UnityMcpBridge.Editor
{
    [InitializeOnLoad]
    public static partial class UnityMcpBridge
    {
        private static TcpListener listener;
        private static bool isRunning = false;
        private static readonly object lockObj = new();
        private static Dictionary<
            string,
            (string commandJson, TaskCompletionSource<string> tcs)
        > commandQueue = new();
        private static readonly int unityPort = 6400; // Hardcoded port
        private static readonly List<TcpClient> activeClients = new(); // 追踪活跃连接
        private static readonly string pidFilePath = Path.Combine(Application.temporaryCachePath, "unity_mcp_bridge.pid"); // PID文件路径
        private static volatile bool isShuttingDown = false; // 关闭状态标志

        public static bool IsRunning => isRunning;

        public static bool FolderExists(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (path.Equals("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string fullPath = Path.Combine(
                Application.dataPath,
                path.StartsWith("Assets/") ? path[7..] : path
            );
            return Directory.Exists(fullPath);
        }

        static UnityMcpBridge()
        {
            // 注册多重退出事件处理，确保在各种情况下都能清理资源
            EditorApplication.quitting += Stop;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;

            // 清理可能残留的PID文件和端口占用
            CleanupResidualResources();

            Start();
        }

        public static void Start()
        {
            Stop();

            try
            {
                ServerInstaller.EnsureServerInstalled();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to ensure UnityMcpServer is installed: {ex.Message}");
            }

            if (isRunning)
            {
                return;
            }

            // 尝试启动监听器，如果端口被占用则尝试解决
            const int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    listener = new TcpListener(IPAddress.Loopback, unityPort);
                    listener.Start();
                    isRunning = true;

                    // 创建并写入PID文件，用于追踪进程
                    CreatePidFile();

                    Debug.Log($"UnityMcpBridge started on port {unityPort} (attempt {attempt}).");
                    Task.Run(ListenerLoop);
                    EditorApplication.update += ProcessCommands;
                    return; // 成功启动，退出重试循环
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        Debug.LogWarning($"[尝试 {attempt}/{maxRetries}] 端口 {unityPort} 被占用，正在检查占用情况...");

                        if (attempt < maxRetries)
                        {
                            // 检查端口占用情况（不再尝试终止进程）
                            bool portAvailable = TryFreePort(unityPort);
                            if (portAvailable)
                            {
                                Debug.Log($"端口 {unityPort} 检查完成，准备重试启动...");
                                Thread.Sleep(1000); // 等待1秒让端口状态稳定
                                continue; // 重试启动
                            }
                            else
                            {
                                Debug.LogWarning($"端口 {unityPort} 仍被占用，请手动处理后重试，将在2秒后重试...");
                                Thread.Sleep(2000); // 等待2秒后重试
                                continue;
                            }
                        }
                        else
                        {
                            Debug.LogError(
                                $"端口 {unityPort} 持续被占用，已重试 {maxRetries} 次均失败。请手动检查并关闭占用该端口的进程，或重启Unity编辑器。"
                            );
                        }
                    }
                    else
                    {
                        Debug.LogError($"启动TCP监听器失败: {ex.Message}");
                        break; // 非端口占用错误，不重试
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"启动UnityMcpBridge时发生意外错误: {ex.Message}");
                    break; // 其他异常，不重试
                }
            }
        }

        public static void Stop()
        {
            if (!isRunning)
            {
                return;
            }

            isShuttingDown = true;

            try
            {
                // 强制关闭所有活跃连接
                CloseAllActiveConnections();

                // 停止监听器
                listener?.Stop();
                listener = null;
                isRunning = false;
                EditorApplication.update -= ProcessCommands;

                // 清理PID文件
                CleanupPidFile();

                Debug.Log("UnityMcpBridge stopped and all resources cleaned up.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error stopping UnityMcpBridge: {ex.Message}");
            }
            finally
            {
                isShuttingDown = false;
            }
        }

        /// <summary>
        /// 处理进程退出事件
        /// </summary>
        private static void OnProcessExit(object sender, EventArgs e)
        {
            Stop();
        }

        /// <summary>
        /// 处理应用程序域卸载事件
        /// </summary>
        private static void OnDomainUnload(object sender, EventArgs e)
        {
            Stop();
        }

        /// <summary>
        /// 清理残留的资源（PID文件和端口占用检查）
        /// </summary>
        private static void CleanupResidualResources()
        {
            try
            {
                // 检查并清理PID文件
                if (File.Exists(pidFilePath))
                {
                    Debug.Log("发现残留的PID文件，正在清理...");

                    try
                    {
                        string pidContent = File.ReadAllText(pidFilePath);
                        if (int.TryParse(pidContent, out int oldPid))
                        {
                            // 检查该PID是否仍在运行且占用我们的端口
                            if (IsProcessRunningAndUsingPort(oldPid, unityPort))
                            {
                                Debug.LogWarning($"发现残留进程 {oldPid} 仍在占用端口 {unityPort}，请手动处理");
                                TryKillProcess(oldPid); // 调用已弃用的方法，仅用于记录信息
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"读取PID文件时出错: {ex.Message}");
                    }

                    // 删除PID文件
                    File.Delete(pidFilePath);
                }

                // 额外的端口占用检查
                var processIds = GetProcessesUsingPort(unityPort);
                if (processIds.Count > 0)
                {
                    Debug.LogWarning($"发现 {processIds.Count} 个进程占用端口 {unityPort}，请手动处理");
                    foreach (int pid in processIds)
                    {
                        TryKillProcess(pid); // 调用已弃用的方法，仅用于记录信息
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"清理残留资源时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查指定进程是否正在运行并占用指定端口
        /// </summary>
        private static bool IsProcessRunningAndUsingPort(int pid, int port)
        {
            try
            {
                Process.GetProcessById(pid); // 如果进程不存在会抛出异常
                var processIds = GetProcessesUsingPort(port);
                return processIds.Contains(pid);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 已弃用：为了系统安全，不再支持进程终止功能
        /// </summary>
        [System.Obsolete("进程终止功能已被禁用以确保系统安全，请手动管理进程")]
        private static void TryKillProcess(int pid)
        {
            try
            {
                Process process = Process.GetProcessById(pid);
                string processName = process.ProcessName;

                Debug.LogWarning($"检测到进程: {processName} (PID: {pid})，但进程终止功能已被禁用以确保系统安全");
                Debug.LogWarning($"如需终止进程 {processName} (PID: {pid})，请手动处理");
            }
            catch (ArgumentException)
            {
                Debug.Log($"进程 {pid} 已经不存在");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"获取进程 {pid} 信息时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 关闭所有活跃连接
        /// </summary>
        private static void CloseAllActiveConnections()
        {
            lock (lockObj)
            {
                Debug.Log($"正在关闭 {activeClients.Count} 个活跃连接...");

                foreach (var client in activeClients.ToList())
                {
                    try
                    {
                        client?.Close();
                        client?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"关闭客户端连接时出错: {ex.Message}");
                    }
                }
                activeClients.Clear();
            }
        }

        /// <summary>
        /// 创建PID文件记录当前进程
        /// </summary>
        private static void CreatePidFile()
        {
            try
            {
                int currentPid = Process.GetCurrentProcess().Id;
                File.WriteAllText(pidFilePath, currentPid.ToString());
                Debug.Log($"已创建PID文件: {pidFilePath}, PID: {currentPid}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"创建PID文件时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理PID文件
        /// </summary>
        private static void CleanupPidFile()
        {
            try
            {
                if (File.Exists(pidFilePath))
                {
                    File.Delete(pidFilePath);
                    Debug.Log("PID文件已清理");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"清理PID文件时出错: {ex.Message}");
            }
        }

        private static async Task ListenerLoop()
        {
            while (isRunning)
            {
                try
                {
                    if (isShuttingDown)
                    {
                        break;
                    }

                    TcpClient client = await listener.AcceptTcpClientAsync();

                    // 将客户端添加到活跃连接列表中进行追踪
                    lock (lockObj)
                    {
                        activeClients.Add(client);
                    }

                    // Enable basic socket keepalive
                    client.Client.SetSocketOption(
                        SocketOptionLevel.Socket,
                        SocketOptionName.KeepAlive,
                        true
                    );

                    // Set longer receive timeout to prevent quick disconnections
                    client.ReceiveTimeout = 60000; // 60 seconds

                    // Fire and forget each client connection
                    _ = HandleClientAsync(client);
                }
                catch (Exception ex)
                {
                    if (isRunning)
                    {
                        Debug.LogError($"Listener error: {ex.Message}");
                    }
                }
            }
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[8192];
                    while (isRunning && !isShuttingDown)
                    {
                        try
                        {
                            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                            if (bytesRead == 0)
                            {
                                break; // Client disconnected
                            }

                            string commandText = System.Text.Encoding.UTF8.GetString(
                                buffer,
                                0,
                                bytesRead
                            );
                            string commandId = Guid.NewGuid().ToString();
                            TaskCompletionSource<string> tcs = new();

                            // Special handling for ping command to avoid JSON parsing
                            if (commandText.Trim() == "ping")
                            {
                                // Direct response to ping without going through JSON parsing
                                byte[] pingResponseBytes = System.Text.Encoding.UTF8.GetBytes(
                                    /*lang=json,strict*/
                                    "{\"status\":\"success\",\"result\":{\"message\":\"pong\"}}"
                                );
                                await stream.WriteAsync(pingResponseBytes, 0, pingResponseBytes.Length);
                                continue;
                            }

                            lock (lockObj)
                            {
                                commandQueue[commandId] = (commandText, tcs);
                            }

                            // 设置30秒超时
                            string response;
                            var timeoutTask = Task.Delay(30000); // 30秒超时
                            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                            if (completedTask == timeoutTask)
                            {
                                // 超时处理
                                var timeoutResponse = new
                                {
                                    status = "error",
                                    error = "Command execution timeout (30 seconds)",
                                    commandText = commandText.Length > 100 ? commandText.Substring(0, 100) + "..." : commandText
                                };
                                response = JsonConvert.SerializeObject(timeoutResponse);

                                // 清理超时的命令
                                lock (lockObj)
                                {
                                    commandQueue.Remove(commandId);
                                }

                                Debug.LogWarning($"[UnityMcpBridge] Command timed out after 30 seconds: {commandText.Substring(0, Math.Min(50, commandText.Length))}");
                            }
                            else
                            {
                                // 正常完成
                                response = await tcs.Task;
                            }
                            byte[] responseBytes = System.Text.Encoding.UTF8.GetBytes(response);
                            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Client handler error: {ex.Message}");
                            break;
                        }
                    }
                }
            }
            finally
            {
                // 从活跃连接列表中移除客户端
                lock (lockObj)
                {
                    activeClients.Remove(client);
                }
            }
        }

        private static void ProcessCommands()
        {
            List<string> processedIds = new();
            lock (lockObj)
            {
                foreach (
                    KeyValuePair<
                        string,
                        (string commandJson, TaskCompletionSource<string> tcs)
                    > kvp in commandQueue.ToList()
                )
                {
                    string id = kvp.Key;
                    string commandText = kvp.Value.commandJson;
                    TaskCompletionSource<string> tcs = kvp.Value.tcs;

                    try
                    {
                        // Special case handling
                        if (string.IsNullOrEmpty(commandText))
                        {
                            var emptyResponse = new
                            {
                                status = "error",
                                error = "Empty command received",
                            };
                            tcs.SetResult(JsonConvert.SerializeObject(emptyResponse));
                            processedIds.Add(id);
                            continue;
                        }

                        // Trim the command text to remove any whitespace
                        commandText = commandText.Trim();

                        // Non-JSON direct commands handling (like ping)
                        if (commandText == "ping")
                        {
                            var pingResponse = new
                            {
                                status = "success",
                                result = new { message = "pong" },
                            };
                            tcs.SetResult(JsonConvert.SerializeObject(pingResponse));
                            processedIds.Add(id);
                            continue;
                        }

                        // Check if the command is valid JSON before attempting to deserialize
                        if (!IsValidJson(commandText))
                        {
                            var invalidJsonResponse = new
                            {
                                status = "error",
                                error = "Invalid JSON format",
                                receivedText = commandText.Length > 50
                                    ? commandText[..50] + "..."
                                    : commandText,
                            };
                            tcs.SetResult(JsonConvert.SerializeObject(invalidJsonResponse));
                            processedIds.Add(id);
                            continue;
                        }

                        // Normal JSON command processing
                        Command command = JsonConvert.DeserializeObject<Command>(commandText);
                        if (command == null)
                        {
                            var nullCommandResponse = new
                            {
                                status = "error",
                                error = "Command deserialized to null",
                                details = "The command was valid JSON but could not be deserialized to a Command object",
                            };
                            tcs.SetResult(JsonConvert.SerializeObject(nullCommandResponse));
                        }
                        else
                        {
                            string responseJson = ExecuteCommand(command);
                            tcs.SetResult(responseJson);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error processing command: {ex.Message}\n{ex.StackTrace}");

                        var response = new
                        {
                            status = "error",
                            error = ex.Message,
                            commandType = "Unknown (error during processing)",
                            receivedText = commandText?.Length > 50
                                ? commandText[..50] + "..."
                                : commandText,
                        };
                        string responseJson = JsonConvert.SerializeObject(response);
                        tcs.SetResult(responseJson);
                    }

                    processedIds.Add(id);
                }

                foreach (string id in processedIds)
                {
                    commandQueue.Remove(id);
                }
            }
        }

        // Helper method to check if a string is valid JSON
        private static bool IsValidJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            text = text.Trim();
            if (
                (text.StartsWith("{") && text.EndsWith("}"))
                || // Object
                (text.StartsWith("[") && text.EndsWith("]"))
            ) // Array
            {
                try
                {
                    JToken.Parse(text);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static string ExecuteCommand(Command command)
        {
            try
            {
                if (string.IsNullOrEmpty(command.type))
                {
                    var errorResponse = new
                    {
                        status = "error",
                        error = "Command type cannot be empty",
                        details = "A valid command type is required for processing",
                    };
                    return JsonConvert.SerializeObject(errorResponse);
                }

                // Handle ping command for connection verification
                if (command.type.Equals("ping", StringComparison.OrdinalIgnoreCase))
                {
                    var pingResponse = new
                    {
                        status = "success",
                        result = new { message = "pong" },
                    };
                    return JsonConvert.SerializeObject(pingResponse);
                }

                // Use JObject for parameters as the new handlers likely expect this
                JObject paramsObject = command.@params ?? new JObject();

                // Route command based on the new tool structure from the refactor plan
                object result = command.type switch
                {
                    // Maps the command type (tool name) to the corresponding handler's static HandleCommand method
                    // Assumes each handler class has a static method named 'HandleCommand' that takes JObject parameters
                    "manage_script" => ManageScript.HandleCommand(paramsObject),
                    "manage_scene" => ManageScene.HandleCommand(paramsObject),
                    "manage_editor" => ManageEditor.HandleCommand(paramsObject),
                    "manage_gameobject" => ManageGameObject.HandleCommand(paramsObject),
                    "manage_asset" => ManageAsset.HandleCommand(paramsObject),
                    "read_console" => ReadConsole.HandleCommand(paramsObject),
                    "execute_menu_item" => ExecuteMenuItem.HandleCommand(paramsObject),
                    _ => throw new ArgumentException(
                        $"Unknown or unsupported command type: {command.type}"
                    ),
                };

                // Standard success response format
                var response = new { status = "success", result };
                return JsonConvert.SerializeObject(response);
            }
            catch (Exception ex)
            {
                // Log the detailed error in Unity for debugging
                Debug.LogError(
                    $"Error executing command '{command?.type ?? "Unknown"}': {ex.Message}\n{ex.StackTrace}"
                );

                // Standard error response format
                var response = new
                {
                    status = "error",
                    error = ex.Message, // Provide the specific error message
                    command = command?.type ?? "Unknown", // Include the command type if available
                    stackTrace = ex.StackTrace, // Include stack trace for detailed debugging
                    paramsSummary = command?.@params != null
                        ? GetParamsSummary(command.@params)
                        : "No parameters", // Summarize parameters for context
                };
                return JsonConvert.SerializeObject(response);
            }
        }

        // Helper method to get a summary of parameters for error reporting
        private static string GetParamsSummary(JObject @params)
        {
            try
            {
                return @params == null || !@params.HasValues
                    ? "No parameters"
                    : string.Join(
                        ", ",
                        @params
                            .Properties()
                            .Select(static p =>
                                $"{p.Name}: {p.Value?.ToString()?.Substring(0, Math.Min(20, p.Value?.ToString()?.Length ?? 0))}"
                            )
                    );
            }
            catch
            {
                return "Could not summarize parameters";
            }
        }

        /// <summary>
        /// 检查指定端口的占用情况，不再尝试终止占用进程以确保系统安全
        /// </summary>
        /// <param name="port">要检查的端口号</param>
        /// <returns>端口是否可用（未被占用）</returns>
        private static bool TryFreePort(int port)
        {
            try
            {
                Debug.Log($"正在检查端口 {port} 的占用情况...");

                // 获取占用指定端口的进程ID
                var processIds = GetProcessesUsingPort(port);

                if (processIds.Count == 0)
                {
                    Debug.Log($"端口 {port} 未被占用，可以正常使用");
                    return true; // 端口未被占用
                }

                Debug.LogWarning($"检测到端口 {port} 被 {processIds.Count} 个进程占用");

                // 详细报告占用进程信息，但不尝试终止
                foreach (int processId in processIds)
                {
                    try
                    {
                        Process process = Process.GetProcessById(processId);
                        string processName = process.ProcessName;
                        Debug.LogWarning($"端口 {port} 被进程占用: {processName} (PID: {processId})");
                    }
                    catch (ArgumentException)
                    {
                        Debug.Log($"进程 {processId} 已经不存在");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"获取进程 {processId} 信息时出错: {ex.Message}");
                    }
                }

                Debug.LogWarning($"端口 {port} 被占用，请手动关闭占用该端口的进程，或使用其他端口");
                return false; // 端口被占用
            }
            catch (Exception ex)
            {
                Debug.LogError($"检查端口 {port} 时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取占用指定端口的进程ID列表
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>进程ID列表</returns>
        private static List<int> GetProcessesUsingPort(int port)
        {
            var processIds = new List<int>();

            try
            {
                // 使用netstat命令查找占用端口的进程
                var startInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        // 解析netstat输出
                        var lines = output.Split('\n');
                        foreach (var line in lines)
                        {
                            if (line.Contains($":{port} ") && line.Contains("LISTENING"))
                            {
                                var parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length >= 5 && int.TryParse(parts[parts.Length - 1], out int pid))
                                {
                                    if (!processIds.Contains(pid))
                                    {
                                        processIds.Add(pid);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"查找占用端口 {port} 的进程时出错: {ex.Message}");
            }

            return processIds;
        }
    }
}
