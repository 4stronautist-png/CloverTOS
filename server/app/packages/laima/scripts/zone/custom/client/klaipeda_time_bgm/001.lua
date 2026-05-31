SS_MAP_BGM = SS_MAP_BGM or { track = "" }

local map_tracks = {
	"April_Town",
	"Night_Paradise",
	"SFA_April_Town",
	"SFA_Night_Paradise",
	"SFA_Whisper_of_Moment",
	"SFA_Openup_Po10",
	"SFA_Open_Up_Po10_inst",
	"SoundTeMP_Village_School",
	"SoundTeMP_Village_school",
	"Village_school",
	"tos_SoundTeMP_Village_School",
	"SFA_Klaipeda_Tavern_2",
	"c_request_1",
	"tos_c_request_1",
	"Intium_Arquebuiser_Lyudmila(short)",
	"Initium_Arquebusier_Lyudmila(short)",
	"tos_SoundTeMP_Village_School.mp3",
	"SoundTeMP_Village_school.mp3",
	"Intium_Arquebuiser_Lyudmila(short).mp3",
	"tos_Initium_Arquebusier_Lyudmila(short).mp3",
}

local function stop_track(name)
	pcall(function() StopBgm(name) end)
	pcall(function() StopBgm(name, 1) end)
	pcall(function() StopMusic(name) end)
	pcall(function() imcSound.StopMusic(name) end)
end

local function stop_map_tracks()
	pcall(function() StopMusic() end)
	pcall(function() imcSound.StopMusic() end)
	pcall(function() imcSound.StopBGM() end)
	for _, name in ipairs(map_tracks) do
		stop_track(name)
	end
end

local function play_track(track_key, names)
	stop_map_tracks()
	SS_MAP_BGM.track = track_key

	for _, name in ipairs(names) do
		pcall(function() PlayBgm(name, 1) end)
		pcall(function() PlayMusic(name, 1) end)
		pcall(function() imcSound.PlayBGM(name) end)
		pcall(function() imcSound.PlayMusic(name) end)
	end
end

function SS_FORCE_MAP_BGM(track_key)
	if track_key == "klaipeda_day" then
		play_track(track_key, {
			"April_Town",
			"SFA_April_Town",
			"SFA_April_Town.mp3",
		})
	elseif track_key == "klaipeda_night" then
		play_track(track_key, {
			"Night_Paradise",
			"SFA_Night_Paradise",
			"SFA_Night_Paradise.mp3",
		})
	elseif track_key == "klaipeda_tavern" then
		play_track(track_key, {
			"SoundTeMP_Village_School",
			"SoundTeMP_Village_school",
			"tos_SoundTeMP_Village_School",
			"tos_SoundTeMP_Village_School.mp3",
			"SFA_Klaipeda_Tavern_2",
			"c_request_1",
			"c_request_1.mp3",
			"tos_c_request_1",
			"tos_c_request_1.mp3",
		})
	else
		SS_MAP_BGM.track = ""
	end
end

function SS_RELEASE_MAP_BGM()
	SS_MAP_BGM.track = ""
end
