local function M_QUEST_TRACKING_SPLIT(value)
	local result = {}
	if value == nil then
		return result
	end

	for token in string.gmatch(value, "%S+") do
		result[#result + 1] = token
	end

	return result
end

local function M_QUEST_TRACKING_TO_POINTS(point, mapName, mapProp)
	local result = {}
	local tokens = M_QUEST_TRACKING_SPLIT(point.Group)
	local index = 1

	while index <= #tokens do
		local groupMap = tokens[index]
		if groupMap ~= mapName then
			break
		end

		local second = tokens[index + 1]
		local third = tokens[index + 2]
		local fourth = tokens[index + 3]
		local fifth = tokens[index + 4]

		if second == nil or third == nil then
			break
		end

		local x = tonumber(second)
		if x ~= nil then
			local y = tonumber(third) or 0
			local z = tonumber(fourth) or 0
			local range = tonumber(fifth) or 100
			result[#result + 1] = { X = x, Y = y, Z = z, Range = range }
			index = index + 5
		else
			local range = tonumber(third) or 100
			if GET_MONGEN_NPCPOS ~= nil then
				local genList = GET_MONGEN_NPCPOS(mapProp, second)
				if genList ~= nil then
					local genCount = genList:Count()
					for i = 0, genCount - 1 do
						local worldPos = genList:Element(i)
						result[#result + 1] = { X = worldPos.x, Y = worldPos.y or 0, Z = worldPos.z, Range = range }
					end
				end
			end
			index = index + 3
		end
	end

	return result
end

local function M_QUEST_TRACKING_ACTIVE(quest)
	if quest == nil or quest.TrackingPoints == nil then
		return false
	end

	if quest.Status ~= "InProgress" and quest.Status ~= "Success" then
		return false
	end

	if quest.Done == true then
		return false
	end

	return true
end

local function M_DRAW_QUEST_TRACKING_POINT(parent, prefix, quest, idx, point, mapProp, mapWidth, mapHeight, offsetX, offsetY)
	local mapPos = mapProp:WorldPosToMinimapPos(point.X, point.Z, mapWidth, mapHeight)
	local range = point.Range or 100
	local rangeX = range * MINIMAP_LOC_MULTI * mapWidth / WORLD_SIZE
	local rangeY = range * MINIMAP_LOC_MULTI * mapHeight / WORLD_SIZE
	local circleX = offsetX + mapPos.x - rangeX / 2
	local circleY = offsetY + mapPos.y - rangeY / 2
	local circle = parent:CreateOrGetControl("picture", prefix .. "_CIR_" .. quest.ClassId .. "_" .. idx, circleX, circleY, rangeX, rangeY)
	tolua.cast(circle, "ui::CPicture")
	SET_PICTURE_QUESTMAP(circle, 30)

	local iconSize = iconW or 24
	local iconX = offsetX + mapPos.x - iconSize / 2
	local iconY = offsetY + mapPos.y - iconSize / 2
	local icon = parent:CreateOrGetControl("picture", prefix .. "_ICON_" .. quest.ClassId .. "_" .. idx, iconX, iconY, iconSize, iconSize)
	tolua.cast(icon, "ui::CPicture")
	icon:SetImage("questinfo_ing")
	icon:SetEnableStretch(1)
	icon:SetEnable(1)
	icon:ShowWindow(1)
	icon:SetTooltipType("texthelp")
	icon:SetTooltipArg("{@st59}" .. (quest.Name or "") .. "{/}")
end

local function M_DRAW_QUEST_TRACKING(parent, prefix, mapName, mapProp, mapWidth, mapHeight, offsetX, offsetY)
	if Melia == nil or Melia.Quests == nil or Melia.Quests.GetAll == nil then
		return
	end

	local quests = Melia.Quests.GetAll()
	if quests == nil then
		return
	end

	local index = 0
	for _, quest in ipairs(quests) do
		if M_QUEST_TRACKING_ACTIVE(quest) then
			for _, trackingPoint in ipairs(quest.TrackingPoints) do
				local points = M_QUEST_TRACKING_TO_POINTS(trackingPoint, mapName, mapProp)
				for _, point in ipairs(points) do
					index = index + 1
					M_DRAW_QUEST_TRACKING_POINT(parent, prefix, quest, index, point, mapProp, mapWidth, mapHeight, offsetX, offsetY)
				end
			end
		end
	end
end

Melia.Hook("UPDATE_MINIMAP", function(original, result, frame)
	local mapName = session.GetMapName()
	local mapProp = geMapTable.GetMapProp(mapName)
	local npcList = frame:GetChild("npclist")
	local picture = GET_CHILD(frame, "map", "ui::CPicture")
	local cursize = GET_MINIMAPSIZE()
	local mapWidth = picture:GetImageWidth() * (100 + cursize) / 100
	local mapHeight = picture:GetImageHeight() * (100 + cursize) / 100

	M_DRAW_QUEST_TRACKING(npcList, "_M_QUEST_MINIMAP", mapName, mapProp, mapWidth, mapHeight, 0, 0)
	return result
end)

Melia.Hook("MAKE_MAP_NPC_ICONS", function(original, result, frame, mapName, mapWidth, mapHeight, offsetX, offsetY)
	local mapProp = geMapTable.GetMapProp(mapName)
	M_DRAW_QUEST_TRACKING(frame, "_M_QUEST_MAP", mapName, mapProp, mapWidth, mapHeight, offsetX, offsetY)
	return result
end)
