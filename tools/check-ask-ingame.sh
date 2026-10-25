#!/usr/bin/env bash
# check-ask-ingame.sh - in-game check of Persoper's questions (dialogue step 5) on a running test server.
# Run it yourself, outside any sandbox. It runs in two parts, with a server restart between them:
#
#   tools/check-ask-ingame.sh <server-folder> [character]                    part 1 (about 6 minutes)
#   (stop and start the test server)
#   tools/check-ask-ingame.sh <server-folder> [character] --after-restart    part 2 (about 1 minute)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a level 1 GM test character in Persoper's realm (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# It uses the headless client: it summons Persoper with /persoper and has her ask with
# "/dialogue ask fav_music" (GM). Part 1 checks that an unanswered question drops after about a minute
# without a word, that an unknown answer gets the "other" reply, that "metal" gets the metal reply, and
# that she then uses the line only metal fans get. Part 2 checks that line again after the restart, which
# proves the answer was saved in the database. The test character ends up with the answer "metal".
# Waits of a couple of minutes between runs keep Persoper from being annoyed by quick re-summons.
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
AFTER_RESTART=0
ARGS=()
for arg in "$@"; do
  if [[ $arg == --after-restart ]]; then AFTER_RESTART=1; else ARGS+=("$arg"); fi
done
SERVER="${ARGS[0]:?usage: tools/check-ask-ingame.sh <server-folder> [character] [--after-restart]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${ARGS[1]:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
CHECKER="$REPO/src/build/DialogueCheck/Release/DialogueCheck.dll"
RECORDS="$SERVER/logs"   # the recordings stay there after the run
ASK='/dialogue ask fav_music'

die() { echo "check-ask-ingame: $*" >&2; exit 2; }
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

# Summons Persoper, waits for her, sends (4 seconds apart: the game drops chat typed too fast) the --send-late lines given as arguments after the first two
# (record file, seconds to record); the server's messages go to the record.
run() {
  local record=$1 seconds=$2; shift 2
  local late=() line
  for line in "$@"; do late+=("--send-late=$line"); done
  "$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
    --send="/persoper" --listen=10 --send-late-gap="${GAP:-4}" "${late[@]}" \
    --record="$record" --record-seconds="$seconds" --overwrite >"$record.log" 2>&1
}

# Said when Persoper wasn't there to ask.
no_persoper() { has "isn't summoned" "$1" && echo "      (Persoper wasn't summoned; see $1.log. Is $CHARACTER in her realm, and is she sulking?)"; }

if (( AFTER_RESTART )); then
  echo "== Part 2. After the restart she still knows the answer"
  # Three hellos 11 seconds apart (she answers chat at most every 10 seconds, and not while casting a buff).
  GAP=11 run "$RECORDS/ask-check-4.txt" 14 "/dialogue answers" "Persoper, hello" "Persoper, hello again" "Persoper, hello there"
  f="$RECORDS/ask-check-4.txt"
  has "saved answers: .*fav_music=metal" "$f" && pass "/dialogue answers lists fav_music=metal, read back from the database after a restart" ||
    fail "the saved answer isn't there after the restart; run part 1 first. See $f"
  has "metalhead" "$f" && pass "she greeted a metal fan with the line only they get, after a restart" ||
    { fail "no metal-fan greeting after the restart (if the answer is listed above she may only have been busy casting); see $f"; no_persoper "$f"; }
  echo
  (( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
  (( failures == 0 ))
  exit
fi

echo "== 1. The checker knows the rule (no server needed)"
out=$("$DOTNET" "$CHECKER" --preview persoper greeting player_likes=metal --folder "$SERVER/dialogue" 2>&1)
grep -qi "YES  .*metalhead" <<<"$out" && pass "preview: a metal fan can get the metal greeting" ||
  { fail "preview said:"; echo "$out" | sed 's/^/      /'; }
grep -qi "YES  .*metalhead" <<<"$("$DOTNET" "$CHECKER" --preview persoper greeting --folder "$SERVER/dialogue" 2>&1)" &&
  fail "preview: someone who answered nothing can get the metal greeting" ||
  pass "preview: someone who answered nothing can't"

echo "== 2. An unanswered question drops without a word (75 seconds)"
run "$RECORDS/ask-check-1.txt" 75 "$ASK"
if ! has "metal or ballads" "$RECORDS/ask-check-1.txt"; then
  fail "Persoper didn't ask; see $RECORDS/ask-check-1.txt"; no_persoper "$RECORDS/ask-check-1.txt"
else
  pass "Persoper asked \"Metal or ballads?\""
  has "person of taste|softie|never heard of it" "$RECORDS/ask-check-1.txt" &&
    fail "she answered herself" || pass "nobody answered, and she said nothing more"
fi

echo "   (waiting a minute before summoning her again, so she isn't annoyed)"; sleep 60
echo "== 3. The dropped question can't be answered late"
run "$RECORDS/ask-check-2.txt" 12 "metal"
has "person of taste" "$RECORDS/ask-check-2.txt" &&
  fail "\"metal\" got the metal reply although the question had run out" ||
  pass "\"metal\", said long after the question ran out, got no metal reply"

echo "   (waiting again; the last part answers \"jazz\", then \"metal\")"; sleep 75
echo "== 4. Answers get replies, and the answer is used"
run "$RECORDS/ask-check-3.txt" 16 "$ASK" "jazz" "$ASK" "metal" "Persoper, hello"
f="$RECORDS/ask-check-3.txt"
has "metal or ballads" "$f" || { fail "Persoper didn't ask; see $f"; no_persoper "$f"; }
has "never heard of it" "$f" && pass "\"jazz\" got the \"other\" reply" || fail "\"jazz\" got no \"other\" reply; see $f"
has "person of taste" "$f" && pass "\"metal\" got the metal reply" || fail "\"metal\" got no metal reply; see $f"
has "metalhead" "$f" && pass "\"Persoper, hello\" got the line only metal fans get" || fail "no metal-fan greeting; see $f"

echo
if (( failures == 0 )); then
  echo "Part 1 passed. Now stop and start the test server, then run:"
  echo "  tools/check-ask-ingame.sh $SERVER $CHARACTER --after-restart"
else
  echo "$failures check(s) failed."
fi
(( failures == 0 ))
