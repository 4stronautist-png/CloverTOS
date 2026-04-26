function M_QUESTS_UPDATE_LIST()
	local questFrame = ui.GetFrame("quest")
	local gbBody = GET_CHILD_RECURSIVELY(questFrame, "gb_progressQuestItem", "ui::CGroupBox")
	if gbBody == nil then
		gbBody = GET_CHILD_RECURSIVELY(questFrame, "gb_body", "ui::CGroupBox")
	end
	local quests = Melia.Quests.GetAll()

	if gbBody ~= nil then
		M_QUESTS_DRAW_LIST(gbBody, quests)
	end
	M_CHASE_UPDATE_VISIBILITY()
end

M_QUESTS_UPDATE_LIST()
