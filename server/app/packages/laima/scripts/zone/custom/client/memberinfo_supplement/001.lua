SSMI=SSMI or {a=0,j="",t=0}

function SSMI_APPLY(a,j)
	SSMI.a=a or 0
	SSMI.j=j or ""
	SSMI.t=0
	SSMI_DRAW()
	local f=ui.GetFrame("compare") or ui.GetFrame("chat")
	if f~=nil then
		local tm=f:CreateOrGetControl("timer","ssmi_timer",0,0,1,1)
		AUTO_CAST(tm)
		tm:SetUpdateScript("SSMI_TICK")
		tm:Start(0.25)
	end
end

function SSMI_TICK()
	SSMI_DRAW()
	SSMI.t=(SSMI.t or 0)+1
	return SSMI.t<8 and 1 or 0
end

function SSMI_DRAW()
	local frame=ui.GetFrame("compare")
	if frame==nil or frame:IsVisible()==0 then return end
	local ag=GET_CHILD_RECURSIVELY(frame,"achieveCount")
	if ag~=nil then ag:SetTextByKey("count",tostring(SSMI.a or 0)) end
	local box=GET_CHILD(frame,"groupbox_3","ui::CGroupBox")
	if box==nil then return end
	box:ShowWindow(1)
	DESTROY_CHILD_BYNAME(box,"ssmi_")
	local clslist,cnt=GetClassList("Job")
	local idx=0
	for part in string.gmatch(SSMI.j or "","([^,]+)") do
		local jid,grade=string.match(part,"(%d+):(%d+)")
		if jid~=nil then
			local cls=GetClassByTypeFromList(clslist,tonumber(jid))
			if cls~=nil then
				local x=(idx%3)*150+28
				local y=math.floor(idx/3)*112+12
				local slot=box:CreateOrGetControl("slot","ssmi_slot_"..idx,x,y,70,70)
				AUTO_CAST(slot)
				slot:EnableHitTest(0)
				local icon=CreateIcon(slot)
				icon:SetImage(cls.Icon)
				local name=box:CreateOrGetControl("richtext","ssmi_name_"..idx,x-34,y+72,138,26)
				AUTO_CAST(name)
				name:SetFontName("white_18_ol")
				name:SetTextAlign("center","center")
				name:SetText("{@st41}"..cls.Name)
				idx=idx+1
			end
		end
	end
	frame:Invalidate()
end
