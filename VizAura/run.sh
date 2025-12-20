#!/bin/bash

# 获取脚本所在的目录
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# 设置工作目录为脚本所在目录
cd "$SCRIPT_DIR"

# 检查VizAura可执行文件是否存在
if [ ! -f "./VizAura" ]; then
    echo "错误: 找不到VizAura可执行文件"
    echo "请确保此脚本与VizAura可执行文件在同一目录下"
    read -p "按回车键退出..."
    exit 1
fi

# 运行VizAura应用程序
echo "正在启动VizAura..."
./VizAura

# 检查应用程序是否正常退出
if [ $? -ne 0 ]; then
    echo "应用程序运行出错"
    read -p "按回车键退出..."
fi