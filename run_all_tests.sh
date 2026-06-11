#!/usr/bin/env bash
# Riptide test gate (master prompt 0B): core-purity grep guard + dotnet test shim.
# Run from anywhere; exits non-zero if any gate fails.
set -euo pipefail
cd "$(dirname "$0")"

echo "== [1/3] Core purity guard =="
# Banned in pure-sim sources (master prompt rule 4 / GDD 8.3).
# DateTime.UtcNow is banned beyond the literal contract -- docs/DECISIONS.md 2026-06-11.
PATTERN='UnityEngine|UnityEditor|System\.Random|DateTime\.(Utc)?Now'
violations=0
for dir in "Assets/Scripts/Core" "Assets/Tests/Core"; do
  if matches=$(grep -RInE --include='*.cs' "$PATTERN" "$dir"); then
    echo "PURITY VIOLATION in $dir:"
    echo "$matches"
    violations=1
  fi
done
if [ "$violations" -ne 0 ]; then
  echo "RESULT: PURITY GUARD FAILED"
  exit 1
fi
echo "purity guard OK: no banned references in Core or Core.Tests sources"

echo "== [2/3] dotnet test (Tools/CoreTests shim) =="
dotnet test Tools/CoreTests/CoreTests.csproj --nologo -v minimal

echo "== [3/3] content fixtures (Tools/ContentCheck) =="
dotnet run --project Tools/ContentCheck -c Release -v q

echo "RESULT: ALL GATES GREEN"
