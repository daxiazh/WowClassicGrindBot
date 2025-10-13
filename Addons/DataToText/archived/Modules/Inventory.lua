----------------------------------------------------------------------------
--  DataToText - Inventory Module
--  Provides: Bag free slots, Equipment durability, Selected items
----------------------------------------------------------------------------

local DataToText = _G["DataToText"]
local C = DataToText.C
local U = DataToText.U

local InventoryModule = {}

-- Rotating item display (to avoid overwhelming OCR)
local itemRotationIndex = 0
local itemRotationTimer = 0
local ITEM_ROTATION_INTERVAL = 1.0 -- Rotate every 1 second

function InventoryModule:GetData()
    -- Bag free slots (bags 0-4)
    local bag0Free, bag0Type = DataToText.GetContainerNumFreeSlots(0)
    local bag1Free, bag1Type = DataToText.GetContainerNumFreeSlots(1)
    local bag2Free, bag2Type = DataToText.GetContainerNumFreeSlots(2)
    local bag3Free, bag3Type = DataToText.GetContainerNumFreeSlots(3)
    local bag4Free, bag4Type = DataToText.GetContainerNumFreeSlots(4)

    -- Encode bag data: Free/Total (each bag)
    local bag0Total = DataToText.GetContainerNumSlots(0)
    local bag1Total = DataToText.GetContainerNumSlots(1)
    local bag2Total = DataToText.GetContainerNumSlots(2)
    local bag3Total = DataToText.GetContainerNumSlots(3)
    local bag4Total = DataToText.GetContainerNumSlots(4)

    -- Equipment durability (average %)
    local durability = self:GetAverageDurability()

    -- Rotate through important equipment slots
    itemRotationTimer = itemRotationTimer + C.UPDATE_INTERVAL
    if itemRotationTimer >= ITEM_ROTATION_INTERVAL then
        itemRotationTimer = 0
        itemRotationIndex = math.mod(itemRotationIndex + 1, 5) -- Rotate through 5 slots
    end

    local equipSlot = 0
    local equipItemId = 0

    -- Important equipment slots: 16=MainHand, 17=OffHand, 5=Chest, 1=Head, 8=Feet
    local importantSlots = {16, 17, 5, 1, 8}
    local slotId = importantSlots[itemRotationIndex + 1]

    if slotId then
        local itemLink = GetInventoryItemLink(C.unitPlayer, slotId)
        if itemLink then
            equipSlot = slotId
            equipItemId = tonumber(string.match(itemLink, "item:(%d+)")) or 0
        end
    end

    -- Format: L4:B0=XX/XX B1=XX/XX B2=XX/XX B3=XX/XX B4=XX/XX DUR=XX EQ=XX/XXXXXXXX
    return string.format(
        "L4:B0=%s/%s B1=%s/%s B2=%s/%s B3=%s/%s B4=%s/%s DUR=%s EQ=%s/%s",
        U.ToHex(bag0Free, 2),
        U.ToHex(bag0Total, 2),
        U.ToHex(bag1Free, 2),
        U.ToHex(bag1Total, 2),
        U.ToHex(bag2Free, 2),
        U.ToHex(bag2Total, 2),
        U.ToHex(bag3Free, 2),
        U.ToHex(bag3Total, 2),
        U.ToHex(bag4Free, 2),
        U.ToHex(bag4Total, 2),
        U.ToHex(durability, 2),
        U.ToHex(equipSlot, 2),
        U.ToHex(equipItemId, 8)
    )
end

-- Calculate average equipment durability
function InventoryModule:GetAverageDurability()
    local total = 0
    local count = 0

    -- Check all equipment slots
    for slot = 1, 18 do
        local hasDurability, current, maximum = GetInventoryItemDurability(slot)
        if hasDurability and maximum and maximum > 0 then
            total = total + (current / maximum)
            count = count + 1
        end
    end

    if count == 0 then
        return 100 -- No items with durability = 100%
    end

    return math.floor((total / count) * 100)
end

-- Register module
DataToText:RegisterModule("Inventory", InventoryModule)
