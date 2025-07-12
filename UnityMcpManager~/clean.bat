@echo off
chcp 65001 >nul
title Unity MCP Manager - 清理工具

echo ========================================
echo    Unity MCP Manager 清理工具
echo ========================================
echo.

echo 正在清理构建文件...
if exist "bin" (
    rmdir /s /q "bin"
    echo ✓ 已删除 bin 目录
)

if exist "obj" (
    rmdir /s /q "obj"
    echo ✓ 已删除 obj 目录
)

echo.
echo 正在清理临时文件...
if exist "*.tmp" del /q "*.tmp"
if exist "*.log" del /q "*.log"

echo.
echo 清理完成！
echo 提示: 运行 start.bat 重新构建项目
echo.
pause 