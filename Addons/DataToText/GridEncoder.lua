----------------------------------------------------------------------------
-- GridEncoder.lua
-- 网格编码器模块 - 将 108 个 24-bit 字段编码为 65×65 黑白网格
--
-- 注意:
-- - Lua 5.1 兼容（使用 bit 库）
-- - 网格格式: 元数据(32 bits) + 数据(2592 bits) + CRC32(32 bits) + 纠错码(~400 bits)
-- - 总容量: 3056 bits (382 bytes)
--
-- VERSION: 2.1.0 (2025-10-17)
-- - 修复 CRC32 字节序 (Big-endian)
-- - 7×7 Finder Pattern + 1格静区
-- - 网格大小计算修复 (+8)
----------------------------------------------------------------------------

-- 创建命名空间
DataToTextGridEncoder = {}
local GE = DataToTextGridEncoder

-- 模块版本号
GE.VERSION_STRING = "2.1.0"

-- ============ 调试开关 ============
-- 改为 true 启用详细调试信息输出
local DEBUG_MODE = false
GE.DEBUG_MODE = DEBUG_MODE  -- 导出供其他模块使用

-- 常量定义
local GRID_SIZE = 65
local CORNER_MARKER_SIZE = 7  -- QR码 Finder Pattern 尺寸
local QUIET_ZONE_SIZE = 1     -- Finder Pattern 周围的静区宽度
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

-- 检查是否在角标记区域（包含静区）
local CORNER_TOTAL_SIZE = CORNER_MARKER_SIZE + QUIET_ZONE_SIZE  -- 7 + 1 = 8
local CORNER_FINDER_RIGHT_COL = GRID_SIZE - CORNER_MARKER_SIZE + 1
local CORNER_FINDER_BOTTOM_ROW = GRID_SIZE - CORNER_MARKER_SIZE + 1
local CORNER_MAX_ROW = CORNER_TOTAL_SIZE
local CORNER_MIN_ROW = GRID_SIZE - CORNER_TOTAL_SIZE + 1
local CORNER_MAX_COL = CORNER_TOTAL_SIZE
local CORNER_MIN_COL = GRID_SIZE - CORNER_TOTAL_SIZE + 1

-- Timing Pattern \u5e38\u91cf
local TIMING_PATTERN_ROW = 3  -- Timing Pattern \u6240\u5728\u884c\uff08Finder\u4e2d\u5fc3\u884c\uff09
local TIMING_PATTERN_COL = 4  -- Timing Pattern \u6240\u5728\u5217\uff08Finder\u4e2d\u5fc3\u5217\uff09
local TIMING_START = CORNER_TOTAL_SIZE + 1          -- 9 (\u5de6\u4fa7Finder\u540e\u7b2c\u4e00\u4e2acell)
local TIMING_END = GRID_SIZE - CORNER_TOTAL_SIZE    -- 57 (\u53f3\u4fa7Finder\u524d\u6700\u540e\u4e00\u4e2acell)

local function InCorner(row, col)
    -- 需要跳过的区域包括：7×7 Finder Pattern + 定向静区
    -- 角标记是 8×8 区域（7×7 Finder + 1格面向数据区的静区）
    -- 
    -- 左上角：Finder(1-7,1-7) + 静区(第8行,第8列)
    -- 占据区域：1-8行, 1-8列
    if row <= CORNER_MAX_ROW and col <= CORNER_MAX_COL then
        return true
    end
    
    -- 右上角：Finder(1-7,58-64) + 静区(第8行, 第57列)
    -- 占据区域：1-8行, 57-65列（包含左侧静区第57列）
    if row <= CORNER_MAX_ROW and col >= CORNER_MIN_COL - 1 then
        return true
    end
    
    -- 左下角：Finder(58-64,1-7) + 静区(第57行, 第8列)
    -- 占据区域：57-65行（包含上侧静区第57行）, 1-8列
    if row >= CORNER_MIN_ROW - 1 and col <= CORNER_MAX_COL then
        return true
    end
    
    -- 右下角：Finder(58-64,58-64) + 静区(第57行, 第57列)
    -- 占据区域：57-65行, 57-65列
    if row >= CORNER_MIN_ROW - 1 and col >= CORNER_MIN_COL - 1 then
        return true
    end
    
    return false
end

--- 判断是否是 Timing Pattern 位置
local function IsTimingPattern(row, col)
    -- 水平 Timing Pattern：第4行，从第9列到第57列
    if row == TIMING_PATTERN_ROW and col >= TIMING_START and col <= TIMING_END then
        return true
    end
    
    -- 垂直 Timing Pattern：第4列，从第9行到第57行
    if col == TIMING_PATTERN_COL and row >= TIMING_START and row <= TIMING_END then
        return true
    end
    
    return false
end

--- 添加 Timing Patterns 到网格
local function AddTimingPatterns(grid)
    -- 水平 Timing Pattern（第4行）：黑白交替
    for col = TIMING_START, TIMING_END do
        local offset = col - TIMING_START
        if math.fmod(offset, 2) == 0 then
            grid[TIMING_PATTERN_ROW][col] = 1  -- 黑色
        else
            grid[TIMING_PATTERN_ROW][col] = 0  -- 白色
        end
    end
    
    -- 垂直 Timing Pattern（第4列）：黑白交替
    for row = TIMING_START, TIMING_END do
        local offset = row - TIMING_START
        if math.fmod(offset, 2) == 0 then
            grid[row][TIMING_PATTERN_COL] = 1  -- 黑色
        else
            grid[row][TIMING_PATTERN_COL] = 0  -- 白色
        end
    end
    
    -- 注意：grid[4][4] 是左上 Finder 的中心黑点
    -- Timing Pattern 会覆盖它，但由于 Finder 中心本身就是黑色，
    -- Timing Pattern 从黑色开始，所以不影响
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
    -- 注意: idx 此时指向下一个空位,所以实际字节数是 idx - 1 = 328
    local totalBytes = idx - 1
    
    -- 使用简单的逐字节拼接,避免 string.char(unpack()) 的参数限制
    local byteString = ""
    for i = 1, totalBytes do
        byteString = byteString .. strchar(bytes[i])
    end

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

-- 添加角标记 - QR码 Finder Pattern (7×7) + 定向静区 (1格)
-- 静区只在朝向数据区域的方向上存在
function GE.AddCornerMarkers(grid)
    local finderSize = CORNER_MARKER_SIZE  -- 7
    local max = GRID_SIZE  -- 65

    -- 7×7 QR码定位图形 (Finder Pattern) + 定向静区
    -- 比例: 1:1:3:1:1 的嵌套正方形
    -- 
    -- 左上角布局:          右上角布局:
    -- █ █ █ █ █ █ █ ░     ░ █ █ █ █ █ █ █
    -- █ ░ ░ ░ ░ ░ █ ░     ░ █ ░ ░ ░ ░ ░ █
    -- █ ░ █ █ █ ░ █ ░     ░ █ ░ █ █ █ ░ █
    -- █ ░ █ █ █ ░ █ ░     ░ █ ░ █ █ █ ░ █
    -- █ ░ █ █ █ ░ █ ░     ░ █ ░ █ █ █ ░ █
    -- █ ░ ░ ░ ░ ░ █ ░     ░ █ ░ ░ ░ ░ ░ █
    -- █ █ █ █ █ █ █ ░     ░ █ █ █ █ █ █ █
    -- ░ ░ ░ ░ ░ ░ ░ ░     ░ ░ ░ ░ ░ ░ ░ ░ 
    
    -- 通用 Finder Pattern 绘制函数
    local function DrawFinderPattern(startRow, startCol)
        -- 外层黑框 (7×7)
        for i = 0, 6 do
            grid[startRow + i][startCol] = 1         -- 左边
            grid[startRow + i][startCol + 6] = 1     -- 右边
            grid[startRow][startCol + i] = 1         -- 上边
            grid[startRow + 6][startCol + i] = 1     -- 下边
        end
        
        -- 内层白框 (5×5区域设为白色)
        for i = 1, 5 do
            for j = 1, 5 do
                grid[startRow + i][startCol + j] = 0
            end
        end
        
        -- 中心黑块 (3×3)
        for i = 2, 4 do
            for j = 2, 4 do
                grid[startRow + i][startCol + j] = 1
            end
        end
    end
    
    -- 通用静区填充函数
    local function FillQuietZone(startRow, startCol, endRow, endCol)
        for r = startRow, endRow do
            for c = startCol, endCol do
                if r >= 1 and r <= max and c >= 1 and c <= max then
                    grid[r][c] = 0  -- 白色
                end
            end
        end
    end
        
    -- 左上角：Finder(1-7,1-7) + 右静区(1-8,8) + 下静区(8,1-8)
    DrawFinderPattern(1, 1)    
    FillQuietZone(1, CORNER_TOTAL_SIZE, CORNER_TOTAL_SIZE, CORNER_TOTAL_SIZE)
    FillQuietZone(CORNER_TOTAL_SIZE, 1, CORNER_TOTAL_SIZE, CORNER_TOTAL_SIZE)
    
    local finderRightCol = GRID_SIZE - CORNER_MARKER_SIZE + 1;
    -- 右上角：Finder(1-7,59-65) + 左静区(1-8,58) + 下静区(8,58-65)
    DrawFinderPattern(1, finderRightCol) -- 59 - 65 列
    local topRightFinderCol = GRID_SIZE - CORNER_TOTAL_SIZE + 1
    FillQuietZone(1, topRightFinderCol, CORNER_TOTAL_SIZE, topRightFinderCol)
    FillQuietZone(CORNER_TOTAL_SIZE, topRightFinderCol, CORNER_TOTAL_SIZE, GRID_SIZE)
    
   -- 左下角：Finder(59-65,1-7) + 上静区(58,1-8) + 右静区(59-65,8)
   local bottomFinderRow = GRID_SIZE - CORNER_MARKER_SIZE + 1  -- 59
   DrawFinderPattern(bottomFinderRow, 1)
   local bottomQuietRow = GRID_SIZE - CORNER_TOTAL_SIZE + 1    -- 58
   FillQuietZone(bottomQuietRow, 1, bottomQuietRow, CORNER_TOTAL_SIZE)
   FillQuietZone(bottomFinderRow, CORNER_TOTAL_SIZE, GRID_SIZE, CORNER_TOTAL_SIZE)
   
   -- 右下角：Finder(59-65,59-65) + 上静区(58,58-65) + 左静区(59-65,58)
   DrawFinderPattern(bottomFinderRow, finderRightCol)
   FillQuietZone(bottomQuietRow, topRightFinderCol, bottomQuietRow, GRID_SIZE)
   FillQuietZone(bottomQuietRow, topRightFinderCol, GRID_SIZE, topRightFinderCol)

end

-- 填充数据到网格
function GE.FillDataToGrid(grid, bits)
    local bitIndex = 1
    local totalBits = getn(bits)

    -- 逐行逐列填充（跳过角标记和Timing Pattern）
    for row = 1, GRID_SIZE do
        for col = 1, GRID_SIZE do
            if not InCorner(row, col) and not IsTimingPattern(row, col) then
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
    
    -- 5. 添加 Timing Patterns
    AddTimingPatterns(grid)

    -- 6. 填充数据
    local usedBits = GE.FillDataToGrid(grid, bits)

    -- 返回网格和统计信息
    return grid, {
        totalFields = TOTAL_FIELDS,
        totalBytes = getn(bytes),
        totalBits = getn(bits),
        usedBits = usedBits,
        gridSize = GRID_SIZE,
        capacity = GRID_SIZE * GRID_SIZE - 4 * CORNER_TOTAL_SIZE * CORNER_TOTAL_SIZE - (2 * (TIMING_END - TIMING_START + 1) - 1)
    }
end

-- 获取网格容量信息
function GE.GetCapacityInfo()
    local totalCells = GRID_SIZE * GRID_SIZE
    local markerCells = 4 * CORNER_TOTAL_SIZE * CORNER_TOTAL_SIZE  -- 包含静区
    local timingPatternCells = 2 * (TIMING_END - TIMING_START + 1) - 1  -- 两条Timing Pattern，减去重复的交点
    local dataCells = totalCells - markerCells - timingPatternCells

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

-- 将字节数组转换为十六进制转储字符串
-- 格式: 每行16字节, 用 - 分隔, 类似 C# BitConverter.ToString() 输出
function GE.BytesToHexDump(bytes)
    local lines = {}
    local totalBytes = table.getn(bytes)
    local bytesPerLine = 16
    
    for i = 1, totalBytes, bytesPerLine do
        local lineBytes = {}
        local endIdx = math.min(i + bytesPerLine - 1, totalBytes)
        
        for j = i, endIdx do
            table.insert(lineBytes, string.format("%02X", bytes[j]))
        end
        
        table.insert(lines, table.concat(lineBytes, "-"))
    end
    
    return table.concat(lines, "\n")
end
