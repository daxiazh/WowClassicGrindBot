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
2. **.NET 9.0 Runtime** ([下载](https://dotnet.microsoft.com/download/dotnet/9.0))
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
