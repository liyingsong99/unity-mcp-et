@echo off
chcp 65001 >nul
title Unity MCP Manager

echo ========================================
echo    Unity MCP Manager 启动脚本
echo ========================================
echo.

:: 检查.NET环境
echo [1/4] 检查.NET环境...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo 错误: 未检测到.NET环境，请先安装.NET 8.0或更高版本
    echo 下载地址: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)
echo ✓ .NET环境检查通过

:: 检查项目文件
echo [2/4] 检查项目文件...
if not exist "UnityMcpManager~.csproj" (
    echo 错误: 未找到UnityMcpManager~.csproj文件
    echo 请确保在正确的目录下运行此脚本
    pause
    exit /b 1
)
echo ✓ 项目文件检查通过

:: 构建项目
echo [3/4] 构建项目...
dotnet build --configuration Release --verbosity quiet
if %errorlevel% neq 0 (
    echo 错误: 项目构建失败
    pause
    exit /b 1
)
echo ✓ 项目构建完成

:: 启动程序
echo [4/4] 启动Unity MCP Manager...
echo.
echo 提示: 
echo - 输入 'help' 查看所有命令
echo - 输入 'start' 启动MCP服务器
echo - 输入 'status' 查看服务器状态
echo - 输入 'quit' 退出程序
echo.

dotnet run --configuration Release

echo.
echo 程序已退出
pause 