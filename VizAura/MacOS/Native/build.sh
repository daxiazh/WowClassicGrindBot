#!/bin/bash
# ScreenCaptureKit Swift 动态库构建脚本
# 
# Release 模式 (默认,优化编译)
#  ./build.sh
#
# Debug 模式 (调试,无优化)
#   ./build.sh Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# 获取编译模式 (默认 Release)
BUILD_CONFIG=${1:-Release}

if [ "$BUILD_CONFIG" == "Debug" ]; then
    OPTIMIZATION="-Onone"
    echo "Building ScreenCaptureKit dynamic library (Debug mode)..."
else
    OPTIMIZATION="-O"
    echo "Building ScreenCaptureKit dynamic library (Release mode)..."
fi

# 编译 Swift 代码为动态库
swiftc -emit-library \
       $OPTIMIZATION \
       -o libScreenCapture.dylib \
       -module-name ScreenCapture \
       ScreenCaptureKit.swift \
       -framework ScreenCaptureKit \
       -framework CoreVideo \
       -framework Foundation \
       -target arm64-apple-macos12.3 \
       -Xlinker -install_name -Xlinker @rpath/libScreenCapture.dylib

echo "✓ libScreenCapture.dylib built successfully"

# 显示库信息
file libScreenCapture.dylib
otool -L libScreenCapture.dylib

echo ""
echo "Library ready at: $(pwd)/libScreenCapture.dylib"
