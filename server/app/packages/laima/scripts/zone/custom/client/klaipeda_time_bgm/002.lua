SS_KFX = SS_KFX or { on = false, tick = 0 }

local function SS_KFX_FRAME()
	local f = ui.GetFrame("chat")
	if f == nil then return nil end
	f:ShowWindow(1)
	return f
end

local function SS_KFX_CLEAR(f)
	if f ~= nil then DESTROY_CHILD_BYNAME(f, "ss_kfx_") end
end

function SS_KLAIPEDA_NIGHTFX(on)
	SS_KFX.on = on == true
	local f = SS_KFX_FRAME()
	if f == nil then return end
	if not SS_KFX.on then SS_KFX_CLEAR(f) return end
	SS_KFX_DRAW()
	local t = f:CreateOrGetControl("timer", "ss_kfx_timer", 0, 0, 1, 1)
	AUTO_CAST(t)
	t:SetUpdateScript("SS_KFX_TICK")
	t:Start(0.35)
end

function SS_KFX_TICK()
	if not SS_KFX.on then return 0 end
	SS_KFX_DRAW()
	return 1
end

function SS_KFX_DRAW()
	local f = SS_KFX_FRAME()
	if f == nil then return end
	SS_KFX_CLEAR(f)
	SS_KFX.tick = (SS_KFX.tick or 0) + 1
	for i = 1, 18 do
		local x = 30 + ((i * 73 + SS_KFX.tick * 11) % 520)
		local y = 20 + ((i * 41 + SS_KFX.tick * 7) % 170)
		local s = 10 + (i % 4) * 3
		local c = f:CreateOrGetControl("richtext", "ss_kfx_" .. i, x, y, 80, 30)
		AUTO_CAST(c)
		c:EnableHitTest(0)
		if i % 3 == 0 then
			c:SetText("{#FFF4A8}{s" .. s .. "}o{/}")
		else
			c:SetText("{#B6FF8A}{s" .. s .. "}·{/}")
		end
	end
end
