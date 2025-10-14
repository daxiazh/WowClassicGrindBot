----------------------------------------------------------------------------
-- GridEncoder.lua
-- 网格编码器模块 - 将 108 个 24-bit 字段编码为 65×65 黑白网格
--
-- 注意:
-- - Lua 5.1 兼容（使用 bit 库）
-- - 网格格式: 元数据(32 bits) + 数据(2592 bits) + CRC32(32 bits) + 纠错码(~400 bits)
-- - 总容量: 3056 bits (382 bytes)
----------------------------------------------------------------------------

-- 创建命名空间
DataToTextGridEncoder = {}
local GE = DataToTextGridEncoder

-- 常量定义
local GRID_SIZE = 65
local CORNER_MARKER_SIZE = 3
local BITS_PER_FIELD = 24
local TOTAL_FIELDS = 108
local VERSION = 1

-- 预缓存函数引用（性能优化）
local band = bit.band
local rshift = bit.rshift
local lshift = bit.lshift
local bxor = bit.bxor
local floor = math.floor
local ceil = math.ceil
local getn = table.getn
local strchar = string.char
local unpack = unpack

----------------------------------------------------------------------------
-- 辅助函数
----------------------------------------------------------------------------

-- 将 24-bit 值拆分为 3 个字节
local function Split24Bit(value)
    local b1 = band(rshift(value, 16), 255)
    local b2 = band(rshift(value, 8), 255)
    local b3 = band(value, 255)
    return b1, b2, b3
end

-- 将字节数组转为位数组
local function BytesToBits(bytes)
    local bits = {}
    local idx = 1
    local byteCount = getn(bytes)

    for i = 1, byteCount do
        local byte = bytes[i]
        -- 展开循环以提高性能，使用预缓存的函数
        bits[idx] = band(rshift(byte, 7), 1)
        bits[idx + 1] = band(rshift(byte, 6), 1)
        bits[idx + 2] = band(rshift(byte, 5), 1)
        bits[idx + 3] = band(rshift(byte, 4), 1)
        bits[idx + 4] = band(rshift(byte, 3), 1)
        bits[idx + 5] = band(rshift(byte, 2), 1)
        bits[idx + 6] = band(rshift(byte, 1), 1)
        bits[idx + 7] = band(byte, 1)
        idx = idx + 8
    end

    return bits
end

-- 检查是否在角标记区域（优化版本）
local CORNER_MAX_ROW = CORNER_MARKER_SIZE
local CORNER_MIN_ROW = GRID_SIZE - CORNER_MARKER_SIZE + 1
local CORNER_MAX_COL = CORNER_MARKER_SIZE
local CORNER_MIN_COL = GRID_SIZE - CORNER_MARKER_SIZE + 1

local function InCorner(row, col)
    -- 上半部分（前3行）
    if row <= CORNER_MAX_ROW then
        return col <= CORNER_MAX_COL or col >= CORNER_MIN_COL
    end
    -- 下半部分（后3行）
    if row >= CORNER_MIN_ROW then
        return col <= CORNER_MAX_COL or col >= CORNER_MIN_COL
    end
    return false
end

----------------------------------------------------------------------------
-- 数据打包
----------------------------------------------------------------------------

-- 将字段打包为字节数组
function GE.PackFieldsToBytes(fields)
    local bytes = {}
    local idx = 1

    -- === 元数据区 (32 bits = 4 bytes) ===
    bytes[idx] = VERSION
    idx = idx + 1
    bytes[idx] = TOTAL_FIELDS
    idx = idx + 1
    bytes[idx] = 0
    idx = idx + 1
    bytes[idx] = 0
    idx = idx + 1

    -- === 数据区 (2592 bits = 324 bytes) ===

    -- 将每个字段 (24 bits) 拆分为 3 个字节
    for i = 0, TOTAL_FIELDS - 1 do
        local value = fields[i] or 0
        local b1, b2, b3 = Split24Bit(value)
        bytes[idx] = b1
        bytes[idx + 1] = b2
        bytes[idx + 2] = b3
        idx = idx + 3
    end

    -- === CRC32 校验 (32 bits = 4 bytes) ===

    -- 将字节数组转换为字符串（用于CRC32计算）
    -- 分4批处理,每批82字节,使用预缓存的函数
    local totalBytes = idx - 1
    local byteString = strchar(
        unpack(bytes, 1, 82)
    ) .. strchar(
        unpack(bytes, 83, 164)
    ) .. strchar(
        unpack(bytes, 165, 246)
    ) .. strchar(
        unpack(bytes, 247, totalBytes)
    )

    -- 计算前面所有数据的 CRC32
    local crc32 = DataToTextUtils.CalculateCRC32(byteString)

    -- 拆分为 4 个字节（Big-endian），使用预缓存的函数
    bytes[idx] = band(rshift(crc32, 24), 255)
    bytes[idx + 1] = band(rshift(crc32, 16), 255)
    bytes[idx + 2] = band(rshift(crc32, 8), 255)
    bytes[idx + 3] = band(crc32, 255)

    return bytes
end

----------------------------------------------------------------------------
-- 网格生成
----------------------------------------------------------------------------

-- 创建空网格
function GE.CreateEmptyGrid()
    local grid = {}
    for row = 1, GRID_SIZE do
        grid[row] = {}
        for col = 1, GRID_SIZE do
            grid[row][col] = 0  -- 0 = 白色
        end
    end
    return grid
end

-- 添加角标记
function GE.AddCornerMarkers(grid)
    local size = CORNER_MARKER_SIZE
    local max = GRID_SIZE

    -- 3×3 角标记图案（中心白色，周围黑色）
    local function AddMarker(startRow, startCol)
        for row = 0, size - 1 do
            for col = 0, size - 1 do
                local r = startRow + row
                local c = startCol + col
                -- 中心白色，周围黑色
                if row == 1 and col == 1 then
                    grid[r][c] = 0  -- 白色
                else
                    grid[r][c] = 1  -- 黑色
                end
            end
        end
    end

    -- 左上角
    AddMarker(1, 1)

    -- 右上角
    AddMarker(1, max - size + 1)

    -- 左下角
    AddMarker(max - size + 1, 1)

    -- 右下角
    AddMarker(max - size + 1, max - size + 1)
end

-- 填充数据到网格
function GE.FillDataToGrid(grid, bits)
    local bitIndex = 1
    local totalBits = getn(bits)

    -- 逐行逐列填充（跳过角标记）
    for row = 1, GRID_SIZE do
        for col = 1, GRID_SIZE do
            if not InCorner(row, col) then
                if bitIndex <= totalBits then
                    grid[row][col] = bits[bitIndex]
                    bitIndex = bitIndex + 1
                else
                    -- 数据填充完毕，剩余区域保持白色
                    break
                end
            end
        end
    end

    return bitIndex - 1  -- 返回实际使用的位数
end

----------------------------------------------------------------------------
-- 主编码函数
----------------------------------------------------------------------------

-- 将字段编码为网格
function GE.EncodeToGrid(fields)
    -- 1. 打包字段为字节数组（带 CRC32）
    local bytes = GE.PackFieldsToBytes(fields)

    -- 2. 字节转位
    local bits = BytesToBits(bytes)

    -- 3. 创建空网格
    local grid = GE.CreateEmptyGrid()

    -- 4. 添加角标记
    GE.AddCornerMarkers(grid)

    -- 5. 填充数据
    local usedBits = GE.FillDataToGrid(grid, bits)

    -- 返回网格和统计信息
    return grid, {
        totalFields = TOTAL_FIELDS,
        totalBytes = getn(bytes),
        totalBits = getn(bits),
        usedBits = usedBits,
        gridSize = GRID_SIZE,
        capacity = GRID_SIZE * GRID_SIZE - 4 * CORNER_MARKER_SIZE * CORNER_MARKER_SIZE
    }
end

-- 获取网格容量信息
function GE.GetCapacityInfo()
    local totalCells = GRID_SIZE * GRID_SIZE
    local markerCells = 4 * CORNER_MARKER_SIZE * CORNER_MARKER_SIZE
    local dataCells = totalCells - markerCells

    local metadataBits = 32
    local fieldBits = TOTAL_FIELDS * BITS_PER_FIELD
    local crc32Bits = 32
    local usedBits = metadataBits + fieldBits + crc32Bits

    return {
        gridSize = GRID_SIZE,
        totalCells = totalCells,
        markerCells = markerCells,
        dataCells = dataCells,
        capacityBits = dataCells,
        capacityBytes = math.floor(dataCells / 8),

        metadataBits = metadataBits,
        fieldBits = fieldBits,
        crc32Bits = crc32Bits,
        usedBits = usedBits,
        usedBytes = math.ceil(usedBits / 8),

        remainingBits = dataCells - usedBits,
        remainingBytes = math.floor((dataCells - usedBits) / 8),

        utilizationPercent = usedBits / dataCells * 100
    }
end
