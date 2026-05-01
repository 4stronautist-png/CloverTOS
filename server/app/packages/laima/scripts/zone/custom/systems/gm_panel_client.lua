SSGM_PANEL_FRAME = "ssgm_panel"
SSGM_PANEL_CATEGORY = SSGM_PANEL_CATEGORY or "items"
SSGM_PANEL_TAB_ORDER = SSGM_PANEL_TAB_ORDER or {}

SSGM_PANEL_CATEGORIES = {
	{ id = "items", text = "Itens" },
	{ id = "move", text = "Movimento" },
	{ id = "world", text = "Mundo" },
	{ id = "player", text = "Player" },
	{ id = "info", text = "Info/Admin" },
}

SSGM_PANEL_DESCRIPTIONS = {
	give = "Cria itens no inventario de um player.",
	removeitem = "Remove itens do inventario de um player.",
	mail = "Envia um ou mais itens no mesmo mail para a caixa de mensagem do player.",
	mailall = "Envia um ou mais itens no mesmo mail para todos os teams registrados.",
	enhance = "Define o refinamento de um equipamento no inventario.",
	item = "Cria item no seu inventario.",
	silver = "Adiciona ou remove silver.",
	medals = "Adiciona ou remove medals/TP.",
	clearinv = "Remove todos os itens do seu inventario.",
	earring = "Cria um Fire Flame Earring com linhas fixas de skill.",
	pos = "Copia a posicao atual para a area de transferencia.",
	warp = "Teleporta para o mapa informado.",
	jump = "Teleporta para coordenadas do mapa atual.",
	["goto"] = "Teleporta ate um player.",
	summon = "Traz um player ate voce.",
	recall = "Envia um player de volta.",
	recallmap = "Retorna todos os players de um mapa.",
	go = "Teleporta para um destino pre-definido.",
	speed = "Altera sua velocidade de movimento.",
	killmon = "Mata monstros proximos ou o monstro em foco.",
	spawn = "Cria monstros na sua posicao.",
	serialkiller = "Ativa/desativa morte em um golpe.",
	broadcast = "Envia mensagem global e mostra o nome do GM na mensagem para todos.",
	aviso = "Envia aviso central global.",
	kick = "Expulsa player, mapa ou todos.",
	daytime = "Altera o horario visual do mundo.",
	fixcam = "Fixa a camera do personagem.",
	godmode = "Ativa/desativa invulnerabilidade.",
	levelup = "Aumenta level do personagem.",
	joblevelup = "Aumenta job level.",
	addjob = "Adiciona uma classe/job ao personagem.",
	removejob = "Remove uma classe/job do personagem.",
	skillpoints = "Altera pontos de skill.",
	statpoints = "Altera pontos de status.",
	abilitypoints = "Altera pontos de atributo.",
	heal = "Cura HP, SP e stamina.",
	alive = "Revive ou mata o personagem.",
	name = "Troca o nome do personagem.",
	tname = "Troca o Team Name do jogador.",
	mute = "Silencia um player por minutos.",
	unmute = "Remove silencio de um player.",
	rollback = "Restaura inventario de um player por minutos.",
	transform = "Transforma visualmente em monstro.",
	skinreset = "Restaura skin e nome normal.",
	where = "Mostra e copia coordenadas atuais.",
	distance = "Calcula distancia para debug.",
	iteminfo = "Mostra informacoes de um item.",
	monsterinfo = "Mostra informacoes de um monstro.",
	whodrops = "Lista monstros que dropam item.",
	whereis = "Mostra mapas onde monstro aparece.",
	storage = "Abre storage pessoal.",
	resetcd = "Reseta cooldowns de skill.",
	nosave = "Ativa/desativa salvamento ao logout.",
	feature = "Ativa/desativa feature do servidor.",
	reloadscripts = "Recarrega scripts.",
	reloadconf = "Recarrega configuracoes.",
	reloaddata = "Recarrega dados.",
}

SSGM_PANEL_COMMANDS = {
	{ cat = "items", cmd = "give", title = "/give", fields = { "item", "quantidade", "player" } },
	{ cat = "items", cmd = "removeitem", title = "/removeitem", fields = { "item", "quantidade", "player" } },
	{ cat = "items", cmd = "mail", title = "/mail", fields = { "item", "quantidade", "player", "titulo", "mensagem" }, mail = true },
	{ cat = "items", cmd = "mailall", title = "/mailall", fields = { "item", "quantidade", "titulo", "mensagem", "dias" }, mailall = true },
	{ cat = "items", cmd = "enhance", title = "/enhance", fields = { "item", "valor" } },
	{ cat = "items", cmd = "item", title = "/item", fields = { "item", "quantidade" } },
	{ cat = "items", cmd = "itemcard", title = "/itemcard", fields = { "player", "card id", "nivel" } },
	{ cat = "items", cmd = "silver", title = "/silver", fields = { "quantidade" } },
	{ cat = "items", cmd = "medals", title = "/medals", fields = { "quantidade" } },
	{ cat = "items", cmd = "clearinv", title = "/clearinv", fields = {}, tip = "Remove todos os itens do inventario." },
	{ cat = "items", cmd = "earring", title = "/earring", fields = { "classeId", "linha1", "linha2", "linha3" } },

	{ cat = "move", cmd = "pos", title = "/pos", fields = {}, tip = "Copia a posicao para a area de transferencia." },
	{ cat = "move", cmd = "warp", title = "/warp", fields = { "id do mapa", "x", "y", "z" } },
	{ cat = "move", cmd = "jump", title = "/jump", fields = { "x", "y", "z" } },
	{ cat = "move", cmd = "goto", title = "/goto", fields = { "nome do player" } },
	{ cat = "move", cmd = "summon", title = "/summon", fields = { "player" } },
	{ cat = "move", cmd = "recall", title = "/recall", fields = { "player" } },
	{ cat = "move", cmd = "recallmap", title = "/recallmap", fields = { "mapa" } },
	{ cat = "move", cmd = "go", title = "/go", fields = { "destino" } },
	{ cat = "move", cmd = "speed", title = "/speed", fields = { "velocidade" } },
	{ cat = "move", cmd = "size", title = "/size", fields = { "player", "scale" } },

	{ cat = "world", cmd = "killmon", title = "/killmon", fields = { "handle" }, tip = "Vazio: mata o monstro em foco." },
	{ cat = "world", cmd = "spawn", title = "/spawn", fields = { "monstro", "quantidade" } },
	{ cat = "world", cmd = "serialkiller", title = "/serialkiller", fields = {} },
	{ cat = "world", cmd = "broadcast", title = "/broadcast", fields = { "mensagem" } },
	{ cat = "world", cmd = "aviso", title = "/aviso", fields = { "mensagem" } },
	{ cat = "world", cmd = "kick", title = "/kick", fields = { "player" } },
	{ cat = "world", cmd = "daytime", title = "/daytime", fields = { "day/night/dawn/dusk" } },
	{ cat = "world", cmd = "fixcam", title = "/fixcam", fields = {} },

	{ cat = "player", cmd = "godmode", title = "/godmode", fields = { "player" } },
	{ cat = "player", cmd = "levelup", title = "/levelup", fields = { "levels" } },
	{ cat = "player", cmd = "joblevelup", title = "/joblevelup", fields = { "levels" } },
	{ cat = "player", cmd = "addjob", title = "/addjob", fields = { "job id", "circle" } },
	{ cat = "player", cmd = "removejob", title = "/removejob", fields = { "job id" } },
	{ cat = "player", cmd = "skillpoints", title = "/skillpoints", fields = { "job id", "quantidade" } },
	{ cat = "player", cmd = "statpoints", title = "/statpoints", fields = { "quantidade" } },
	{ cat = "player", cmd = "abilitypoints", title = "/abilitypoints", fields = { "quantidade" } },
	{ cat = "player", cmd = "heal", title = "/heal", fields = { "hp", "sp", "stamina" } },
	{ cat = "player", cmd = "alive", title = "/alive", fields = {} },
	{ cat = "player", cmd = "name", title = "/name", fields = { "novo nome" } },
	{ cat = "player", cmd = "tname", title = "/tname", fields = { "player", "novo team name" } },
	{ cat = "player", cmd = "mute", title = "/mute", fields = { "player", "minutos" } },
	{ cat = "player", cmd = "unmute", title = "/unmute", fields = { "player" } },

	{ cat = "info", cmd = "where", title = "/where", fields = {} },
	{ cat = "info", cmd = "distance", title = "/distance", fields = {} },
	{ cat = "info", cmd = "iteminfo", title = "/iteminfo", fields = { "nome/id" } },
	{ cat = "info", cmd = "monsterinfo", title = "/monsterinfo", fields = { "nome/id" } },
	{ cat = "info", cmd = "whodrops", title = "/whodrops", fields = { "item" } },
	{ cat = "info", cmd = "whereis", title = "/whereis", fields = { "monstro" } },
	{ cat = "info", cmd = "storage", title = "/storage", fields = {} },
	{ cat = "info", cmd = "resetcd", title = "/resetcd", fields = {} },
	{ cat = "info", cmd = "nosave", title = "/nosave", fields = { "enabled" } },
	{ cat = "info", cmd = "feature", title = "/feature", fields = { "feature", "enabled" } },
	{ cat = "info", cmd = "reloadscripts", title = "/reloadscripts", fields = {} },
	{ cat = "info", cmd = "reloadconf", title = "/reloadconf", fields = {} },
	{ cat = "info", cmd = "reloaddata", title = "/reloaddata", fields = {} },
}

SSGM_JOB_LIST_PAGE = SSGM_JOB_LIST_PAGE or 1
SSGM_JOB_LIST = {
	{
		{1001, "Char1_1", "Warrior", "Swordsman"}, {1002, "Char1_2", "Warrior", "Highlander"}, {1003, "Char1_3", "Warrior", "Peltasta"}, {1004, "Char1_4", "Warrior", "Hoplite"},
		{1005, "Char1_5", "Warrior", "Centurion"}, {1006, "Char1_6", "Warrior", "Barbarian"}, {1007, "Char1_7", "Warrior", "Cataphract"}, {1009, "Char1_9", "Warrior", "Doppelsoeldner"},
		{1010, "Char1_10", "Warrior", "Rodelero"}, {1012, "Char1_12", "Warrior", "Murmillo"}, {1014, "Char1_14", "Warrior", "Fencer"}, {1015, "Char1_15", "Warrior", "Dragoon"},
		{1016, "Char1_16", "Warrior", "Templar"}, {1017, "Char1_17", "Warrior", "Lancer"}, {1018, "Char1_19", "Warrior", "Matador"}, {1019, "Char1_20", "Warrior", "Nak Muay"},
		{1020, "Char1_18", "Warrior", "Retiarius"}, {1021, "Char1_21", "Warrior", "Hackapell"}, {1022, "Char1_22", "Warrior", "Blossom Blader"}, {1023, "Char1_23", "Warrior", "Luchador"},
	},
	{
		{1024, "Char1_24", "Warrior", "Shenji"}, {1025, "Char1_25", "Warrior", "Winged Hussar"}, {1026, "Char1_26", "Warrior", "Vanquisher"}, {1027, "Char1_27", "Warrior", "Sledger[S]"},
		{1028, "Char1_28", "Warrior", "Bonemancer[S]"}, {1029, "Char1_29", "Warrior", "Grimmark[S]"}, {1030, "Char1_30", "Warrior", "Eskrimer"}, {2001, "Char2_1", "Wizard", "Wizard"},
	},
	{
		{2002, "Char2_2", "Wizard", "Pyromancer"},
		{2003, "Char2_3", "Wizard", "Cryomancer"}, {2004, "Char2_4", "Wizard", "Psychokino"}, {2005, "Char2_5", "Wizard", "Alchemist"}, {2006, "Char2_6", "Wizard", "Sorcerer"},
		{2008, "Char2_8", "Wizard", "Chronomancer"}, {2009, "Char2_9", "Wizard", "Necromancer"}, {2011, "Char2_11", "Wizard", "Elementalist"}, {2012, "Char2_12", "Wizard", "Mimic"},
		{2014, "Char2_14", "Wizard", "Sage"}, {2015, "Char2_15", "Wizard", "Warlock"}, {2016, "Char2_16", "Wizard", "Featherfoot"}, {2017, "Char2_17", "Wizard", "Rune Caster"},
		{2019, "Char2_19", "Wizard", "Shadowmancer"}, {2020, "Char2_20", "Wizard", "Onmyoji"}, {2021, "Char2_21", "Wizard", "Taoist"}, {2022, "Char2_22", "Wizard", "Bokor"},
		{2023, "Char2_23", "Wizard", "Terramancer"},
	},
	{
		{2024, "Char2_24", "Wizard", "Keraunos"}, {2025, "Char2_25", "Wizard", "Illusionist"}, {2026, "Char2_26", "Wizard", "Vulture [W]"}, {2027, "Char2_27", "Wizard", "Bonemancer[W]"},
		{2028, "Char2_28", "Wizard", "Aether Blader [W]"}, {2029, "Char2_29", "Wizard", "Hermit[W]"}, {2030, "Char2_30", "Wizard", "Kneller[W]"}, {3001, "Char3_1", "Archer", "Archer"},
		{3002, "Char3_2", "Archer", "Hunter"},
		{3003, "Char3_3", "Archer", "Quarrel Shooter"}, {3004, "Char3_4", "Archer", "Ranger"}, {3005, "Char3_5", "Archer", "Sapper"}, {3006, "Char3_6", "Archer", "Wugushi"},
		{3011, "Char3_11", "Archer", "Fletcher"}, {3012, "Char3_12", "Archer", "Pied Piper"}, {3013, "Char3_13", "Archer", "Appraiser"}, {3014, "Char3_14", "Archer", "Falconer"},
		{3015, "Char3_15", "Archer", "Cannoneer"}, {3016, "Char3_16", "Archer", "Musketeer"}, {3017, "Char3_17", "Archer", "Mergen"},
	},
	{
		{3101, "Char3_18", "Archer", "Matross"},
		{3102, "Char3_19", "Archer", "Tiger Hunter"}, {3103, "Char3_20", "Archer", "Arbalester"}, {3104, "Char3_21", "Archer", "Arquebusier"}, {3105, "Char3_22", "Archer", "Hwarang"},
		{3106, "Char3_23", "Archer", "Engineer"}, {3107, "Char3_24", "Archer", "Godeye"}, {3108, "Char3_25", "Archer", "Vulture [A]"}, {3109, "Char3_26", "Archer", "Bonemancer[A]"},
		{3110, "Char3_27", "Archer", "Blitz Hunter [A]"}, {3111, "Char3_28", "Archer", "Hermit[A]"}, {3112, "Char3_29", "Archer", "Grimmark[A]"}, {3113, "Char3_30", "Archer", "Commodore[A]"},
		{4001, "Char4_1", "Cleric", "Cleric"}, {4002, "Char4_2", "Cleric", "Priest"},
		{4003, "Char4_3", "Cleric", "Krivis"}, {4005, "Char4_5", "Cleric", "Druid"}, {4006, "Char4_6", "Cleric", "Sadhu"}, {4007, "Char4_7", "Cleric", "Dievdirbys"},
		{4008, "Char4_8", "Cleric", "Oracle"}, {4009, "Char4_9", "Cleric", "Monk"},
	},
	{
		{4010, "Char4_10", "Cleric", "Pardoner"}, {4011, "Char4_11", "Cleric", "Paladin"},
		{4012, "Char4_12", "Cleric", "Chaplain"}, {4013, "Char4_13", "Cleric", "Shepherd"}, {4014, "Char4_14", "Cleric", "Plague Doctor"}, {4015, "Char4_15", "Cleric", "Kabbalist"},
		{4016, "Char4_16", "Cleric", "Inquisitor"}, {4018, "Char4_18", "Cleric", "Miko"}, {4019, "Char4_19", "Cleric", "Zealot"}, {4020, "Char4_20", "Cleric", "Exorcist"},
		{4021, "Char4_21", "Cleric", "Crusader"}, {4022, "Char4_22", "Cleric", "Lama"}, {4023, "Char4_23", "Cleric", "Pontifex"}, {4024, "Char4_24", "Cleric", "Sledger[C]"},
		{4025, "Char4_25", "Cleric", "Bonemancer[C]"}, {4026, "Char4_26", "Cleric", "Aether Blader [C]"}, {4027, "Char4_27", "Cleric", "Hermit[C]"}, {4028, "Char4_28", "Cleric", "Kneller[C]"},
		{5001, "Char5_1", "Scout", "Scout"}, {5002, "Char5_2", "Scout", "Assassin"}, {5003, "Char5_3", "Scout", "Outlaw"}, {5004, "Char5_4", "Scout", "Squire"},
	},
	{
		{5005, "Char5_5", "Scout", "Corsair"},
		{5006, "Char5_6", "Scout", "Shinobi"}, {5007, "Char5_7", "Scout", "Thaumaturge"}, {5008, "Char5_8", "Scout", "Enchanter"}, {5009, "Char5_9", "Scout", "Linker"},
		{5010, "Char5_10", "Scout", "Rogue"}, {5011, "Char5_11", "Scout", "Schwarzer Reiter"}, {5012, "Char5_12", "Scout", "Bullet Marker"}, {5013, "Char5_13", "Scout", "Ardito"},
		{5014, "Char5_14", "Scout", "Sheriff"}, {5015, "Char5_15", "Scout", "Rangda"}, {5016, "Char5_16", "Scout", "Clown"}, {5017, "Char5_17", "Scout", "Hakkapeliter"},
		{5018, "Char5_18", "Scout", "Jaguar"}, {5019, "Char5_19", "Scout", "Desperado"}, {5020, "Char5_20", "Scout", "Vulture [T]"}, {5021, "Char5_21", "Scout", "Blitz Hunter [T]"},
		{5022, "Char5_22", "Scout", "Aether Blader [T]"}, {5023, "Char5_23", "Scout", "Grimmark[T]"}, {5024, "Char5_24", "Scout", "Kneller[T]"},
	},
	{
		{5025, "Char5_25", "Scout", "Commodore[T]"}, {9001, "Char4_99", "Cleric", "GM"},
	},
}

function SSGM_PANEL_TRY(fn)
	local ok = pcall(fn)
	return ok
end

function SSGM_PANEL_CAST(ctrl)
	if ctrl ~= nil and AUTO_CAST ~= nil then
		AUTO_CAST(ctrl)
	end
	return ctrl
end

function SSGM_PANEL_SKIN(ctrl, skin)
	if ctrl ~= nil then
		SSGM_PANEL_TRY(function() ctrl:SetSkinName(skin) end)
	end
end

function SSGM_PANEL_TEXT(parent, name, x, y, w, h, text, center)
	local ctrl = parent:CreateOrGetControl("richtext", name, x, y, w, h)
	SSGM_PANEL_CAST(ctrl)
	ctrl:SetText(text or "")
	return ctrl
end

function SSGM_PANEL_TITLE_TEXT(parent, name, x, y, w, h, text)
	local ctrl = SSGM_PANEL_TEXT(parent, name, x, y, w, h, text, false)
	SSGM_PANEL_TRY(function() ctrl:SetGravity(ui.CENTER_HORZ, ui.TOP) end)
	SSGM_PANEL_TRY(function() ctrl:SetTextAlign(ui.CENTER_HORZ, ui.CENTER_VERT) end)
	SSGM_PANEL_TRY(function() ctrl:SetTextAlign("center", "center") end)
	return ctrl
end

function SSGM_PANEL_LOWER(text)
	if text == nil then
		return ""
	end

	text = string.lower(tostring(text))
	text = string.gsub(text, "%[.-%]", "")
	text = string.gsub(text, "%s+", "")
	text = string.gsub(text, "[^a-z0-9]", "")
	return text
end

function SSGM_JOB_ICON(job)
	if job == nil then
		return ""
	end

	local icon = ""
	SSGM_PANEL_TRY(function()
		local cls = GetClassByType("Job", job[1])
		if cls ~= nil then
			icon = TryGetProp(cls, "Icon", "")
		end
	end)
	SSGM_PANEL_TRY(function()
		if icon == "" then
			local cls = GetClass("Job", job[2])
			if cls ~= nil then
				icon = TryGetProp(cls, "Icon", "")
			end
		end
	end)
	if icon ~= nil and icon ~= "" and icon ~= "None" then
		return icon
	end

	local tree = SSGM_PANEL_LOWER(job[3])
	local name = SSGM_PANEL_LOWER(job[4])
	return "c_" .. tree .. "_" .. name
end

function SSGM_PANEL_FOCUS_CONTROL(ctrl)
	if ctrl == nil then
		return
	end

	SSGM_PANEL_TRY(function() ctrl:AcquireFocus() end)
	SSGM_PANEL_TRY(function() ctrl:SetFocus(1) end)
	SSGM_PANEL_TRY(function() ctrl:MakeFocus() end)
	SSGM_PANEL_TRY(function() ui.SetFocus(ctrl) end)
	SSGM_PANEL_TRY(function() ui.SetKeyboardFocus(ctrl) end)
end

function SSGM_PANEL_TAB_NEXT(frame, ctrl, argStr, argNum)
	local panel = ui.GetFrame(SSGM_PANEL_FRAME)
	if panel == nil then
		panel = frame
	end

	local nextName = ""
	SSGM_PANEL_TRY(function() nextName = ctrl:GetUserValue("SSGM_NEXT_INPUT") end)
	if nextName == nil or nextName == "" or nextName == "None" then
		return
	end

	local nextCtrl = GET_CHILD_RECURSIVELY(panel, nextName, "ui::CControl")
	SSGM_PANEL_FOCUS_CONTROL(nextCtrl)
end

function SSGM_PANEL_REGISTER_TAB(edit)
	if edit == nil then
		return
	end

	local idx = #SSGM_PANEL_TAB_ORDER + 1
	SSGM_PANEL_TAB_ORDER[idx] = edit:GetName()
	SSGM_PANEL_TRY(function() edit:SetTabIndex(idx) end)
	SSGM_PANEL_TRY(function() edit:SetTabOrder(idx) end)
	if ui ~= nil and ui.TABKEY ~= nil then
		SSGM_PANEL_TRY(function() edit:SetEventScript(ui.TABKEY, "SSGM_PANEL_TAB_NEXT") end)
	end
end

function SSGM_PANEL_LINK_TABS(frame)
	for i = 1, #SSGM_PANEL_TAB_ORDER do
		local name = SSGM_PANEL_TAB_ORDER[i]
		local nextName = SSGM_PANEL_TAB_ORDER[i + 1]
		if nextName == nil then
			nextName = SSGM_PANEL_TAB_ORDER[1]
		end

		local ctrl = GET_CHILD_RECURSIVELY(frame, name, "ui::CControl")
		if ctrl ~= nil and nextName ~= nil then
			SSGM_PANEL_TRY(function() ctrl:SetUserValue("SSGM_NEXT_INPUT", nextName) end)
		end
	end
end

function SSGM_PANEL_BUTTON(parent, name, x, y, w, h, text, script)
	local btn = parent:CreateOrGetControl("button", name, x, y, w, h)
	SSGM_PANEL_CAST(btn)
	SSGM_PANEL_SKIN(btn, "test_gray_button")
	btn:SetText(text)
	btn:SetEventScript(ui.LBUTTONUP, script)
	return btn
end

function SSGM_PANEL_COPY_TEXT(text)
	local copied = false
	local attempts = {
		function() imc.SetClipboardText(text) end,
		function() ui.SetClipboardText(text) end,
		function() SetClipboardText(text) end,
		function() imcSetClipboardText(text) end,
	}

	for i = 1, #attempts do
		if copied == false then
			copied = pcall(attempts[i])
		end
	end

	if copied then
		ui.SysMsg("Coordenadas copiadas.")
	else
		ui.MsgBox(text)
	end
end

function SSGM_PANEL_INPUT(parent, name, x, y, w, label)
	SSGM_PANEL_TEXT(parent, name .. "_Label", x, y, w, 16, "{@st41b}{s11}" .. label .. "{/}", true)

	local edit = parent:CreateOrGetControl("edit", name, x, y + 17, w, 28)
	SSGM_PANEL_CAST(edit)
	SSGM_PANEL_SKIN(edit, "test_edit_4")
	SSGM_PANEL_TRY(function() edit:SetFontName("white_16_ol") end)
	SSGM_PANEL_TRY(function() edit:SetText("") end)
	SSGM_PANEL_REGISTER_TAB(edit)
	return edit
end

function SSGM_PANEL_TRIM(text)
	if text == nil then
		return ""
	end

	text = tostring(text)
	text = string.gsub(text, "^%s+", "")
	text = string.gsub(text, "%s+$", "")
	return text
end

function SSGM_PANEL_GET_TEXT(frame, name)
	local ctrl = GET_CHILD_RECURSIVELY(frame, name, "ui::CControl")
	if ctrl == nil then
		return ""
	end

	local text = ""
	SSGM_PANEL_TRY(function() text = ctrl:GetText() end)
	return SSGM_PANEL_TRIM(text)
end

function SSGM_PANEL_SET_TEXT(frame, name, text)
	local ctrl = GET_CHILD_RECURSIVELY(frame, name, "ui::CControl")
	if ctrl ~= nil then
		SSGM_PANEL_CAST(ctrl)
		SSGM_PANEL_TRY(function() ctrl:SetText(text or "") end)
	end
end

function SSGM_MAIL_ADD_CLOSE(frame, ctrl, argStr, argNum)
	ui.CloseFrame("ssgm_mail_add_item")
end

function SSGM_MAIL_ADD_APPLY(frame, ctrl, argStr, argNum)
	local panel = ui.GetFrame(SSGM_PANEL_FRAME)
	local addFrame = ui.GetFrame("ssgm_mail_add_item")
	if panel == nil or addFrame == nil or SSGM_MAIL_ADD_IDX == nil then
		return
	end

	local item = SSGM_PANEL_GET_TEXT(addFrame, "MailAddItem")
	local amount = SSGM_PANEL_GET_TEXT(addFrame, "MailAddAmount")
	if item == "" or amount == "" then
		ui.SysMsg("Preencha item e quantidade.")
		return
	end

	local idx = tostring(SSGM_MAIL_ADD_IDX)
	local itemName = "SsgmInput_" .. idx .. "_1"
	local amountName = "SsgmInput_" .. idx .. "_2"
	local currentItem = SSGM_PANEL_GET_TEXT(panel, itemName)
	local currentAmount = SSGM_PANEL_GET_TEXT(panel, amountName)
	local pair = item .. ":" .. amount

	if currentItem ~= "" and string.find(currentItem, "[:xX,;]") == nil and currentAmount ~= "" then
		currentItem = currentItem .. ":" .. currentAmount
	end

	if currentItem == "" then
		currentItem = pair
	else
		currentItem = currentItem .. "," .. pair
	end

	SSGM_PANEL_SET_TEXT(panel, itemName, currentItem)
	SSGM_PANEL_SET_TEXT(panel, amountName, "1")
	ui.CloseFrame("ssgm_mail_add_item")
end

function SSGM_MAIL_ADD_OPEN(frame, ctrl, argStr, argNum)
	SSGM_MAIL_ADD_IDX = argNum
	local addFrame = ui.GetFrame("ssgm_mail_add_item")
	if addFrame == nil then
		addFrame = ui.CreateNewFrame("postbox_itemget", "ssgm_mail_add_item")
	end
	if addFrame == nil then
		ui.SysMsg("Nao foi possivel abrir adicionar item.")
		return
	end

	SSGM_PANEL_CAST(addFrame)
	addFrame:ShowWindow(0)
	addFrame:RemoveAllChild()
	addFrame:Resize(285, 170)
	addFrame:SetOffset(380, 220)
	addFrame:EnableHittestFrame(1)
	addFrame:EnableMove(1)
	addFrame:ShowTitleBar(0)
	SSGM_PANEL_SKIN(addFrame, "test_frame_low")

	SSGM_PANEL_TITLE_TEXT(addFrame, "MailAddTitle", 0, 14, 285, 26, "{@st42}{s18}Adicionar Item{/}")
	SSGM_PANEL_INPUT(addFrame, "MailAddItem", 24, 48, 106, "item")
	SSGM_PANEL_INPUT(addFrame, "MailAddAmount", 146, 48, 106, "quantidade")
	SSGM_PANEL_BUTTON(addFrame, "MailAddOk", 58, 118, 82, 30, "{@st41b}Adicionar{/}", "SSGM_MAIL_ADD_APPLY")
	SSGM_PANEL_BUTTON(addFrame, "MailAddCancel", 150, 118, 76, 30, "{@st41b}Fechar{/}", "SSGM_MAIL_ADD_CLOSE")
	addFrame:ShowWindow(1)
end

function SSGM_MAIL_APPEND_ITEMS(parts, itemText, amountText)
	itemText = SSGM_PANEL_TRIM(itemText)
	amountText = SSGM_PANEL_TRIM(amountText)

	if itemText == "" then
		return
	end

	if string.find(itemText, "[:xX,;]") ~= nil then
		for entry in string.gmatch(itemText, "[^,;]+") do
			local item, amount = string.match(entry, "^%s*([^:xX]+)[:xX]([^:xX]+)%s*$")
			if item ~= nil and amount ~= nil then
				table.insert(parts, SSGM_PANEL_TRIM(item))
				table.insert(parts, SSGM_PANEL_TRIM(amount))
			end
		end
	else
		table.insert(parts, itemText)
		if amountText ~= "" then
			table.insert(parts, amountText)
		end
	end
end

function SSGM_PANEL_BUILD_COMMAND(frame, idx)
	local data = SSGM_PANEL_COMMANDS[idx]
	if data == nil then
		return ""
	end

	local parts = { "/" .. data.cmd }
	local values = {}

	for i = 1, #data.fields do
		values[i] = SSGM_PANEL_GET_TEXT(frame, "SsgmInput_" .. tostring(idx) .. "_" .. tostring(i))
	end

	if data.mail == true then
		SSGM_MAIL_APPEND_ITEMS(parts, values[1], values[2])
		for i = 3, 4 do
			if values[i] ~= nil and values[i] ~= "" then
				table.insert(parts, values[i])
			end
		end
		table.insert(parts, "|")
		if values[5] ~= nil and values[5] ~= "" then
			table.insert(parts, values[5])
		end
	elseif data.mailall == true then
		SSGM_MAIL_APPEND_ITEMS(parts, values[1], values[2])
		if values[3] ~= nil and values[3] ~= "" then
			table.insert(parts, values[3])
		end
		table.insert(parts, "|")
		if values[4] ~= nil and values[4] ~= "" then
			table.insert(parts, values[4])
		end
		if values[5] ~= nil and values[5] ~= "" then
			table.insert(parts, values[5])
		end
	else
		for i = 1, #values do
			if values[i] ~= "" then
				table.insert(parts, values[i])
			end
		end
	end

	return table.concat(parts, " ")
end

function SSGM_PANEL_CLEAR_FIELDS(frame, idx)
	local data = SSGM_PANEL_COMMANDS[idx]
	if data == nil then
		return
	end

	for i = 1, #data.fields do
		local ctrl = GET_CHILD_RECURSIVELY(frame, "SsgmInput_" .. tostring(idx) .. "_" .. tostring(i), "ui::CControl")
		if ctrl ~= nil then
			SSGM_PANEL_CAST(ctrl)
			SSGM_PANEL_TRY(function() ctrl:SetText("") end)
		end
	end
end

function SSGM_PANEL_RUN(frame, ctrl, argStr, argNum)
	local panel = ui.GetFrame(SSGM_PANEL_FRAME)
	if panel == nil then
		panel = frame
	end

	local command = SSGM_PANEL_BUILD_COMMAND(panel, argNum)
	if command == "" then
		return
	end

	ui.Chat(command)
	SSGM_PANEL_CLEAR_FIELDS(panel, argNum)
end

function SSGM_PANEL_CLOSE(frame, ctrl, argStr, argNum)
	ui.CloseFrame(SSGM_PANEL_FRAME)
end

function SSGM_PANEL_SHOW_CATEGORY(frame, ctrl, argStr, argNum)
	SSGM_PANEL_CATEGORY = argStr
	local panel = ui.GetFrame(SSGM_PANEL_FRAME)
	if panel ~= nil then
		SSGM_PANEL_DRAW(panel)
	end
end

function SSGM_PANEL_CATEGORY_BUTTONS(frame)
	for i = 1, #SSGM_PANEL_CATEGORIES do
		local cat = SSGM_PANEL_CATEGORIES[i]
		local btn = SSGM_PANEL_BUTTON(frame, "SsgmCat_" .. cat.id, 18 + ((i - 1) * 145), 74, 136, 34, "{@st41b}" .. cat.text .. "{/}", "SSGM_PANEL_SHOW_CATEGORY")
		btn:SetEventScriptArgString(ui.LBUTTONUP, cat.id)

		if cat.id == SSGM_PANEL_CATEGORY then
			SSGM_PANEL_SKIN(btn, "test_red_button")
		end
	end
end

function SSGM_JOBLIST_CLOSE(frame, ctrl, argStr, argNum)
	ui.CloseFrame("ssgm_job_list")
end

function SSGM_JOBLIST_PAGE(frame, ctrl, argStr, argNum)
	SSGM_JOB_LIST_PAGE = argNum
	SSGM_JOBLIST_DRAW()
end

function SSGM_JOBLIST_SEARCH(frame, ctrl, argStr, argNum)
	local jobFrame = ui.GetFrame("ssgm_job_list")
	if jobFrame == nil then
		return
	end

	SSGM_JOB_SEARCH_TEXT = SSGM_PANEL_GET_TEXT(jobFrame, "JobSearchInput")
	SSGM_JOB_LIST_PAGE = 1
	SSGM_JOBLIST_DRAW()
end

function SSGM_JOBLIST_CLEAR_SEARCH(frame, ctrl, argStr, argNum)
	SSGM_JOB_SEARCH_TEXT = ""
	SSGM_JOB_LIST_PAGE = 1
	SSGM_JOBLIST_DRAW()
end

function SSGM_JOBLIST_COLLECT()
	local query = SSGM_PANEL_LOWER(SSGM_JOB_SEARCH_TEXT)

	local found = {}
	for page = 1, #SSGM_JOB_LIST do
		local jobs = SSGM_JOB_LIST[page]
		for i = 1, #jobs do
			local job = jobs[i]
			local haystack = SSGM_PANEL_LOWER(tostring(job[1]) .. " " .. job[2] .. " " .. job[3] .. " " .. job[4])
			if query == "" or string.find(haystack, query, 1, true) ~= nil then
				table.insert(found, job)
			end
		end
	end

	if query == "" then
		local paged = {}
		local first = ((SSGM_JOB_LIST_PAGE or 1) - 1) * 20 + 1
		local last = first + 19
		for i = first, last do
			if found[i] ~= nil then
				table.insert(paged, found[i])
			end
		end
		return paged
	end

	return found
end

function SSGM_JOBLIST_DRAW()
	local frame = ui.GetFrame("ssgm_job_list")
	if frame == nil then
		frame = ui.CreateNewFrame("postbox_itemget", "ssgm_job_list")
	end

	if frame == nil then
		ui.SysMsg("Nao foi possivel abrir Job List.")
		return
	end

	SSGM_PANEL_CAST(frame)
	frame:ShowWindow(0)
	frame:RemoveAllChild()
	frame:Resize(620, 660)
	frame:SetOffset(185, 70)
	frame:EnableHittestFrame(1)
	frame:EnableMove(1)
	frame:ShowTitleBar(0)
	SSGM_PANEL_SKIN(frame, "test_frame_low")

	SSGM_PANEL_TITLE_TEXT(frame, "JobTitle", 0, 16, 620, 30, "{@st42}{s20}Job List{/}")
	SSGM_PANEL_TITLE_TEXT(frame, "JobSub", 0, 44, 620, 22, "{@st68}{s12}Id Das classes{/}")

	local close = SSGM_PANEL_BUTTON(frame, "JobClose", 520, 18, 76, 30, "{@st41b}Fechar{/}", "SSGM_JOBLIST_CLOSE")
	SSGM_PANEL_SKIN(close, "test_gray_button")

	SSGM_PANEL_TEXT(frame, "JobSearchLabel", 24, 108, 70, 18, "{@st41b}{s12}Buscar{/}", false)
	local search = frame:CreateOrGetControl("edit", "JobSearchInput", 82, 104, 250, 28)
	SSGM_PANEL_CAST(search)
	SSGM_PANEL_SKIN(search, "test_edit_4")
	SSGM_PANEL_TRY(function() search:SetFontName("white_16_ol") end)
	SSGM_PANEL_TRY(function() search:SetText(SSGM_JOB_SEARCH_TEXT or "") end)

	local searchBtn = SSGM_PANEL_BUTTON(frame, "JobSearchBtn", 342, 103, 82, 30, "{@st41b}{s12}Buscar{/}", "SSGM_JOBLIST_SEARCH")
	SSGM_PANEL_SKIN(searchBtn, "test_gray_button")
	local clearBtn = SSGM_PANEL_BUTTON(frame, "JobClearBtn", 432, 103, 76, 30, "{@st41b}{s12}Limpar{/}", "SSGM_JOBLIST_CLEAR_SEARCH")
	SSGM_PANEL_SKIN(clearBtn, "test_gray_button")

	local searching = SSGM_PANEL_LOWER(SSGM_JOB_SEARCH_TEXT) ~= ""
	for i = 1, 7 do
		local btn = SSGM_PANEL_BUTTON(frame, "JobPage_" .. tostring(i), 24 + ((i - 1) * 54), 76, 46, 26, "{@st41b}" .. tostring(i) .. "{/}", "SSGM_JOBLIST_PAGE")
		btn:SetEventScriptArgNumber(ui.LBUTTONUP, i)
		if i == SSGM_JOB_LIST_PAGE then
			SSGM_PANEL_SKIN(btn, "test_red_button")
		end
		if searching then
			btn:EnableHitTest(0)
		end
	end

	local jobs = SSGM_JOBLIST_COLLECT()
	local maxJobs = #jobs
	if maxJobs > 20 then
		maxJobs = 20
	end

	for i = 1, maxJobs do
		local job = jobs[i]
		local col = math.floor((i - 1) / 10)
		local row = (i - 1) % 10
		local x = 24 + (col * 292)
		local y = 146 + (row * 46)
		local box = frame:CreateOrGetControl("groupbox", "JobRow_" .. tostring(i), x, y, 276, 40)
		SSGM_PANEL_CAST(box)
		box:RemoveAllChild()
		SSGM_PANEL_SKIN(box, "chat_window")

		local pic = box:CreateOrGetControl("picture", "JobIcon_" .. tostring(i), 8, 8, 24, 24)
		SSGM_PANEL_CAST(pic)
		SSGM_PANEL_TRY(function() pic:SetImage(SSGM_JOB_ICON(job)) end)
		SSGM_PANEL_TRY(function() pic:SetEnableStretch(1) end)
		SSGM_PANEL_TRY(function() pic:Resize(24, 24) end)
		SSGM_PANEL_TRY(function() pic:SetTextTooltip("{@st59}" .. job[2] .. "{/}") end)

		SSGM_PANEL_TEXT(box, "JobName_" .. tostring(i), 40, 5, 158, 18, "{@st41b}{s12}" .. job[4] .. "{/}", false)
		SSGM_PANEL_TEXT(box, "JobId_" .. tostring(i), 204, 5, 64, 18, "{@st41b}{s12}" .. tostring(job[1]) .. "{/}", true)
		SSGM_PANEL_TEXT(box, "JobMeta_" .. tostring(i), 40, 21, 216, 14, "{@st68}{s10}" .. job[2] .. " - " .. job[3] .. "{/}", false)
	end

	if searching and #jobs == 0 then
		SSGM_PANEL_TEXT(frame, "JobNoSearch", 0, 170, 620, 30, "{@st68}{s14}Nenhuma classe encontrada{/}", true)
	elseif searching and #jobs > 20 then
		SSGM_PANEL_TEXT(frame, "JobManySearch", 0, 622, 620, 20, "{@st68}{s11}Mostrando os primeiros 20 resultados{/}", true)
	end

	frame:ShowWindow(1)
end

function SSGM_JOBLIST_OPEN(frame, ctrl, argStr, argNum)
	SSGM_JOB_LIST_PAGE = 1
	SSGM_JOB_SEARCH_TEXT = ""
	SSGM_JOBLIST_DRAW()
end

function SSGM_PANEL_ADD_COMMAND(parent, idx, x, y, w)
	local data = SSGM_PANEL_COMMANDS[idx]
	local row = parent:CreateOrGetControl("groupbox", "SsgmRow_" .. tostring(idx), x, y, w, 58)
	SSGM_PANEL_CAST(row)
	row:RemoveAllChild()
	SSGM_PANEL_SKIN(row, "chat_window")

	local buttonY = 15
	local buttonH = 32
	if data.cmd == "addjob" then
		buttonY = 25
		buttonH = 28
	end

	local btn = SSGM_PANEL_BUTTON(row, "SsgmButton_" .. tostring(idx), 8, buttonY, 108, buttonH, "{@st41b}" .. data.title .. "{/}", "SSGM_PANEL_RUN")
	SSGM_PANEL_SKIN(btn, "test_red_button")
	btn:SetEventScriptArgNumber(ui.LBUTTONUP, idx)
	SSGM_PANEL_TRY(function() btn:SetTextTooltip("{@st59}" .. (SSGM_PANEL_DESCRIPTIONS[data.cmd] or data.tip or data.title) .. "{/}") end)

	if data.cmd == "addjob" then
		local jobBtn = SSGM_PANEL_BUTTON(row, "SsgmJobList_" .. tostring(idx), 16, 3, 90, 18, "{@st41b}{s11}Job List{/}", "SSGM_JOBLIST_OPEN")
		SSGM_PANEL_SKIN(jobBtn, "test_gray_button")
		SSGM_PANEL_TRY(function() jobBtn:SetTextTooltip("{@st59}Abre lista de classes e IDs.{/}") end)
	end

	if data.mail == true or data.mailall == true then
		local plusBtn = SSGM_PANEL_BUTTON(row, "SsgmMailPlus_" .. tostring(idx), 104, 3, 22, 18, "{@st41b}{s13}+{/}", "SSGM_MAIL_ADD_OPEN")
		SSGM_PANEL_SKIN(plusBtn, "test_gray_button")
		plusBtn:SetEventScriptArgNumber(ui.LBUTTONUP, idx)
		SSGM_PANEL_TRY(function() plusBtn:SetTextTooltip("{@st59}Adiciona outro item ao mesmo mail.{/}") end)
	end

	if #data.fields == 0 then
		SSGM_PANEL_TEXT(row, "SsgmNoArgs_" .. tostring(idx), 126, 18, w - 136, 24, "{@st68}{s13}sem argumentos{/}", true)
		return
	end

	local fieldX = 124
	local gap = 5
	local fieldW = math.floor((w - fieldX - 10 - ((#data.fields - 1) * gap)) / #data.fields)
	if fieldW < 58 then
		fieldW = 58
	end

	for i = 1, #data.fields do
		SSGM_PANEL_INPUT(row, "SsgmInput_" .. tostring(idx) .. "_" .. tostring(i), fieldX + ((i - 1) * (fieldW + gap)), 7, fieldW, data.fields[i])
	end
end

function SSGM_PANEL_DRAW(frame)
	frame:RemoveAllChild()

	SSGM_PANEL_TITLE_TEXT(frame, "SsgmTitle", 0, 18, 780, 34, "{@st42}{s22}GM Panel{/}")
	SSGM_PANEL_TITLE_TEXT(frame, "SsgmHelp", 0, 48, 780, 20, "{@st68}{s12}Painel Exclusivo Para os Game Masters{/}")

	local close = SSGM_PANEL_BUTTON(frame, "SsgmClose", 675, 18, 82, 32, "{@st41b}Fechar{/}", "SSGM_PANEL_CLOSE")
	SSGM_PANEL_SKIN(close, "test_gray_button")

	SSGM_PANEL_CATEGORY_BUTTONS(frame)

	local body = frame:CreateOrGetControl("groupbox", "SsgmBody", 14, 116, 752, 584)
	SSGM_PANEL_CAST(body)
	body:RemoveAllChild()
	SSGM_PANEL_SKIN(body, "None")

	local list = {}
	for i = 1, #SSGM_PANEL_COMMANDS do
		if SSGM_PANEL_COMMANDS[i].cat == SSGM_PANEL_CATEGORY then
			table.insert(list, i)
		end
	end

	local columns = 2
	local colW = 366
	if SSGM_PANEL_CATEGORY == "items" then
		columns = 1
		colW = 724
	end

	local rowsPerCol = math.ceil(#list / columns)
	if rowsPerCol < 1 then
		rowsPerCol = 1
	end

	for i = 1, #list do
		local col = math.floor((i - 1) / rowsPerCol)
		local row = (i - 1) % rowsPerCol
		SSGM_PANEL_ADD_COMMAND(body, list[i], 8 + (col * (colW + 12)), 8 + (row * 64), colW)
	end

	SSGM_PANEL_LINK_TABS(frame)
end

function SSGM_PANEL_OPEN()
	local frame = ui.GetFrame(SSGM_PANEL_FRAME)
	if frame == nil then
		frame = ui.CreateNewFrame("postbox_itemget", SSGM_PANEL_FRAME)
	end

	if frame == nil then
		ui.SysMsg("Nao foi possivel criar a janela GM.")
		return
	end

	SSGM_PANEL_CAST(frame)
	frame:ShowWindow(0)
	frame:RemoveAllChild()
	SSGM_PANEL_TAB_ORDER = {}
	frame:Resize(780, 720)
	frame:SetOffset(70, 50)
	frame:EnableHittestFrame(1)
	frame:EnableMove(1)
	frame:ShowTitleBar(0)
	SSGM_PANEL_SKIN(frame, "test_frame_low")

	SSGM_PANEL_DRAW(frame)
	frame:ShowWindow(1)
end
