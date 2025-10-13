# DataToText 开发任务清单

## 项目概述
将游戏数据以 OCR 友好的文本格式显示在屏幕顶部,替代像素编码方案 (DataToColor)。

---

## ✅ 已完成

### 核心框架
- [x] 创建插件基础结构 (init.lua)
- [x] 实现模块注册系统
- [x] 创建显示框架 (Core.lua)
- [x] 实现拖拽移动功能
- [x] 添加斜杠命令 (/dtt)
- [x] 实现自动保存位置

### 工具函数 (Utils.lua)
- [x] 十六进制编码/解码函数
- [x] 校验和计算 (XOR)
- [x] GUID 编码函数
- [x] NPC ID 提取函数
- [x] 名称编码函数 (8字符大写)
- [x] 坐标/时间编码函数
- [x] 安全的 Unit API 包装函数

### 数据模块
- [x] Player 模块 - 玩家数据 (第1行)
  - HP, MP, 坐标(X,Y), 朝向, 等级, 经验值, 金钱
- [x] Target 模块 - 目标数据 (第2行)
  - 目标HP, 名称, GUID, NPC ID, 等级, 分类, 目标的目标
- [x] Combat 模块 - 战斗数据 (第3行)
  - GCD, 连击点, 宠物HP, 施法信息, 符文状态
- [x] Inventory 模块 - 背包/装备数据 (第4行)
  - 背包空位统计, 装备耐久度, 装备槽位信息
- [x] Status 模块 - 状态标志 (第5行)
  - 16位标志位, 形态, 距离, Buff/Debuff 数量, 错误代码

### 显示功能
- [x] 6行文本显示 (每行对应一个数据模块)
- [x] 校验和显示 (第6行)
- [x] 等宽字体支持 (Tiny-Bold.ttf)
- [x] 纯黑背景 + 纯白文字 (OCR 优化)
- [x] 可配置刷新率 (20 FPS / 50ms)

### 文档
- [x] README.md - 完整使用说明
- [x] 数据格式说明
- [x] OCR 配置建议

### 中文名称编码 (2025-01-XX 新增)
- [x] 实现 UTF-8 十六进制编码 (`U.EncodeNameHex`)
- [x] 实现 CRC8 校验和 (`U.CalculateCRC8`)
- [x] 实现解码函数用于测试 (`U.DecodeNameHex`)
- [x] 更新 Target 模块使用新编码
- [x] 添加详细代码注释和示例

**编码格式**:
- 输入: "野猪人" (UTF-8)
- 输出: "E9878EE78CAAE4BABA:XX" (十六进制:CRC8)
- 支持最多 18 字节 (6个中文字符 或 18个英文字符)

---

## 🚧 进行中

### 测试和调试（人工做，无法通过自动化方式来执行）
- [ ] 在游戏中测试所有模块功能
  - [ ] 测试 Player 模块数据准确性
  - [ ] 测试 Target 模块 (不同类型目标)
  - [ ] 测试 Combat 模块 (施法、GCD、连击点)
  - [ ] 测试 Inventory 模块 (背包变化、装备耐久)
  - [ ] 测试 Status 模块 (各种状态标志)
  - [ ] 测试校验和功能
- [ ] 修复已知问题
  - [ ] 检查 TOC 文件加载顺序
  - [ ] 验证依赖库加载正确
  - [ ] 确认字体文件加载成功

---

## 📋 待完成 (优先级排序)

### 🔥 最高优先级 - C# 端实现

#### C#-1. 创建 TextAddonReader 类
**文件**: `Core/Addon/TextAddonReader.cs` (新建)
**任务**:
- [ ] 创建新的 AddonReader 实现,用于解析 DataToText 的文本输出
- [ ] 实现 `IAddonReader` 接口
- [ ] 添加 OCR 文本解析逻辑

#### C#-2. 实现十六进制名称解码
**文件**: `Core/Addon/TextAddonReader.cs`
**任务**:
- [ ] 实现 `DecodeHexToUtf8(string hex)` 方法
- [ ] 解析格式: `TN=E9878EE78CAA:A5` → "野猪"
- [ ] 处理空值和错误情况

**参考实现**:
```csharp
private string DecodeHexToUtf8(string hexWithCrc)
{
    if (string.IsNullOrEmpty(hexWithCrc) || hexWithCrc == "NONE:00")
        return string.Empty;

    // Split hex data and CRC
    var parts = hexWithCrc.Split(':');
    if (parts.Length != 2) return string.Empty;

    string hex = parts[0];
    string crcHex = parts[1];

    // Convert hex to bytes
    int byteCount = hex.Length / 2;
    byte[] bytes = new byte[byteCount];

    for (int i = 0; i < byteCount; i++)
    {
        bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
    }

    // Verify CRC8
    byte expectedCrc = CalculateCRC8(bytes);
    byte actualCrc = Convert.ToByte(crcHex, 16);

    if (expectedCrc != actualCrc)
    {
        // CRC mismatch - OCR error detected
        // Log warning and return empty or use NPC ID fallback
        return string.Empty;
    }

    // Decode UTF-8
    return Encoding.UTF8.GetString(bytes);
}
```

#### C#-3. 实现 CRC8 校验
**文件**: `Core/Addon/TextAddonReader.cs`
**任务**:
- [ ] 实现 `CalculateCRC8(byte[] data)` 方法
- [ ] 使用 CRC-8-CCITT 多项式 (0x07)
- [ ] 与 Lua 端的实现保持一致

**参考实现**:
```csharp
private byte CalculateCRC8(byte[] data)
{
    byte crc = 0;
    byte polynomial = 0x07;

    foreach (byte b in data)
    {
        crc ^= b;

        for (int i = 0; i < 8; i++)
        {
            if ((crc & 0x80) != 0)
                crc = (byte)((crc << 1) ^ polynomial);
            else
                crc = (byte)(crc << 1);
        }
    }

    return crc;
}
```

#### C#-4. 集成 OCR 文本解析
**文件**: `Core/Addon/TextAddonReader.cs`, OCR 模块
**任务**:
- [ ] 实现正则表达式解析 `TN=<HEX>:<CRC>`
- [ ] 处理 OCR 识别错误 (CRC 校验失败)
- [ ] 实现备用方案 (使用 NPC ID 查询数据库)

**参考实现**:
```csharp
public void ParseLine2(string ocrText)
{
    // Parse: L2:THP=1234/5678 TN=E9878EE78CAA:A5 TGUID=...
    var match = Regex.Match(ocrText, @"TN=([0-9A-F:]+)");
    if (match.Success)
    {
        string hexWithCrc = match.Groups[1].Value;
        TargetName = DecodeHexToUtf8(hexWithCrc);

        // Fallback to NPC database if decode fails
        if (string.IsNullOrEmpty(TargetName))
        {
            TargetName = GetNameFromDatabase(playerReader.TargetId);
        }
    }
}
```

#### C#-5. 更新 AddonReader 集成
**文件**: `Core/Addon/AddonReader.cs`
**任务**:
- [ ] 决定是否替换现有 TargetName 逻辑或创建新的 Reader
- [ ] 添加配置选项切换 DataToColor/DataToText
- [ ] 确保向后兼容性

#### C#-6. 测试和验证
**任务**:
- [ ] 单元测试 CRC8 计算 (与 Lua 端对比)
- [ ] 测试十六进制解码 (英文、中文、混合)
- [ ] 测试 OCR 错误处理 (CRC 校验失败)
- [ ] 集成测试 (完整流程)

---

### 高优先级 - 核心功能

#### 1. 修复 TOC 文件加载顺序
**文件**: `DataToText.toc`
**问题**: 当前 TOC 可能没有正确加载所有模块文件
**任务**:
- [ ] 检查 TOC 中是否包含所有 Lua 文件
- [ ] 确保加载顺序正确:
  1. 依赖库 (libs/*)
  2. init.lua (初始化)
  3. Utils.lua (工具函数)
  4. Core.lua (核心框架)
  5. 数据模块 (Modules/*.lua)
- [ ] 验证文件路径正确性

#### 2. 测试插件在游戏中的表现
**任务**:
- [ ] 将插件复制到 WoW 插件目录
- [ ] 启动游戏并启用插件
- [ ] 检查是否有 Lua 错误
- [ ] 验证所有6行数据是否正确显示
- [ ] 测试 /dtt 命令是否工作

#### 3. 优化 OCR 识别效果
**文件**: `Core.lua`, `Utils.lua`
**任务**:
- [ ] 调整字体大小 (当前 12px,可能需要更大)
- [ ] 优化字符间距 (等宽字体)
- [ ] 测试不同分辨率下的显示效果
- [ ] 添加字符白名单: `0123456789ABCDEF:=/_ `

#### 4. 处理中文目标名称
**文件**: `Utils.lua`, `Modules/Target.lua`
**问题**: 中文名称需要特殊处理才能被 OCR 识别
**任务**:
- [ ] 研究 WoW 中文编码 (可能是 UTF-8 或 GBK)
- [ ] 实现中文字符编码方案:
  - 方案A: 转换为拼音首字母
  - 方案B: 使用 NPC ID 替代名称
  - 方案C: 使用 Unicode 码点编码
- [ ] 更新 `U.EncodeName()` 函数
- [ ] 测试不同中文NPC名称

### 中优先级 - 功能增强

#### 5. 添加小地图按钮
**文件**: `DataToText.lua`, `init.lua`
**状态**: 代码已存在但可能未集成
**任务**:
- [ ] 检查 LibDBIcon 是否正确初始化
- [ ] 验证小地图按钮是否显示
- [ ] 测试点击按钮切换显示
- [ ] 添加右键菜单 (可选)

#### 6. 完善错误处理
**文件**: 所有模块
**任务**:
- [ ] 为所有 API 调用添加 pcall 保护
- [ ] 实现错误日志系统
- [ ] 在 Status 模块中报告错误代码
- [ ] 添加降级处理 (某个模块出错不影响其他模块)

#### 7. 性能优化
**文件**: `Core.lua`, 各数据模块
**任务**:
- [ ] 分析更新频率需求 (某些数据可以更新慢一点)
- [ ] 实现分级更新:
  - Player/Combat: 50ms (20 FPS)
  - Target: 100ms (10 FPS)
  - Inventory: 500ms (2 FPS)
  - Status: 100ms (10 FPS)
- [ ] 缓存不常变化的数据
- [ ] 减少字符串拼接开销

#### 8. 添加配置界面
**新文件**: `Config.lua`
**任务**:
- [ ] 创建配置面板 (使用 AceConfig 或原生UI)
- [ ] 添加配置选项:
  - 启用/禁用各个模块
  - 调整刷新率
  - 选择字体和字号
  - 设置透明度
  - 锁定/解锁窗口
- [ ] 保存配置到 DataToTextDB

### 低优先级 - 扩展功能

#### 9. 添加更多数据字段
**可选扩展**:
- [ ] 添加移动速度 (Player 模块)
- [ ] 添加仇恨值信息 (Combat 模块)
- [ ] 添加技能冷却信息 (新模块)
- [ ] 添加任务进度 (新模块)
- [ ] 添加副本/战场信息 (新模块)

#### 10. 创建 OCR 解析器 (C# 端)
**新项目**: 在 WowClassicGrindBot 主项目中
**任务**:
- [ ] 创建 TextAddonReader 类 (类似 AddonReader)
- [ ] 实现屏幕顶部区域截图
- [ ] 集成 Tesseract OCR
- [ ] 实现文本解析和校验和验证
- [ ] 将解析结果映射到 PlayerReader 数据结构
- [ ] 性能测试和优化

#### 11. 多客户端版本兼容性
**文件**: `init.lua`, 各模块
**任务**:
- [ ] 测试 Classic Era (1.12)
- [ ] 测试 TBC Classic (2.4.3)
- [ ] 测试 WotLK Classic (3.3.5)
- [ ] 测试 Cataclysm Classic (4.3.4)
- [ ] 修复版本特定的 API 差异
- [ ] 更新 TOC 接口版本号

#### 12. 本地化支持
**新文件**: `Locales/enUS.lua`, `Locales/zhCN.lua`
**任务**:
- [ ] 提取所有显示文本
- [ ] 创建本地化表
- [ ] 添加语言切换功能
- [ ] 翻译为中文/英文

---

## 🐛 已知问题

### 问题 1: 中文目标名称无法识别
**描述**: OCR 只能识别 ASCII 字符,中文名称会识别失败
**影响**: 在中文客户端无法正确显示目标名称
**优先级**: 高
**状态**: 待解决
**相关任务**: #4 处理中文目标名称

### 问题 2: 字体文件可能加载失败
**描述**: 如果字体文件路径错误或文件损坏,会回退到系统字体
**影响**: OCR 识别率降低
**优先级**: 中
**状态**: 待验证
**相关任务**: #2 测试插件在游戏中的表现

### 问题 3: TOC 文件加载顺序未验证
**描述**: 当前 TOC 可能缺少某些文件或顺序错误
**影响**: 插件可能无法正常加载
**优先级**: 高
**状态**: 待修复
**相关任务**: #1 修复 TOC 文件加载顺序

### 问题 4: 旧版 DataToText.lua 与新架构冲突
**描述**: 根目录下的 DataToText.lua 实现了简化版本,可能与模块化架构冲突
**影响**: 可能导致重复初始化或功能冲突
**优先级**: 高
**状态**: 待解决
**解决方案**:
- 选项A: 删除旧的 DataToText.lua,完全使用模块化架构
- 选项B: 保留旧版作为备用,在 TOC 中注释掉
- 选项C: 合并两个版本的优点

---

## 📝 开发笔记

### 当前架构
```
DataToText/
├── init.lua            # 入口,初始化,命令
├── Core.lua            # 显示框架,模块管理
├── Utils.lua           # 工具函数
├── Modules/
│   ├── Player.lua      # 第1行
│   ├── Target.lua      # 第2行
│   ├── Combat.lua      # 第3行
│   ├── Inventory.lua   # 第4行
│   └── Status.lua      # 第5行
├── libs/               # 依赖库
├── Fonts/              # 字体文件
└── DataToText.lua      # 旧版实现(待处理)
```

### 数据流
1. `init.lua` 初始化插件,注册事件和命令
2. `Core.lua` 创建显示框架,注册模块
3. 各 `Modules/*.lua` 注册到 DataToText.M
4. OnUpdate 触发 `UpdateDisplay()`
5. 调用各模块的 `GetData()` 方法
6. 生成6行文本 + 校验和
7. 更新 UI 显示

### 性能指标
- 目标刷新率: 20 FPS (50ms)
- 单次更新耗时: < 5ms
- 内存占用: < 1MB

### OCR 配置参考
```python
# Tesseract 配置
config = '--psm 7 -c tessedit_char_whitelist=0123456789ABCDEF:=/_ '

# 截图区域 (1920x1080 分辨率)
x, y, w, h = 500, 0, 900, 100
```

---

## 🔄 更新日志

### [未发布] - 进行中
- 创建 TODO.md 任务清单
- 整理项目结构和待办事项

### [1.0.0] - 2025-10-11
- 实现基础模块化架构
- 完成所有数据模块
- 添加 README 文档

---

## 📌 下一步行动

### 立即执行
1. **修复 TOC 文件** (任务 #1)
   - 检查并更新 DataToText.toc
   - 确保所有模块文件都被加载

2. **解决架构冲突** (问题 #4)
   - 决定保留哪个版本的 DataToText.lua
   - 清理冗余代码

3. **游戏内测试** (任务 #2)
   - 复制插件到 WoW 目录
   - 启动游戏验证功能

### 本周目标
- [ ] 完成所有高优先级任务 (#1-#4)
- [ ] 解决所有已知问题
- [ ] 完成基础测试

### 长期目标
- [ ] 创建 C# OCR 解析器集成到机器人
- [ ] 性能优化达到目标指标
- [ ] 支持所有魔兽世界经典版本

---

## 📞 需要帮助?

如果在开发过程中遇到问题:
1. 检查 WoW 错误日志: `WTF/Logs/`
2. 使用 `/dtt debug` 命令查看调试信息
3. 检查 Lua 语法错误
4. 参考 WoW API 文档
