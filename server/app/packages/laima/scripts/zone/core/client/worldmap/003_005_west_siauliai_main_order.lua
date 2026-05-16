local M_WEST_SIAULIAI_MAIN_ORDER = {
	{ Id = 1001, Name = "SIAUL_WEST_MEET_TITAS" },
	{ Id = 1002, Name = "SIAUL_WEST_WEST_FOREST" },
	{ Id = 1003, Name = "SIAUL_WEST_DRASIUS1" },
	{ Id = 20127, Name = "SIAUL_WEST_STATUS_TUTO_1" },
	{ Id = 1004, Name = "SIAUL_WEST_DRASIUS2" },
	{ Id = 1014, Name = "SIAUL_WEST_MEET_NAGLIS" },
	{ Id = 1020, Name = "SIAUL_WEST_SOLDIER3" },
	{ Id = 1021, Name = "SIAUL_WEST_HAMING_LEAF" },
	{ Id = 1013, Name = "SIAUL_WEST_KNIGHT" },
	{ Id = 1015, Name = "SIAUL_WEST_LAIMONAS1" },
}

local function M_WEST_SIAULIAI_ORDER_INDEX(questId)
	for i = 1, #M_WEST_SIAULIAI_MAIN_ORDER do
		if M_WEST_SIAULIAI_MAIN_ORDER[i].Id == questId then
			return i
		end
	end

	return nil
end

local function M_WEST_SIAULIAI_GET_QUEST_CLASS(entry)
	local cls = nil

	if GetClassByType ~= nil then
		local ok, result = pcall(function()
			return GetClassByType("QuestProgressCheck", entry.Id)
		end)
		if ok then
			cls = result
		end
	end

	if cls == nil and GetClass ~= nil then
		local ok, result = pcall(function()
			return GetClass("QuestProgressCheck", entry.Name)
		end)
		if ok then
			cls = result
		end
	end

	return cls
end

local function M_WEST_SIAULIAI_QUEST_STATE(entry)
	if SCR_QUEST_CHECK_C == nil or GetMyPCObject == nil then
		return "IMPOSSIBLE"
	end

	local cls = M_WEST_SIAULIAI_GET_QUEST_CLASS(entry)
	if cls == nil then
		return "IMPOSSIBLE"
	end

	local ok, state = pcall(function()
		return SCR_QUEST_CHECK_C(GetMyPCObject(), cls.ClassName)
	end)
	if ok and state ~= nil then
		return state
	end

	return "IMPOSSIBLE"
end

local function M_WEST_SIAULIAI_IS_BLOCKED_QUEST_ID(questId)
	local index = M_WEST_SIAULIAI_ORDER_INDEX(questId)
	if index == nil then
		return false
	end

	for i = 1, index - 1 do
		local state = M_WEST_SIAULIAI_QUEST_STATE(M_WEST_SIAULIAI_MAIN_ORDER[i])
		if state ~= "COMPLETE" then
			return true
		end
	end

	return false
end

local function M_WEST_SIAULIAI_CONTROL_NUMBER(ctrl, getter)
	if ctrl == nil or getter == nil then
		return nil
	end

	local ok, value = pcall(function()
		if getter == "GetValue" then
			return ctrl:GetValue()
		elseif getter == "GetValue2" then
			return ctrl:GetValue2()
		end

		return nil
	end)
	if ok then
		return tonumber(value)
	end

	return nil
end

local function M_WEST_SIAULIAI_CONTROL_TEXT(ctrl)
	local parts = {}

	local okName, name = pcall(function()
		return ctrl:GetName()
	end)
	if okName and name ~= nil then
		parts[#parts + 1] = string.lower(tostring(name))
	end

	local okTooltip, tooltip = pcall(function()
		return ctrl:GetTooltipArg()
	end)
	if okTooltip and tooltip ~= nil then
		parts[#parts + 1] = string.lower(tostring(tooltip))
	end

	local okState, state = pcall(function()
		return ctrl:GetSValue()
	end)
	if okState and state ~= nil then
		parts[#parts + 1] = string.lower(tostring(state))
	end

	return table.concat(parts, " ")
end

local function M_WEST_SIAULIAI_SHOULD_HIDE_CONTROL(ctrl)
	local questId = M_WEST_SIAULIAI_CONTROL_NUMBER(ctrl, "GetValue")
	if questId ~= nil and M_WEST_SIAULIAI_IS_BLOCKED_QUEST_ID(questId) then
		return true
	end

	local text = M_WEST_SIAULIAI_CONTROL_TEXT(ctrl)
	if string.find(text, "siaul_west_sol3", 1, true) ~= nil or
		string.find(text, "siaul_west_soldier3", 1, true) ~= nil or
		string.find(text, "battle commander", 1, true) ~= nil or
		string.find(text, "soldier3", 1, true) ~= nil then
		return M_WEST_SIAULIAI_IS_BLOCKED_QUEST_ID(1020)
	end

	return false
end

local function M_WEST_SIAULIAI_CHILD_COUNT(ctrl)
	local ok, count = pcall(function()
		return ctrl:GetChildCount()
	end)
	if ok and count ~= nil then
		return count
	end

	return 0
end

local function M_WEST_SIAULIAI_CHILD(ctrl, index)
	local ok, child = pcall(function()
		return ctrl:GetChildByIndex(index)
	end)
	if ok then
		return child
	end

	return nil
end

local function M_WEST_SIAULIAI_HIDE_BLOCKED_CONTROLS(parent)
	if parent == nil then
		return
	end

	local count = M_WEST_SIAULIAI_CHILD_COUNT(parent)
	for i = 0, count - 1 do
		local child = M_WEST_SIAULIAI_CHILD(parent, i)
		if child ~= nil then
			if M_WEST_SIAULIAI_SHOULD_HIDE_CONTROL(child) then
				child:ShowWindow(0)
			end

			M_WEST_SIAULIAI_HIDE_BLOCKED_CONTROLS(child)
		end
	end
end

local function M_WEST_SIAULIAI_SUPPRESS_BLOCKED_MAP_STATE(frame, mapName)
	local currentMap = mapName
	if currentMap == nil and session ~= nil and session.GetMapName ~= nil then
		currentMap = session.GetMapName()
	end

	if currentMap ~= "f_siauliai_west" then
		return
	end

	M_WEST_SIAULIAI_HIDE_BLOCKED_CONTROLS(frame)
end

Melia.Hook("UPDATE_MINIMAP", function(original, result, frame)
	M_WEST_SIAULIAI_SUPPRESS_BLOCKED_MAP_STATE(frame, nil)
	return result
end)

Melia.Hook("MAKE_MAP_NPC_ICONS", function(original, result, frame, mapName, mapWidth, mapHeight, offsetX, offsetY)
	M_WEST_SIAULIAI_SUPPRESS_BLOCKED_MAP_STATE(frame, mapName)
	return result
end)
