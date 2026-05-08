-- Override the draw equip function, in case we want custom grades.
local function CLOVER_HAIR_RANK_TEXT(hairRank)
	if hairRank == 1 then
		return '{@st41b}{#FF4040}Avançado{/}{/}'
	elseif hairRank == 2 then
		return '{@st41b}{#FFD84A}Lendário{/}{/}'
	elseif hairRank >= 3 then
		return '{@st41b}{#9FE8FF}Goddess{/}{/}'
	end

	return '{@st41b}{#8B5A2B}Comum{/}{/}'
end

local function CLOVER_REPLACE_HAIR_RATING_TEXT(ctrl, rankText)
	if ctrl == nil then
		return
	end

	pcall(function()
		local text = ctrl:GetText()
		if text ~= nil and (string.find(text, 'Rating') ~= nil or string.find(text, '/ 20') ~= nil or string.find(text, '/20') ~= nil) then
			ctrl:SetText(rankText)
			ctrl:ShowWindow(1)
		end
	end)

	local childCount = 0
	pcall(function()
		childCount = ctrl:GetChildCount()
	end)

	for i = 0, childCount - 1 do
		local child = nil
		pcall(function()
			child = ctrl:GetChildByIndex(i)
		end)
		CLOVER_REPLACE_HAIR_RATING_TEXT(child, rankText)
	end
end

local function CLOVER_FIX_HAIR_TOOLTIP_TEXT(tooltipframe, rankText)
	CLOVER_REPLACE_HAIR_RATING_TEXT(tooltipframe, rankText)
	pcall(function()
		ReserveScript(string.format("CLOVER_REPLACE_VISIBLE_HAIR_RATING('%s')", rankText), 0.01)
	end)
end

function CLOVER_REPLACE_VISIBLE_HAIR_RATING(rankText)
	local tooltip = ui.GetFrame('wholeitem')
	CLOVER_REPLACE_HAIR_RATING_TEXT(tooltip, rankText)

	tooltip = ui.GetFrame('inventory')
	CLOVER_REPLACE_HAIR_RATING_TEXT(tooltip, rankText)
end

Melia.Override('DRAW_EQUIP_COMMON_TOOLTIP_SMALL_IMG', function (original, tooltipframe, invitem, mainframename, isForgery)
    local result = original(tooltipframe, invitem, mainframename, isForgery)

    local gBox = GET_CHILD(tooltipframe, mainframename,'ui::CGroupBox')
    --Melia.Log.Info('{0}', gBox)

    local equipCommonCSet = GET_CHILD_RECURSIVELY(tooltipframe, 'equip_common_cset', 'ui::CControlSet')
    --Melia.Log.Info('{0}', equipCommonCSet)
	tolua.cast(equipCommonCSet, "ui::CControlSet");

	local itemObj = GetIES(invitem:GetObject())
	local equipType = TryGetProp(itemObj, 'EquipXpGroup', 'None')
	local className = TryGetProp(itemObj, 'ClassName', 'None')
	local hairRank = TryGetProp(itemObj, 'EnchantItemRank', -1)
	if hairRank >= 0 and (equipType == 'Hat' or string.find(className, 'Hat') ~= nil) then
		local rankText = CLOVER_HAIR_RANK_TEXT(hairRank)
		CLOVER_FIX_HAIR_TOOLTIP_TEXT(tooltipframe, rankText)

		local gradeName = GET_CHILD_RECURSIVELY(equipCommonCSet, 'gradeName')
		gradeName:SetText(rankText)
		gradeName:ShowWindow(1)
		return result
	end

	local grade = GET_ITEM_GRADE(invitem)
    local score = GET_GEAR_SCORE(invitem)
	local score_text = ''
	if score > 0 then
		score_text = ' (' .. score .. ')'
	end
	--Melia.Log.Info('Grade: {0} Score {1}', grade, score);
    local gradeText = equipCommonCSet:GetUserConfig("GRADE_TEXT_FONT")
	if grade == 1 then
		gradeText = gradeText .. equipCommonCSet:GetUserConfig("NORMAL_GRADE_TEXT")
	elseif grade == 2 then
		gradeText = gradeText .. equipCommonCSet:GetUserConfig("MAGIC_GRADE_TEXT")
	elseif grade == 3 then
		gradeText = gradeText .. equipCommonCSet:GetUserConfig("RARE_GRADE_TEXT")
	elseif grade == 4 then
		gradeText = gradeText .. equipCommonCSet:GetUserConfig("UNIQUE_GRADE_TEXT")
	elseif grade == 5 then
		gradeText = gradeText .. equipCommonCSet:GetUserConfig("LEGEND_GRADE_TEXT")		
	elseif grade == 6 then
		gradeText = gradeText .. equipCommonCSet:GetUserConfig("GODDESS_GRADE_TEXT")
	end
    gradeText = gradeText .. score_text

    local gradeName = GET_CHILD_RECURSIVELY(equipCommonCSet, 'gradeName')
    if 0 < grade then 
		gradeName:SetText(gradeText)
	else
		gradeName:ShowWindow(0);
	end

    return result
end)
