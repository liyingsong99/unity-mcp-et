@echo off
chcp 65001 >nul
title Unity MCP Manager - 服务模式

echo ========================================
echo    Unity MCP Manager 服务模式启动
echo ========================================
echo.

:: 检查.NET环境
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo 错误: 未检测到.NET环境
    pause
    exit /b 1
)

:: 构建项目
echo 构建项目...
dotnet build --configuration Release --verbosity quiet
if %errorlevel% neq 0 (
    echo 错误: 项目构建失败
    pause
    exit /b 1
)

:: 启动服务模式
echo 启动服务模式...
echo 提示: 服务模式将在后台运行，使用Ctrl+C停止
echo.

dotnet run --configuration Release -- --mode service

echo.
echo 服务已停止
pause 