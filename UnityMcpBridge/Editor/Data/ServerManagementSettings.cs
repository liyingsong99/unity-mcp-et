using System;
using System.IO;
using UnityEngine;
using UnityMcpBridge.Editor.Models;

namespace UnityMcpBridge.Editor.Data
{
    /// <summary>
    /// 服务器管理设置类
    /// 负责加载和保存服务器管理配置
    /// </summary>
    public static class ServerManagementSettings
    {
        private static readonly string SettingsPath = Path.Combine(Application.dataPath, "..", "UnityMcpBridge", "Editor", "Data", "ServerManagementSettings.json");
        private static ServerManagementConfig _cachedConfig;

        /// <summary>
        /// 获取服务器管理配置
        /// </summary>
        /// <returns>服务器管理配置</returns>
        public static ServerManagementConfig GetConfig()
        {
            if (_cachedConfig != null)
            {
                return _cachedConfig;
            }

            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    _cachedConfig = JsonUtility.FromJson<ServerManagementConfig>(json);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"加载服务器管理配置时出错: {ex.Message}");
            }

            // 如果加载失败或文件不存在，使用默认配置
            if (_cachedConfig == null)
            {
                _cachedConfig = new ServerManagementConfig();
                SaveConfig(_cachedConfig);
            }

            return _cachedConfig;
        }

        /// <summary>
        /// 保存服务器管理配置
        /// </summary>
        /// <param name="config">配置对象</param>
        public static void SaveConfig(ServerManagementConfig config)
        {
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(config, true);
                File.WriteAllText(SettingsPath, json);
                _cachedConfig = config;
            }
            catch (Exception ex)
            {
                Debug.LogError($"保存服务器管理配置时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public static void ResetToDefault()
        {
            _cachedConfig = new ServerManagementConfig();
            SaveConfig(_cachedConfig);
        }

        /// <summary>
        /// 更新配置
        /// </summary>
        /// <param name="updater">配置更新函数</param>
        public static void UpdateConfig(Action<ServerManagementConfig> updater)
        {
            var config = GetConfig();
            updater(config);
            SaveConfig(config);
        }
    }
}