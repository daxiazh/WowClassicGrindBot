# FontString 字符网格测试指南

## 📋 概述

FontString 字符网格是对原始 Texture 黑白网格方案的性能优化版本。通过使用特殊字符（█ 和 ░）代替像素纹理，显著减少了对象数量，从而提升游戏帧率。

## 🎯 技术对比

| 方案 | 对象数量 | 预期帧率 | 实现方式 |
|------|---------|---------|---------|
| **Texture 方案** | 4225 个 | 30 FPS | CreateTexture + SetTexture (每像素一个对象) |
| **FontString 方案** | 32 个 | 50+ FPS | CreateFontString (每行一个对象) |
| **对象减少** | -99.2% | +66% | 使用特殊字符代替像素 |

## 🛠️ 测试步骤

### 1. 游戏内测试

```lua
-- 加载插件
/reload

-- 打开界面
/dtt

-- 运行 FontString 测试
/dtt gridtext
```

### 2. 查看输出信息

测试成功后会在聊天框显示：

```
[DataToText] 开始生成 FontString 网格...
[DataToText] FontString 测试网格已生成: 32x32
[DataToText] 对象数量: 32 (vs 原方案 4225)
[DataToText] 请截图并使用 CoreTests 中的解码器分析
[DataToText] 或使用 Node.js 解码器: node grid_text_decoder.js <截图路径>
```

### 3. 检查显示效果

在游戏窗口左上角的 DataToText 界面中，你应该看到：

```
█ █ █                         █ █ █
█ ░ █                         █ ░ █
█ █ █                         █ █ █
░ ░ ░ ░                       ░ ░ ░
░ ░ ░ ░ ░                     ░ ░ ░
░ ░ ░ ░ ░ ░                   ░ ░ ░
        ░ ░ █                 ░ ░ ░
          ░ ░ █               ░ ░ ░
            ░ ░ █             ░ ░ ░
              ░ ░ █           ░ ░ ░
                ...           ░ ░ ░
                              █ ░ ░
                              ░ █ ░
█ █ █                         █ █ █
█ ░ █                         █ ░ █
█ █ █                         █ █ █
```

**特征**：
- **四个角**：3×3 标记（黑色边框，中心白色）
- **中心对角线**：从左上到右下的黑色对角线
- **其余区域**：白色（使用 ░ 字符显示）

### 4. 性能测试

**使用帧率插件测试**（推荐 **FpsDisplay** 或游戏内 `/frameinfo`）：

1. **基准测试**：
   - 关闭 DataToText 窗口（`/dtt`）
   - 记录基准帧率（例如：60 FPS）

2. **Texture 方案测试**：
   - 打开 DataToText 窗口（`/dtt`）
   - 正常显示状态（会自动渲染 Texture 网格）
   - 记录帧率（预期：~30 FPS，下降 50%）

3. **FontString 方案测试**：
   - 运行 `/dtt gridtext`
   - 等待生成完成（约 1-2 秒）
   - 记录帧率（预期：~50 FPS，下降 17%）

### 5. 截图（用于解码器测试）

**截图步骤**：
1. 运行 `/dtt gridtext` 生成网格
2. 使用游戏内截图（PrintScreen 或 F12）
3. 截图保存在 `WoW/Screenshots/` 目录

**截图要求**：
- 包含完整的 DataToText 窗口
- 网格清晰可见（不要有其他窗口遮挡）
- 推荐分辨率：1920×1080 或更高

## 🔍 解码器验证

### 方案 A：C# 解码器（推荐）

在 CoreTests 项目中实现（待完成）：

```csharp
// CoreTests/GridTextDecoder/Test_GridTextDecoder.cs
// 使用 WowScreenDXGI.ScreenImage 捕获屏幕
// 使用 SixLabors.ImageSharp 处理图像
```

**运行测试**：
```bash
cd CoreTests
dotnet run
```

### 方案 B：Node.js 解码器（备选）

如果需要独立验证，可以使用 Node.js 脚本：

```bash
# 安装依赖
npm install jimp

# 运行解码器
node grid_text_decoder.js <截图路径>
```

**预期输出**：
```
📖 开始解码 FontString 字符网格...
✅ 读取图片: 1920×1080 像素
🔍 检测网格区域...
✅ 检测到网格: 起点(15, 35), 单元格尺寸: 6×6
📊 解码结果:
  - 总单元格: 1024
  - 黑色: 140 (13.7%)
  - 白色: 884 (86.3%)
🎯 验证定位标记:
  - 左上角: ✅ 正确
  - 右上角: ✅ 正确
  - 左下角: ✅ 正确
  - 右下角: ✅ 正确
✅ FontString 方案可行！角标记识别准确率: 100%
```

## 📊 测试清单

- [ ] **游戏内加载测试**
  - [ ] `/reload` 无 Lua 错误
  - [ ] `/dtt gridtext` 命令成功执行
  - [ ] 聊天框显示生成成功信息

- [ ] **显示效果测试**
  - [ ] 四个角的 3×3 标记清晰可见
  - [ ] 对角线图案正确显示
  - [ ] 字符对齐整齐（等宽字体）
  - [ ] 黑白对比度足够（█ vs ░）

- [ ] **性能测试**
  - [ ] 记录基准帧率（无窗口）
  - [ ] 记录 Texture 方案帧率（原方案）
  - [ ] 记录 FontString 方案帧率（新方案）
  - [ ] 计算性能提升百分比

- [ ] **截图测试**
  - [ ] 截图包含完整网格
  - [ ] 截图清晰无遮挡
  - [ ] 保存截图路径记录

- [ ] **解码器测试**
  - [ ] 运行 C# 或 Node.js 解码器
  - [ ] 验证角标记识别率 > 75%
  - [ ] 验证整体识别准确率 > 95%

## ⚙️ 调试参数

如果显示效果不理想，可以调整以下参数（在 `DataToText.lua:355-356`）：

```lua
-- 测试参数
local TEST_GRID_SIZE = 32  -- 网格尺寸（32×32 或 65×65）
local CHAR_BLACK = "█"      -- U+2588 全方块（黑色）
local CHAR_WHITE = "░"      -- U+2591 浅色方块（白色/对比）
local FONT_SIZE_GRID = 6    -- 字体大小（像素）
```

**调整建议**：
- **字符不清晰**：增大 `FONT_SIZE_GRID`（6 → 8 → 10）
- **显示太大**：减小 `FONT_SIZE_GRID`（6 → 5 → 4）
- **对比度不够**：更换字符组合（尝试 █ vs 空格，或 ▓ vs ░）
- **需要更多数据**：增大 `TEST_GRID_SIZE`（32 → 48 → 65）

## 🐛 常见问题

### Q1: 字符显示为方框 □ 而不是实心方块 █
**原因**: 字体不支持 Unicode 特殊字符
**解决**: 确保使用 Tiny-Bold.ttf 字体，如果仍有问题，尝试更换字符：
```lua
local CHAR_BLACK = "#"  -- 使用 ASCII 字符代替
local CHAR_WHITE = "."
```

### Q2: 字符行不对齐，上下错位
**原因**: 行高计算不准确
**解决**: 调整 `fontString:SetPoint` 的 Y 偏移量：
```lua
-- 原代码（行428）
fontString:SetPoint("TOPLEFT", gridFrame, "TOPLEFT", 0, -(row - 1) * FONT_SIZE_GRID)

-- 调整为（增加行间距）
fontString:SetPoint("TOPLEFT", gridFrame, "TOPLEFT", 0, -(row - 1) * (FONT_SIZE_GRID + 1))
```

### Q3: 帧率提升不明显
**原因**: 可能有其他插件占用资源
**解决**:
1. 禁用其他插件测试
2. 检查 CPU 占用（任务管理器）
3. 确认网格对象数量（查看聊天框输出）

### Q4: 解码器无法识别角标记
**原因**: 字体渲染导致字符模糊或重叠
**解决**:
1. 增大字体尺寸（FONT_SIZE_GRID）
2. 降低网格尺寸（TEST_GRID_SIZE）
3. 提高截图分辨率

## 📝 测试记录模板

```markdown
### 测试时间: 2025-01-13

**环境**:
- 游戏版本: WoW Classic 1.14.4
- 插件版本: DataToText 1.1.1
- 屏幕分辨率: 1920×1080
- 显示器 Gamma: 2.2

**性能测试结果**:
- 基准帧率（无窗口）: 60 FPS
- Texture 方案帧率: 30 FPS (-50%)
- FontString 方案帧率: 52 FPS (-13%)
- 性能提升: +73% (相对于 Texture 方案)

**显示效果**:
- [✅] 四个角标记正确
- [✅] 对角线图案清晰
- [✅] 字符对齐整齐
- [✅] 黑白对比度足够

**解码器测试**:
- 角标记识别率: 100% (4/4)
- 整体识别准确率: 98.5%
- 截图路径: `Screenshots/WoWScrnShot_013125_123456.jpg`

**结论**:
FontString 方案可行，性能提升显著，推荐替代 Texture 方案。
```

## 🔗 相关文档

- `DATATOCOLOR_FIELDS.md` - 完整字段映射表
- `GRID_ENCODING.md` - 黑白网格编码系统说明
- `TODO.md` - 开发任务清单
- `README.md` - 插件总体说明

## 🚀 下一步

1. **游戏内测试**: 验证显示效果和性能
2. **C# 解码器**: 在 CoreTests 中实现自动化测试
3. **65×65 完整网格**: 如果测试成功，扩展到完整尺寸
4. **集成到主逻辑**: 替换 Texture 方案，用于实际数据显示
