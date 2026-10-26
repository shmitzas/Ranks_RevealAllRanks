#!/usr/bin/env bash
# Runs the release workflow's own version + changelog parsing against the real files,
# so a quoting or path mistake surfaces here rather than on the one push that releases.
# Also catches the two claims nothing else would: a missing changelog section, and the
# hand-written SwiftlyS2 badge drifting away from the package actually built against.
set -euo pipefail
cd "$(dirname "$0")/.."

MAIN_CS="src/Ranks_RevealAllRanks.cs"
VERSION=$(sed -n 's/.*Version[[:space:]]*=[[:space:]]*"\([^"]*\)".*/\1/p' "$MAIN_CS" | head -1)
[ -n "$VERSION" ] || { echo "FAIL: no version in $MAIN_CS"; exit 1; }
echo "version       : $VERSION"

awk -v ver="$VERSION" '
  /^## \[/ {
    if (found) exit
    if ($0 ~ "\\[v?" ver "\\]") { found = 1; next }
  }
  found { print }
' CHANGELOG.md > /tmp/release-notes.md

[ -s /tmp/release-notes.md ] || { echo "FAIL: no '## [$VERSION]' section in CHANGELOG.md"; exit 1; }
echo "notes lines   : $(wc -l < /tmp/release-notes.md)"

# The SwiftlyS2 badge is static while the csproj floats "*-*", so it is the one claim in the
# README that can rot without anything failing. shields.io escapes a literal dash as "--".
if [ -f obj/project.assets.json ]; then
  RESOLVED=$(grep -o '"SwiftlyS2\.CS2/[^"]*"' obj/project.assets.json | head -1 | sed 's|.*/||; s|"$||')
  BADGE_RAW=$(sed -n 's|.*img\.shields\.io/badge/SwiftlyS2-\(.*\)-blue.*|\1|p' README.md | head -1)
  BADGE=${BADGE_RAW//--/-}

  [ -n "$BADGE" ] || { echo "FAIL: no SwiftlyS2 badge found in README.md"; exit 1; }

  if [ "$BADGE" != "$RESOLVED" ]; then
    echo "FAIL: README badge says SwiftlyS2 $BADGE but the build resolved $RESOLVED"
    exit 1
  fi
  echo "sw2 badge     : $BADGE (matches resolved package)"
else
  echo "sw2 badge     : skipped - run dotnet restore first"
fi

PLUGIN_ZIP="build/Ranks_RevealAllRanks-v${VERSION}.zip"
[ -f "$PLUGIN_ZIP" ] || { echo "FAIL: $PLUGIN_ZIP missing - run dotnet publish -c Release first"; exit 1; }
echo "zip           : $PLUGIN_ZIP"

# Ranks.Contract.dll must stay out: the host loads the single copy shipped in Ranks'
# resources/exports, and a second one gives IRanksApi two Type identities (FR-API-00).
if unzip -l "$PLUGIN_ZIP" | grep -q 'Ranks\.Contract\.dll'; then
  echo "FAIL: zip contains Ranks.Contract.dll"
  exit 1
fi
echo "contract dll  : correctly absent"

echo
echo "--- release notes preview ---"
head -8 /tmp/release-notes.md
echo
echo "PASS"
