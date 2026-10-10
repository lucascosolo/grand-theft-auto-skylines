#!/usr/bin/env bash
# Everything that can be checked without the game: build (0 warnings) and every unit test.
# Reports land in reports/. Never runs a clean task and never deletes anything (project rule).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${ROOT:?}"
REPORTS="${ROOT:?}/reports"
mkdir -p "${REPORTS:?}/cs"
DN="${ROOT:?}/tools/dotnet.sh"

printf '\n== build (net35 + net10.0)\n'
"${DN}" build GtaSkylines.sln -c Release -m:1 -nologo -v quiet -warnaserror

printf '\n== unit tests\n'
"${DN}" test GtaSkylines.sln -c Release --no-build -m:1 -nologo \
  --logger "trx;LogFilePrefix=unit" --results-directory "${REPORTS}/cs" 2>&1 | grep -E 'Passed!|Failed!|error|Failed [A-Za-z]' || true
# The summary lines above are for reading; the exit status comes from the TRX files.
python3 - "${REPORTS}/cs" <<'PY'
import sys, os, glob, xml.etree.ElementTree as ET
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
files = sorted(glob.glob(os.path.join(sys.argv[1], "unit*.trx")), key=os.path.getmtime)
# newest report per test assembly
latest = {}
for f in files:
    try:
        root = ET.parse(f).getroot()
    except ET.ParseError:
        continue
    defs = root.find("t:TestDefinitions", ns)
    name = None
    if defs is not None:
        for d in defs:
            tm = d.find("t:TestMethod", ns)
            if tm is not None:
                name = os.path.basename(tm.get("codeBase", "?"))
                break
    latest[name or f] = root
total = failed = 0
for name, root in sorted(latest.items()):
    c = root.find("t:ResultSummary/t:Counters", ns)
    t, fl = int(c.get("total")), int(c.get("failed")) + int(c.get("error", "0"))
    total += t; failed += fl
    print(f"  {name}: {t} tests, {fl} failed")
print(f"  total {total}, failed {failed}")
sys.exit(1 if failed or total == 0 else 0)
PY
printf '\n== tool scripts (bash)\n'
for t in "${ROOT}"/tests/tools/*.test.sh; do
  bash -n "${t}"
  bash "${t}"
done
printf '\nAll checks passed.\n'
