#!/usr/bin/env bash
# check-regroup-ingame.sh - in-game check that bots are let go when the player leaves their group, on a running
# test server. Run it yourself, outside any sandbox:
#
#   tools/check-regroup-ingame.sh <server-folder> [character]       (about 2.5 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a GM test character in Midgard (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Three logins. The first invites Eaglefang and Shempenstein (the character leads the group), brings them over with
# /dialogue gather and logs out, leaving a group of two bots behind. The second logs straight back in and invites
# both again. The third invites both, removes Eaglefang alone with "/disband Eaglefang" and invites him again.
# Then:
#   1. the first login got both bots into the group;
#   2. the second login got both again ("joins your group"), with no "already following another player-led group"
#      refusal (before the fix they stayed tied to the logged-out player until a server restart);
#   3. the third login: Eaglefang left the group, then joined it again (a quick re-invite may annoy a personality
#      bot; that is intended, and he still joins first).
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-regroup-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
RECORDS="$SERVER/logs"   # the recordings stay there after the run
BOTS=(Eaglefang Shempenstein)
FIRST=${BOTS[0]}

die() { echo "check-regroup-ingame: $*" >&2; exit 2; }
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

invites=(); for bot in "${BOTS[@]}"; do invites+=("--send=/invite $bot"); done
# One login: invite the bots, gather them, record a few seconds, then /quit (the client always leaves that way).
run() {
  "$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" "${invites[@]}" "$@" --listen=5 \
    --record="$f" --record-seconds=10 --overwrite >"$f.log" 2>&1
}
# Checks one recording: every bot joined, none refused as tied to another group.
joined() {
  for bot in "${BOTS[@]}"; do
    if grep -qF "$bot is already following another player-led group" "$f"; then
      fail "$1: $bot refused, still tied to another player-led group; see $f"
    elif grep -qF "$bot joins your group" "$f"; then pass "$1: $bot joined the group"
    else fail "$1: no \"$bot joins your group\"; see $f"; fi
  done
}

echo "== First login: invite ${BOTS[*]}, then log out with them still grouped"
f="$RECORDS/regroup-check-1.txt"
run --send="/dialogue gather"
joined "first login"

echo "== Second login, straight after: invite them again"
f="$RECORDS/regroup-check-2.txt"
run
joined "after logging back in"

echo "== Third login: invite both, remove $FIRST alone, invite him again"
f="$RECORDS/regroup-check-3.txt"
run --send="/dialogue gather" --send-late-gap=5 --send-late="/disband $FIRST" --send-late="/invite $FIRST"
joined "third login"
grep -qF "$FIRST has left the group" "$f" && pass "third login: $FIRST left the group on /disband" ||
  fail "third login: no \"$FIRST has left the group\"; see $f"
joins=$(grep -cF "$FIRST joins your group" "$f")
if grep -qF "$FIRST is already following another player-led group" "$f"; then
  fail "third login: re-invite refused, $FIRST still tied to the group; see $f"
elif (( joins >= 2 )); then pass "third login: $FIRST joined again after being removed"
else fail "third login: $FIRST didn't join again after /disband ($joins join(s)); see $f"; fi

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
