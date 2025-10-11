# DataToText - OCR-Friendly Game Data Display

基于 OCR 识别的游戏数据展示插件,用于替代 DataToColor 的像素编码方案。

## 📦 安装

1. 将 `DataToText` 文件夹复制到 WoW 游戏目录:
   ```
   World of Warcraft\_classic_\Interface\AddOns\DataToText
   ```

2. **重要**: 将字体文件 `Tiny-Bold.ttf` 放置到:
   ```
   DataToText\Fonts\Tiny-Bold.ttf
   ```

3. 启动游戏,在角色选择界面点击"插件"确认 DataToText 已启用

## 🎮 使用方法

### 基本命令

```
/dtt                 - 切换显示开关
/dtt toggle          - 切换显示开关
/dtt reset           - 重置窗口位置到屏幕顶部
```

### 移动窗口

- 左键拖拽黑色显示框即可移动到任意位置
- 位置会自动保存到角色配置

## 📊 数据格式

插件在屏幕顶部显示 6 行文本,所有数值使用**十六进制编码** (0-9, A-F):

### 第 1 行:玩家数据
```
L1:HP=XXXX/XXXX MP=XXXX/XXXX X=XXXX Y=XXXX F=XXXX LV=XX XP=XXXX/XXXX $=XXXXXXXX
```
- `HP` = 生命值 (当前/最大)
- `MP` = 法力/怒气/能量 (当前/最大)
- `X`, `Y` = 坐标 (乘以 100)
- `F` = 朝向 (弧度 × 100)
- `LV` = 等级
- `XP` = 经验值 (当前/最大)
- `$` = 金钱 (铜币)

### 第 2 行:目标数据
```
L2:THP=XXXX/XXXX TN=NAME TGUID=XXXXXXXX TID=XXXX TLV=XX TCLS=X TT=X
```
- `THP` = 目标生命值
- `TN` = 目标名称 (前8个字符,大写)
- `TGUID` = 目标 GUID (后8位)
- `TID` = 目标 NPC ID
- `TLV` = 目标等级
- `TCLS` = 目标分类 (0=普通, 1=灰色, 2=精英, 3=稀有, 4=稀有精英, 5=世界Boss)
- `TT` = 目标的目标 (0=无, 1=玩家, 2=其他)

### 第 3 行:战斗数据
```
L3:GCD=XXXX CP=X PET=XXXX/XXXX CAST=XXXX CT=XXXX RUNE=XXXX
```
- `GCD` = 公共冷却剩余时间 (毫秒)
- `CP` = 连击点/神圣能量
- `PET` = 宠物生命值
- `CAST` = 正在施放的法术 ID
- `CT` = 施法剩余时间 (毫秒)
- `RUNE` = 符文状态 (死骑专用,4位十六进制:血/邪/冰/死亡)

### 第 4 行:背包/装备数据
```
L4:B0=XX/XX B1=XX/XX B2=XX/XX B3=XX/XX B4=XX/XX DUR=XX EQ=XX/XXXXXXXX
```
- `B0-B4` = 背包空位/总槽位
- `DUR` = 装备平均耐久度 (百分比)
- `EQ` = 当前显示的装备槽位/物品ID (轮询显示)

### 第 5 行:状态标志
```
L5:FLG=XXXX FORM=XX RNG=XX PB=XX PD=XX TB=XX TD=XX ERR=XXXX
```
- `FLG` = 16位标志位 (二进制转十六进制)
  - Bit 0: 战斗中
  - Bit 1: 移动中
  - Bit 2: 死亡/灵魂
  - Bit 3: 潜行
  - Bit 4: 骑乘
  - Bit 5: 目标是玩家
  - Bit 6: PVP 标记
  - Bit 7: 休息中
  - Bit 8: 飞行点
  - Bit 9: 飞行中
  - Bit 10: 游泳
  - Bit 11: 室内
  - Bit 12: 引导法术
  - Bit 13: 施法中
  - Bit 14: 有宠物
  - Bit 15: 目标死亡
- `FORM` = 形态/姿态 (0=无)
- `RNG` = 到目标距离 (码)
- `PB` = 玩家 Buff 数量
- `PD` = 玩家 Debuff 数量
- `TB` = 目标 Buff 数量
- `TD` = 目标 Debuff 数量
- `ERR` = UI 错误代码

### 第 6 行:校验和
```
L6:CHK=XXXX
```
- `CHK` = 前5行数据的校验和 (XOR 校验)

## 🔧 十六进制解码

所有数值使用十六进制编码,解码方法:

```csharp
// C# 示例
int value = Convert.ToInt32("322F", 16); // 12847
float coord = Convert.ToInt32("2D5E", 16) / 100.0f; // 115.66
```

```python
# Python 示例
value = int("322F", 16)  # 12847
coord = int("2D5E", 16) / 100.0  # 115.66
```

## 🎨 字体要求

**必须使用等宽字体 (Monospace Font)**,推荐:
- Tiny-Bold.ttf (项目提供)
- Courier New
- Consolas
- Monaco

非等宽字体会导致 OCR 识别错误!

## 📝 OCR 配置建议

### Tesseract OCR 设置

```python
import pytesseract

# 仅识别十六进制字符
config = '--psm 7 -c tessedit_char_whitelist=0123456789ABCDEF:=/_ '

# 二值化预处理
image = cv2.threshold(gray, 128, 255, cv2.THRESH_BINARY)[1]

# 识别
text = pytesseract.image_to_string(image, config=config)
```

### 截图区域

- **位置**: 屏幕顶部
- **高度**: 约 100 像素
- **宽度**: 约 900 像素
- **刷新率**: 10-20 FPS (50-100ms 间隔)

## 🚀 扩展开发

### 添加新数据字段

1. 编辑对应的模块文件 (如 `Modules/Player.lua`)
2. 在 `GetData()` 函数中添加新字段
3. 更新格式字符串
4. 无需修改其他文件

示例:添加移动速度:
```lua
-- Modules/Player.lua
function PlayerModule:GetData()
    -- ... 现有代码 ...

    local speed = GetUnitSpeed(C.unitPlayer) or 0

    return string.format(
        "L1:HP=%s/... SPD=%s",
        -- ... 现有字段 ...
        U.ToHexFloat(speed, 4)
    )
end
```

### 创建新模块

1. 在 `Modules/` 下创建新文件 `MyModule.lua`
2. 实现 `GetData()` 函数
3. 调用 `DataToText:RegisterModule("MyModule", MyModule)`
4. 在 `DataToText.toc` 中添加文件引用
5. 在 `Core.lua` 的 `UpdateDisplay()` 中添加新行

## ⚙️ 配置文件

插件配置保存在 `WTF/Account/YOUR_ACCOUNT/SavedVariables/DataToText.lua`:

```lua
DataToTextDB = {
    enabled = true,
    posX = 0,
    posY = -5,
}
```

## 🐛 故障排除

### 字体未加载
- 检查 `Fonts/Tiny-Bold.ttf` 文件是否存在
- 插件会自动回退到系统字体,但识别率会降低

### 数据不更新
- 输入 `/reload` 重载 UI
- 检查插件是否启用: `/dtt`

### 窗口位置错误
- 输入 `/dtt reset` 重置位置

## 📄 许可证

本插件为 WowClassicGrindBot 项目的一部分,使用相同的许可证。

## 🔗 相关资源

- 原项目: WowClassicGrindBot
- DataToColor (像素方案): `Addons/DataToColor`
- Tesseract OCR: https://github.com/tesseract-ocr/tesseract
