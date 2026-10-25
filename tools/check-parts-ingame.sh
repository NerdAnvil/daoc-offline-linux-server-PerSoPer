#!/usr/bin/env bash
# check-parts-ingame.sh - in-game check of swap-in parts (dialogue step 6) on a running test server.
# Run it yourself, outside any sandbox:
#
#   tools/check-parts-ingame.sh <server-folder> [character]       (about 2 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a level 1 GM test character in Persoper's realm (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's dialogue/persoper.dlg to be the new one (with "~ part greet") and the
# server to have been restarted onto the new build. Persoper's greeting for a player who answered
# "metal" is "{greet}, metalhead <name>! ...", where {greet} is one of Hail, Well met, Oi, Ah there you are.
# Part 1 (no server needed) asks the checker for every line she could say: one per greeting word, none
# with the raw "{greet}". Part 2 has her ask the music question, answers "metal", and says hello four
# times; every greeting she gives must start with one of the greeting words, and none may show "{greet}".
# The words are random, so the script reports how many different ones it saw but doesn't fail on that.
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-parts-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
CHECKER="$REPO/src/build/DialogueCheck/Release/DialogueCheck.dll"
RECORDS="$SERVER/logs"   # the recordings stay there after the run
ASK='/dialogue ask fav_music'

die() { echo "check-parts-ingame: $*" >&2; exit 2; }
if [[ -z ${DAOC_PASSWORD:-} ]]; then
  [[ -t 0 ]] || die "set DAOC_PASSWORD (the test account's password) first"
  read -rsp "Password for $ACCOUNT: " DAOC_PASSWORD; echo
  export DAOC_PASSWORD
fi
[[ -f $CLIENT && -f $CHECKER ]] || die "build first: dotnet build src/DaocServer.sln -c Release"
pid=$(cat "$SERVER/run/server.pid" 2>/dev/null || true)
[[ $pid =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null || die "the server in $SERVER isn't running; start it first"
login=$("$DOTNET" "$CLIENT" --account="$ACCOUNT" --login-only 2>&1) ||
  die "can't log in as $ACCOUNT ($(grep -m1 FAIL <<<"$login")); check DAOC_PASSWORD"
grep -q " $CHARACTER, level" <<<"$login" || die "$ACCOUNT has no character $CHARACTER"

failures=0
pass() { echo "PASS  $*"; }
fail() { echo "FAIL  $*"; failures=$((failures + 1)); }
has() { grep -qiE "$1" "$2"; }   # has <regex> <record>
WORDS='Hail|Well met|Oi|Ah, there you are'

# Summons Persoper, waits for her, sends the --send-late lines given after the first two arguments
# (record file, seconds to record), GAP seconds apart; the server's messages go to the record.
run() {
  local record=$1 seconds=$2; shift 2
  local late=() line
  for line in "$@"; do late+=("--send-late=$line"); done
  "$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
    --send="/persoper" --listen=10 --send-late-gap="${GAP:-4}" "${late[@]}" \
    --record="$record" --record-seconds="$seconds" --overwrite >"$record.log" 2>&1
}

echo "== 1. The checker lists one greeting per word (no server needed)"
out=$("$DOTNET" "$CHECKER" --preview persoper greeting player_likes=metal --folder "$SERVER/dialogue" 2>&1)
n=$(grep -ciE "YES  ($WORDS), metalhead" <<<"$out")
(( n == 4 )) && pass "preview: a metal fan can get 4 greetings, one per greeting word" ||
  { fail "preview: expected 4 greetings for a metal fan, found $n. Is the new persoper.dlg in $SERVER/dialogue? It said:"; echo "$out" | sed 's/^/      /'; }
grep -q "{" <<<"$out" && fail "preview: a raw {part} is left in a line" || pass "preview: no raw {part} left in any line"

echo "== 2. In game she greets with a filled-in part (about 90 seconds)"
GAP=11 run "$RECORDS/parts-check-1.txt" 85 "$ASK" "metal" "Persoper, hello" "Persoper, hello again" "Persoper, hello there" "Persoper, hello you"
f="$RECORDS/parts-check-1.txt"
if ! has "metal or ballads" "$f"; then
  fail "Persoper didn't ask; see $f (is $CHARACTER in her realm, and is she sulking?)"
else
  greetings=$(grep -oiE "($WORDS), metalhead" "$f" | tr 'A-Z' 'a-z' | sort)
  count=$(grep -c . <<<"$greetings")
  [[ -z $greetings ]] && count=0
  (( count >= 1 )) && pass "she gave $count metal-fan greeting(s) that start with a greeting word" ||
    fail "no greeting like \"Hail, metalhead ...\"; see $f (she may only have been busy casting)"
  echo "      different words seen: $(sort -u <<<"$greetings" | tr '\n' ';')"
  has "\{greet\}" "$f" && fail "a raw {greet} reached the chat" || pass "no raw {greet} in the chat"
fi

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
