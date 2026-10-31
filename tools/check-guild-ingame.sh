#!/usr/bin/env bash
# check-guild-ingame.sh - in-game check that Persoper answers her owner in guild chat (/gu) and in
# faction chat (/faction) on a running test server. Run it yourself, outside any sandbox:
#
#   tools/check-guild-ingame.sh <server-folder> [character]      (about 1.5 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a test character in a guild (default Headlesstest; new characters start in one)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# It uses the headless client: it summons Persoper, waits 25 s for her buffs, then says two lines in
# guild chat (one without her name, one naming her) and two in faction chat, 13 seconds apart (guild
# replies wait 10 s and faction replies 12 s per guild or realm). It passes when she answers at
# least once in each; a bot that is casting drops a due reply, so run it again if a count is short.
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-guild-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
RECORD="$SERVER/logs/guild-check.txt"   # the recording stays there after the run

die() { echo "check-guild-ingame: $*" >&2; exit 2; }
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

echo "== Summoning Persoper and talking in guild and faction chat (about 1.5 minutes)"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/persoper" --listen=25 --send-late-gap=13 \
  "--send-late=/gu anyone up for a hunt tonight" \
  "--send-late=/gu Persoper, how are you holding up" \
  "--send-late=/faction anyone near the keep today" \
  "--send-late=/faction good luck out there everyone" \
  --record="$RECORD" --record-seconds=90 --overwrite >"$RECORD.log" 2>&1

grep -q "Persoper .* joined your group" "$RECORD" && pass "Persoper joined" ||
  fail "Persoper didn't join; see $RECORD and $RECORD.log"

guild=$(grep -c '\[Guild\] Persoper ' "$RECORD")
(( guild > 0 )) && pass "$guild guild line(s) from Persoper (2 asked)" || fail "no guild line from Persoper; is $CHARACTER in a guild? See $RECORD"
faction=$(grep -cE '\(Persoper [^)]*\) <Faction>' "$RECORD")
(( faction > 0 )) && pass "$faction faction line(s) from Persoper (2 asked)" || fail "no faction line from Persoper; see $RECORD"

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
