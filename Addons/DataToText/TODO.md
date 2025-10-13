# DataToText 开发任务清单

## 项目概述
将游戏数据以 OCR 友好的文本格式显示在屏幕顶部,替代像素编码方案 (DataToColor)。

**当前状态**: 使用稳定的简化版本 (DataToText.lua + DataToText.xml)，渐进式改进中。

---

## 🎯 当前渐进式改进计划 (2025-01-13)

> **策略**: 从稳定版本开始，每次只添加一个功能并测试，避免一次性改动过大导致难以调试。

### ✅ 已完成
- [x] **步骤1**: 优化字体设置为 MONOCHROME（黑底白字不需要 OUTLINE）
  - 修改 `DataToText.lua:154-156`
  - 从 `"OUTLINE, MONOCHROME"` 改为 `"MONOCHROME"`
  - 字体更粗更清晰，OCR 识别更准确

- [x] **清理**: 备份未使用的模块化文件到 archived 目录
  - 移动 init.lua, Core.lua, Utils.lua (旧版), Modules/* 到 archived/
  - 创建 archived/README.md 说明归档原因
  - 保持主目录简洁

- [x] **步骤2**: 添加十六进制工具函数和测试
  - 添加 `ToHex()` 和 `FromHex()` 函数
  - 创建 8 个测试用例验证十六进制转换
  - 添加 `/dtt test` 命令运行测试
  - ✅ 所有测试通过

- [x] **重构**: 分离工具函数到 Utils.lua
  - 创建 `DataToTextUtils` 命名空间
  - 包含函数: `ToHex`, `FromHex`, `GetUnitGUID`, `Trim`
  - 在 TOC 中正确排序加载

- [x] **重构**: 分离测试函数到 Tests.lua
  - 创建 `DataToTextTests` 命名空间
  - 包含测试: `TestHexConversion`, `TestStringUtils`, `TestBitLibrary`
  - 实现 `RunAllTests()` 统一测试入口

- [x] **步骤3a**: 测试 bit 库的可用性
  - 创建 `TestBitLibrary()` 检测位运算环境
  - 检测 bit 库（LuaJIT）、bit32 库（Lua 5.2+）
  - 检测 math.fmod / math.mod 函数可用性
  - 修复语法错误（移除 % 运算符直接使用）
  - ✅ **游戏内测试通过**

- [x] **研究**: 分析可用的 API 库
  - 发现 `C:\Users\zhanghua\Documents\wow-clean\Interface\AddOns\!Libs` 包含完整的 API 封装
  - 发现 `luaAPI.lua` 提供: `string.trim`, `string.split`, `math.fmod` 等
  - 发现 LibSharedMedia-3.0 使用了 bit 库，说明 bit 库应该可用
  - DataToColor/libs 包含 Ace3 和其他库

- [x] **步骤3b**: 确认位运算方案
  - ✅ **bit 库核心函数完全可用**
  - ✅ 可用函数: band, bor, bxor, bnot, lshift, rshift, arshift
  - ❌ 不可用函数: rol, ror, bswap (但不影响 UTF-8 编码实现)
  - ✅ 功能测试通过: bit.band(15,7)=7, bit.bxor(15,7)=8, bit.lshift(1,4)=16
  - 🎯 **决定**: 直接使用 bit 库实现 UTF-8 编码和 CRC8

### ✅ 已完成 (续)
- [x] **步骤4**: 实现 UTF-8 名称编码功能
  - ✅ 添加 `EncodeUTF8String()` 函数 - 将 UTF-8 字符串转为字节数组
  - ✅ 添加 `CalculateCRC8()` 函数 - 使用 bit.bxor 实现校验
  - ✅ 添加 `EncodeNameHex()` 函数 - 完整的名称编码（长度+CRC8+字节）
  - ✅ 添加测试用例验证中文名称编码 (TestUTF8Encoding)
  - ✅ 在目标显示中集成编码功能 (NAME_HEX字段)
  - ✅ 测试结果:
    - ASCII "ABC" → `0352414243` (长度03 + CRC52 + 字节414243)
    - 中文 "测试" → `0666E6B58BEBAF95` (长度06 + CRC66 + UTF-8字节)

- [x] **步骤5**: 全局 CRC32 校验系统
  - ✅ 添加 `CalculateCRC32()` 函数到 Utils.lua
  - ✅ 修改 `GetAllData()` 使用全局 CRC32 校验
  - ✅ 移除每个字段的独立 CRC8 校验
  - ✅ 所有数据用 `|` 分隔符合并
  - ✅ 按固定行宽（60字符）换行显示
  - ✅ 每行添加前缀（D1:, D2:, D3:...）方便 OCR 重组
  - ✅ 添加 CRC32 测试用例 (TestCRC32)
  - ✅ 添加 `/dtt crc32` 命令

### ⏳ 进行中

### 📋 待执行

- [ ] **步骤6**: 切换到模块化架构
  - 迁移到 init.lua + Core.lua + Modules 架构
  - 更新 TOC 加载顺序
  - 保持向后兼容

---

## 📝 技术笔记

### 当前版本架构
- **主文件**: `DataToText.lua` (主逻辑) + `DataToText.xml` (UI定义)
- **工具库**: `Utils.lua` (工具函数) + `Tests.lua` (测试套件)
- **字体**: Tiny-Bold.ttf, 14px, MONOCHROME
- **更新频率**: 0.1秒 (10 FPS)
- **显示内容**: 玩家数据、目标数据、背包数据

### 文件加载顺序 (DataToText.toc)
```
# 第三方库
libs\LibStub.lua
libs\CallbackHandler-1.0.lua
libs\AceCore-3.0.lua
libs\AceHook-3.0.lua
libs\LibDataBroker-1.1.lua
libs\LibDBIcon-1.0.lua

# 工具函数和测试
Utils.lua
Tests.lua

# 主文件
DataToText.xml
```

### 可用的外部库 (重要!)

#### 1. !Libs 目录 (wow-clean/Interface/AddOns/!Libs)
这个目录包含了游戏中总是可用的封装完整的库，**可以直接使用**。

**luaAPI.lua** (`!Libs/!MyLib/api/luaAPI.lua`):
- `string.trim(str, chars)` / `strtrim(str, chars)` - 去除首尾空格或指定字符
- `string.split(subject, delimiter, trim)` / `strsplit(...)` - 字符串分割
- `string.join(delimiter, ...)` / `strjoin(...)` - 字符串连接
- `string.match(str, pattern, index)` / `strmatch(...)` - 正则匹配
- `string.reverse(str)` / `strrev(str)` - 字符串反转
- `math.fmod(x, y)` - 等价于 `math.mod` (第17行)
- `math.modf(i)` - 返回整数和小数部分
- `math.cosh(i)`, `math.sinh(i)`, `math.tanh(i)` - 双曲函数
- `clamp(x, min, max)` - 限制数值范围
- `Round(input, places)` - 四舍五入
- `HexColors(r, g, b)` - 生成颜色码

**其他库**:
- Ace2 / Ace3 - 插件框架
- LibSharedMedia-3.0 - 媒体库（使用了 bit 库）
- Abacus-2.0, Tourist-2.0 等 - 数据处理库

#### 2. DataToColor/libs 目录
- Ace3 完整库
- LibClassicCasterino
- LibRangeCheck-2.0 / LibRangeCheck-3.0

### 位运算环境 (已确认)
✅ **bit 库可用** (LuaJIT) - 核心函数全部支持

**✅ 可用的 bit 库函数** (经测试验证):
- `bit.band(a, b)` - 按位与 (AND) ✓
- `bit.bor(a, b)` - 按位或 (OR) ✓
- `bit.bxor(a, b)` - 按位异或 (XOR) ✓
- `bit.bnot(x)` - 按位取反 (NOT) ✓
- `bit.lshift(x, n)` - 左移 ✓
- `bit.rshift(x, n)` - 逻辑右移 ✓
- `bit.arshift(x, n)` - 算术右移 ✓

**❌ 不可用的函数**:
- `bit.rol(x, n)` - 循环左移 ✗
- `bit.ror(x, n)` - 循环右移 ✗
- `bit.bswap(x)` - 字节交换 ✗

**功能测试结果**:
- bit.band(15, 7) = 7 ✓
- bit.bxor(15, 7) = 8 ✓
- bit.lshift(1, 4) = 16 ✓

**UTF-8 编码实现方案**:
- 使用 `bit.bxor` 实现 CRC8 校验
- 使用 `bit.band` 提取字节 (0xFF 掩码)
- 使用 `bit.rshift` 处理多字节字符
- **核心函数足够使用，无需循环移位**

### UTF-8 名称编码格式 (已实现)
✅ **编码格式**: `[长度2位][CRC82位][字节1-2位][字节2-2位]...`

**编码示例**:
- `"ABC"` → `0352414243`
  - `03` - 长度 (3字节)
  - `52` - CRC8 校验值
  - `414243` - UTF-8字节 (A=0x41, B=0x42, C=0x43)

- `"测试"` → `0666E6B58BEBAF95`
  - `06` - 长度 (6字节，中文每字符3字节)
  - `66` - CRC8 校验值
  - `E6B58B` - "测" 的UTF-8编码
  - `EBAF95` - "试" 的UTF-8编码

**CRC8 算法**:
- 多项式: 0x07 (7)
- 使用 `bit.bxor`, `bit.lshift`, `bit.band` 实现
- 检测位: 0x80 (128)

### 最终显示格式 (全局 CRC32 + 固定行宽)

✅ **当前格式**: 全局 CRC32 校验 + 固定行宽换行

**格式规范**:
```
第1行: CRC:XXXXXXXX           (8位十六进制 CRC32 校验值)
第2行: D1:数据片段1           (D1: 前缀 + 最多60字符数据)
第3行: D2:数据片段2           (D2: 前缀 + 最多60字符数据)
第N行: DN:数据片段N           (DN: 前缀 + 剩余数据)
```

**数据格式**:
- 所有字段用 `|` 分隔
- 每个字段格式: `KEY:VALUE`
- 值使用自然宽度十六进制（无补零）
- 名称编码为 UTF-8 字节的十六进制

**显示示例**:
```
CRC:A5B3C2D1
D1:P_HP:1F4/3E8|P_MANA:12C/1F4|P_LEVEL:A|P_XP:3E8/1388|P
D2:_GUID:0x0000000000000001|T_NAME:E6B58BEBAF95|T_HP:64/
D3:C8|T_LEVEL:5|T_DEAD:0|T_GUID:0x0000000000000002|BAG_
D4:USED:A/10|BAG_FREE:6
```

**OCR 解析流程**:
1. 读取 CRC 行获取校验值
2. 读取所有 D1:, D2:, D3:... 行
3. 去除行前缀，按顺序拼接为完整数据字符串
4. 计算 CRC32 并与 CRC 行比对，验证数据完整性
5. 按 `|` 分割字段
6. 解析每个 KEY:VALUE 对

**CRC32 算法**:
- 多项式: 0xEDB88320 (IEEE 802.3 反转多项式)
- 使用 `bit.bxor`, `bit.rshift`, `bit.band` 实现
- 初始值: 0xFFFFFFFF
- 最终异或: 0xFFFFFFFF

### 已知限制
- ~~目标名称尚未支持中文（需要 UTF-8 编码）~~ ✅ 已完成
- ~~每个字段独立 CRC8 校验~~ ✅ 已改为全局 CRC32
- 未实现完整的数据格式（只有基础信息）
- 暂未启用模块化架构（已归档到 archived/）

### 下一步测试要求
每个步骤完成后需要：
1. 在游戏中 `/reload` 重载插件
2. 检查聊天框是否有 Lua 错误
3. 使用 `/dtt` 验证功能正常
4. 确认显示内容正确

---

## 🔄 更新日志

### [1.0.3] - 2025-01-13
- ✅ 实现全局 CRC32 校验系统
- ✅ 添加 `CalculateCRC32()` 函数到 Utils.lua (IEEE 802.3 标准)
- ✅ 重构 `GetAllData()` 使用全局 CRC32 替代每字段 CRC8
- ✅ 所有数据用 `|` 分隔符合并为单一字符串
- ✅ 实现固定行宽（60字符）自动换行
- ✅ 每行添加前缀（D1:, D2:, D3:...）方便 OCR 拼接
- ✅ 添加 CRC32 测试用例 (TestCRC32)
- ✅ 添加 `/dtt crc32` 命令单独运行 CRC32 测试
- ✅ 更新 TODO.md 记录最终显示格式和 OCR 解析流程
- ✅ 简化名称编码（移除分行逻辑，由全局换行处理）

### [1.0.2] - 2025-01-13
- ✅ 重构: 分离工具函数到 Utils.lua
- ✅ 重构: 分离测试函数到 Tests.lua
- ✅ 添加 `/dtt test` 命令运行单元测试
- ✅ 创建 TestBitLibrary() 检测位运算环境
- ✅ 研究并记录可用的外部库 (!Libs 目录)
- ✅ 确认 bit 库完全可用 (核心函数测试通过)
- ✅ 更新 TODO.md 记录 bit 库的详细信息
- ✅ 实现 UTF-8 编码功能 (EncodeUTF8String, CalculateCRC8, EncodeNameHex)
- ✅ 添加 UTF-8 编码测试 (TestUTF8Encoding)
- ✅ 添加 `/dtt utf8` 命令单独运行 UTF-8 测试
- ✅ 修复 Lua 5.1 十六进制字面量兼容性问题 (0x80 → 128)
- ✅ 集成 NAME_HEX 字段到目标显示
- ✅ 验证中文名称编码正常工作

### [1.0.1] - 2025-01-13
- ✅ 优化字体渲染: MONOCHROME 替代 OUTLINE
- ✅ 添加十六进制工具函数 (ToHex, FromHex)
- ✅ 添加工具函数测试 (8个测试用例)
- ✅ 清理未使用代码到 archived/ 目录
- ✅ 创建测试框架

### [1.0.0] - 2025-01-13
- ✅ 初始稳定版本
- ✅ 基础显示功能正常运行
- ✅ 支持玩家、目标、背包数据显示

---

## 🚫 已归档 (暂不执行)

以下功能已从当前计划中移除，待基础功能稳定后再考虑：

### 模块化架构 (已归档)
- init.lua, Core.lua, Utils.lua, Modules/* 架构
- 暂时保留但不使用，避免复杂度

### C# 端实现 (已归档)
- TextAddonReader 类
- OCR 文本解析
- 待 Lua 端完全稳定后再实施

### 高级功能 (已归档)
- 性能优化（分级更新）
- 错误处理（pcall 保护）
- 配置界面
- 多版本兼容性测试

---

## 📌 重要提醒

1. **每次只改一个地方**，改完就测试
2. **保持 Lua 5.1 兼容性**，避免使用新语法
3. **优先使用已验证的函数**，如 `math.mod()` 而不是 `%`
4. **所有注释使用中文**
5. **更新此 TODO.md 反映实际进度**
6. **优先使用 !Libs 中的库函数**，避免重复造轮子
   - 字符串处理: 使用 `strtrim`, `strsplit`, `strjoin` 等
   - 数学函数: 使用 `math.fmod`, `clamp`, `Round` 等
   - 检查 `luaAPI.lua` 是否已有所需功能
7. **位运算**: 等待 `/dtt test` 结果再决定实现方案
