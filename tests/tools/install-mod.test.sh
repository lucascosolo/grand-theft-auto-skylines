#!/usr/bin/env bash
# Shell test for tools/install-mod.sh. Uses only its own mktemp directory (XDG_DATA_HOME points into it);
# the real game folders are never read or written. Deletes nothing (the temp dir is left for the OS).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
INSTALL="${ROOT:?}/tools/install-mod.sh"
T="$(mktemp -d "${TMPDIR:-/tmp}/gta-install-test.XXXXXX")"
: "${T:?}"
export XDG_DATA_HOME="${T}/data"
export GTA_GAME_PROCESS="gta-nogame-x"
unset GTA_MOD_SRC
MODS="${XDG_DATA_HOME}/Colossal Order/Cities_Skylines/Addons/Mods/GtaSkylines"
CFG="${XDG_DATA_HOME}/Colossal Order/Cities_Skylines/GtaSkylines/launch.cfg"
SRC="${T}/build"
FAILS=0
ok() { echo "ok   $1"; }
bad() { echo "FAIL $1" >&2; FAILS=$((FAILS + 1)); }
expect_file() { if [ -f "$1" ]; then ok "exists ${1#"${T}"/}"; else bad "missing ${1#"${T}"/}"; fi; }
expect_cfg() { # expect_cfg "<expected non-comment lines>" label
  local got; got="$(grep -v '^#' "${CFG}")"
  if [ "${got}" = "$1" ]; then ok "$2"; else bad "$2: got [${got}]"; fi
}

# 1. missing build is refused and nothing is created
if bash "${INSTALL}" --src "${SRC}" >/dev/null 2>"${T}/err"; then bad "missing build accepted"; else ok "missing build refused"; fi
grep -q 'missing in' "${T}/err" && ok "refusal names the missing files" || bad "refusal message"
[ ! -e "${XDG_DATA_HOME}" ] && ok "nothing created on refusal" || bad "created folders on refusal"

# 2. fake build output
mkdir -p "${SRC}"
for f in GtaSkylines GtaSkylines.Game Skylines.Core Skylines.Host; do echo "dll ${f}" > "${SRC}/${f}.dll"; done
echo pdb > "${SRC}/GtaSkylines.pdb"
echo mdb > "${SRC}/Skylines.Host.dll.mdb"
echo other > "${SRC}/Unrelated.dll"

# 3. --check changes nothing
CHK="$(bash "${INSTALL}" --check --src "${SRC}")"
case "${CHK}" in *'would copy   GtaSkylines.dll'*) ok "--check lists files" ;; *) bad "--check output" ;; esac
[ ! -e "${XDG_DATA_HOME}" ] && ok "--check writes nothing" || bad "--check wrote"

# 4. default (normal)
bash "${INSTALL}" --src "${SRC}" >/dev/null
for f in GtaSkylines.dll GtaSkylines.Game.dll Skylines.Core.dll Skylines.Host.dll GtaSkylines.pdb Skylines.Host.dll.mdb; do expect_file "${MODS}/${f}"; done
[ ! -e "${MODS}/Unrelated.dll" ] && ok "unrelated file not copied" || bad "copied unrelated file"
cmp -s "${SRC}/GtaSkylines.dll" "${MODS}/GtaSkylines.dll" && ok "dll content identical" || bad "dll content differs"
expect_cfg $'selftest = off\nselftest_quit = false' "normal launch.cfg"

# 5. --selftest default map and named map
bash "${INSTALL}" --selftest --src "${SRC}" >/dev/null
expect_cfg $'selftest = city_load\nselftest_quit = true\nautoload = new:Green Plains' "selftest default map"
bash "${INSTALL}" --selftest "Grand River" --src "${SRC}" >/dev/null
expect_cfg $'selftest = city_load\nselftest_quit = true\nautoload = new:Grand River' "selftest named map"
ls "${CFG}.backup-"* >/dev/null 2>&1 && ok "changed launch.cfg was backed up" || bad "no launch.cfg backup"

# 6. --selftest-save
bash "${INSTALL}" --selftest-save "My Test City" --src "${SRC}" >/dev/null
expect_cfg $'selftest = city_load\nselftest_quit = true\nautoload = My Test City' "selftest-save"

# 7. back to normal, config only (no DLL copy needed)
bash "${INSTALL}" --normal --config-only --src "${T}/does-not-exist" >/dev/null
expect_cfg $'selftest = off\nselftest_quit = false' "normal after selftest"
grep -q '^autoload' "${CFG}" && bad "autoload line left in normal mode" || ok "no autoload in normal mode"

# 8. refuses to copy while the game runs (a stand-in process named like the game)
FAKE="${T}/gtafake"
cp "$(command -v bash)" "${FAKE}"
"${FAKE}" -c 'sleep 30; :' </dev/null &
FAKE_PID=$!
if GTA_GAME_PROCESS="gtafake" bash "${INSTALL}" --src "${SRC}" >/dev/null 2>"${T}/err2"; then bad "ran while game running"; else ok "refused while game running"; fi
kill "${FAKE_PID}" 2>/dev/null || true
wait "${FAKE_PID}" 2>/dev/null || true

if [ "${FAILS}" -gt 0 ]; then echo "${FAILS} failed" >&2; exit 1; fi
echo "install-mod tests passed"
