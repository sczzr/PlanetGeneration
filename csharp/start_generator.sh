#!/bin/bash
# 地理文明生成器启动脚本

echo "🌍 启动地理文明生成器..."

# 构建项目
echo "🔨 正在构建项目..."
dotnet build PlanetGeneration.csproj

if [ $? -eq 0 ]; then
    echo "✅ 构建成功!"
    
    # 启动Godot编辑器
    echo "🎮 启动Godot编辑器..."
    godot --path . scenes/Main.tscn
    
else
    echo "❌ 构建失败，请检查错误信息"
    exit 1
fi