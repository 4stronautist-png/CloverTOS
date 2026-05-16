Melia.Hook("SET_MONGEN_NPC_VISIBLE", function(original, result, picture, mapprop, MonProp)
    local mapName = session.GetMapName()
    if mapName == "f_siauliai_west" and MonProp ~= nil then
        local genType = nil
        local ok = pcall(function()
            genType = MonProp.GenType
        end)

        if ok and tonumber(genType) == 2002 then
            picture:ShowWindow(0)
            return result
        end
    end

    picture:ShowWindow(1)
    SET_MONGEN_NPC_VISIBLE_BY_MGAME(picture);
end)
