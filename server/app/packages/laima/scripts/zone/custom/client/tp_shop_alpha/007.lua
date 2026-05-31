function CA_BUY()
	local f=ui.GetFrame('tpitem')
	if f==nil then return end
	local s=CA_GET(f,'basketslotset')
	if s~=nil then
		for i=0,s:GetSlotCount()-1 do
			if s:GetIconByIndex(i)~=nil then
				local sl=s:GetSlotByIndex(i)
				local cmd=sl:GetUserValue('CA_CMD')
				if cmd~='None' then ui.Chat('/'..cmd) end
			end
		end
	end
end

function CA_ASK()
	local f=ui.GetFrame('tpitem')
	if f==nil or f:GetUserValue('CA_BASKET')~='1' then return end
	local s=CA_GET(f,'basketslotset')
	if s==nil or s:GetIconByIndex(0)==nil then ui.SysMsg(ClMsg('NoItemInBasket')) return end
	local t=CA_TOTAL(f)
	ui.MsgBox('Will you buy the items above in total of '..t..' TP?', 'CA_BUY()', 'None')
end

function CA_RESTORE()
	local f=ui.GetFrame('tpitem')
	if f~=nil then f:SetUserValue('CA_BASKET','0') end
	local btn=CA_GET(f,'basketBuyBtn')
	if btn~=nil then btn:SetEventScript(ui.LBUTTONUP,'TPSHOP_ITEM_BASKET_BUY') end
end
