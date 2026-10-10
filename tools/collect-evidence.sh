#!/usr/bin/env bash
# Copy the mod's logs, the latest self-test report and screenshots, and the game's Player.log into
# ~/.cache/gta-skylines/evidence/collected-<UTC>/ for a manual playtest. Copies only.
#   tools/collect-evidence.sh
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=tools/lib-paths.sh
source "${ROOT:?}/tools/lib-paths.sh"
OUT="${EVIDENCE_ROOT:?}/collected-$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "${OUT:?}"
for f in "${MOD_LOG}" "${PLAYER_LOG}" "${SELFTEST_REPORT}" "${SELFTEST_REPORT_TXT}" "${LAUNCH_CFG}"; do
  if [ -f "${f}" ]; then cp "${f}" "${OUT:?}/"; else echo "not found: ${f}" >&2; fi
done
if [ -d "${SELFTEST_SHOTS}" ]; then cp -r "${SELFTEST_SHOTS}" "${OUT:?}/"; else echo "not found: ${SELFTEST_SHOTS}" >&2; fi
echo "copied to ${OUT}"
