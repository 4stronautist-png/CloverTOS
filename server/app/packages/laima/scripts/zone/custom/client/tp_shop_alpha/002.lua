function CA_TREE()
	local f=ui.GetFrame('tpitem')
	CA_PREMIUM_TREE(f)
end

function CA_HOOK()
	if MAKE_CATEGORY_TREE==nil or TPITEM_DRAW_ITEM_WITH_CATEGORY==nil then
		ReserveScript('CA_HOOK()',1)
		return
	end
	if CA_OLD_MAKE==nil then
		CA_OLD_MAKE=MAKE_CATEGORY_TREE
		MAKE_CATEGORY_TREE=function()
			CA_OLD_MAKE()
			CA_TREE()
		end
	end
	if CA_OLD_DRAW==nil then
		CA_OLD_DRAW=TPITEM_DRAW_ITEM_WITH_CATEGORY
		TPITEM_DRAW_ITEM_WITH_CATEGORY=function(f,cat,sub,initdraw,isSub,filter,allFlag)
			if CA_PREMIUM_DRAW(f,cat,sub)==1 then return end
			CA_RESTORE()
			CA_OLD_DRAW(f,cat,sub,initdraw,isSub,filter,allFlag)
		end
	end
end

CA_HOOK()
