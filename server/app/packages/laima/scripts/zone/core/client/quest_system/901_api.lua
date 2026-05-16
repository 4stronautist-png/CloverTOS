local _questList = {}

Melia.Quests = {}

local function M_QUESTS_SAFE_UPDATE_LIST()
	if M_QUESTS_UPDATE_LIST ~= nil then
		M_QUESTS_UPDATE_LIST()
	end
end

local function M_QUESTS_SAFE_DETAILS_UPDATE(quest)
	if M_QUESTS_DETAILS_UPDATE ~= nil then
		M_QUESTS_DETAILS_UPDATE(quest)
	end
end

local function M_QUESTS_SAFE_DETAILS_CLOSE()
	if M_QUESTS_DETAILS_CLOSE ~= nil then
		M_QUESTS_DETAILS_CLOSE()
	end
end

local function M_QUEST_CLASS_ID(quest)
	if quest == nil or quest.ClassId == nil then
		return nil
	end

	if type(quest.ClassId) == "number" then
		return quest.ClassId
	end

	local classId = tostring(quest.ClassId)
	local hex = string.match(classId, "0x(%x+)")
	if hex ~= nil then
		return tonumber(hex, 16)
	end

	return tonumber(classId)
end

Melia.Quests.Add = function(quest)
	Melia.Quests.Restore(quest)
	ui.SysMsg("New Quest: " .. quest.Name)
end

Melia.Quests.Restore = function(quest)
	local questClassId = M_QUEST_CLASS_ID(quest)
	for i = #_questList, 1, -1 do
		local existingQuest = _questList[i]
		if existingQuest.ObjectId == quest.ObjectId or (questClassId ~= nil and M_QUEST_CLASS_ID(existingQuest) == questClassId) then
			table.remove(_questList, i)
		end
	end

	table.insert(_questList, quest)
	M_QUESTS_SAFE_UPDATE_LIST()
end

Melia.Quests.Update = function(quest)
	local existingQuest = Melia.Quests.Get(quest.ObjectId)
	if existingQuest ~= nil then
		existingQuest.Status = quest.Status
		existingQuest.Done = quest.Done
		if quest.Tracked ~= nil then
			existingQuest.Tracked = quest.Tracked
		end
		existingQuest.Objectives = quest.Objectives
		if quest.TrackingPoints ~= nil then
			existingQuest.TrackingPoints = quest.TrackingPoints
		end
		
		M_QUESTS_SAFE_UPDATE_LIST()
		M_QUESTS_SAFE_DETAILS_UPDATE(existingQuest)
	end
end

Melia.Quests.Get = function(questObjectId)
	for i = 1, #_questList do
		local quest = _questList[i]
		if quest.ObjectId == questObjectId then
			return quest
		end
	end

	return nil
end

Melia.Quests.GetAll = function()
	return _questList
end

Melia.Quests.Clear = function()
	_questList = {}
	M_QUESTS_SAFE_UPDATE_LIST()
	M_QUESTS_SAFE_DETAILS_CLOSE()
end

Melia.Quests.Remove = function(questObjectId)
	for i = 1, #_questList do
		local quest = _questList[i]
		if quest.ObjectId == questObjectId then
			table.remove(_questList, i)

			M_QUESTS_SAFE_UPDATE_LIST()
			M_QUESTS_SAFE_DETAILS_CLOSE()
			return
		end
	end
end

Melia.Quests.CountTracked = function()
	local result = 0
	
	for i = 1, #_questList do
		local quest = _questList[i]
		if quest.Tracked then
			result = result + 1
		end
	end

	return result
end

Melia.Quests.RequestComplete = function(questObjectId)
	ui.Chat("/quest complete " .. questObjectId)
end

Melia.Quests.RequestCancel = function(questObjectId)
	ui.Chat("/quest cancel " .. questObjectId)
end

Melia.Quests.RequestTrack = function(questObjectId, enabled)
	ui.Chat("/quest track " .. questObjectId .. " " .. tostring(enabled))
end
