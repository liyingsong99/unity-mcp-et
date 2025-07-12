"""
Configuration settings for the Unity MCP Server.
This file contains all configurable parameters for the server.
"""

from dataclasses import dataclass
from typing import List, Optional

@dataclass
class ServerConfig:
    """Main configuration class for the MCP server."""
    
    # Network settings
    unity_host: str = "localhost"
    unity_port: int = 6400
    mcp_port: int = 6500
    
    # Connection settings
    connection_timeout: float = 86400.0  # 24 hours timeout
    buffer_size: int = 16 * 1024 * 1024  # 16MB buffer
    
    # Logging settings
    log_level: str = "INFO"
    log_format: str = "%(asctime)s - %(name)s - %(levelname)s - %(message)s"
    
    # Server settings
    max_retries: int = 3
    retry_delay: float = 1.0
    
    # Whitelist settings - 白名单配置
    auto_approve_tools: bool = True  # 是否自动批准工具执行
    whitelisted_actions: Optional[List[str]] = None  # 白名单操作列表
    
    def __post_init__(self):
        """Post-initialization to set default whitelisted actions."""
        if self.whitelisted_actions is None:
            # 默认白名单：包含所有Unity MCP操作
            self.whitelisted_actions = [
                # 编辑器管理操作
                "manage_editor.get_state",
                "manage_editor.get_compilation_status", 
                "manage_editor.refresh_assets",
                "manage_editor.save_assets",
                "manage_editor.recompile_scripts",
                "manage_editor.full_refresh",
                "manage_editor.get_tags",
                "manage_editor.get_layers",
                "manage_editor.add_tag",
                "manage_editor.remove_tag",
                "manage_editor.add_layer",
                "manage_editor.remove_layer",
                "manage_editor.play",
                "manage_editor.pause", 
                "manage_editor.stop",
                
                # 场景管理操作
                "manage_scene.get_hierarchy",
                "manage_scene.load",
                "manage_scene.save",
                "manage_scene.create",
                
                # 游戏对象管理操作
                "manage_gameobject.find",
                "manage_gameobject.create",
                "manage_gameobject.modify",
                "manage_gameobject.delete",
                
                # 资源管理操作
                "manage_asset.search",
                "manage_asset.get_info",
                "manage_asset.create",
                "manage_asset.modify",
                "manage_asset.delete",
                "manage_asset.import",
                "manage_asset.refresh",
                
                # 脚本管理操作
                "manage_script.read",
                "manage_script.create",
                "manage_script.update",
                "manage_script.delete",
                
                # 控制台操作
                "read_console.get",
                "read_console.clear",
                
                # 菜单执行操作
                "execute_menu_item.execute"
            ]

# Create a global config instance
config = ServerConfig() 