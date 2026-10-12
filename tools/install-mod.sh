#!/usr/bin/env bash
# Copy the built Release net35 mod into the CS1 local mods folder and write the launch config.
# Builds nothing (run tools/check.sh first). Copies only; deletes nothing; touches no other mod, no
# game install and no save. To uninstall, move the GtaSkylines folder away.
#
#   tools/install-mod.sh [--normal]                    install, launch.cfg for normal play (default)
#   tools/install-mod.sh --selftest [<map>]            install, unattended self-test on a new city (default map: Green Plains)
#   tools/install-mod.sh --selftest-save "<save>"      install, unattended self-test on that existing save
#   tools/install-mod.sh --check                       print what would be copied and written; change nothing
#   options: --src <dir>      build output (default src/GtaSkylines.Mod/bin/Release/net35, or $GTA_MOD_SRC)
#            --config-only    write launch.cfg only, copy no DLLs (used by selftest.sh to restore normal play)
#
# launch.cfg (in the mod data folder, see tools/lib-paths.sh; key = value, # comment) keys read by the mod:
#   selftest       = off | city_load      run the self-test once after each city load
#   selftest_quit  = true | false         quit the game after the self-test report is written
#   autoload       = new:<map> | <save>   from the main menu, start a new game on <map> or load <save>; absent = off
# --normal writes selftest = off, selftest_quit = false and no autoload line.
# An existing, different launch.cfg is first copied to launch.cfg.backup-<UTC>.
# Copies are refused (exit 1) while the game process (Cities.x64) runs. Exit: 0 ok, 1 refused/missing build, 2 usage.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=tools/lib-paths.sh
source "${ROOT:?}/tools/lib-paths.sh"

MODE=normal
MAP=""
SAVE=""
CHECK=0
CONFIG_ONLY=0
SRC="${GTA_MOD_SRC:-${ROOT:?}/src/GtaSkylines.Mod/bin/Release/net35}"
while [ $# -gt 0 ]; do
  case "$1" in
    --normal) MODE=normal ;;
    --selftest)
      MODE=selftest
      if [ $# -ge 2 ] && [ "${2#--}" = "$2" ]; then MAP="$2"; shift; fi ;;
    --selftest-save)
      [ $# -ge 2 ] || { echo "--selftest-save needs a save name" >&2; exit 2; }
      MODE=save; SAVE="$2"; shift ;;
    --check) CHECK=1 ;;
    --config-only) CONFIG_ONLY=1 ;;
    --src)
      [ $# -ge 2 ] || { echo "--src needs a folder" >&2; exit 2; }
      SRC="$2"; shift ;;
    *) echo "unknown argument: $1 (see the header of $0)" >&2; exit 2 ;;
  esac
  shift
done
[ "${MODE}" != selftest ] || MAP="${MAP:-Green Plains}"

REQUIRED=(GtaSkylines.dll GtaSkylines.Game.dll GtaSkylines.Core.dll GtaSkylines.Host.dll)
# Older installs shipped the shared libraries as Skylines.Core/Skylines.Host, the same names the MinecraftSkylines
# mod uses; CS1 then binds one mod to the other's copy. Such leftovers are moved (never deleted) to ARCHIVE_DIR.
STALE_NAMES=(Skylines.Core.dll Skylines.Core.pdb Skylines.Core.xml Skylines.Core.dll.mdb
             Skylines.Host.dll Skylines.Host.pdb Skylines.Host.xml Skylines.Host.dll.mdb)
ARCHIVE_DIR="${GTA_ARCHIVE_DIR:-${HOME:?}/.archive/$(date +%Y-%m-%d)/gta-skylines-stale-install}"
STALE=()
for f in "${STALE_NAMES[@]}"; do
  if [ -f "${MOD_DIR:?}/${f}" ]; then STALE+=("${f}"); fi
done
FILES=()
if [ "${CONFIG_ONLY}" = 0 ]; then
  MISSING=()
  for f in "${REQUIRED[@]}"; do
    [ -f "${SRC:?}/${f}" ] || MISSING+=("${f}")
  done
  if [ "${#MISSING[@]}" -gt 0 ]; then
    echo "missing in ${SRC}: ${MISSING[*]}; build first with tools/check.sh" >&2
    exit 1
  fi
  for f in "${REQUIRED[@]}"; do
    FILES+=("${f}")
    for ext in pdb mdb; do
      if [ -f "${SRC:?}/${f%.dll}.${ext}" ]; then FILES+=("${f%.dll}.${ext}"); fi
      if [ -f "${SRC:?}/${f}.${ext}" ]; then FILES+=("${f}.${ext}"); fi
    done
  done
fi

case "${MODE}" in
  normal) CFG=$'selftest = off\nselftest_quit = false' ;;
  selftest) CFG=$'selftest = city_load\nselftest_quit = true\nautoload = new:'"${MAP}" ;;
  save) CFG=$'selftest = city_load\nselftest_quit = true\nautoload = '"${SAVE}" ;;
esac
CFG=$'# written by tools/install-mod.sh\n'"${CFG}"

if [ "${CHECK}" = 1 ]; then
  echo "source:      ${SRC}"
  echo "destination: ${MOD_DIR}"
  for f in "${FILES[@]}"; do echo "would copy   ${f}"; done
  if [ "${CONFIG_ONLY}" = 0 ]; then
    for f in "${STALE[@]}"; do echo "would move stale ${f} to ${ARCHIVE_DIR}"; done
  fi
  echo "would write  ${LAUNCH_CFG} (mode ${MODE}):"
  printf '%s\n' "${CFG}" | sed 's/^/    /'
  exit 0
fi

if [ "${CONFIG_ONLY}" = 0 ] && pgrep -x "${GAME_PROCESS:?}" >/dev/null; then
  echo "${GAME_PROCESS} is running; quit the game first. Nothing was changed." >&2
  exit 1
fi

if [ "${CONFIG_ONLY}" = 0 ]; then
  mkdir -p "${MOD_DIR:?}"
  for f in "${FILES[@]}"; do
    cp -f "${SRC:?}/${f}" "${MOD_DIR:?}/${f}"
    echo "installed ${f}  sha256=$(sha256sum "${MOD_DIR:?}/${f}" | cut -d' ' -f1)"
  done
  if [ "${#STALE[@]}" -gt 0 ]; then
    mkdir -p "${ARCHIVE_DIR:?}"
    for f in "${STALE[@]}"; do
      mv -f "${MOD_DIR:?}/${f}" "${ARCHIVE_DIR:?}/${f}"
      echo "moved stale ${f} to ${ARCHIVE_DIR}"
    done
  fi
fi

mkdir -p "${MOD_DATA:?}"
if [ -f "${LAUNCH_CFG:?}" ] && [ "$(cat "${LAUNCH_CFG:?}")" != "${CFG}" ]; then
  BACKUP="${LAUNCH_CFG:?}.backup-$(date -u +%Y%m%dT%H%M%SZ)"
  cp -p "${LAUNCH_CFG:?}" "${BACKUP:?}"
  echo "old launch.cfg kept as ${BACKUP}"
fi
printf '%s\n' "${CFG}" > "${LAUNCH_CFG:?}"
echo "wrote ${LAUNCH_CFG} (mode ${MODE})"
echo "Mod folder: ${MOD_DIR}"
