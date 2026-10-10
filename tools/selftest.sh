#!/usr/bin/env bash
# Unattended in-game self-test (run it in a terminal; the game window opens on the desktop):
#
#   tools/selftest.sh                       new city on the default map (Green Plains), nothing saved
#   tools/selftest.sh --new-game "<map>"    new city on that map
#   tools/selftest.sh --save "<save name>"  load that save instead (as the Load panel lists it)
#
# Installs the mod with tools/install-mod.sh --selftest (launch.cfg: selftest = city_load,
# selftest_quit = true, autoload = new:<map> | <save>), starts the game, waits for the report
# (SELFTEST_REPORT in tools/lib-paths.sh = <mod data folder>/selftest-report.json, only counted when
# written after the start), copies report, screenshots and logs to
# ~/.cache/gta-skylines/evidence/selftest-<UTC>/ and puts launch.cfg back to normal play. Never kills a
# process and never deletes anything.
#
# Exit codes: 0 report found; 1 refused (game already running) or install failed; 2 game did not start;
# 3 timeout (no report in time, or the game exited without one); 4 bad usage.
# Env: GTA_LAUNCH=direct (default: starts the game binary) | steam (steam -applaunch 255710, opens the
# Paradox Launcher which needs a click); GTA_GAME_DIR (install folder; default cs1_install from
# ~/.cache/gta-skylines/refs/environment.txt); GTA_MOD_SRC (build output); GTA_GAME_PROCESS (Cities.x64);
# GTA_STEAM_CMD; GTA_POLL_SECONDS (10); GTA_TIMEOUT_SECONDS (2400); GTA_START_SECONDS (180);
# GTA_EXIT_WAIT_SECONDS (120).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=tools/lib-paths.sh
source "${ROOT:?}/tools/lib-paths.sh"
INSTALL="${ROOT:?}/tools/install-mod.sh"
POLL="${GTA_POLL_SECONDS:-10}"
TIMEOUT="${GTA_TIMEOUT_SECONDS:-2400}"
START_WINDOW="${GTA_START_SECONDS:-180}"
EXIT_WAIT="${GTA_EXIT_WAIT_SECONDS:-120}"
LAUNCH="${GTA_LAUNCH:-direct}"

MAP=""
SAVE=""
while [ $# -gt 0 ]; do
  case "$1" in
    --help|-h) sed -n '2,/^set -euo/p' "$0" | sed '$d'; exit 0 ;;
    --new-game) [ $# -ge 2 ] || { echo "--new-game needs a map name" >&2; exit 4; }; MAP="$2"; shift ;;
    --save) [ $# -ge 2 ] || { echo "--save needs a save name" >&2; exit 4; }; SAVE="$2"; shift ;;
    *) echo "unknown argument: $1" >&2; exit 4 ;;
  esac
  shift
done
if [ -n "${MAP}" ] && [ -n "${SAVE}" ]; then echo "use --new-game or --save, not both" >&2; exit 4; fi

if pgrep -x "${GAME_PROCESS:?}" >/dev/null; then
  echo "${GAME_PROCESS} is already running; quit it first. Nothing was changed." >&2
  exit 1
fi

if [ -n "${GTA_STEAM_CMD:-}" ]; then
  read -r -a STEAM <<< "${GTA_STEAM_CMD}"
elif command -v steam >/dev/null; then
  STEAM=(steam)
else
  echo "steam not found on PATH" >&2; exit 2
fi
if [ -z "${GTA_STEAM_CMD:-}" ] && ! pgrep -x steam >/dev/null; then
  echo "Steam is not running; start it (logged in) in the desktop session first." >&2
  exit 2
fi

INSTALL_ARGS=()
if [ -n "${SAVE}" ]; then
  INSTALL_ARGS+=(--selftest-save "${SAVE}")
else
  INSTALL_ARGS+=(--selftest)
  if [ -n "${MAP}" ]; then INSTALL_ARGS+=("${MAP}"); fi
fi
if [ -n "${GTA_MOD_SRC:-}" ]; then INSTALL_ARGS+=(--src "${GTA_MOD_SRC}"); fi
echo "== installing the mod: ${INSTALL_ARGS[*]}"
bash "${INSTALL:?}" "${INSTALL_ARGS[@]}" || { echo "install failed" >&2; exit 1; }

EVIDENCE=""
copy_evidence() {
  [ -z "${EVIDENCE}" ] || return 0
  EVIDENCE="${EVIDENCE_ROOT:?}/selftest-$(date -u +%Y%m%dT%H%M%SZ)"
  mkdir -p "${EVIDENCE:?}"
  local f
  for f in "${SELFTEST_REPORT}" "${SELFTEST_REPORT_TXT}" "${MOD_LOG}" "${PLAYER_LOG}" "${LAUNCH_CFG}"; do
    if [ -f "${f}" ]; then cp "${f}" "${EVIDENCE:?}/"; fi
  done
  if [ -d "${SELFTEST_SHOTS}" ]; then cp -r "${SELFTEST_SHOTS}" "${EVIDENCE:?}/"; fi
  echo "Evidence: ${EVIDENCE}" >&2
}
LAUNCHED=0
restore() {
  local rc=$?
  if [ "${LAUNCHED}" = 1 ]; then copy_evidence; fi
  echo "== launch.cfg back to normal play"
  bash "${INSTALL:?}" --normal --config-only || echo "WARNING: could not reset launch.cfg; run: bash ${INSTALL} --normal" >&2
  exit "${rc}"
}
trap restore EXIT

START="$(date +%s)"
fresh_report() {
  [ -f "${SELFTEST_REPORT:?}" ] && [ "$(stat -c %Y "${SELFTEST_REPORT:?}")" -ge "${START}" ]
}

LAUNCHED=1
if [ "${LAUNCH}" = direct ]; then
  GAME_DIR="${GTA_GAME_DIR:-$(sed -n 's/^cs1_install: //p' "${HOME:?}/.cache/gta-skylines/refs/environment.txt" 2>/dev/null | head -1)}"
  GAME_DIR="${GAME_DIR:-${HOME:?}/.steam/debian-installation/steamapps/common/Cities_Skylines}"
  if [ ! -x "${GAME_DIR:?}/${GAME_PROCESS}" ]; then
    echo "game binary not found: ${GAME_DIR}/${GAME_PROCESS} (set GTA_GAME_DIR)" >&2
    exit 2
  fi
  GAME_OUT="${EVIDENCE_ROOT:?}/game-stdout-$(date -u +%Y%m%dT%H%M%SZ).log"
  mkdir -p "${EVIDENCE_ROOT:?}"
  echo "== $(date -u +%FT%TZ) starting ${GAME_DIR}/${GAME_PROCESS} directly (output: ${GAME_OUT})"
  (cd "${GAME_DIR:?}" && SteamAppId=255710 SteamGameId=255710 setsid -f "./${GAME_PROCESS}" > "${GAME_OUT:?}" 2>&1 < /dev/null) \
    || { echo "could not start ${GAME_PROCESS}" >&2; exit 2; }
else
  echo "== $(date -u +%FT%TZ) steam -applaunch 255710"
  "${STEAM[@]}" -applaunch 255710 >/dev/null 2>&1 || { echo "steam command failed" >&2; exit 2; }
fi

SEEN=0
while ! fresh_report; do
  ELAPSED=$(( $(date +%s) - START ))
  if pgrep -x "${GAME_PROCESS}" >/dev/null; then UP=yes; SEEN=1; else UP=no; fi
  STATUS=""
  if [ -f "${MOD_LOG}" ]; then STATUS="$(grep -i 'self-test\|autoload' "${MOD_LOG}" | tail -1 || true)"; fi
  echo "[${ELAPSED}s] game ${UP}; ${STATUS:-no self-test line yet}"
  if [ "${SEEN}" = 0 ] && [ "${ELAPSED}" -ge "${START_WINDOW}" ]; then
    echo "the game did not start within ${START_WINDOW} s" >&2; exit 2
  fi
  if [ "${SEEN}" = 1 ] && [ "${UP}" = no ]; then
    sleep 2
    if fresh_report; then break; fi
    echo "the game exited without writing a self-test report" >&2; exit 3
  fi
  if [ "${ELAPSED}" -ge "${TIMEOUT}" ]; then
    echo "no report after ${TIMEOUT} s; the game is left running (nothing is killed)" >&2; exit 3
  fi
  sleep "${POLL}"
done
echo "== report: ${SELFTEST_REPORT}"

WAITED=0
while pgrep -x "${GAME_PROCESS}" >/dev/null && [ "${WAITED}" -lt "${EXIT_WAIT}" ]; do
  sleep "${POLL}"; WAITED=$((WAITED + POLL))
done
if pgrep -x "${GAME_PROCESS}" >/dev/null; then
  echo "WARNING: the game is still running after ${EXIT_WAIT} s; copying evidence anyway" >&2
fi

copy_evidence
python3 - "${SELFTEST_REPORT}" <<'PY' || true
import json, sys
r = json.load(open(sys.argv[1]))
s = r.get("summary", {})
print("Self-test: %s pass, %s fail, %s skip, %s error" % tuple(s.get(k, "?") for k in ("pass", "fail", "skip", "error")))
PY
