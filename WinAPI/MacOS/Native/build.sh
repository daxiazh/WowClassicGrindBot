#!/bin/bash

# ScreenCaptureKit Swift 动态库构建脚本

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "Building ScreenCaptureKit dynamic library..."

# 编译 Swift 代码为动态库
swiftc -emit-library \
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
