# DataToText v2.1 开发记录

## 项目概述

DataToText 是一个将游戏数据编码为 65×65 黑白网格的插件,用于通过OCR方式读取游戏状态。

**当前版本**: v2.1.0
**状态**: ✅ Lua端和C#解码器均已完成，已集成 Timing Pattern 精确定位

---

## ✅ 已完成功能

### 核心系统
- ✅ **108个字段数据收集** (FieldCollector.lua)
  - 玩家属性 (HP, 法力, 等级, XP等)
  - 目标信息 (HP, 等级, 名称, GUID等)
  - 背包系统 (槽位, 物品)
  - Buff/Debuff 系统
  - 动作条状态
  - 战斗日志队列

- ✅ **65×65网格编码系统** (GridEncoder.lua)
  - 元数据: 版本号(8 bits) + 字段数(8 bits) + 保留位(16 bits)
  - 数据区: 108字段 × 24 bits = 2592 bits
  - CRC32校验: IEEE 802.3标准 (32 bits)
  - 4个角标记: 7×7 Finder Pattern + 1格静区 (4×8×8 = 256 cells)
  - Timing Pattern: 水平+垂直黑白交替 (97 cells)
  - 总容量: 3872 cells 可用, 2656 bits 使用 (68.6% 利用率)

- ✅ **FontString高性能渲染** (GridRenderer.lua)
  - 65个FontString对象 (每行一个)
  - 字符: █ (U+2588实心方块)
  - 颜色编码: 白色(|cFFFFFFFF)=1, 黑色(|cFF000000)=0
  - 字体: Tiny-Bold.ttf, 12px, MONOCHROME
  - 对象复用机制 (避免每帧重建)

- ✅ **模块化架构**
  - Utils.lua: 字符串处理 + CRC32校验
  - Tests.lua: 单元测试套件
  - FieldCollector.lua: 数据收集
  - GridEncoder.lua: 网格编码
  - GridRenderer.lua: 渲染引擎
  - DataToText.lua: 主控制器

- ✅ **性能优化**
  - CRC32查找表算法 (O(n×8) → O(n))
  - 预缓存函数引用 (bit.*, math.*, string.*)
  - 循环展开 (BytesToBits)
  - 直接索引赋值替代table.insert()
  - **最终性能**: 7.14ms总耗时 (~140 FPS理论帧率)

- ✅ **Timing Pattern 精确定位** (v2.1新增)
  - 水平 Timing Pattern: 第3行(Finder内部), 49个黑白交替cells
  - 垂直 Timing Pattern: 第4列(Finder中心), 49个黑白交替cells  
  - 用途: 精确计算网格大小和单元格中心点坐标
  - 优化: 复用Finder检测时已扫描的runs，提升性能
  - 精度: 使用浮点数避免整数除法舍入误差累积

- ✅ **WoW 1.12 Vanilla兼容性**
  - 使用UnitBuff()/UnitDebuff() API
  - 避免使用不存在的API (UnitAura, GetPlayerMapPosition等)
  - Lua 5.1语法 (无0x字面量, 使用math.mod)

---

## 📊 技术规格

### 数据格式

```
元数据区 (4 bytes):
  [0] 版本号 (uint8)
  [1] 字段数量 (uint8)
  [2-3] 保留位 (2× uint8)

数据区 (324 bytes):
  108个字段 × 3 bytes (24 bits每字段)
  编码方式: Big-endian
  值域: 0-16777215

CRC32校验 (4 bytes):
  算法: IEEE 802.3
  多项式: 0xEDB88320
  输入: 元数据 + 数据区 (328 bytes)
```

### 网格布局

```
65×65 = 4225 cells

角标记 (4×8×8 = 256 cells):
  每个角包含:
  - 7×7 Finder Pattern (1:1:3:1:1 比例)
  - 1格静区 (面向数据区方向)
  
  █ █ █ █ █ █ █ ░         ░ █ █ █ █ █ █ █
  █ ░ ░ ░ ░ ░ █ ░   ...   ░ █ ░ ░ ░ ░ ░ █
  █ ░ █ █ █ ░ █ ░         ░ █ ░ █ █ █ ░ █
  █ ░ █ █ █ ░ █ ░         ░ █ ░ █ █ █ ░ █
  █ ░ █ █ █ ░ █ ░         ░ █ ░ █ █ █ ░ █
  █ ░ ░ ░ ░ ░ █ ░         ░ █ ░ ░ ░ ░ ░ █
  █ █ █ █ █ █ █ ░         ░ █ █ █ █ █ █ █
  ░ ░ ░ ░ ░ ░ ░ ░         ░ ░ ░ ░ ░ ░ ░ ░

Timing Pattern (97 cells):
  - 水平: 第3行，列9-57 (49 cells，黑白交替)
  - 垂直: 第4列，行9-57 (49 cells，黑白交替)
  - 重叠: (3,4) 位置 (1 cell)

数据区 (3872 cells):
  逐行填充 (跳过角标记和Timing Pattern)
  0 = 黑色, 1 = 白色
```

### 性能指标

| 模块 | 耗时 | 占比 |
|------|------|------|
| FieldCollector.CollectAllFields() | - | - |
| GridEncoder.EncodeToGrid() | 4.45ms | 62.3% |
| GridRenderer.RenderGrid() | 2.69ms | 37.7% |
| **总计** | **7.14ms** | **100%** |

**帧率影响**: 从Pause时60 FPS降至约45 FPS (正常范围)

---

## 🎮 可用命令

```lua
/dtt                -- 切换显示
/dtt help           -- 显示帮助
/dtt test           -- 运行所有测试
/dtt profile        -- 启用/禁用性能分析
/dtt report         -- 显示性能报告
/dtt capacity       -- 显示网格容量信息
/dtt crc32          -- CRC32测试
```

---

## ✅ C#解码器开发已完成

### 已实现功能

1. ✅ **屏幕捕获**
   - 集成 WowScreenDXGI 捕获游戏窗口
   - 全屏搜索 FontString 网格区域

2. ✅ **Finder Pattern 识别**
   - 识别4个 7×7 Finder Pattern（1:1:3:1:1 比例）
   - 包含1格定向静区（8×8总区域）
   - 自适应单元格大小检测（4px-100px）
   - 支持所有分辨率（800×600到8K）

3. ✅ **Timing Pattern 检测与解码**
   - 水平方向：在第3行检测两个Finder并提取Timing Pattern
   - 垂直方向：在第4列检测第三个Finder并提取Timing Pattern
   - 精确计算：生成65个单元格中心点的浮点坐标数组
   - 性能优化：复用Finder检测时已扫描的run-length数据
   - 误差控制：使用 List<float> 避免整数除法舍入误差

4. ✅ **网格解码**
   - 逐cell采样中心点颜色（使用XCenters/YCenters数组）
   - 提取2656位数据（跳过256个角标记cells + 97个Timing Pattern cells）
   - 严格验证：gridSizeH == gridSizeV（必须完全相等）

5. ✅ **数据解析**
   - 提取元数据（版本号、字段数）
   - IEEE 802.3 CRC32校验
   - 解析108个24-bit字段
   - Big-endian字节序

6. ✅ **性能优化**
   - 位置缓存机制（10帧验证一次）
   - 步进搜索算法（step = cellSize/2）
   - CRC32查找表
   - 静态方法优化

7. ✅ **调试开关系统** (v2.1新增)
   - C# 端：`ENABLE_DEBUG_LOGGING` 常量（默认false）
   - Lua 端：`DEBUG_MODE` 变量（默认false）
   - 可控的详细调试信息输出（mergedRuns, bits, bytes等）
   - 一键启用/禁用所有调试日志

8. ✅ **测试框架**
   - CoreTests集成
   - 性能压力测试
   - 持续监控模式
   - 调试截图保存

### C#解码器文件

- `Core/DataToText/DataToTextGridDecoder.cs` - 核心解码器（457行）
- `CoreTests/Test_DataToTextDecoder.cs` - 测试类（147行）
- `CoreTests/Program.cs` - 测试入口集成

### Lua端改进

- ✅ **EditBox数据显示增强**
  - 显示编码统计信息
  - 显示前10个字段原始值
  - 显示关键游戏数据（等级、生命、能量、金钱、背包）
  - 实时更新百分比显示

---

## 📁 文件结构

```
DataToText/
├── DataToText.toc          # 插件清单
├── DataToText.xml          # UI定义
├── DataToText.lua          # 主控制器 (318行)
├── FieldCollector.lua      # 数据收集 (450行)
├── GridEncoder.lua         # 网格编码 (247行)
├── GridRenderer.lua        # 渲染引擎 (119行)
├── Utils.lua               # 工具函数 (78行)
├── Tests.lua               # 测试套件 (335行)
├── Fonts/
│   └── Tiny-Bold.ttf      # 等宽字体
├── libs/                   # 第三方库
│   ├── LibStub.lua
│   ├── AceCore-3.0.lua
│   └── ...
└── docs/
    ├── DATATOCOLOR_FIELDS.md    # 字段定义文档
    ├── GRID_ENCODING.md         # 网格编码规范
    └── TODO.md                  # 本文件
```

**总代码量**: 约1547行 (不含库)

---

## 🔧 开发约定

### Lua 5.1 兼容性
- ❌ 禁止: 0x字面量 → 使用十进制
- ❌ 禁止: % 运算符 → 使用 math.mod()
- ❌ 禁止: # 运算符 → 使用 table.getn()
- ✅ 使用: bit.* 库 (WoW 1.12内置)

### 代码规范
- 所有注释使用中文
- 函数使用驼峰命名 (CollectAllFields)
- 局部变量使用小写 (local fieldCount)
- 常量使用大写 (local GRID_SIZE = 65)

### 性能优化原则
- 预缓存全局函数引用
- 避免不必要的table.insert()
- 复用对象而非每帧重建
- 使用查找表算法

---

## 📝 更新日志

### [2.1.0] - 2025-01-21
- ✅ **Timing Pattern 系统**
  - Lua端: 添加水平(第3行)和垂直(第4列) Timing Pattern
  - C#端: 自动检测 Timing Pattern 并计算精确网格大小
  - 生成浮点精度中心点数组 (XCenters/YCenters)
  - 避免整数除法舍入误差累积
- ✅ **网格定位优化**
  - Finder Pattern 中间 3×3 块拆分为 3 个独立 cells
  - 删除不合理的第65行/列边界跳过逻辑
  - 统一水平和垂直方向实现（都使用 CalculateRunEndPosition）
  - 严格验证 gridSizeH == gridSizeV
- ✅ **调试开关系统**
  - C#: `ENABLE_DEBUG_LOGGING` 常量开关
  - Lua: `DEBUG_MODE` 变量开关  
  - 默认关闭详细调试信息，需要时一键启用
- 📊 网格容量更新: 3872 cells 可用 (68.6% 利用率)

### [2.0.0] - 2025-01-14
- ✅ 完整实现65×65网格编码系统
- ✅ 108个字段数据收集
- ✅ CRC32数据校验
- ✅ 模块化架构重构
- ✅ 性能优化 (参考DataToColor)
- ✅ WoW 1.12 API兼容
- ✅ 代码清理 (删除旧文件和函数)
- 📊 性能: 7.14ms (EncodeGrid: 4.45ms, RenderGrid: 2.69ms)

### [1.x.x] - 2025-01-13
- ✅ 初始版本开发 (简化实现)
- ✅ Texture方案 → FontString方案迁移
- ✅ UTF-8编码支持
- ✅ 全局CRC32校验
- ✅ EditBox文本显示

---

## 🎯 项目目标与状态

**最终目标**: 提供一个稳定、高性能的游戏状态读取方案,替代像素编码(DataToColor),支持更多数据字段和更可靠的OCR识别。

**当前状态**: ✅ v2.1.0 完成
- Lua端编码系统（含 Timing Pattern）
- C#端解码器（含 Timing Pattern 自动检测）
- 调试开关系统（便于问题排查）
- 解码成功率：100%（在正确的网格定位下）
- 性能：Lua端 7.14ms，C#端 <100ms（首次定位）

**下一步计划**:
- 集成到实际机器人工作流
- 长时间稳定性测试
- 优化 C# 端首次定位速度（如需）
