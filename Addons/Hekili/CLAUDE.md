# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

这是 Hekili 的泰坦时光服特殊版本，专为中国大陆服特有的泰坦时光服（Wrath of the Lich King）定制，仅支持简体中文。

Hekili 是一个基于 SimulationCraft 行动列表的 DPS 和坦克技能优先级提示插件，使用 Lua 编写，遵循 World of Warcraft 插件开发规范。

## 构建和发布

### 版本发布流程

使用 GitHub Actions 自动构建和发布：

```bash
# 通过 release.json 配置发布信息
# 触发方式：
# 1. 推送带有 "v*" 前缀的 git tag
git tag v3.80.0-2.0.8-CN
git push origin v3.80.0-2.0.8-CN

# 2. 手动触发工作流（workflow_dispatch）
# 在 GitHub Actions 页面手动运行 "Package and Release"
```

### 版本配置

编辑 `release.json` 文件来配置发布版本：
- `name`: 插件名称
- `version`: 版本号（格式：v主版本-子版本-CN）
- `filename`: 生成的 zip 文件名
- `metadata.flavor`: 游戏版本（"titan" 表示泰坦时光服）
- `metadata.interface`: 接口版本（38000 对应 WotLK 3.8.0）

### TOC 文件

`Hekili.toc` 是插件的清单文件，定义了：
- 接口版本（Interface）
- 版本号（Version）
- 插件元数据（Title, Author, Notes）
- 依赖库（OptionalDeps）
- 加载顺序（文件列表）

## 代码架构

### 核心模块结构

插件采用模块化架构，主要组件按以下顺序加载（参见 `Hekili.toc`）：

1. **embeds.xml**: 加载所有第三方库（Ace3 系列、LibStub、LibRangeCheck 等）
2. **Hekili.lua**: 插件初始化，创建 AceAddon 实例，定义版本和环境检测函数
3. **Translation.lua**: 翻译和本地化支持
4. **Utils.lua**: 工具函数和辅助方法
5. **Formatting.lua**: 格式化和显示相关功能
6. **MultilineEditor.lua**: 多行编辑器组件
7. **Constants.lua**: 常量定义（法术 ID、物品 ID 等）
8. **State.lua**: 游戏状态管理系统（最核心的模块）
9. **Events.lua**: 游戏事件处理
10. **Classes.lua**: 职业系统基础架构
11. **Wrath/\*.lua**: 巫妖王之怒版本的职业实现
12. **Targets.lua**: 目标选择和管理
13. **Options.lua**: 配置界面（使用 AceConfig）
14. **UI.lua**: 用户界面框架
15. **Scripts.lua**: APL（行动优先级列表）脚本执行引擎
16. **Core.lua**: 核心逻辑和主循环

### 关键架构概念

#### State 系统（State.lua）

State 系统是插件的核心，模拟未来游戏状态以进行预测：

- 维护游戏状态的虚拟副本（资源、冷却、buff/debuff 等）
- 支持"时间旅行"：可以模拟未来 N 秒后的状态
- 用于 APL 条件判断和技能优先级计算
- 包含 `state.now`（当前时间）、`state.offset`（时间偏移）、`state.modified`（修改标记）等关键变量

#### Classes 系统（Classes.lua + Wrath/\*.lua）

职业系统定义了每个专精的：

- **abilities**: 技能定义（冷却、消耗、效果等）
- **auras**: buff/debuff 定义
- **resources**: 资源系统（法力、能量、符文等）
- **talents**: 天赋查询
- **gear/setBonuses**: 装备和套装加成
- **stateExprs/stateFuncs**: 状态表达式和函数（用于 APL）

每个职业文件（如 `Wrath/DeathKnight.lua`）都会调用 `class:RegisterResource()`, `class:RegisterAura()`, `class:RegisterAbility()` 等方法。

#### APL 脚本引擎（Scripts.lua）

APL（Action Priority List）是从 SimulationCraft 移植的技能优先级列表：

- 存储在 `Wrath/APLs/` 目录
- 使用 Lua 脚本格式，但遵循 SimC 的逻辑结构
- 脚本通过 `state` 对象访问游戏状态
- 引擎会评估每个条件并选择最优技能

#### Event 系统（Events.lua）

处理 WoW 游戏事件：

- 使用 Ace3 的事件系统
- 注册关键战斗事件（COMBAT_LOG_EVENT_UNFILTERED、UNIT_AURA 等）
- 更新缓存的 aura 信息（`ns.auras`）
- 触发状态刷新

### 泰坦服特殊功能

插件包含泰坦时光服的特定优化：

1. **冰冠堡垒优化**（CHANGELOG 提及）：
   - 战斗目标忽略名单（教授小怪、议会三王等）
   - 巫妖王"污染"技能特殊处理（`defile_target_is_me` 函数）
   - 火箭靴判断（`nitro_boosts` 函数）

2. **职业特定修复**：
   - 死亡骑士：双光环 DKT 循环、冰霜疾病识别
   - 盗贼：女王蜂拥之影监控、暗影斗篷提示
   - 元素萨：修复技能 lua 错误

3. **红玉圣殿饰品**：
   - 破甲饰品调用函数 `piercing_twilight`

## 常用命令

### 开发和测试

```bash
# 在 WoW Classic (Wrath) 客户端中加载插件
# 将项目复制到 WoW 安装目录：
# World of Warcraft/_classic_/Interface/AddOns/Hekili/

# 在游戏中使用以下命令：
/hekili           # 打开配置界面
/hekili toggle    # 切换显示
/hekili dots      # 切换 DoT 模式
/hekili cd        # 切换冷却模式
/hekili pause     # 暂停/恢复
```

### 调试

插件包含 CPU 和帧性能分析：

```lua
-- 在 Hekili.lua 中：
Hekili:ProfileCPU(name, func)    -- 注册 CPU 性能分析
Hekili:ProfileFrame(name, frame) -- 注册帧性能分析
```

## 代码规范

### Lua 代码风格

- 使用 4 空格缩进
- 局部变量优先：经常使用 `local` 关键字减少全局命名空间污染
- 缓存全局函数：`local format = string.format`
- 注释使用中文

### 命名约定

- 全局对象：`Hekili`（插件主对象）
- 命名空间：`ns`（addon 的私有命名空间）
- 子系统：`class`, `state`, `scripts`（通过 `Hekili.Class` 等访问）
- 格式化键名：使用 `formatKey()` 函数规范化字符串键

### 兼容性处理

插件需要处理经典版本和零售版本的 API 差异：

```lua
-- 示例（Core.lua）：
local GetSpecialization = function() return GetActiveTalentGroup() end
local GetSpecializationInfo = function()
    local name, baseName, id = UnitClass("player")
    return id, baseName, name
end
```

### 添加新技能或光环

在对应的职业文件中（`Wrath/职业名.lua`）：

```lua
-- 注册技能
spec:RegisterAbility( "ability_key", {
    id = 12345,              -- 法术 ID
    cast = 1.5,              -- 施法时间
    cooldown = 10,           -- 冷却时间
    gcd = "spell",           -- GCD 类型
    spend = 40,              -- 资源消耗
    spendType = "mana",      -- 资源类型
    -- 更多属性...
} )

-- 注册光环
spec:RegisterAura( "aura_key", {
    id = 12345,              -- 光环 ID
    duration = 30,           -- 持续时间
    max_stack = 1,           -- 最大层数
} )
```

## 第三方库

插件使用以下主要库（通过 `embeds.xml` 加载）：

- **Ace3**: 框架核心（AceAddon, AceConfig, AceDB, AceEvent 等）
- **LibStub**: 库版本管理
- **LibRangeCheck-2.0**: 距离检测
- **LibSpellRange-1.0**: 法术距离检测
- **LibCustomGlow-1.0**: 技能高亮效果
- **LibCompress/LibDeflate**: 数据压缩
- **LibDBIcon-1.0**: 小地图图标
- **LibDualSpec-1.0**: 双天赋切换支持

## 注意事项

1. **接口版本**：修改插件时确保 `Hekili.toc` 中的 Interface 版本匹配目标游戏版本（38000 = 3.8.0）

2. **APL 修改**：修改 APL 时需要理解 SimulationCraft 的语法，测试时建议使用游戏内的模拟目标人偶

3. **状态同步**：修改 State 系统时要非常小心，错误可能导致技能建议不准确

4. **性能考虑**：插件在每帧都可能运行，避免在热路径中使用昂贵的操作（如大量字符串操作）

5. **本地化**：所有面向用户的字符串都应该通过 `Translation.lua` 系统

6. **赞助者名单**：`ns.Patrons` 包含贡献者名单，更新时注意维护
