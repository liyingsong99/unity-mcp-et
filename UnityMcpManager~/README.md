# Unity MCP Server Manager

Unity MCP服务器管理器是一个独立的C#控制台应用程序，用于管理Unity MCP服务器的启动、监控和端口管理。它解决了传统MCP服务器在Unity开发过程中经常遇到的端口占用和连接稳定性问题。

## 功能特性

### 🚀 智能端口管理

- **动态端口分配**：自动检测可用端口，避免端口冲突
- **端口冲突解决**：检测到端口冲突时自动重新分配
- **端口使用监控**：实时监控端口使用情况

### 🔧 进程生命周期管理

- **启动管理**：检查现有进程，避免重复启动
- **优雅关闭**：支持优雅关闭和强制终止
- **自动重启**：进程异常时自动重启

### 🏥 健康检查系统

- **定期检查**：定期检查MCP服务器健康状态
- **自动恢复**：检测到问题时自动尝试恢复
- **性能监控**：监控内存使用和CPU时间

### ⚙️ 配置管理

- **灵活配置**：支持JSON配置文件
- **配置验证**：启动前验证配置有效性
- **热重载**：支持运行时重新加载配置

## 系统要求

- **.NET 6.0** 或更高版本
- **Python 3.12+** 和必要的依赖包
- **Windows 10/11** 或 **Linux/macOS**

## 安装和配置

### 1. 编译项目

```bash
cd UnityMcpManager~
dotnet build -c Release
```

### 2. 配置设置

编辑 `appsettings.json` 文件：

```json
{
  "UnityMcpServer": {
    "PythonExecutable": "python",
    "ServerScriptPath": "../UnityMcpServer~/src/server.py",
    "WorkingDirectory": "../UnityMcpServer~/src",
    "DefaultUnityPort": 6400,
    "DefaultMcpPort": 6500,
    "PortRange": {
      "Min": 6400,
      "Max": 6599
    },
    "HealthCheck": {
      "IntervalSeconds": 30,
      "TimeoutSeconds": 5,
      "MaxRetries": 3
    },
    "ProcessManagement": {
      "StartupTimeoutSeconds": 30,
      "ShutdownTimeoutSeconds": 10,
      "RestartDelaySeconds": 5
    }
  }
}
```

### 3. 确保Python环境

确保已安装Python 3.12+和必要的依赖：

```bash
pip install httpx>=0.27.2 mcp[cli]>=1.4.1
```

## 使用方法

### 使用批处理脚本（推荐）

#### 完整启动脚本

```bash
start.bat
```

- 检查.NET环境
- 验证项目文件
- 构建项目
- 启动交互模式

#### 快速启动脚本

```bash
quick-start.bat
```

- 直接启动已构建的程序
- 如果未构建则自动构建

#### 服务模式启动

```bash
start-service.bat
```

- 启动后台服务模式
- 自动管理MCP服务器

#### 清理工具

```bash
clean.bat
```

- 清理构建文件
- 删除临时文件

### 直接使用dotnet命令

#### 命令行模式

```bash
# 启动MCP服务器
dotnet run -- start

# 停止MCP服务器
dotnet run -- stop

# 重启MCP服务器
dotnet run -- restart

# 查看服务器状态
dotnet run -- status

# 查看健康检查状态
dotnet run -- health

# 查看配置信息
dotnet run -- config

# 查看端口使用情况
dotnet run -- ports

# 以服务模式运行
dotnet run -- service

# 显示帮助信息
dotnet run -- help
```

#### 交互模式

```bash
dotnet run
```

在交互模式中，您可以输入以下命令：

- `start` - 启动MCP服务器
- `stop` - 停止MCP服务器
- `restart` - 重启MCP服务器
- `status` - 显示服务器状态
- `health` - 显示健康检查状态
- `config` - 显示配置信息
- `ports` - 显示端口使用情况
- `help` - 显示帮助信息
- `quit` 或 `exit` - 退出程序

#### 服务模式

```bash
dotnet run -- service
```

在服务模式下：

- 自动启动MCP服务器
- 定期执行健康检查
- 自动处理端口冲突
- 异常时自动重启
- 按 Ctrl+C 停止服务

## 配置说明

### PythonExecutable

Python可执行文件的路径或命令。默认为 `"python"`。

### ServerScriptPath

MCP服务器脚本的完整路径。默认为 `"../UnityMcpServer~/src/server.py"`。

### WorkingDirectory

Python脚本的工作目录。默认为 `"../UnityMcpServer~/src"`。

### DefaultUnityPort / DefaultMcpPort

默认的Unity和MCP端口。如果端口被占用，会自动分配其他可用端口。

### PortRange

端口分配的范围。当默认端口不可用时，会在此范围内查找可用端口。

### HealthCheck

健康检查配置：

- `IntervalSeconds`：检查间隔（秒）
- `TimeoutSeconds`：连接超时时间（秒）
- `MaxRetries`：最大重试次数

### ProcessManagement

进程管理配置：

- `StartupTimeoutSeconds`：启动超时时间（秒）
- `ShutdownTimeoutSeconds`：关闭超时时间（秒）
- `RestartDelaySeconds`：重启延迟时间（秒）

## 解决端口占用问题

### 问题描述

传统的MCP服务器在Unity开发过程中经常遇到端口占用问题：

- Unity或游戏运行时占用端口
- 多个Unity实例导致端口冲突
- 服务器重启时端口释放延迟

### 解决方案

Unity MCP Server Manager通过以下方式解决这些问题：

1. **智能端口检测**：启动前检查端口可用性
2. **动态端口分配**：自动分配可用端口
3. **端口冲突解决**：检测到冲突时自动重新分配
4. **进程隔离**：独立进程管理，避免Unity重启影响
5. **健康监控**：定期检查端口和进程状态

## 故障排除

### 常见问题

#### 1. Python环境问题

**症状**：启动失败，提示Python不可用
**解决**：

- 确保Python已正确安装
- 检查PATH环境变量
- 验证Python版本（需要3.12+）

#### 2. 脚本路径问题

**症状**：启动失败，提示脚本不存在
**解决**：

- 检查`ServerScriptPath`配置
- 确保UnityMcpServer项目已正确部署
- 验证工作目录设置

#### 3. 端口冲突问题

**症状**：启动失败，提示端口被占用
**解决**：

- 检查端口使用情况：`UnityMcpManager~.exe ports`
- 调整端口范围配置
- 手动释放被占用的端口

#### 4. 权限问题

**症状**：无法启动进程或访问文件
**解决**：

- 以管理员权限运行
- 检查文件和目录权限
- 确保防火墙设置正确

### 日志分析

程序会输出详细的日志信息，包括：

- 启动和停止过程
- 端口分配情况
- 健康检查结果
- 错误和异常信息

### 调试模式

启用详细日志输出：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

## 性能优化

### 内存使用

- 程序本身内存占用约10-20MB
- MCP服务器内存占用取决于Python进程
- 建议定期重启以释放内存

### CPU使用

- 健康检查间隔可调整（默认30秒）
- 端口检测使用异步操作
- 进程监控开销较小

### 网络性能

- 端口检测使用本地连接
- 健康检查使用TCP连接测试
- 支持连接超时配置

## 开发说明

### 项目结构

```
UnityMcpManager~/
├── Models/           # 数据模型
├── Services/         # 核心服务
├── Utils/           # 工具类
├── Program.cs       # 主程序
├── appsettings.json # 配置文件
└── README.md        # 说明文档
```

### 扩展开发

- 添加新的健康检查项目
- 实现自定义端口管理策略
- 集成其他监控系统
- 添加Web管理界面

## 许可证

本项目采用MIT许可证。详见LICENSE文件。

## 贡献

欢迎提交Issue和Pull Request来改进这个项目。

## 更新日志

### v1.0.0

- 初始版本发布
- 支持基本的MCP服务器管理
- 实现智能端口管理
- 添加健康检查系统
- 提供命令行和交互模式
