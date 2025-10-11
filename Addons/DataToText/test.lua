-- Minimal test file
DEFAULT_CHAT_FRAME:AddMessage("DataToText test.lua loaded!")

-- Test basic functionality
local function TestAddon()
    DEFAULT_CHAT_FRAME:AddMessage("|cff00ff00[DataToText Test] Plugin is working!|r")
end

-- Call immediately
TestAddon()

-- Also register a slash command
SLASH_DTTTEST1 = "/dtttest"
SlashCmdList["DTTTEST"] = function()
    DEFAULT_CHAT_FRAME:AddMessage("|cff00ff00[DataToText Test] Slash command works!|r")
end
