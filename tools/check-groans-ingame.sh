#!/usr/bin/env bash
# check-groans-ingame.sh - in-game check that named bots answer each other with situation lines
# ([if nearby shnacksize] groans) on a running test server. Run it yourself, outside any sandbox:
#
#   tools/check-groans-ingame.sh <server-folder> [character]      (about 2 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a GM test character in Midgard (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# It uses the headless client: it summons Persoper, invites Shnacksize, Kobaldjovi, Spyburd and
# Eaglefang (they must be online), brings them beside the character with "/dialogue gather" (GM),
# then has Shnacksize tell jokes with "/dialogue joke Shnacksize", each after "/dialogue forget" so
# the 30-minute cooldowns don't hide the groans. The groan lines are read from the server's own
# dialogue/ folder, so new ones are picked up. Grouped with an idle leader the bots stand still.
# Not every groupmate answers every joke, so it passes when at least one groan is heard, and says who.
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-groans-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
RECORD="$SERVER/logs/groan-check.txt"   # the recording stays there after the run
JOKER=Shnacksize
GROANERS=(Persoper Kobaldjovi Spyburd Eaglefang)
JOKES=4

die() { echo "check-groans-ingame: $*" >&2; exit 2; }
if [[ -z ${DAOC_PASSWORD:-} ]]; then
  [[ -t 0 ]] || die "set DAOC_PASSWORD (the test account's password) first"
  read -rsp "Password for $ACCOUNT: " DAOC_PASSWORD; echo
  export DAOC_PASSWORD
fi
[[ -f $CLIENT ]] || die "build first: dotnet build src/DaocServer.sln -c Release"
pid=$(cat "$SERVER/run/server.pid" 2>/dev/null || true)
[[ $pid =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null || die "the server in $SERVER isn't running; start it first"
login=$("$DOTNET" "$CLIENT" --account="$ACCOUNT" --login-only 2>&1) ||
  die "can't log in as $ACCOUNT ($(grep -m1 FAIL <<<"$login")); check DAOC_PASSWORD"
grep -q " $CHARACTER, level" <<<"$login" || die "$ACCOUNT has no character $CHARACTER"

failures=0
pass() { echo "PASS  $*"; }
fail() { echo "FAIL  $*"; failures=$((failures + 1)); }

# A groan line's longest piece without {0} (the speaker's name), from a bot's "~ reply joke" section.
groans() {
  awk '/^~ /{section=$0} section=="~ reply joke" && /\[if nearby shnacksize\]/' "$SERVER/dialogue/${1,,}.dlg" 2>/dev/null |
    sed 's/ *\[[^]]*\]//g' |
    awk -F'[{]0[}]' '{best=""; for (i = 1; i <= NF; i++) if (length($i) > length(best)) best = $i; gsub(/^[ ,.]+|[ ,.]+$/, "", best); print best}'
}

late=()
for bot in "$JOKER" "${GROANERS[@]:1}"; do late+=("--send-late=/invite $bot"); done
late+=("--send-late=/dialogue gather")
for ((i = 0; i < JOKES; i++)); do late+=("--send-late=/dialogue forget" "--send-late=/dialogue joke $JOKER"); done

echo "== Grouping the nameds and letting $JOKER tell $JOKES jokes (about 2 minutes)"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/persoper" --listen=10 --send-late-gap=6 "${late[@]}" \
  --record="$RECORD" --record-seconds=110 --overwrite >"$RECORD.log" 2>&1

told=$(grep -c "$JOKER tells a joke" "$RECORD")
(( told > 0 )) && pass "$JOKER told $told joke(s)" ||
  fail "$JOKER told no jokes; is she online and in Midgard? See $RECORD and $RECORD.log"

heard=()
for bot in "${GROANERS[@]}"; do
  pieces=$(groans "$bot")
  [[ -n $pieces ]] || { fail "$bot has no groan lines in $SERVER/dialogue"; continue; }
  grep -qF -f <(echo "$pieces") "$RECORD" && heard+=("$bot")
done
(( ${#heard[@]} > 0 )) && pass "groans at $JOKER's own jokes from: ${heard[*]}" ||
  fail "nobody groaned at $JOKER's jokes; see $RECORD (did the groaners join the group?)"

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
