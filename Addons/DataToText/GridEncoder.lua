----------------------------------------------------------------------------
--  GridEncoder.lua - 黑白网格编码器
--  将游戏数据编码为黑白网格，完全避免颜色gamma问题
----------------------------------------------------------------------------

-- 创建命名空间
DataToTextGridEncoder = DataToTextGridEncoder or {}
local GridEncoder = DataToTextGridEncoder

-- 引用工具函数
local U = DataToTextUtils

-- 常量定义
local GRID_SIZE = 65           -- 网格尺寸 65x65
local BITS_PER_FIELD = 24      -- 每个字段24位
local TOTAL_FIELDS = 108       -- DataToColor的108个字段
local ERROR_CORRECTION = 0.15  -- 15%纠错率

-- 定位标记尺寸
local CORNER_MARKER_SIZE = 3   -- 3x3的角标记

----------------------------------------------------------------------------
-- CRC32 计算（使用Utils.lua中的函数）
----------------------------------------------------------------------------

-- 引用Utils中的CRC32函数
GridEncoder.CalculateCRC32 = U.CalculateCRC32

----------------------------------------------------------------------------
-- 位操作辅助函数
----------------------------------------------------------------------------

-- 将数字转为位数组（高位在前）
function GridEncoder:NumberToBits(num, bitCount)
    local bits = {}
    for i = bitCount - 1, 0, -1 do
        local bit = bit.band(bit.rshift(num, i), 1)
        table.insert(bits, bit)
    end
    return bits
end

-- 将位数组转为数字
function GridEncoder:BitsToNumber(bits, startIdx, length)
    local num = 0
    for i = 0, length - 1 do
        local bit = bits[startIdx + i] or 0
        num = bit.bor(num, bit.lshift(bit, length - 1 - i))
    end
    return num
end

----------------------------------------------------------------------------
-- 数据打包 - 将所有字段打包为位流
----------------------------------------------------------------------------

-- 定义字段索引（对应DataToColor的108个字段）
GridEncoder.FIELD_INDICES = {
    -- 基础信息 (0-9)
    INIT_FLAG = 0,
    P_X = 1,
    P_Y = 2,
    P_FACING = 3,
    P_MAP = 4,
    P_LEVEL = 5,
    CORPSE_X = 6,
    CORPSE_Y = 7,
    BITS1 = 8,
    BITS2 = 9,

    -- 玩家属性 (10-15)
    P_HP_MAX = 10,
    P_HP = 11,
    P_POWER_MAX = 12,
    P_POWER = 13,
    P_MANA_MAX = 14,
    P_MANA = 15,

    -- 目标信息 (16-19)
    T_NAME_1 = 16,
    T_NAME_2 = 17,
    T_HP_MAX = 18,
    T_HP = 19,

    -- 背包系统 (20-24)
    BAG_INFO = 20,
    BAG_SLOT = 21,
    BAG_ITEM_ID = 22,
    EQUIP_SLOT = 23,
    EQUIP_ITEM_ID = 24,

    -- 动作条状态 (25-37)
    ACTION_CURRENT_1 = 25,
    ACTION_CURRENT_2 = 26,
    ACTION_CURRENT_3 = 27,
    ACTION_CURRENT_4 = 28,
    ACTION_CURRENT_5 = 29,
    ACTION_USABLE_1 = 30,
    ACTION_USABLE_2 = 31,
    ACTION_USABLE_3 = 32,
    ACTION_USABLE_4 = 33,
    ACTION_USABLE_5 = 34,
    ACTION_COST_META = 35,
    ACTION_COST_VALUE = 36,
    ACTION_COOLDOWN = 37,

    -- 宠物信息 (38-39)
    PET_HP_MAX = 38,
    PET_HP = 39,

    -- 技能和法术 (40)
    SPELL_RANGE = 40,

    -- Buff/Debuff (41-42)
    PLAYER_BUFF_MASK = 41,
    TARGET_DEBUFF_MASK = 42,

    -- 目标扩展信息 (43)
    T_LEVEL_CLASS = 43,

    -- 背包金钱 (44-45)
    MONEY_COPPER = 44,
    MONEY_GOLD = 45,

    -- 职业和客户端信息 (46)
    RACE_CLASS_CLIENT = 46,

    -- UI错误 (47)
    UI_ERROR_TIME = 47,

    -- 变形形态 (48)
    SHAPESHIFT = 48,

    -- 距离范围 (49)
    DISTANCE_RANGE = 49,

    -- 经验值 (50-51)
    P_XP = 50,
    P_XP_MAX = 51,

    -- UI错误消息 (52)
    UI_ERROR_MSG = 52,

    -- 玩家施法 (53)
    P_CASTING_SPELL = 53,

    -- 装备和连击点 (54)
    DURABILITY_COMBO = 54,

    -- Buff/Debuff计数 (55)
    BUFF_DEBUFF_COUNT = 55,

    -- 目标NPC信息 (56-59)
    T_NPC_ID = 56,
    T_GUID = 57,
    T_CASTING_SPELL = 58,
    T_MOUSEOVER_TARGETTARGET = 59,

    -- 战斗时间记录 (60-63)
    LAST_AUTO_SHOT = 60,
    LAST_MELEE = 61,
    LAST_CAST_EVENT = 62,
    LAST_CAST_SPELL = 63,

    -- 战斗日志队列 (64-67)
    DAMAGE_DEALT = 64,
    DAMAGE_TAKEN = 65,
    MOB_DEATH = 66,
    MISS_TYPE = 67,

    -- 宠物GUID (68-69)
    PET_GUID = 68,
    PET_TARGET_GUID = 69,

    -- 施法次数 (70)
    CAST_COUNT = 70,

    -- 法术书和天赋队列 (71-72)
    SPELLBOOK_QUEUE = 71,
    TALENT_QUEUE = 72,

    -- Gossip对话 (73)
    GOSSIP_QUEUE = 73,

    -- 自定义触发器 (74)
    CUSTOM_TRIGGER = 74,

    -- 近战攻击速度 (75)
    MELEE_ATTACK_SPEED = 75,

    -- 剩余施法时间 (76)
    CAST_TIME_REMAIN = 76,

    -- 焦点目标 (77-78)
    FOCUS_GUID = 77,
    FOCUS_TARGET_GUID = 78,

    -- 玩家Buff详情 (79-80)
    PLAYER_BUFF_TEX = 79,
    PLAYER_BUFF_DURATION = 80,

    -- 目标Debuff详情 (81-82)
    TARGET_DEBUFF_TEX = 81,
    TARGET_DEBUFF_DURATION = 82,

    -- 目标Buff详情 (83-84)
    TARGET_BUFF_TEX = 83,
    TARGET_BUFF_DURATION = 84,

    -- 鼠标悬停信息 (85-87)
    MOUSEOVER_LEVEL_CLASS = 85,
    MOUSEOVER_NPC_ID = 86,
    MOUSEOVER_GUID = 87,

    -- 远程伤害 (88)
    RANGED_DAMAGE = 88,

    -- 焦点生命值 (89-90)
    FOCUS_HP_MAX = 89,
    FOCUS_HP = 90,

    -- 焦点Buff (91-93)
    FOCUS_BUFF_MASK = 91,
    FOCUS_BUFF_TEX = 92,
    FOCUS_BUFF_DURATION = 93,

    -- 最后GCD (94)
    LAST_GCD = 94,

    -- 当前GCD (95)
    CURRENT_GCD = 95,

    -- 法术队列窗口和延迟 (96)
    SPELL_QUEUE_LATENCY = 96,

    -- 拾取信息 (97)
    LOOT_INFO = 97,

    -- 聊天消息 (98-99)
    CHAT_MSG_DATA = 98,
    CHAT_MSG_META = 99,

    -- 布尔标志位3 (100)
    BITS3 = 100,

    -- SoftInteract (101-103)
    SOFT_INTERACT_GUID = 101,
    SOFT_INTERACT_NPC = 102,
    SOFT_INTERACT_TYPE = 103,

    -- 玩家Debuff详情 (104-105)
    PLAYER_DEBUFF_TEX = 104,
    PLAYER_DEBUFF_DURATION = 105,

    -- 全局时间戳 (106)
    GLOBAL_TIME = 106,

    -- 元数据/验证标志 (107)
    METADATA = 107,
}

-- 打包所有字段为位流
function GridEncoder:PackFieldsToBits(fieldData)
    local bitStream = {}

    -- 1. 添加版本和元数据 (32 bits)
    local version = 1
    local metadata = bit.bor(bit.lshift(version, 24), bit.lshift(TOTAL_FIELDS, 16))
    local metadataBits = self:NumberToBits(metadata, 32)
    for _, bit in ipairs(metadataBits) do
        table.insert(bitStream, bit)
    end

    -- 2. 添加所有字段数据 (108 × 24 = 2592 bits)
    for fieldId = 0, TOTAL_FIELDS - 1 do
        local value = fieldData[fieldId] or 0
        -- 确保值在24位范围内 (0xFFFFFF = 16777215)
        value = bit.band(value, 16777215)
        local fieldBits = self:NumberToBits(value, BITS_PER_FIELD)
        for _, bit in ipairs(fieldBits) do
            table.insert(bitStream, bit)
        end
    end

    -- 3. 计算并添加CRC32校验 (32 bits)
    -- 将位流转为字节数组用于CRC计算
    local dataBytes = {}
    local bitStreamLen = table.getn(bitStream)
    for i = 1, bitStreamLen, 8 do
        local byte = 0
        for j = 0, 7 do
            if bitStream[i + j] == 1 then
                byte = bit.bor(byte, bit.lshift(1, 7 - j))
            end
        end
        table.insert(dataBytes, byte)
    end

    -- 计算CRC32
    local dataString = ""
    for _, byte in ipairs(dataBytes) do
        dataString = dataString .. string.char(byte)
    end
    local crc32 = self.CalculateCRC32(dataString)

    -- 添加CRC32到位流
    local crc32Bits = self:NumberToBits(crc32, 32)
    for _, bit in ipairs(crc32Bits) do
        table.insert(bitStream, bit)
    end

    return bitStream
end

----------------------------------------------------------------------------
-- 纠错码生成
----------------------------------------------------------------------------

-- 简单的行列校验码（作为纠错手段）
function GridEncoder:AddErrorCorrection(bitStream)
    local correctedStream = {}

    -- 复制原始数据
    for _, bit in ipairs(bitStream) do
        table.insert(correctedStream, bit)
    end

    -- 每8位添加1位奇偶校验
    local bitStreamLen = table.getn(bitStream)
    local checkBits = math.ceil(bitStreamLen * ERROR_CORRECTION)
    for i = 1, checkBits do
        -- 简单的XOR校验
        local checkBit = 0
        local startIdx = math.floor((i - 1) * 8 / ERROR_CORRECTION) + 1
        local endIdx = math.min(startIdx + 7, bitStreamLen)
        for j = startIdx, endIdx do
            checkBit = bit.bxor(checkBit, bitStream[j])
        end
        table.insert(correctedStream, checkBit)
    end

    return correctedStream
end

----------------------------------------------------------------------------
-- 网格布局生成
----------------------------------------------------------------------------

-- 创建空网格
function GridEncoder:CreateEmptyGrid()
    local grid = {}
    for i = 1, GRID_SIZE do
        grid[i] = {}
        for j = 1, GRID_SIZE do
            grid[i][j] = 0  -- 0=白色, 1=黑色
        end
    end
    return grid
end

-- 添加定位标记（四个角的3x3图案）
function GridEncoder:AddAlignmentMarkers(grid)
    -- 定位图案: 外围一圈黑色，中心白色
    -- █ █ █
    -- █ ░ █
    -- █ █ █

    local function addCorner(startRow, startCol)
        for i = 0, CORNER_MARKER_SIZE - 1 do
            for j = 0, CORNER_MARKER_SIZE - 1 do
                local row = startRow + i
                local col = startCol + j
                -- 中心点为白色，其他为黑色
                if i == 1 and j == 1 then
                    grid[row][col] = 0  -- 白色
                else
                    grid[row][col] = 1  -- 黑色
                end
            end
        end
    end

    -- 左上角
    addCorner(1, 1)

    -- 右上角
    addCorner(1, GRID_SIZE - CORNER_MARKER_SIZE + 1)

    -- 左下角
    addCorner(GRID_SIZE - CORNER_MARKER_SIZE + 1, 1)

    -- 右下角（可选，用于验证）
    addCorner(GRID_SIZE - CORNER_MARKER_SIZE + 1, GRID_SIZE - CORNER_MARKER_SIZE + 1)
end

-- 填充数据到网格
function GridEncoder:FillDataToGrid(grid, bitStream)
    local bitIndex = 1
    local bitStreamLen = table.getn(bitStream)

    -- 从左到右，从上到下填充
    for row = 1, GRID_SIZE do
        for col = 1, GRID_SIZE do
            -- 跳过定位标记区域
            local inCorner = false

            -- 左上角
            if row <= CORNER_MARKER_SIZE and col <= CORNER_MARKER_SIZE then
                inCorner = true
            end

            -- 右上角
            if row <= CORNER_MARKER_SIZE and col > GRID_SIZE - CORNER_MARKER_SIZE then
                inCorner = true
            end

            -- 左下角
            if row > GRID_SIZE - CORNER_MARKER_SIZE and col <= CORNER_MARKER_SIZE then
                inCorner = true
            end

            -- 右下角
            if row > GRID_SIZE - CORNER_MARKER_SIZE and col > GRID_SIZE - CORNER_MARKER_SIZE then
                inCorner = true
            end

            if not inCorner and bitIndex <= bitStreamLen then
                grid[row][col] = bitStream[bitIndex]
                bitIndex = bitIndex + 1
            end
        end
    end

    return bitIndex - 1  -- 返回实际填充的位数
end

----------------------------------------------------------------------------
-- 主编码函数
----------------------------------------------------------------------------

-- 将游戏数据编码为黑白网格
-- @param fieldData: table, 字段数组 fieldData[0..107] 包含所有字段值
-- @return grid: table, 65x65的二维数组，0=白色，1=黑色
function GridEncoder:EncodeToGrid(fieldData)
    -- 1. 打包字段为位流
    local bitStream = self:PackFieldsToBits(fieldData)

    -- 2. 添加纠错码
    local correctedStream = self:AddErrorCorrection(bitStream)

    -- 3. 创建网格
    local grid = self:CreateEmptyGrid()

    -- 4. 添加定位标记
    self:AddAlignmentMarkers(grid)

    -- 5. 填充数据
    local bitsUsed = self:FillDataToGrid(grid, correctedStream)

    -- 调试信息
    -- print(string.format("[GridEncoder] 编码完成: %d bits, 使用 %d/%d 单元格",
    --     #correctedStream, bitsUsed, GRID_SIZE * GRID_SIZE))

    return grid
end

----------------------------------------------------------------------------
-- 测试和调试函数
----------------------------------------------------------------------------

-- 打印网格（调试用）
function GridEncoder:PrintGrid(grid, maxSize)
    maxSize = maxSize or 20  -- 默认只打印20x20

    print("Grid Preview (" .. maxSize .. "x" .. maxSize .. "):")
    print(string.rep("-", maxSize * 2))

    for row = 1, math.min(maxSize, GRID_SIZE) do
        local line = ""
        for col = 1, math.min(maxSize, GRID_SIZE) do
            line = line .. (grid[row][col] == 1 and "█" or " ")
        end
        print(line)
    end

    print(string.rep("-", maxSize * 2))
end

-- 测试编码器
function GridEncoder:Test()
    print("[GridEncoder] 运行测试...")

    -- 创建测试数据
    local testData = {}
    for i = 0, TOTAL_FIELDS - 1 do
        testData[i] = math.mod(i * 1000, 16777215)  -- 模拟数据 (0xFFFFFF = 16777215)
    end

    -- 编码
    local grid = self:EncodeToGrid(testData)

    -- 统计黑白像素
    local blackCount = 0
    local whiteCount = 0
    for row = 1, GRID_SIZE do
        for col = 1, GRID_SIZE do
            if grid[row][col] == 1 then
                blackCount = blackCount + 1
            else
                whiteCount = whiteCount + 1
            end
        end
    end

    print(string.format("[GridEncoder] 网格尺寸: %dx%d", GRID_SIZE, GRID_SIZE))
    print(string.format("[GridEncoder] 黑色像素: %d (%.1f%%)", blackCount, blackCount / (GRID_SIZE * GRID_SIZE) * 100))
    print(string.format("[GridEncoder] 白色像素: %d (%.1f%%)", whiteCount, whiteCount / (GRID_SIZE * GRID_SIZE) * 100))

    -- 打印预览
    self:PrintGrid(grid, 20)

    print("[GridEncoder] 测试完成!")

    return grid
end

----------------------------------------------------------------------------
-- 导出
----------------------------------------------------------------------------

return GridEncoder
