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
export GTA_ARCHIVE_DIR="${T}/archive"
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
for f in GtaSkylines GtaSkylines.Game GtaSkylines.Core GtaSkylines.Host; do echo "dll ${f}" > "${SRC}/${f}.dll"; done
echo pdb > "${SRC}/GtaSkylines.pdb"
echo mdb > "${SRC}/GtaSkylines.Host.dll.mdb"
echo other > "${SRC}/Unrelated.dll"

# 3. --check changes nothing
CHK="$(bash "${INSTALL}" --check --src "${SRC}")"
case "${CHK}" in *'would copy   GtaSkylines.dll'*) ok "--check lists files" ;; *) bad "--check output" ;; esac
[ ! -e "${XDG_DATA_HOME}" ] && ok "--check writes nothing" || bad "--check wrote"

# 3b. stale files from an older install: --check reports them and moves nothing
mkdir -p "${MODS}"
for f in Skylines.Core.dll Skylines.Host.dll Skylines.Host.pdb; do echo "stale ${f}" > "${MODS}/${f}"; done
echo keep > "${MODS}/Keep.txt"
CHK2="$(bash "${INSTALL}" --check --src "${SRC}")"
case "${CHK2}" in *'would move stale'*) ok "--check reports stale files" ;; *) bad "--check stale output" ;; esac
for f in Skylines.Core.dll Skylines.Host.dll Skylines.Host.pdb; do expect_file "${MODS}/${f}"; done
[ ! -e "${GTA_ARCHIVE_DIR}" ] && ok "--check archives nothing" || bad "--check created archive"

# 4. default (normal)
INST="$(bash "${INSTALL}" --src "${SRC}")"
for f in Skylines.Core.dll Skylines.Host.dll Skylines.Host.pdb; do
  case "${INST}" in *"moved stale"*"${f}"*) ok "reports moved stale ${f}" ;; *) bad "no 'moved stale' line for ${f}" ;; esac
  [ ! -e "${MODS}/${f}" ] && ok "stale ${f} gone from mod folder" || bad "stale ${f} still installed"
  if [ -f "${GTA_ARCHIVE_DIR}/${f}" ] && [ "$(cat "${GTA_ARCHIVE_DIR}/${f}")" = "stale ${f}" ]; then ok "stale ${f} archived intact"; else bad "stale ${f} not archived intact"; fi
done
[ "$(cat "${MODS}/Keep.txt" 2>/dev/null)" = "keep" ] && ok "Keep.txt untouched" || bad "Keep.txt changed"
for f in GtaSkylines.dll GtaSkylines.Game.dll GtaSkylines.Core.dll GtaSkylines.Host.dll GtaSkylines.pdb GtaSkylines.Host.dll.mdb; do expect_file "${MODS}/${f}"; done
for f in Skylines.Core.dll Skylines.Host.dll; do [ ! -e "${MODS}/${f}" ] && ok "no old ${f} in mod folder" || bad "old ${f} present"; done
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
