----------------------------------------------------------------------------
-- GridRenderer.lua
-- 网格渲染器模块 - 使用 FontString 渲染 65×65 黑白网格
--
-- 注意:
-- - 使用 FontString 而非 Texture，减少对象数量
-- - 重用 FontString 对象，避免频繁创建/销毁
-- - 优化字符串拼接，使用 table.concat
----------------------------------------------------------------------------

-- 创建命名空间
DataToTextGridRenderer = {}
local GR = DataToTextGridRenderer

-- 渲染参数
local CHAR_BLOCK = "█"  -- U+2588 实心方块
local FONT_SIZE = 6      -- 字体大小（像素）
local LINE_SPACING = 6.5 -- 行间距（像素）
local COLOR_WHITE = "|cFFFFFFFF"  -- 白色（数据位1）
local COLOR_BLACK = "|cFF000000"  -- 黑色（数据位0）
local COLOR_RESET = "|r"

-- FontString 对象池（重用）
local fontStringPool = {}

-- 字体路径（从外部传入）
local fontPath = nil

----------------------------------------------------------------------------
-- 初始化
----------------------------------------------------------------------------

-- 设置字体路径
function GR.SetFontPath(path)
    fontPath = path
end

-- 设置渲染参数
function GR.SetRenderParams(params)
    if params.fontSize then
        FONT_SIZE = params.fontSize
    end
    if params.lineSpacing then
        LINE_SPACING = params.lineSpacing
    end
end

----------------------------------------------------------------------------
-- 渲染函数
----------------------------------------------------------------------------

-- 渲染网格为 FontString（优化版：重用对象）
function GR.RenderGrid(grid, gridFrame)
    if not gridFrame then
        return false, "GridFrame not found"
    end

    if not fontPath then
        return false, "Font path not set"
    end

    local gridSize = table.getn(grid)

    -- 确保 GridFrame 可见
    gridFrame:Show()

    -- 如果 FontString 对象尚未创建，则创建它们（只创建一次）
    if table.getn(fontStringPool) == 0 then
        for row = 1, gridSize do
            local fontString = gridFrame:CreateFontString(nil, "OVERLAY")
            fontString:SetFont(fontPath, FONT_SIZE, "MONOCHROME")
            fontString:SetJustifyH("LEFT")
            fontString:SetJustifyV("TOP")
            fontString:SetPoint("TOPLEFT", gridFrame, "TOPLEFT", 5, -5 - (row - 1) * LINE_SPACING)
            table.insert(fontStringPool, fontString)
        end
    end

    -- 更新每行的文本内容（重用现有对象 + 优化字符串拼接）
    local rowChars = {}  -- 重用 table 减少内存分配
    for row = 1, gridSize do
        -- 清空 table
        for i = 1, table.getn(rowChars) do
            rowChars[i] = nil
        end

        -- 使用 table 存储字符，避免字符串重复拼接
        local idx = 1
        local rowData = grid[row]
        for col = 1, gridSize do
            -- 为每个字符添加颜色代码（1=黑色，0=白色）
            if rowData[col] == 1 then
                rowChars[idx] = COLOR_BLACK
                rowChars[idx + 1] = CHAR_BLOCK
                rowChars[idx + 2] = COLOR_RESET
            else
                rowChars[idx] = COLOR_WHITE
                rowChars[idx + 1] = CHAR_BLOCK
                rowChars[idx + 2] = COLOR_RESET
            end
            idx = idx + 3
        end

        -- 一次性拼接字符串
        fontStringPool[row]:SetText(table.concat(rowChars))
        fontStringPool[row]:Show()
    end

    return true, "Rendered " .. gridSize .. "x" .. gridSize .. " grid"
end

-- 清除渲染（隐藏所有 FontString）
function GR.ClearGrid()
    for i = 1, table.getn(fontStringPool) do
        if fontStringPool[i] then
            fontStringPool[i]:Hide()
        end
    end
end

-- 重置渲染器（清除对象池）
function GR.Reset()
    GR.ClearGrid()
    fontStringPool = {}
end

-- 获取渲染统计信息
function GR.GetStats()
    return {
        fontStringCount = table.getn(fontStringPool),
        fontSize = FONT_SIZE,
        lineSpacing = LINE_SPACING
    }
end
