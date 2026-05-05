SSW=SSW or {e=false}
SSW_OLD_HOTKEY_AUTO_MOVE=SSW_OLD_HOTKEY_AUTO_MOVE or HOTKEY_AUTO_MOVE

function HOTKEY_WALK()
	SS_WALK_TOGGLE()
end

function HOTKEY_AUTO_MOVE()
	SS_WALK_TOGGLE()
end

function SS_WALK_CLICK()
	HOTKEY_WALK()
end

function SS_WALK_TOGGLE()
	SSW.e=SSW.e~=true
	SS_WALK_DRAW()
	if SSW.e==true then
		ui.Chat("/walk on")
	else
		ui.Chat("/walk off")
	end
end

function SS_WALK_SYNC(e)
	SSW.e=e==true
	SS_WALK_DRAW()
end

function SS_WALK_HIDE_MAP_BUTTON(f)
	if f==nil then return end
	local names={
		"map","mapbtn","mapBtn","map_btn","btn_map","btnMap","button_map",
		"worldmap","worldMap","worldmap_btn","btn_worldmap","openmap","openMap",
		"minimap_map","minimapMap","m_btn","btn_m"
	}
	for i=1,#names do
		local c=GET_CHILD_RECURSIVELY(f,names[i],"ui::CControl")
		if c~=nil and c:GetName()~="ss_walk_btn" then
			c:ShowWindow(0)
			pcall(function() c:EnableHitTest(0) end)
		end
	end
end

function SS_WALK_DRAW()
	local mf=ui.GetFrame("minimap")
	if mf==nil then return 1 end

	SS_WALK_HIDE_MAP_BUTTON(mf)

	local oldFrame=ui.GetFrame("ss_walk_minimap_btn")
	if oldFrame~=nil then oldFrame:ShowWindow(0) end

	local parent=mf:GetParent()
	if parent==nil then parent=mf end

	local x=mf:GetX()+mf:GetWidth()-42
	local y=mf:GetY()+mf:GetHeight()+8
	if parent==mf then
		x=mf:GetWidth()-42
		y=mf:GetHeight()-27
	end

	local b=parent:CreateOrGetControl("button","ss_walk_btn",x,y,34,27)
	AUTO_CAST(b)
	b:ShowWindow(1)
	b:SetEventScript(ui.LBUTTONUP,"SS_WALK_CLICK")
	pcall(function() b:SetTextAlign("center","center") end)
	if SSW.e==true then
		b:SetSkinName("test_red_button")
		b:SetTextTooltip("{@st59} Walk {/}")
		b:SetText("{#FFFFFF}{s16}W{/}")
	else
		b:SetSkinName("test_gray_button")
		b:SetTextTooltip("{@st59} Run {/}")
		b:SetText("{#FFFFFF}{s16}R{/}")
	end
	return 1
end

local f=ui.GetFrame("minimap")
if f~=nil then
	local t=f:CreateOrGetControl("timer","ss_walk_timer",0,0,1,1)
	AUTO_CAST(t)
	t:SetUpdateScript("SS_WALK_DRAW")
	t:Start(0.5)
end

SS_WALK_DRAW()
