# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

**Master Of Puppets** 是一个用 C# 编写的魔兽世界经典版机器人，支持经典旧世、燃烧的远征经典版、巫妖王之怒经典版和大地的裂变经典版。机器人使用屏幕截图和计算机视觉技术（无内存读取和 DLL 注入）来自动化打怪、战斗、寻路和资源采集。

## 构建命令

**环境要求:**
- .NET SDK 9.0.100（在 `global.json` 中指定）
- Windows 10 及以上版本
- 支持 `AnyCPU`、`x86` 和 `x64` 构建

**构建项目:**
```powershell
dotnet build -c Release
```

**运行 BlazorServer（带前端 UI）:**
```powershell
cd BlazorServer
dotnet run -c Release
```

**运行 HeadlessServer（无前端）:**
```powershell
cd HeadlessServer
dotnet run -c Release -- <类配置文件名.json> [选项]
# 示例: dotnet run -c Release -- Hunter_1.json -m RemoteV3
```

**运行测试:**
```powershell
dotnet test
```

**仅加载配置文件（验证）:**
```powershell
cd HeadlessServer
dotnet run -c Release -- Hunter_1.json --loadonly
```

## 架构

### 高层组件

1. **Addon（插件）** - 修改自 [Happy-Pixels 插件](https://github.com/FreeHongKongMMO/Happy-Pixels)，通过在游戏窗口顶部绘制彩色像素来读取游戏状态
2. **Frontend（前端）** - ASP.NET Core Razor 组件（Blazor）
3. **BlazorServer** - 托管前端 UI 的 ASP.NET Core Blazor 服务器
4. **HeadlessServer（无头服务器）** - 无 UI 的命令行机器人（资源占用更低）
5. **Core（核心）** - 主要的机器人逻辑、GOAP 规划器、目标和状态管理
6. **Game（游戏）** - 底层游戏接口（屏幕截图、输入模拟、WoW 进程管理）

### 项目结构

- **Core/** - 包含主机器人控制器、GOAP（目标导向行动规划）系统、插件读取器、目标和战斗逻辑
- **Game/** - 屏幕截图（DXGI）、输入模拟和 WoW 进程交互
- **Frontend/** - Blazor UI 组件
- **BlazorServer/** - 托管前端的 Web 服务器
- **HeadlessServer/** - 无 UI 运行机器人的 CLI 入口点
- **PPather/** - V1 本地寻路（使用 MPQ 文件）
- **PathingAPI/** - V1 远程寻路服务
- **SharedLib/** - 共享工具、NPC 查找、数据结构
- **WinAPI/** - Windows API 包装器，用于进程交互
- **DataConfig/** - 配置文件管理
- **WowheadDB/** - 游戏物品、NPC、区域数据库
- **Utilities/** - 路径制作、DBC 提取、Wowhead 数据提取工具
- **Json/** - 类、路径和游戏数据的配置文件
  - **Json/class/** - 类配置文件（技能循环、行为）
  - **Json/path/** - 寻路路线（打怪路径）
  - **Json/MPQ/** - V1 寻路的 MPQ 文件（可选）

### 核心架构 - GOAP 系统

机器人使用 **GOAP（目标导向行动规划）** 来做决策。这是一个规划型 AI 架构：

1. **GoapAgent**（`Core/GOAP/GoapAgent.cs`）- 主规划循环：
   - 根据游戏条件更新世界状态
   - 规划要实现的目标序列
   - 执行当前目标

2. **GoapGoal**（`Core/Goals/GoapGoal.cs`）- 所有目标的基类。目标包含：
   - **前置条件（Preconditions）** - 执行所需的世界状态要求
   - **效果（Effects）** - 目标如何改变世界状态
   - **成本（Cost）** - 规划的优先级
   - **Update()** - 执行目标逻辑

3. **世界状态（World State）**（`Core/GOAP/GoapKey.cs` 中的 `GoapKey`）- 游戏状态的位打包表示：
   - hastarget（有目标）、incombat（战斗中）、targetisalive（目标存活）、isdead（已死亡）、shouldloot（应拾取）等
   - 基于插件数据和战斗日志每帧更新

4. **目标（Goals）**（`Core/Goals/`）- 各种行为，如：
   - `FollowRouteGoal` - 跟随打怪路径
   - `PullTargetGoal` - 发起战斗
   - `CombatGoal` - 执行战斗循环
   - `LootGoal` - 拾取尸体
   - `SkinningGoal` - 从尸体采集（剥皮/采药/采矿）
   - `AdhocGoal` / `AdhocNPCGoal` - 自定义脚本行为
   - `WalkToCorpseGoal` - 死亡后跑尸
   - `AssistFocusGoal` - 跟随并协助焦点目标

### BotController（机器人控制器）

`BotController`（`Core/BotController.cs`）是主入口点：
- 管理三个主线程：
  - **Addon 线程** - 从屏幕像素读取插件数据
  - **Screenshot 线程** - 运行 NPC 名称查找器（NPC 名牌的 OCR）
  - **Remote pathing 线程** - 在 V1/V3 寻路器上可视化路径
- 从 `Json/class/` 加载类配置文件
- 为每个配置文件创建带依赖注入的作用域会话
- 管理 `GoapAgent` 的生命周期

### 插件系统

插件在 WoW 窗口顶部绘制彩色单元格。每个单元格编码游戏状态（生命值、法力值、增益、目标信息等）。`AddonReader`（`Core/Addon/AddonReader.cs`）使用屏幕截图解码这些像素。

关键插件组件：
- **AddonReader** - 读取所有插件数据帧
- **PlayerReader** - 玩家状态（HP、法力、位置、增益等）
- **ActionBar** - 动作条槽位状态（可用、冷却、消耗）
- **BagReader** - 背包状态

### 输入系统

- **ConfigurableInput**（`Game/Input/ConfigurableInput.cs`）- 模拟键盘输入
- 支持修饰键（Shift、Ctrl、Alt）
- **KeyAction** - 定义技能/法术，包括需求、冷却、施放条件

### 寻路

三种寻路模式（按顺序自动发现）：

1. **V3 Remote** - 进程外 [AmeisenNavigation](https://github.com/Xian55/AmeisenNavigation)（Recast/Detour 导航网格，C++）
2. **V1 Remote** - 进程外 PathingAPI（C#，读取 MPQ 文件）
3. **V1 Local** - 进程内 PPather（C#，读取 MPQ 文件）

室内寻路需要设置 `PathFilename`。不支持副本/地下城。

**大地的裂变经典版** 仅支持 V3 Remote（Cataclysm 没有 MPQ 文件）。

### 类配置

每个类在 `Json/class/` 中都有一个 JSON 文件，定义：
- **KeyActions** - 战斗循环（Pull、Combat、Adhoc、NPC、Wait 目标）
- **Paths** - 带等级要求的打怪路线
- **Behaviors** - 拾取、剥皮、采药、采矿、坐骑使用
- **Requirements** - 条件逻辑（等级、增益、生命值%、法力值%等）

关键配置属性：
- `PathFilename` 或 `Paths[]` - 打怪路线
- `Pull`、`Combat`、`Flee`、`AssistFocus`、`Adhoc`、`NPC`、`Wait` - 特定目标的按键动作
- `Mode` - Grind（打怪）、AttendedGrind（辅助打怪）、AttendedGather（辅助采集）、CorpseRun（跑尸）、AssistFocus（协助焦点）
- `TargetMask` - 攻击的单位类型（Normal、Trivial、Rare、Elite 等）
- `Blacklist` - 要避免的 NPC 名称

### NPC 名称查找

`NpcNameFinder`（`SharedLib/NpcFinder/NpcNameFinder.cs`）使用 OCR/图像处理查找 NPC 头顶的名称：
- 读取预期名牌位置的彩色像素
- 将 NPC 分类为友方、敌方或尸体
- 用于目标选择和交互

## 常见开发任务

### 添加新目标

1. 在 `Core/Goals/` 中创建继承自 `GoapGoal` 的新类
2. 定义前置条件（需要的世界状态位）
3. 定义效果（此目标如何改变世界状态）
4. 设置成本（优先级）
5. 实现 `Update()` 方法，编写目标逻辑
6. 在 `GoalFactory.Create()`（`Core/Goals/GoalFactory.cs`）中注册目标

### 修改类循环

编辑 `Json/class/` 中特定类的 JSON 文件，修改 `Combat` 或 `Pull` KeyActions 数组。

### 创建新路径

1. 使用 `Utilities/PathMaker` 创建路径点
2. 保存为 JSON 到 `Json/path/`
3. 在类配置的 `PathFilename` 或 `Paths` 数组中引用

### 测试插件更改

1. 修改 `Addons/` 目录中的插件
2. 重新构建项目（构建后自动复制插件到输出目录）
3. 在 BlazorServer 中使用 `AddonConfigurator` 安装更新的插件
4. 重启 WoW（重载 UI）

### 调试

- 在类配置中启用日志：`"Log": true`
- 启用背包日志：`"LogBagChanges": true`
- 使用诊断模式：`HeadlessServer/run.bat Hunter_1.json -d`（保存截图到 `Json/cap/`）
- 使用覆盖模式：`HeadlessServer/run.bat Hunter_1.json -o`（显示 NPC 检测覆盖层）

## 重要约定

- **KeyAction 名称** - 类配置中小写 = 宏，大写 = 法术/技能
- **Requirements（需求）** - 运行时评估的布尔表达式（例如：`"Health% < 30"`、`"Level >= 5"`）
- **ConsoleKey** - 键位绑定使用 .NET `ConsoleKey` 枚举值
- **路径文件** - 存储在 `Json/path/`，相对于该目录引用
- **类文件** - 存储在 `Json/class/`，相对于该目录引用
- **配置持久化** - `addon_config.json`、`frame_config.json`、`data_config.json` 在初始设置期间生成

## 代码导航

- 主入口点：`BlazorServer/Program.cs` 和 `HeadlessServer/Program.cs`
- GOAP 循环：`Core/GOAP/GoapAgent.cs:GoapThread()`
- 机器人控制器：`Core/BotController.cs`
- 目标定义：`Core/Goals/`
- 插件读取：`Core/Addon/AddonReader.cs`
- 输入模拟：`Game/Input/`
- 屏幕截图：`Game/Screen/`

## 数据流

1. **Addon** 绘制像素 → **AddonReader** 解码 → **PlayerReader/AddonBits** 解析状态
2. **NpcNameFinder** 从截图读取名牌
3. **GoapAgent** 更新世界状态 → 规划目标 → 执行当前目标
4. **Goals** 使用 **ConfigurableInput** 发送按键和鼠标点击
5. **CombatLog** 跟踪伤害事件以检测击杀
6. **BagChangeTracker** 监控背包变化
7. **RouteInfo** 管理当前路径和路径点

## 外部依赖

- **Serilog** - 日志记录
- **Newtonsoft.Json** - JSON 序列化
- **GregsStack.InputSimulatorStandard** - 键盘/鼠标模拟
- **Vortice.Direct3D11** - DXGI 屏幕截图
- **GameOverlay.Net** - NPC 名称覆盖层渲染
- **Microsoft.Extensions.DependencyInjection** - 依赖注入
- 使用lua 5.1 的语法
- 每次执行完TODO.md中的任务后， 都需要同步更新进度，如果需要也同步调整新的计划
- 这个工程运行在windows上
- 在 Lua 5.1 中不支持 0x 开头的十六进制字面量。
- lua中不支持#来获取Table的长度，改为table.getn()
- lua中不支持%，需要使用函数math.fmod / math.mod