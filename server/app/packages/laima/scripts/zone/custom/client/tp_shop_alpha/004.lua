function CA_WIPE_X(c)
	if c==nil then return end
	local ok,t=pcall(function() return c:GetText() end)
	if ok and t~=nil and string.find(t,'XXX')~=nil then
		c:SetText('')
	end
	local okc,cnt=pcall(function() return c:GetChildCount() end)
	if okc and cnt~=nil then
		for i=0,cnt-1 do
			local okch,ch=pcall(function() return c:GetChildByIndex(i) end)
			if okch and ch~=nil then CA_WIPE_X(ch) end
		end
	end
end

function CA_CARD(main,idx,itemId,price,fn)
	local item=GetClassByType('Item',itemId)
	if item==nil then return end
	local x=((idx-1)%3)*ui.GetControlSetAttribute('tpshop_item','width')
	local y=(math.ceil(idx/3)-1)*ui.GetControlSetAttribute('tpshop_item','height')
	local c=main:CreateOrGetControlSet('tpshop_item','eachitem_'..idx,x,y)
	c:SetUserValue('TPITEM_CLSID',0)
	CA_CLEAN(c)
	local n=CA_GET(c,'title')
	if n~=nil then n:SetText('{@st41b}'..item.Name) end
	local st=CA_GET(c,'subtitle')
	if st~=nil then st:SetText('') end
	for i=1,3 do
		local x=CA_GET(c,'noneBtnPreSlot_'..i)
		if x~=nil then x:SetText('') end
	end
	local p=CA_GET(c,'nxp')
	if p~=nil then p:SetText('{@st43}{s18}'..price..'{/}') end
	local slot=CA_GET(c,'icon')
	if slot~=nil then
		SET_SLOT_IMG(slot,GET_ITEM_ICON_IMAGE(item))
		local ic=slot:GetIcon()
		ic:SetTooltipType('wholeitem')
		ic:SetTooltipArg('',itemId,0)
	end
	local b=CA_GET(c,'buyBtn')
	if b~=nil then
		b:SetSkinName('test_red_button')
		b:SetText('{#FFFFFF}Add{/}')
		b:SetEventScript(ui.LBUTTONUP,fn)
		b:SetEnable(1)
	end
	local pr=CA_GET(c,'previewBtn')
	if pr~=nil then pr:ShowWindow(0) end
	CA_WIPE_X(c)
end
