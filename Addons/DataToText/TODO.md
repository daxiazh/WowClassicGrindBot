# DataToText v2.0 开发记录

## 项目概述

DataToText 是一个将游戏数据编码为 65×65 黑白网格的插件,用于通过OCR方式读取游戏状态。

**当前版本**: v2.0.0
**状态**: ✅ Lua端实现完成，待C#解码器开发

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
  - 4个3×3角标记定位
  - 总容量: 4189 bits 可用, 2656 bits 使用 (63.4% 利用率)

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

角标记 (4× 3×3 = 36 cells):
  █ █ █     ...     █ █ █
  █ ░ █             █ ░ █
  █ █ █             █ █ █

  ...               ...

  █ █ █             █ █ █
  █ ░ █     ...     █ ░ █
  █ █ █             █ █ █

数据区 (4189 cells):
  逐行填充 (跳过角标记)
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

2. ✅ **角标记识别**
   - 识别4个3×3角标记（黑框+白心图案）
   - 自适应单元格大小检测（4px-100px）
   - 支持所有分辨率（800×600到8K）

3. ✅ **网格解码**
   - 逐cell采样中心点颜色（二值化）
   - 提取4189位数据
   - 跳过4个角标记区域

4. ✅ **数据解析**
   - 提取元数据（版本号、字段数）
   - IEEE 802.3 CRC32校验
   - 解析108个24-bit字段
   - Big-endian字节序

5. ✅ **性能优化**
   - 位置缓存机制（10帧验证一次）
   - 步进搜索算法（step = cellSize/2）
   - CRC32查找表
   - 静态方法优化

6. ✅ **测试框架**
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

## 🎯 项目目标

**最终目标**: 提供一个稳定、高性能的游戏状态读取方案,替代像素编码(DataToColor),支持更多数据字段和更可靠的OCR识别。

**当前状态**: Lua端实现完成,等待C#端解码器集成。
