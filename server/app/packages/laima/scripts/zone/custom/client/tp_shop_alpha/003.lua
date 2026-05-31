function CA_EMPTY(f,cat,sub)
	local main=CA_GET(f,'mainSubGbox')
	local title=CA_GET(f,'mainText')
	if main==nil then return end
	CA_RESTORE()
	f:SetUserValue('LAST_OPEN_CATEGORY',cat)
	f:SetUserValue('LAST_OPEN_SUB_CATEGORY',sub)
	DESTROY_CHILD_BYNAME(main,'eachitem_')
	if title~=nil then title:SetText('Premium Item > '..ScpArgMsg(sub)) end
	f:SetUserValue('CHILD_ITEM_INDEX',0)
	main:Invalidate()
	f:Invalidate()
end

function CA_CLEAN(c)
	local hide={'isNew_mark','isSale_mark','isHot_mark','isLimit_mark','isEvent_mark','isCubeSale_mark','titleLine_limited','case_limited','bg_limited','time_limited_bg','time_limited_text','tradeable'}
	for i=1,#hide do
		local h=CA_GET(c,hide[i])
		if h~=nil then h:ShowWindow(0) end
	end
end
