# DataToText 开发任务清单

## 项目概述
将游戏数据以 OCR 友好的文本格式显示在屏幕顶部,替代像素编码方案 (DataToColor)。

---

## ✅ 已完成

### 核心框架
- [x] 创建插件基础结构 (init.lua, Core.lua, Utils.lua)
- [x] 实现模块注册系统
- [x] 创建显示框架 (可拖拽、自动保存位置)
- [x] 斜杠命令 (`/dtt`, `/dtt reset`, `/dtt debug`)

### 数据模块 (完整6行输出)
- [x] Player 模块 - HP, MP, 坐标, 朝向, 等级, 经验, 金钱
- [x] Target 模块 - HP, 名称(UTF-8十六进制), GUID, NPC ID, 等级, 分类
- [x] Combat 模块 - GCD, 连击点, 宠物HP, 施法信息, 符文状态
- [x] Inventory 模块 - 背包空位, 装备耐久度
- [x] Status 模块 - 16位标志位, 形态, 距离, Buff/Debuff计数
- [x] Checksum 模块 - 第6行校验和

### 中文名称编码 ✅ (2025-01-13 完成)
- [x] 实现 UTF-8 十六进制编码 (`U.EncodeNameHex`)
- [x] 实现 CRC8 校验和验证 (`U.CalculateCRC8`)
- [x] 更新 Target 模块使用新编码格式

**编码格式示例**:
```
"野猪人" → "E9878EE78CAAE4BABA:3F"
          ↑ UTF-8 hex (18字节)  ↑ CRC8校验
```

### 文档
- [x] README.md - 完整使用说明
- [x] TODO.md - 开发任务清单

---

## 📋 待完成任务

### 🔥 最高优先级 - C# 端实现

> **目标**: 创建 OCR 文本解析器,还原 UTF-8 中文名称

#### C#-1. 创建 TextAddonReader 类
**文件**: `Core/Addon/TextAddonReader.cs` (新建)
- [ ] 实现 `IAddonReader` 接口
- [ ] 添加 OCR 文本解析逻辑
- [ ] 参考实现已写在旧版 TODO.md (行87-122)

#### C#-2. 实现十六进制解码和CRC8校验
- [ ] `DecodeHexToUtf8(string hexWithCrc)` - 解析 `TN=E9878E...6A:3F`
- [ ] `CalculateCRC8(byte[] data)` - CRC-8-CCITT (多项式 0x07)
- [ ] 处理 CRC 校验失败时的备用方案 (使用 NPC ID 查询数据库)

#### C#-3. 集成到 AddonReader
- [ ] 决定替换现有 TargetName 逻辑或创建新的 Reader
- [ ] 添加配置选项切换 DataToColor/DataToText
- [ ] 单元测试 CRC8 (与 Lua 端对比)

**参考实现**: 见旧版 TODO.md 行 86-194

---

### 高优先级 - Lua 端完善

#### 1. 验证 TOC 文件
- [ ] 检查所有模块是否已加载 (init.lua, Core.lua, Utils.lua, Modules/*)
- [ ] 验证加载顺序: libs → init → Utils → Core → Modules

#### 2. 游戏内测试 ✅ **已完成**
- [x] 复制插件到 WoW 目录测试
- [x] 验证 UTF-8 十六进制编码 (测试目标: "Ravager Assassin")
- [x] 测试 CRC8 校验和计算 (校验值: `:83`)
- [x] 检查 6 行数据显示
- [x] 验证字体加载 (14px 等宽字体)

**测试结果**:
- ✅ 编码正确: `5261766167657220417373617373696E:83` → "Ravager Assassin"
- ✅ 所有数据字段正常显示
- ✅ 无 Lua 错误
- ✅ 字体清晰可读

#### 3. 解决架构冲突
**问题**: 根目录下的 `DataToText.lua` 与新架构可能冲突
- [ ] 方案A: 删除旧文件,完全使用模块化架构
- [ ] 方案B: 在 TOC 中注释掉旧文件

---

### 中优先级 - 功能扩展

#### 4. OCR 配置优化
- [ ] 调整字体大小 (当前12px)
- [ ] 配置 Tesseract 字符白名单: `0123456789ABCDEF:=/`
- [ ] 测试不同分辨率

#### 5. 性能优化
- [ ] 实现分级更新 (Player:50ms, Inventory:500ms)
- [ ] 缓存不常变化的数据
- [ ] 减少字符串拼接开销

#### 6. 错误处理
- [ ] 为所有 API 调用添加 pcall 保护
- [ ] 在 Status 模块中报告错误代码

---

### 低优先级 - 可选扩展

#### 7. 配置界面
- [ ] 创建配置面板 (启用/禁用模块、调整刷新率)

#### 8. 多版本兼容性
- [ ] 测试 Classic Era (1.12)
- [ ] 测试 TBC (2.4.3)
- [ ] 测试 WotLK (3.3.5)
- [ ] 测试 Cataclysm (4.3.4)

---

## 🐛 已知问题

### 问题 1: TOC 加载顺序未验证 ⚠️
- **影响**: 插件可能无法正常加载
- **状态**: 待修复
- **相关**: 任务 #1

### 问题 2: 旧架构冲突 ⚠️
- **影响**: DataToText.lua 与模块化架构冲突
- **状态**: 待解决
- **相关**: 任务 #3

---

## 📝 技术笔记

### 数据格式 (最新)
```
L1:HP=1234/5678 MP=0ABC/1000 X=2D5E Y=3A12 F=0190 LV=3C XP=4567/FFFF $=000F4240
L2:THP=0456/1234 TN=E9878EE78CAA:A5 TGUID=12345678 TID=1A2B TLV=3D TCLS=0 TT=1
L3:GCD=0000 CP=0 PET=0000/0000 CAST=0000 CT=0000 RUNE=0000
L4:B0=10/10 B1=08/0C B2=00/08 B3=00/08 B4=00/08 DUR=64 EQ=0E/00001234
L5:FLG=0012 FORM=00 RNG=08 PB=03 PD=00 TB=01 TD=02 ERR=0000
L6:CHK=3F8A
```

### 中文名称编码原理
1. **编码端 (Lua)**:
   ```lua
   "野猪" → UTF-8字节: E9 87 8E E7 8C AA
   → 十六进制: "E9878EE78CAA"
   → CRC8校验: 0xA5
   → 输出: "E9878EE78CAA:A5"
   ```

2. **解码端 (C#)**:
   ```csharp
   "E9878EE78CAA:A5"
   → 分离数据和CRC: hex="E9878EE78CAA", crc="A5"
   → 转换为字节数组: [0xE9, 0x87, 0x8E, 0xE7, 0x8C, 0xAA]
   → 验证CRC8
   → UTF-8解码: "野猪"
   ```

### OCR 配置参考
```python
# Tesseract 配置
config = '--psm 6 -c tessedit_char_whitelist=0123456789ABCDEF:=/'

# 正则解析
match = re.search(r'TN=([0-9A-F]+):([0-9A-F]{2})', ocr_text)
hex_data = match.group(1)
crc = match.group(2)
```

---

## 📌 下一步行动

### 立即执行
1. [ ] 验证 TOC 文件加载顺序 (任务 #1)
2. ✅ **游戏内测试完成** - UTF-8 编码和 CRC8 校验正常工作
3. [ ] 解决旧架构冲突 (任务 #3)

### 本周目标
- [ ] 完成 Lua 端剩余高优先级任务 (#1, #3)
- [ ] **开始 C# 端 TextAddonReader 实现** ⭐ 最高优先级

### 长期目标
- [ ] 完整的 OCR 文本解析系统
- [ ] 性能优化达到 20 FPS
- [ ] 支持所有魔兽世界经典版本

---

## 🔄 更新日志

### [1.1.0] - 2025-01-13
- ✅ 实现 UTF-8 十六进制名称编码
- ✅ 添加 CRC8 校验和验证
- ✅ 更新 Target 模块支持中文
- ✅ **游戏内测试通过** - 编码功能正常工作

**测试数据**:
- 目标: "Ravager Assassin"
- 编码: `5261766167657220417373617373696E:83`
- 校验: CRC8 = 0x83
- 结果: ✅ 完全正确

### [1.0.0] - 2025-10-11
- ✅ 初始发布
- ✅ 实现基础模块化架构
- ✅ 完成所有6个数据模块
