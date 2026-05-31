function CA_GET(f,n)
	if f==nil then return nil end
	return GET_CHILD_RECURSIVELY(f,n)
end

function CA_SET_TOTALS(f,v)
	local b=CA_GET(f,'basketTP')
	if b~=nil then b:SetText(tostring(v)) end
	local h=CA_GET(f,'haveTP')
	if h~=nil then h:SetText(tostring(GET_CASH_TOTAL_POINT_C())) end
	local r=CA_GET(f,'remainTP')
	if r~=nil then r:SetText(tostring(GET_CASH_TOTAL_POINT_C()-v)) end
end
