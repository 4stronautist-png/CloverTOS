LCV=LCV or {e=false,a=false,t=0}

function LCV_FRAME()
	local n={"equipcardalbum","cardalbum","cardequip","equipcard","monstercardslot"}
	for i=1,#n do
		local f=ui.GetFrame(n[i])
		if f~=nil and f:IsVisible()==1 then return f end
	end
	return nil
end

function LCV_SYNC(e,a)
	LCV.e=e==true
	LCV.a=a==true
	LCV_DRAW()
end

function LCV_CLICK()
	if LCV.a==true then
		ui.Chat("/legendcardvisual toggle")
	else
		ui.Chat("/legendcardvisual off")
	end
end

function LCV_DRAW()
	local f=LCV_FRAME()
	if f==nil then return end
	local w,h=54,22
	local x=math.floor((f:GetWidth()-w)/2)
	local y=math.floor(f:GetHeight()*0.43)
	local b=f:CreateOrGetControl("button","lcvisual_btn",x,y,w,h)
	b:SetEventScript(ui.LBUTTONUP,"LCV_CLICK")
	if LCV.a==true and LCV.e==true then
		b:SetSkinName("test_gray_button")
		b:SetText("{#FFFFFF}{s12}ON{/}")
	elseif LCV.a==true then
		b:SetSkinName("test_red_button")
		b:SetText("{#FFFFFF}{s12}OFF{/}")
	else
		b:SetSkinName("test_normal_button")
		b:SetText("{#777777}{s12}OFF{/}")
	end
end

function LCV_TICK()
	local f=LCV_FRAME()
	if f~=nil then
		LCV_DRAW()
		LCV.t=(LCV.t or 0)+1
		if LCV.t>=4 then
			LCV.t=0
			ui.Chat("/legendcardvisual status")
		end
	end
	return 1
end

local cf=ui.GetFrame("sysmenu")
if cf==nil then cf=ui.GetFrame("chat") end
if cf~=nil then
	local tm=cf:CreateOrGetControl("timer","lcvisual_timer",0,0,1,1)
	AUTO_CAST(tm)
	tm:SetUpdateScript("LCV_TICK")
	tm:Start(1.0)
end
