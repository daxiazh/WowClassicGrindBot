# VizAura

**macOS 专用的魔兽世界 Hekili 辅助按键工具**

## 功能简介

VizAura 是一个基于 Avalonia MVVM 框架开发的桌面应用程序,专为 macOS 设计,用于辅助魔兽世界玩家根据 Hekili 插件的技能推荐自动执行按键操作。

### 核心功能

- **屏幕截图分析**: 从魔兽世界游戏窗口捕获屏幕数据
- **游戏数据提取**: 通过 DataToColor 插件从截图中解析游戏状态信息
- **Hekili 集成**: 读取 Hekili 插件推荐的技能队列
- **自动按键**: 根据推荐的技能自动执行相应的键盘操作
- **macOS 原生支持**: 使用 macOS 系统 API 实现屏幕捕获和输入模拟

## 技术架构

- **UI 框架**: Avalonia (跨平台 .NET UI 框架)
- **架构模式**: MVVM (Model-View-ViewModel)
- **目标平台**: macOS (需要 .NET 9.0 运行时)
- **依赖项目**: 
  - Core - 核心游戏逻辑
  - SharedLib - 共享工具库

## 平台支持

⚠️ **当前仅支持 macOS**

VizAura 使用 macOS 特定的系统 API 来实现:
- 屏幕捕获 (Screen Capture)
- 键盘/鼠标输入模拟
- 窗口管理

Windows 用户请使用主项目 `BlazorServer` 或 `HeadlessServer`。

## 工作原理

```
魔兽世界客户端
    ↓
DataToColor 插件 (将游戏数据编码为屏幕色块)
    ↓
Hekili 插件 (技能循环推荐)
    ↓
VizAura 屏幕捕获 (macOS Screenshot API)
    ↓
数据解析 (AddonReader)
    ↓
Hekili 推荐读取 (HekiliReader)
    ↓
按键决策
    ↓
macOS 输入模拟 (CGEvent API)
```

## 快速开始

### 前置要求

1. **macOS** 系统 (版本 13+)
2. **.NET 10.0 Runtime** ([下载](https://dotnet.microsoft.com/download/dotnet/10.0))
3. **魔兽世界客户端** (经典版/巫妖王之怒)
4. **DataToColor 插件** (已安装并配置)
5. **Hekili 插件** (已安装)

### 编译运行

```bash
# 进入项目目录
cd VizAura

# 编译项目
dotnet build

# 运行应用
dotnet run
```

### 发布

#### 创建独立的 .app Bundle

```bash
# 进入项目目录
cd VizAura

# 发布为自包含应用 (Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained false
```
```bash
# 发布为自包含应用 (Intel)
dotnet publish -c Release -r osx-x64 --self-contained false
```

**输出位置**:
- Apple Silicon: `bin/Release/net10.0/osx-arm64/VizAura.app`
- Intel: `bin/Release/net10.0/osx-x64/VizAura.app`

#### 分发应用

**方法 1: 直接拷贝**
```bash
# 将 .app 拖到 /Applications 文件夹
cp -r bin/Release/net10.0/osx-arm64/VizAura.app /Applications/
```

**方法 2: 创建压缩包**
```bash
# 创建 tar.gz 压缩包
cd bin/Release/net10.0/osx-arm64/
tar -czf VizAura-macOS-arm64.tar.gz VizAura.app

# 解压使用
tar -xzf VizAura-macOS-arm64.tar.gz
```

**方法 3: 创建 DMG 安装包** (需要 `create-dmg` 工具)
```bash
# 安装 create-dmg
brew install create-dmg

# 创建 DMG
create-dmg \
  --volname "VizAura" \
  --window-pos 200 120 \
  --window-size 800 400 \
  --icon-size 100 \
  --app-drop-link 600 185 \
  "VizAura-Installer.dmg" \
  "bin/Release/net10.0/osx-arm64/VizAura.app"
```

⚠️ **首次运行时**: macOS 可能提示"无法打开未经验证的开发者",请在**系统设置 > 隐私与安全性**中点击"仍要打开"。

### 查看 .app 运行日志

**方法 1: 终端直接运行 (推荐,可实时查看日志)**
```bash
# 直接运行 .app 可执行文件
/Applications/VizAura.app/Contents/MacOS/VizAura
```

**方法 2: 使用系统日志流**
```bash
# 实时查看 VizAura 日志
log stream --predicate 'processImagePath CONTAINS "VizAura"' --level debug
```

**方法 3: 查看崩溃报告**
```bash
# 打开崩溃报告目录
open ~/Library/Logs/DiagnosticReports/

# 或列出最新的 VizAura 崩溃
ls -lt ~/Library/Logs/DiagnosticReports/ | grep VizAura | head -5
```

**方法 4: 使用 Console.app**
```bash
# 打开系统控制台应用
open -a Console
# 然后在左侧选择 "Reports" → "Crash Reports" 查找 VizAura
```

### 配置文件位置

VizAura 会根据运行环境自动选择配置文件存储位置:

- **开发模式** (`dotnet run`): 配置文件保存在项目目录
  ```
  VizAura/
  ├── addon_config.json
  └── frame_config.json
  ```

- **.app Bundle 模式**: 配置文件保存在用户目录
  ```bash
  ~/Library/Application Support/VizAura/
  ├── addon_config.json
  └── frame_config.json
  ```

**查看/删除配置文件**:
```bash
# 查看 .app 配置目录
ls -la ~/Library/Application\ Support/VizAura/

# 删除所有配置(重新开始配置流程)
rm -rf ~/Library/Application\ Support/VizAura/
```

### 首次使用配置

1. 启动魔兽世界并登录角色
2. 确保 DataToColor 和 Hekili 插件已加载
3. 启动 VizAura 应用
4. 授予应用屏幕录制权限 (macOS 系统设置 > 隐私与安全性 > 屏幕录制)
5. 授予应用辅助功能权限 (macOS 系统设置 > 隐私与安全性 > 辅助功能)

## 权限说明

VizAura 需要以下 macOS 系统权限:

- **屏幕录制权限**: 用于捕获游戏画面并读取 DataToColor 数据
- **辅助功能权限**: 用于模拟键盘/鼠标输入

这些权限仅用于与魔兽世界客户端交互,不会访问其他应用或系统数据。

## 与主项目的区别

| 特性 | VizAura | BlazorServer/HeadlessServer |
|------|---------|----------------------------|
| 平台 | macOS 专用 | Windows 专用 |
| UI 框架 | Avalonia | Blazor Web UI |
| 屏幕捕获 | macOS Screenshot API | Windows DXGI |
| 输入模拟 | macOS CGEvent | Windows SendInput |
| Hekili 支持 | ✅ | ✅ |
| 完整机器人功能 | 🚧 开发中 | ✅ 完整支持 |

## 开发状态

🚧 **Alpha 版本 - 开发中**

当前功能:
- ✅ macOS 屏幕捕获
- ✅ DataToColor 数据解析
- ✅ Hekili 推荐读取
- 🚧 自动按键 (开发中)
- 🚧 UI 界面 (开发中)

## 许可证

本项目是 [Master Of Puppets](../README.md) 的一部分,遵循相同的开源许可证。

## 相关链接

- [主项目文档](../README.md)
- [DataToColor 插件](../Addons/DataToColor/)
- [Hekili 插件](https://github.com/Hekili/hekili)
- [Avalonia UI](https://avaloniaui.net/)

## 免责声明

⚠️ **本工具仅供学习和研究使用。使用自动化工具可能违反游戏服务条款,请自行承担风险。**
