using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal; // Required for tag management
using UnityEngine;
using UnityMcpBridge.Editor.Helpers; // For Response class

namespace UnityMcpBridge.Editor.Tools
{
    /// <summary>
    /// Handles operations related to controlling and querying the Unity Editor state,
    /// including managing Tags and Layers.
    /// </summary>
    public static class ManageEditor
    {
        // Constant for starting user layer index
        private const int FirstUserLayerIndex = 8;

        // Constant for total layer count
        private const int TotalLayerCount = 32;

        /// <summary>
        /// Main handler for editor management actions.
        /// </summary>
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString().ToLower();
            // Parameters for specific actions
            string tagName = @params["tagName"]?.ToString();
            string layerName = @params["layerName"]?.ToString();
            bool waitForCompletion = @params["waitForCompletion"]?.ToObject<bool>() ?? false; // Example - not used everywhere

            if (string.IsNullOrEmpty(action))
            {
                return Response.Error("Action parameter is required.");
            }

            // Route action
            switch (action)
            {
                // Play Mode Control
                case "play":
                    try
                    {
                        if (!EditorApplication.isPlaying)
                        {
                            EditorApplication.isPlaying = true;
                            return Response.Success("Entered play mode.");
                        }
                        return Response.Success("Already in play mode.");
                    }
                    catch (Exception e)
                    {
                        return Response.Error($"Error entering play mode: {e.Message}");
                    }
                case "pause":
                    try
                    {
                        if (EditorApplication.isPlaying)
                        {
                            EditorApplication.isPaused = !EditorApplication.isPaused;
                            return Response.Success(
                                EditorApplication.isPaused ? "Game paused." : "Game resumed."
                            );
                        }
                        return Response.Error("Cannot pause/resume: Not in play mode.");
                    }
                    catch (Exception e)
                    {
                        return Response.Error($"Error pausing/resuming game: {e.Message}");
                    }
                case "stop":
                    try
                    {
                        if (EditorApplication.isPlaying)
                        {
                            EditorApplication.isPlaying = false;
                            return Response.Success("Exited play mode.");
                        }
                        return Response.Success("Already stopped (not in play mode).");
                    }
                    catch (Exception e)
                    {
                        return Response.Error($"Error stopping play mode: {e.Message}");
                    }

                // Editor State/Info
                case "get_state":
                    return GetEditorState();
                case "get_windows":
                    return GetEditorWindows();
                case "get_active_tool":
                    return GetActiveTool();
                case "get_selection":
                    return GetSelection();
                case "set_active_tool":
                    string toolName = @params["toolName"]?.ToString();
                    if (string.IsNullOrEmpty(toolName))
                        return Response.Error("'toolName' parameter required for set_active_tool.");
                    return SetActiveTool(toolName);

                // Tag Management
                case "add_tag":
                    if (string.IsNullOrEmpty(tagName))
                        return Response.Error("'tagName' parameter required for add_tag.");
                    return AddTag(tagName);
                case "remove_tag":
                    if (string.IsNullOrEmpty(tagName))
                        return Response.Error("'tagName' parameter required for remove_tag.");
                    return RemoveTag(tagName);
                case "get_tags":
                    return GetTags(); // Helper to list current tags

                // Layer Management
                case "add_layer":
                    if (string.IsNullOrEmpty(layerName))
                        return Response.Error("'layerName' parameter required for add_layer.");
                    return AddLayer(layerName);
                case "remove_layer":
                    if (string.IsNullOrEmpty(layerName))
                        return Response.Error("'layerName' parameter required for remove_layer.");
                    return RemoveLayer(layerName);
                case "get_layers":
                    return GetLayers(); // Helper to list current layers

                // Unity资源刷新和编译管理
                case "refresh_assets":
                    bool forceRefresh = @params["forceRefresh"]?.ToObject<bool>() ?? false;
                    return RefreshAssets(forceRefresh);
                case "save_assets":
                    return SaveAssets();
                case "recompile_scripts":
                    return RecompileScripts();
                case "full_refresh":
                    return FullRefresh();
                case "get_compilation_status":
                    return GetCompilationStatus();

                // --- Settings (Example) ---
                // case "set_resolution":
                //     int? width = @params["width"]?.ToObject<int?>();
                //     int? height = @params["height"]?.ToObject<int?>();
                //     if (!width.HasValue || !height.HasValue) return Response.Error("'width' and 'height' parameters required.");
                //     return SetGameViewResolution(width.Value, height.Value);
                // case "set_quality":
                //     // Handle string name or int index
                //     return SetQualityLevel(@params["qualityLevel"]);

                default:
                    return Response.Error(
                        $"Unknown action: '{action}'. Supported actions include play, pause, stop, get_state, get_windows, get_active_tool, get_selection, set_active_tool, add_tag, remove_tag, get_tags, add_layer, remove_layer, get_layers, refresh_assets, save_assets, recompile_scripts, full_refresh, get_compilation_status."
                    );
            }
        }

        // --- Editor State/Info Methods ---
        private static object GetEditorState()
        {
            try
            {
                var state = new
                {
                    isPlaying = EditorApplication.isPlaying,
                    isPaused = EditorApplication.isPaused,
                    isCompiling = EditorApplication.isCompiling,
                    isUpdating = EditorApplication.isUpdating,
                    applicationPath = EditorApplication.applicationPath,
                    applicationContentsPath = EditorApplication.applicationContentsPath,
                    timeSinceStartup = EditorApplication.timeSinceStartup,
                };
                return Response.Success("Retrieved editor state.", state);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting editor state: {e.Message}");
            }
        }

        private static object GetEditorWindows()
        {
            try
            {
                // Get all types deriving from EditorWindow
                var windowTypes = AppDomain
                    .CurrentDomain.GetAssemblies()
                    .SelectMany(assembly => assembly.GetTypes())
                    .Where(type => type.IsSubclassOf(typeof(EditorWindow)))
                    .ToList();

                var openWindows = new List<object>();

                // Find currently open instances
                // Resources.FindObjectsOfTypeAll seems more reliable than GetWindow for finding *all* open windows
                EditorWindow[] allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();

                foreach (EditorWindow window in allWindows)
                {
                    if (window == null)
                        continue; // Skip potentially destroyed windows

                    try
                    {
                        openWindows.Add(
                            new
                            {
                                title = window.titleContent.text,
                                typeName = window.GetType().FullName,
                                isFocused = EditorWindow.focusedWindow == window,
                                position = new
                                {
                                    x = window.position.x,
                                    y = window.position.y,
                                    width = window.position.width,
                                    height = window.position.height,
                                },
                                instanceID = window.GetInstanceID(),
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"Could not get info for window {window.GetType().Name}: {ex.Message}"
                        );
                    }
                }

                return Response.Success("Retrieved list of open editor windows.", openWindows);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting editor windows: {e.Message}");
            }
        }

        private static object GetActiveTool()
        {
            try
            {
                Tool currentTool = UnityEditor.Tools.current;
                string toolName = currentTool.ToString(); // Enum to string
                bool customToolActive = UnityEditor.Tools.current == Tool.Custom; // Check if a custom tool is active
                string activeToolName = customToolActive
                    ? EditorTools.GetActiveToolName()
                    : toolName; // Get custom name if needed

                var toolInfo = new
                {
                    activeTool = activeToolName,
                    isCustom = customToolActive,
                    pivotMode = UnityEditor.Tools.pivotMode.ToString(),
                    pivotRotation = UnityEditor.Tools.pivotRotation.ToString(),
                    handleRotation = UnityEditor.Tools.handleRotation.eulerAngles, // Euler for simplicity
                    handlePosition = UnityEditor.Tools.handlePosition,
                };

                return Response.Success("Retrieved active tool information.", toolInfo);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting active tool: {e.Message}");
            }
        }

        private static object SetActiveTool(string toolName)
        {
            try
            {
                Tool targetTool;
                if (Enum.TryParse<Tool>(toolName, true, out targetTool)) // Case-insensitive parse
                {
                    // Check if it's a valid built-in tool
                    if (targetTool != Tool.None && targetTool <= Tool.Custom) // Tool.Custom is the last standard tool
                    {
                        UnityEditor.Tools.current = targetTool;
                        return Response.Success($"Set active tool to '{targetTool}'.");
                    }
                    else
                    {
                        return Response.Error(
                            $"Cannot directly set tool to '{toolName}'. It might be None, Custom, or invalid."
                        );
                    }
                }
                else
                {
                    // Potentially try activating a custom tool by name here if needed
                    // This often requires specific editor scripting knowledge for that tool.
                    return Response.Error(
                        $"Could not parse '{toolName}' as a standard Unity Tool (View, Move, Rotate, Scale, Rect, Transform, Custom)."
                    );
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error setting active tool: {e.Message}");
            }
        }

        private static object GetSelection()
        {
            try
            {
                var selectionInfo = new
                {
                    activeObject = Selection.activeObject?.name,
                    activeGameObject = Selection.activeGameObject?.name,
                    activeTransform = Selection.activeTransform?.name,
                    activeInstanceID = Selection.activeInstanceID,
                    count = Selection.count,
                    objects = Selection
                        .objects.Select(obj => new
                        {
                            name = obj?.name,
                            type = obj?.GetType().FullName,
                            instanceID = obj?.GetInstanceID(),
                        })
                        .ToList(),
                    gameObjects = Selection
                        .gameObjects.Select(go => new
                        {
                            name = go?.name,
                            instanceID = go?.GetInstanceID(),
                        })
                        .ToList(),
                    assetGUIDs = Selection.assetGUIDs, // GUIDs for selected assets in Project view
                };

                return Response.Success("Retrieved current selection details.", selectionInfo);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting selection: {e.Message}");
            }
        }

        // --- Tag Management Methods ---

        private static object AddTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return Response.Error("Tag name cannot be empty or whitespace.");

            // Check if tag already exists
            if (InternalEditorUtility.tags.Contains(tagName))
            {
                return Response.Error($"Tag '{tagName}' already exists.");
            }

            try
            {
                // Add the tag using the internal utility
                InternalEditorUtility.AddTag(tagName);
                // Force save assets to ensure the change persists in the TagManager asset
                AssetDatabase.SaveAssets();
                return Response.Success($"Tag '{tagName}' added successfully.");
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to add tag '{tagName}': {e.Message}");
            }
        }

        private static object RemoveTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return Response.Error("Tag name cannot be empty or whitespace.");
            if (tagName.Equals("Untagged", StringComparison.OrdinalIgnoreCase))
                return Response.Error("Cannot remove the built-in 'Untagged' tag.");

            // Check if tag exists before attempting removal
            if (!InternalEditorUtility.tags.Contains(tagName))
            {
                return Response.Error($"Tag '{tagName}' does not exist.");
            }

            try
            {
                // Remove the tag using the internal utility
                InternalEditorUtility.RemoveTag(tagName);
                // Force save assets
                AssetDatabase.SaveAssets();
                return Response.Success($"Tag '{tagName}' removed successfully.");
            }
            catch (Exception e)
            {
                // Catch potential issues if the tag is somehow in use or removal fails
                return Response.Error($"Failed to remove tag '{tagName}': {e.Message}");
            }
        }

        private static object GetTags()
        {
            try
            {
                string[] tags = InternalEditorUtility.tags;
                return Response.Success("Retrieved current tags.", tags);
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to retrieve tags: {e.Message}");
            }
        }

        // --- Layer Management Methods ---

        private static object AddLayer(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                return Response.Error("Layer name cannot be empty or whitespace.");

            // Access the TagManager asset
            SerializedObject tagManager = GetTagManager();
            if (tagManager == null)
                return Response.Error("Could not access TagManager asset.");

            SerializedProperty layersProp = tagManager.FindProperty("layers");
            if (layersProp == null || !layersProp.isArray)
                return Response.Error("Could not find 'layers' property in TagManager.");

            // Check if layer name already exists (case-insensitive check recommended)
            for (int i = 0; i < TotalLayerCount; i++)
            {
                SerializedProperty layerSP = layersProp.GetArrayElementAtIndex(i);
                if (
                    layerSP != null
                    && layerName.Equals(layerSP.stringValue, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return Response.Error($"Layer '{layerName}' already exists at index {i}.");
                }
            }

            // Find the first empty user layer slot (indices 8 to 31)
            int firstEmptyUserLayer = -1;
            for (int i = FirstUserLayerIndex; i < TotalLayerCount; i++)
            {
                SerializedProperty layerSP = layersProp.GetArrayElementAtIndex(i);
                if (layerSP != null && string.IsNullOrEmpty(layerSP.stringValue))
                {
                    firstEmptyUserLayer = i;
                    break;
                }
            }

            if (firstEmptyUserLayer == -1)
            {
                return Response.Error("No empty User Layer slots available (8-31 are full).");
            }

            // Assign the name to the found slot
            try
            {
                SerializedProperty targetLayerSP = layersProp.GetArrayElementAtIndex(
                    firstEmptyUserLayer
                );
                targetLayerSP.stringValue = layerName;
                // Apply the changes to the TagManager asset
                tagManager.ApplyModifiedProperties();
                // Save assets to make sure it's written to disk
                AssetDatabase.SaveAssets();
                return Response.Success(
                    $"Layer '{layerName}' added successfully to slot {firstEmptyUserLayer}."
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to add layer '{layerName}': {e.Message}");
            }
        }

        private static object RemoveLayer(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                return Response.Error("Layer name cannot be empty or whitespace.");

            // Access the TagManager asset
            SerializedObject tagManager = GetTagManager();
            if (tagManager == null)
                return Response.Error("Could not access TagManager asset.");

            SerializedProperty layersProp = tagManager.FindProperty("layers");
            if (layersProp == null || !layersProp.isArray)
                return Response.Error("Could not find 'layers' property in TagManager.");

            // Find the layer by name (must be user layer)
            int layerIndexToRemove = -1;
            for (int i = FirstUserLayerIndex; i < TotalLayerCount; i++) // Start from user layers
            {
                SerializedProperty layerSP = layersProp.GetArrayElementAtIndex(i);
                // Case-insensitive comparison is safer
                if (
                    layerSP != null
                    && layerName.Equals(layerSP.stringValue, StringComparison.OrdinalIgnoreCase)
                )
                {
                    layerIndexToRemove = i;
                    break;
                }
            }

            if (layerIndexToRemove == -1)
            {
                return Response.Error($"User layer '{layerName}' not found.");
            }

            // Clear the name for that index
            try
            {
                SerializedProperty targetLayerSP = layersProp.GetArrayElementAtIndex(
                    layerIndexToRemove
                );
                targetLayerSP.stringValue = string.Empty; // Set to empty string to remove
                // Apply the changes
                tagManager.ApplyModifiedProperties();
                // Save assets
                AssetDatabase.SaveAssets();
                return Response.Success(
                    $"Layer '{layerName}' (slot {layerIndexToRemove}) removed successfully."
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to remove layer '{layerName}': {e.Message}");
            }
        }

        private static object GetLayers()
        {
            try
            {
                var layers = new Dictionary<int, string>();
                for (int i = 0; i < TotalLayerCount; i++)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(layerName)) // Only include layers that have names
                    {
                        layers.Add(i, layerName);
                    }
                }
                return Response.Success("Retrieved current named layers.", layers);
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to retrieve layers: {e.Message}");
            }
        }

        // --- Unity资源刷新和编译管理方法 ---

        /// <summary>
        /// 刷新Unity资源数据库，使Unity重新扫描和导入资源
        /// </summary>
        /// <param name="forceRefresh">是否强制刷新所有资源</param>
        /// <returns>操作结果</returns>
        private static object RefreshAssets(bool forceRefresh = false)
        {
            try
            {
                if (forceRefresh)
                {
                    // 强制刷新，使用ImportAssetOptions.ForceUpdate
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    return Response.Success("资源数据库已强制刷新，所有资源已重新导入。");
                }
                else
                {
                    // 标准刷新
                    AssetDatabase.Refresh();
                    return Response.Success("资源数据库已刷新。");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManageEditor.RefreshAssets] 刷新资源时出错: {e}");
                return Response.Error($"刷新资源失败: {e.Message}");
            }
        }

        /// <summary>
        /// 保存所有已修改的资源到磁盘
        /// </summary>
        /// <returns>操作结果</returns>
        private static object SaveAssets()
        {
            try
            {
                AssetDatabase.SaveAssets();
                return Response.Success("所有已修改的资源已保存到磁盘。");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManageEditor.SaveAssets] 保存资源时出错: {e}");
                return Response.Error($"保存资源失败: {e.Message}");
            }
        }

        /// <summary>
        /// 请求重新编译脚本和程序集
        /// </summary>
        /// <returns>操作结果</returns>
        private static object RecompileScripts()
        {
            try
            {
                if (EditorApplication.isCompiling)
                {
                    return Response.Success("Unity当前正在编译中，无需重复请求编译。", new { isCompiling = true });
                }

                EditorUtility.RequestScriptReload();
                return Response.Success("已请求重新编译脚本，Unity将开始编译程序集。", new { isCompiling = EditorApplication.isCompiling });
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManageEditor.RecompileScripts] 重新编译脚本时出错: {e}");
                return Response.Error($"重新编译脚本失败: {e.Message}");
            }
        }

        /// <summary>
        /// 执行完整的刷新流程：保存资源、刷新资源数据库、重新编译脚本
        /// </summary>
        /// <returns>操作结果</returns>
        private static object FullRefresh()
        {
            try
            {
                // 1. 保存所有修改的资源
                AssetDatabase.SaveAssets();
                Debug.Log("[ManageEditor.FullRefresh] 步骤1: 已保存所有修改的资源");

                // 2. 刷新资源数据库
                AssetDatabase.Refresh();
                Debug.Log("[ManageEditor.FullRefresh] 步骤2: 已刷新资源数据库");

                // 3. 请求重新编译（如果当前没在编译）
                if (!EditorApplication.isCompiling)
                {
                    EditorUtility.RequestScriptReload();
                    Debug.Log("[ManageEditor.FullRefresh] 步骤3: 已请求重新编译脚本");
                }
                else
                {
                    Debug.Log("[ManageEditor.FullRefresh] 步骤3: Unity当前正在编译，跳过重新编译请求");
                }

                return Response.Success("完整刷新已执行：已保存资源、刷新数据库并请求重新编译。", new
                {
                    savedAssets = true,
                    refreshedDatabase = true,
                    requestedRecompile = !EditorApplication.isCompiling,
                    isCompiling = EditorApplication.isCompiling
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManageEditor.FullRefresh] 执行完整刷新时出错: {e}");
                return Response.Error($"完整刷新失败: {e.Message}");
            }
        }

        /// <summary>
        /// 获取当前编译状态信息
        /// </summary>
        /// <returns>编译状态信息</returns>
        private static object GetCompilationStatus()
        {
            try
            {
                var compilationStatus = new
                {
                    isCompiling = EditorApplication.isCompiling,
                    isUpdating = EditorApplication.isUpdating,
                    timeSinceStartup = EditorApplication.timeSinceStartup,
                    // 添加编译相关的详细状态
                    canRecompile = !EditorApplication.isCompiling && !EditorApplication.isUpdating
                };

                string statusMessage = EditorApplication.isCompiling ? "Unity正在编译中" :
                                     EditorApplication.isUpdating ? "Unity正在更新中" :
                                     "Unity处于空闲状态，可以执行编译操作";

                return Response.Success(statusMessage, compilationStatus);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManageEditor.GetCompilationStatus] 获取编译状态时出错: {e}");
                return Response.Error($"获取编译状态失败: {e.Message}");
            }
        }

        // --- Helper Methods ---

        /// <summary>
        /// Gets the SerializedObject for the TagManager asset.
        /// </summary>
        private static SerializedObject GetTagManager()
        {
            try
            {
                // Load the TagManager asset from the ProjectSettings folder
                UnityEngine.Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath(
                    "ProjectSettings/TagManager.asset"
                );
                if (tagManagerAssets == null || tagManagerAssets.Length == 0)
                {
                    Debug.LogError("[ManageEditor] TagManager.asset not found in ProjectSettings.");
                    return null;
                }
                // The first object in the asset file should be the TagManager
                return new SerializedObject(tagManagerAssets[0]);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManageEditor] Error accessing TagManager.asset: {e.Message}");
                return null;
            }
        }

        // --- Example Implementations for Settings ---
        /*
        private static object SetGameViewResolution(int width, int height) { ... }
        private static object SetQualityLevel(JToken qualityLevelToken) { ... }
        */
    }

    // Helper class to get custom tool names (remains the same)
    internal static class EditorTools
    {
        public static string GetActiveToolName()
        {
            // This is a placeholder. Real implementation depends on how custom tools
            // are registered and tracked in the specific Unity project setup.
            // It might involve checking static variables, calling methods on specific tool managers, etc.
            if (UnityEditor.Tools.current == Tool.Custom)
            {
                // Example: Check a known custom tool manager
                // if (MyCustomToolManager.IsActive) return MyCustomToolManager.ActiveToolName;
                return "Unknown Custom Tool";
            }
            return UnityEditor.Tools.current.ToString();
        }
    }
}

