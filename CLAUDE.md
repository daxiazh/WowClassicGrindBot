# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Master Of Puppets** 是一个用于《魔兽世界》经典版本（包括 Season of Mastery、燃烧的远征、巫妖王之怒、大灾变）的自动化机器人项目，使用 C# 和 .NET 10 编写。

该项目通过修改后的插件读取游戏状态，使用屏幕捕获和键鼠模拟实现自动化，**不涉及内存篡改或 DLL 注入**。

## Build Commands

```bash
# 构建整个解决方案
dotnet build -c Release

# 运行 BlazorServer (带 UI 的主应用)
cd BlazorServer
dotnet run -c Release

# 运行 HeadlessServer (无 UI 版本)
cd HeadlessServer
dotnet run -c Release -- Hunter_1.json

# 运行测试
cd CoreTests
dotnet test
```

## Architecture

### 核心项目结构

- **Core**: 核心逻辑库，包含 GOAP (Goal-Oriented Action Planning) 系统、目标管理、路径规划、插件读取等
- **BlazorServer**: 带有 Web UI 的主应用程序，运行在 `localhost:5000`
- **HeadlessServer**: 命令行版本，适用于低资源消耗场景
- **Frontend**: Blazor 前端组件库
- **Game**: 游戏进程交互、输入模拟
- **PPather**: 路径规划算法实现 (V1 本地寻路)
- **PathingAPI**: 独立的寻路 API 服务
- **SharedLib**: 共享工具库，包含 NPC 查找器等
- **DataConfig**: 配置数据管理
- **WinAPI**: Windows API 封装

### GOAP 系统架构

项目使用 Goal-Oriented Action Planning 系统来决策机器人行为：

- **Goals** (`Core/Goals/`): 各种行为目标（战斗、拾取、移动等）
  - `CombatGoal`: 战斗逻辑
  - `PullTargetGoal`: 拉怪逻辑
  - `LootGoal`: 拾取物品
  - `FollowRouteGoal`: 路线跟随
  - `AdhocGoal`: 临时任务（buff、吃喝等）
  - `AssistFocusGoal`: 协助焦点目标
  - `FleeGoal`: 逃跑逻辑
  
- **GoapAgent** (`Core/GOAP/GoapAgent.cs`): GOAP 代理，执行目标规划
- **GoapPlanner** (`Core/GOAP/GoapPlanner.cs`): 目标优先级规划器

### 插件系统

- **Addons/DataToColor**: 修改的 WoW 插件，在屏幕顶部显示色块来传递游戏状态
- **AddonReader** (`Core/Addon/AddonReader.cs`): 读取插件数据
- **PlayerReader** (`Core/Addon/PlayerReader.cs`): 解析玩家状态

### 寻路系统

支持三种寻路模式：
1. **V1 Local**: 进程内 PPather 寻路（使用 MPQ 文件）
2. **V1 Remote**: 远程 PathingAPI 服务
3. **V3 Remote**: AmeisenNavigation 服务（使用 .mmap 文件）

### 职业配置系统

- **ClassConfiguration** (`Core/ClassConfig/`): JSON 格式的职业配置
- **KeyAction**: 技能按键定义
- **Requirement**: 技能施放条件
- 配置文件位于 `Json/class/` 目录

## Important Files & Directories

### 配置文件
- `Json/class/`: 各职业配置文件（战斗轮换、按键绑定等）
- `Json/path/`: 路线文件（行走路径坐标）
- `Json/dbc/`: 游戏数据库（物品、技能、NPC 等）
- `addon_config.json`: 插件配置
- `frame_config.json`: 屏幕帧配置
- `data_config.json`: 数据文件路径配置

### 数据文件要求
- **V1 寻路**: 需要 MPQ 文件放在 `Json/MPQ/` 目录
  - Vanilla: `common-2.MPQ` (1.7GB)
  - TBC: `expansion.MPQ` (1.8GB)  
  - WOTLK: `lichking.MPQ` (2.5GB)

## Development Workflow

### 添加新功能时
1. 确定功能属于哪个层（Goal/Reader/Component）
2. 在 `Core/` 相应目录创建新类
3. 在 `DependencyInjection.cs` 注册服务
4. 如需 UI，在 `Frontend/Pages/` 添加 Razor 组件
5. 更新相关配置类（如果需要）

### 调试技巧
- BlazorServer 提供实时 UI 调试（Goals、Screenshot、Route 等组件）
- HeadlessServer 使用 `-d` 参数可保存屏幕截图到 `Json/cap/`
- 使用 `-o` 参数显示 NPC 名称查找 Overlay
- `--loadonly` 参数可验证配置文件是否正确加载

### 测试
```bash
# 运行所有测试
dotnet test

# 运行特定测试项目
cd CoreTests
dotnet test --filter "FullyQualifiedName~NpcNameFinder"

# 性能基准测试
cd Benchmarks
dotnet run -c Release
```

## Coding Conventions

### C# 风格
- 使用 Rider 默认命名规则
- 私有字段使用 camelCase
- 公共属性使用 PascalCase
- 接口以 `I` 开头
- 使用 `sealed` 标记不会被继承的类
- 优先使用 `readonly` 字段

### 依赖注入
- 所有服务在各项目的 `DependencyInjection.cs` 中注册
- 使用构造函数注入
- 避免使用 ServiceLocator 模式

### 配置管理
- 职业配置使用 JSON 格式，位于 `Json/class/`
- 使用 `ClassConfiguration` 类解析
- 配置更改需要重启应用

## Common Tasks

### 添加新的 Goal
1. 在 `Core/Goals/` 创建新类，继承 `GoapGoal`
2. 实现 `CreateCostKey()`, `CanRun()`, `Update()` 方法
3. 在 `GoalFactory.cs` 中注册
4. 如需 UI 显示，更新 `GoalsComponent.razor`

### 修改职业配置格式
1. 更新 `ClassConfiguration.cs` 或相关配置类
2. 确保向后兼容或提供迁移逻辑
3. 更新 README.md 中的配置说明
4. 测试所有现有配置文件能否正常加载

### 优化性能
- 屏幕捕获是主要性能瓶颈，优先优化此部分
- NPC 名称查找使用图像处理，注意避免不必要的扫描
- 使用 Benchmarks 项目测量性能变化

## External Dependencies

### 必需的外部服务
- **V3 Remote 寻路**: AmeisenNavigation Server (端口 47111)
- **游戏进程**: 魔兽世界客户端必须运行

### 系统要求
- Windows 10+
- .NET 10.0 SDK
- 支持的分辨率: 1024x768, 1920x1080, 3440x1440, 3840x2160

## Notes
- 项目仅支持 Windows（使用 Windows API 进行屏幕捕获和输入）
- 部分功能需要特定的游戏设置（见 README.md "Configure the Wow Client" 部分）
- 寻路缓存保存在 `Json/PathInfo/` 目录
- Session 统计保存在 `Json/History/` 目录

- 确保所有的服务都应该在 ConfigureServices() 中注册