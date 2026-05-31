function CA_TOTAL(f)
	local s=CA_GET(f,'basketslotset')
	local t=0
	if s~=nil then
		for i=0,s:GetSlotCount()-1 do
			if s:GetIconByIndex(i)~=nil then
				local sl=s:GetSlotByIndex(i)
				t=t+tonumber(sl:GetUserValue('CA_PRICE') or '0')
			end
		end
	end
	f:SetUserValue('CA_PRICE',t)
	CA_SET_TOTALS(f,t)
	return t
end

function CA_ADD_ITEM(itemId,price,cmd)
	local f=ui.GetFrame('tpitem')
	local item=GetClassByType('Item',itemId)
	if f==nil or item==nil then return end
	f:SetUserValue('CA_BASKET','1')
	local s=CA_GET(f,'basketslotset')
	if s~=nil then
		local slot=nil
		for i=0,s:GetSlotCount()-1 do
			if s:GetIconByIndex(i)==nil then slot=s:GetSlotByIndex(i) break end
		end
		if slot==nil then return end
		slot:SetUserValue('CLASSNAME',item.ClassName)
		slot:SetUserValue('TPITEMNAME',cmd)
		slot:SetUserValue('CA_CMD',cmd)
		slot:SetUserValue('CA_PRICE',price)
		SET_SLOT_IMG(slot,GET_ITEM_ICON_IMAGE(item))
		local ic=slot:GetIcon()
		ic:SetTooltipType('wholeitem')
		ic:SetTooltipArg('',itemId,0)
	end
	CA_TOTAL(f)
	local btn=CA_GET(f,'basketBuyBtn')
	if btn~=nil then
		btn:SetEnable(1)
		btn:SetEventScript(ui.LBUTTONUP,'CA_ASK')
	end
end

function CA_ADD() CA_ADD_ITEM(CA_PREMIUM_ALPHA_ITEM,CA_PREMIUM_ALPHA_PRICE,'cloveralpha_buy_633156') end
function CA_ADD_TOKEN() CA_ADD_ITEM(CA_PREMIUM_TOKEN_ITEM,CA_PREMIUM_TOKEN_PRICE,'cloveralpha_buy_token') end
