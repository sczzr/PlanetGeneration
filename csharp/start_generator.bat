@echo off
title 地理文明生成器

echo 🌍 启动地理文明生成器...

REM 构建项目
echo 🔨 正在构建项目...
dotnet build PlanetGeneration.csproj

if %ERRORLEVEL% EQU 0 (
    echo ✅ 构建成功!
    
    REM 启动Godot编辑器
    echo 🎮 启动Godot编辑器...
    godot --path . scenes/Main.tscn
    
) else (
    echo ❌ 构建失败，请检查错误信息
    pause
    exit /b 1
)