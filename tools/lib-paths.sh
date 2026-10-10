# Sourced by install-mod.sh, selftest.sh and collect-evidence.sh: the one place the mod's paths are defined.
# Paths follow ColossalFramework.IO.DataLocation on Linux; the data folder matches ModRuntime.DataFolder.
# Sourcing this file changes and creates nothing.
XDG_DATA="${XDG_DATA_HOME:-${HOME:?}/.local/share}"
CS1_APPDATA="${XDG_DATA:?}/Colossal Order/Cities_Skylines"
# Where the DLLs go (CS1 local mods folder).
MOD_DIR="${CS1_APPDATA:?}/Addons/Mods/GtaSkylines"
# The mod's data folder (<DataLocation.localApplicationData>/GtaSkylines): launch.cfg in, self-test output out.
MOD_DATA="${CS1_APPDATA:?}/GtaSkylines"
LAUNCH_CFG="${MOD_DATA:?}/launch.cfg"
# Written by the in-game self-test (M6b). Text twin: selftest-report.txt. Screenshots: selftest-screenshots/*.png.
SELFTEST_REPORT="${MOD_DATA:?}/selftest-report.json"
SELFTEST_REPORT_TXT="${MOD_DATA:?}/selftest-report.txt"
SELFTEST_SHOTS="${MOD_DATA:?}/selftest-screenshots"
MOD_LOG="${CS1_APPDATA:?}/ModLogs/GtaSkylines.log"
PLAYER_LOG="${XDG_CONFIG_HOME:-${HOME:?}/.config}/unity3d/Colossal Order/Cities_ Skylines/Player.log"
EVIDENCE_ROOT="${HOME:?}/.cache/gta-skylines/evidence"
GAME_PROCESS="${GTA_GAME_PROCESS:-Cities.x64}"
