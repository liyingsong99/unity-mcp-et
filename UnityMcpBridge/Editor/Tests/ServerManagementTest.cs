using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityMcpBridge.Editor.Models;
using UnityMcpBridge.Editor.Services;
using UnityMcpBridge.Editor.Data;

namespace UnityMcpBridge.Editor.Tests
{
    /// <summary>
    /// 服务器管理功能测试类
    /// </summary>
    public static class ServerManagementTest
    {
        /// <summary>
        /// 运行所有测试
        /// </summary>
        [UnityEditor.MenuItem("Tools/Unity MCP/Test Server Management")]
        public static async void RunAllTests()
        {
            Debug.Log("=== 开始服务器管理功能测试 ===");

            try
            {
                // 测试1：配置加载
                TestConfigLoading();

                // 测试2：控制台管理器检测
                TestConsoleManagerDetection();

                // 测试3：服务器状态获取
                TestServerStatusRetrieval();

                // 测试4：健康检查
                await TestHealthCheck();

                Debug.Log("=== 所有测试完成 ===");
            }
            catch (Exception ex)
            {
                Debug.LogError($"测试过程中发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试配置加载
        /// </summary>
        private static void TestConfigLoading()
        {
            Debug.Log("测试1：配置加载");

            try
            {
                var config = ServerManagementSettings.GetConfig();
                Debug.Log($"配置加载成功: StartupMode={config.startupMode}, AutoStart={config.autoStartOnUnityLaunch}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"配置加载测试失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试控制台管理器检测
        /// </summary>
        private static void TestConsoleManagerDetection()
        {
            Debug.Log("测试2：控制台管理器检测");

            try
            {
                bool isRunning = ConsoleManagerService.IsConsoleManagerRunning();
                Debug.Log($"控制台管理器检测结果: {(isRunning ? "运行中" : "未运行")}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"控制台管理器检测测试失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试服务器状态获取
        /// </summary>
        private static void TestServerStatusRetrieval()
        {
            Debug.Log("测试3：服务器状态获取");

            try
            {
                var status = ConsoleManagerService.GetServerStatus();
                Debug.Log($"服务器状态: Running={status.isRunning}, Healthy={status.isHealthy}, UnityPort={status.unityPort}, McpPort={status.mcpPort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"服务器状态获取测试失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试健康检查
        /// </summary>
        private static async Task TestHealthCheck()
        {
            Debug.Log("测试4：健康检查");

            try
            {
                bool isHealthy = await ConsoleManagerService.CheckServerHealthAsync();
                Debug.Log($"健康检查结果: {(isHealthy ? "健康" : "不健康")}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"健康检查测试失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试控制台管理器启动
        /// </summary>
        [UnityEditor.MenuItem("Tools/Unity MCP/Test Console Manager Start")]
        public static async void TestConsoleManagerStart()
        {
            Debug.Log("=== 测试控制台管理器启动 ===");

            try
            {
                var config = ServerManagementSettings.GetConfig();
                bool success = await ConsoleManagerService.StartConsoleManagerAsync(config);
                Debug.Log($"控制台管理器启动结果: {(success ? "成功" : "失败")}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"控制台管理器启动测试失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试控制台管理器停止
        /// </summary>
        [UnityEditor.MenuItem("Tools/Unity MCP/Test Console Manager Stop")]
        public static async void TestConsoleManagerStop()
        {
            Debug.Log("=== 测试控制台管理器停止 ===");

            try
            {
                bool success = await ConsoleManagerService.StopConsoleManagerAsync();
                Debug.Log($"控制台管理器停止结果: {(success ? "成功" : "失败")}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"控制台管理器停止测试失败: {ex.Message}");
            }
        }
    }
}