using System;
using UnityEngine;

namespace UnityMcpBridge.Editor.Models
{
    /// <summary>
    /// 服务器启动方式枚举
    /// </summary>
    public enum ServerStartupMode
    {
        /// <summary>
        /// 自动检测：优先使用控制台管理器，如果不可用则使用Python直接启动
        /// </summary>
        Auto,

        /// <summary>
        /// 控制台管理器：使用UnityMcpManager控制台程序管理服务器
        /// </summary>
        ConsoleManager,

        /// <summary>
        /// Python直接启动：直接启动Python服务器
        /// </summary>
        PythonDirect
    }

    /// <summary>
    /// 服务器管理配置类
    /// </summary>
    [Serializable]
    public class ServerManagementConfig
    {
        /// <summary>
        /// 服务器启动方式
        /// </summary>
        public ServerStartupMode startupMode = ServerStartupMode.Auto;

        /// <summary>
        /// 控制台管理器可执行文件路径
        /// </summary>
        public string consoleManagerPath = "UnityMcpManager~/UnityMcpManager~.exe";

        /// <summary>
        /// 是否在Unity启动时自动启动服务器
        /// </summary>
        public bool autoStartOnUnityLaunch = true;

        /// <summary>
        /// 控制台管理器启动超时时间（秒）
        /// </summary>
        public int consoleManagerStartupTimeout = 30;

        /// <summary>
        /// 是否启用健康检查
        /// </summary>
        public bool enableHealthCheck = true;

        /// <summary>
        /// 健康检查间隔（秒）
        /// </summary>
        public int healthCheckInterval = 30;

        /// <summary>
        /// 端口分配策略
        /// </summary>
        public PortAllocationStrategy portStrategy = PortAllocationStrategy.Auto;

        /// <summary>
        /// 默认Unity端口
        /// </summary>
        public int defaultUnityPort = 6400;

        /// <summary>
        /// 默认MCP端口
        /// </summary>
        public int defaultMcpPort = 6500;

        /// <summary>
        /// 端口范围最小值
        /// </summary>
        public int portRangeMin = 6400;

        /// <summary>
        /// 端口范围最大值
        /// </summary>
        public int portRangeMax = 6599;
    }

    /// <summary>
    /// 端口分配策略枚举
    /// </summary>
    public enum PortAllocationStrategy
    {
        /// <summary>
        /// 自动分配：优先使用默认端口，如果被占用则自动分配其他端口
        /// </summary>
        Auto,

        /// <summary>
        /// 固定端口：使用配置的默认端口，如果被占用则失败
        /// </summary>
        Fixed,

        /// <summary>
        /// 动态分配：总是分配可用端口，不固定
        /// </summary>
        Dynamic
    }

    /// <summary>
    /// 服务器状态信息
    /// </summary>
    [Serializable]
    public class ServerStatusInfo
    {
        /// <summary>
        /// 服务器是否运行
        /// </summary>
        public bool isRunning;

        /// <summary>
        /// 当前使用的Unity端口
        /// </summary>
        public int unityPort;

        /// <summary>
        /// 当前使用的MCP端口
        /// </summary>
        public int mcpPort;

        /// <summary>
        /// 服务器启动方式
        /// </summary>
        public ServerStartupMode startupMode;

        /// <summary>
        /// 控制台管理器是否运行
        /// </summary>
        public bool consoleManagerRunning;

        /// <summary>
        /// 最后健康检查时间
        /// </summary>
        public DateTime lastHealthCheck;

        /// <summary>
        /// 健康状态
        /// </summary>
        public bool isHealthy;

        /// <summary>
        /// 错误信息
        /// </summary>
        public string errorMessage;
    }
}