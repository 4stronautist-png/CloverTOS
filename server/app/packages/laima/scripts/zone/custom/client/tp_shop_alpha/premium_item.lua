CA_PREMIUM_CAT='TP_Premium'
CA_PREMIUM_ALPHA_SUB='CA_Giltine_Alpha'
CA_PREMIUM_ALPHA_ITEM=633156
CA_PREMIUM_ALPHA_PRICE=20
CA_PREMIUM_TOKEN_ITEM=490000
CA_PREMIUM_TOKEN_PRICE=38

function CA_PREMIUM_PARENT(tree)
	local p=tree:FindByValue(CA_PREMIUM_CAT)
	if p==nil or tree:IsExist(p)==0 then p=tree:FindByValue('TP_PremiumItem') end
	if p==nil or tree:IsExist(p)==0 then p=tree:FindByValue('TP_Premium_Item') end
	if p==nil or tree:IsExist(p)==0 then p=tree:FindByValue('TP_Item') end
	return p
end

function CA_PREMIUM_TREE(f)
	local tree=CA_GET(f,'tpitemtree')
	if tree==nil or tree:IsExist(tree:FindByValue(CA_PREMIUM_CAT..'#'..CA_PREMIUM_ALPHA_SUB))==1 then return end
	local p=CA_PREMIUM_PARENT(tree)
	if p==nil or tree:IsExist(p)==0 then return end
	tree:Add(p,'{@st66}[Giltine Sin] Alpha Items',CA_PREMIUM_CAT..'#'..CA_PREMIUM_ALPHA_SUB,'{#000000}')
	tree:SetFitToChild(true,10)
end

function CA_PREMIUM_DRAW(f,cat,sub)
	if sub==CA_PREMIUM_ALPHA_SUB then CA_PREMIUM_ALPHA_DRAW(f) return 1 end
	if cat==CA_PREMIUM_CAT then CA_EMPTY(f,cat,sub) return 1 end
	return 0
end

function CA_PREMIUM_ALPHA_DRAW(f)
	local main=CA_GET(f,'mainSubGbox')
	local title=CA_GET(f,'mainText')
	if main==nil then return end
	CA_RESTORE()
	f:SetUserValue('LAST_OPEN_CATEGORY',CA_PREMIUM_CAT)
	f:SetUserValue('LAST_OPEN_SUB_CATEGORY',CA_PREMIUM_ALPHA_SUB)
	local s=CA_GET(f,'basketslotset')
	if s~=nil then s:ClearIconAll() end
	CA_SET_TOTALS(f,0)
	DESTROY_CHILD_BYNAME(main,'eachitem_')
	if title~=nil then title:SetText('Premium Item > [Giltine Sin] Alpha Items') end
	CA_CARD(main,1,CA_PREMIUM_ALPHA_ITEM,CA_PREMIUM_ALPHA_PRICE,'CA_ADD')
	CA_CARD(main,2,CA_PREMIUM_TOKEN_ITEM,CA_PREMIUM_TOKEN_PRICE,'CA_ADD_TOKEN')
	f:SetUserValue('CHILD_ITEM_INDEX',2)
	TPSHOP_TPITEM_ALIGN_LIST(2)
	main:Invalidate()
	f:Invalidate()
end
