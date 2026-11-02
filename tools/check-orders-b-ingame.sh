#!/usr/bin/env bash
# check-orders-b-ingame.sh - in-game check of the B6b orders: "assist me", "rest up", "leave the group", and a bot that
# has stormed off after a bad joke refusing an order, on a running test server. Run it yourself, outside any sandbox:
#
#   tools/check-orders-b-ingame.sh <server-folder> [character]       (about 2 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a level 1 GM test character in Midgard (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's dialogue files for intents, Persoper, Shempenstein and Casual to be the repo's, and its
# GameServer.dll to have "/dialogue walkoff <bot>". Invites Shempenstein (a world bot), summons Persoper (a companion),
# brings them over with /dialogue gather and turns on "/dialogue orders always" (a named order is always answered; their
# real state still decides). The character fights nothing. Then, 10 s apart:
#   1. "/g persoper, assist me": nothing to assist on, so server.log notes Assist/NoTarget and she says a
#      "~ reply ordernotarget" line;
#   2. "/g shemp, rest up": noted Rest/Full and an orderfull line, or (if he has something to rest off) Rest/Yes and
#      an orderrest line;
#   3. "/g leave the group" (to nobody): noted as open, and nobody is told to leave;
#   4. "/g persoper, leave the group": a companion never leaves, so Leave/No and a Persoper "~ reply orderno" line;
#   5. "/dialogue walkoff Shempenstein" (he storms off now), then "/s shemp, follow me": Follow/Away and a
#      "~ reply orderaway" line, not "not yours" (he is out of the group meanwhile);
#   6. once he is back, "/g shemp, leave the group": Leave/Yes, an orderleave line, then "PERSONALITY_ORDER left" for
#      him in server.log.
# The run ends with "/dialogue orders normal" and "/dialogue walkoff reset". Assisting a real fight and resting off a
# real loss are unit tested (UT_Orders: Decide), as the character can't start a fight here.
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-orders-b-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
DIR="src/GameServer/bots/persoper/dialogue"
RECORDS="$SERVER/logs"   # the recording stays there after the run
LOG="$SERVER/logs/server.log"

die() { echo "check-orders-b-ingame: $*" >&2; exit 2; }
if [[ -z ${DAOC_PASSWORD:-} ]]; then
  [[ -t 0 ]] || die "set DAOC_PASSWORD (the test account's password) first"
  read -rsp "Password for $ACCOUNT: " DAOC_PASSWORD; echo
  export DAOC_PASSWORD
fi
[[ -f $CLIENT ]] || die "build first: dotnet build src/DaocServer.sln -c Release"
for file in intents.dlg persoper.dlg shempenstein.dlg casual.dlg; do
  cmp -s "$REPO/$DIR/$file" "$SERVER/dialogue/$file" || die "$SERVER/dialogue/$file isn't the repo's; copy it over first"
done
pid=$(cat "$SERVER/run/server.pid" 2>/dev/null || true)
[[ $pid =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null || die "the server in $SERVER isn't running; start it first"
[[ -f $LOG ]] || die "no $LOG"
login=$("$DOTNET" "$CLIENT" --account="$ACCOUNT" --login-only 2>&1) ||
  die "can't log in as $ACCOUNT ($(grep -m1 FAIL <<<"$login")); check DAOC_PASSWORD"
grep -q " $CHARACTER, level" <<<"$login" || die "$ACCOUNT has no character $CHARACTER"

failures=0
pass() { echo "PASS  $*"; }
fail() { echo "FAIL  $*"; failures=$((failures + 1)); }

# The rows of one section of a bot's file, tags off, {0} filled in with the character's name.
section() { awk -v want="$2" '/^~ /{f=($0==want); next} /^===/{f=0} f&&NF&&!/^#/' "$REPO/$DIR/$1.dlg" |
  sed -E "s/[[:space:]]*\[(if|once|cooldown|allow)[^]]*\]//g; s/[[:space:]]+\$//; s/\{0\}/$CHARACTER/g"; }
# A bot's rows of one section: Shempenstein's own over Casual's; Persoper's from her file.
rows() { if [[ $1 == Persoper ]]; then section persoper "$2"; else section casual "$2"; section shempenstein "$2"; fi; }
# True when $1 is one of the rows on stdin, alone or with a mood tint before or after it ({monster} and the like
# stand for some words).
one_of() { local said="$1" row; while IFS= read -r row; do
  [[ -z $row ]] && continue
  local glob="${row//\{[a-z]*\}/*}"
  # shellcheck disable=SC2053
  [[ $said == $glob || $said == *" "$glob || $said == $glob" "* ]] && return 0
done; return 1; }
# What one bot said in a recording: party chat ("Name: "..."") or /say ("Name says, "...").
said_by() { sed -nE "s/.*\\] ([Tt]he )?$1[^:\"]*: \"(.*)\"\$/\\2/p; s/.*\\] ([Tt]he )?$1( [A-Za-z]+)? says, \"(.*)\"\$/\\3/p" "$2" |
  sed -E 's/[[:space:]]+$//'; }
# The chat lines from the moment the player said $2 (group chat or /say) until $3 (default 10) seconds later.
within() { awk -v pat="$2" -v me="$CHARACTER" -v span="${3:-10}" '
  function secs(t) { split(t, a, ":"); return a[1] * 3600 + a[2] * 60 + a[3] }
  !seen && (index($0, "[Party] " me ": \"" pat) || index($0, "You say, \"" pat)) { start = secs($1); seen = 1; next }
  seen && secs($1) - start <= span' "$1"; }
# This run's order notes for the character in server.log (lines after $1).
notes() { tail -n +"$1" "$LOG" | grep -F "PERSONALITY_ORDER" | grep -F "player=\"$CHARACTER\""; }
# True when a note says bot $2 got order $3 with answer $4 (named True/False $5).
noted() { grep -F "bot=\"$2" <<<"$1" | grep -qF "order=$3 answer=$4 named=$5"; }
# The check for one answer line: who, the line the player said, the section, and how it was said.
answered() { local who="$1" line="$2" sect="$3" said one hit=""
  said=$(said_by "$who" <(within "$f" "$line"))
  [[ -n $said ]] && echo "$said" | sed "s/^/      $who: /"
  while IFS= read -r one; do [[ -n $one ]] && rows "$who" "$sect" | one_of "$one" && hit="$one"; done <<<"$said"
  [[ -n $hit ]]; }

ASSIST="persoper, assist me"
REST="shemp, rest up"
OPEN_LEAVE="leave the group"
LEAVE_PERSOPER="persoper, leave the group"
AWAY="shemp, follow me"
LEAVE_SHEMP="shemp, leave the group"
start=$(( $(wc -l <"$LOG") + 1 ))
f="$RECORDS/orders-b-check.txt"
echo "== Orders B6b: assist, rest, leave, and an order while he has stormed off (about 2 minutes)"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/invite Shempenstein" --send="/persoper" --send="/dialogue gather" --listen=5 \
  --wait-for=Shempenstein --send-late-gap=10 \
  --send-late="/dialogue forget" --send-late="/dialogue orders always" \
  --send-late="/g $ASSIST" --send-late="/g $REST" --send-late="/g $OPEN_LEAVE" --send-late="/g $LEAVE_PERSOPER" \
  --send-late="/dialogue walkoff Shempenstein" --send-late="/s $AWAY" --send-late="/dialogue walkoff reset" \
  --send-late="/dialogue gather" --send-late="/g $LEAVE_SHEMP" \
  --send-late="/dialogue orders normal" \
  --record="$f" --record-seconds=8 --overwrite >"$f.log" 2>&1
grep -q "Shempenstein" "$f" || fail "Shempenstein never showed up in the recording; see $f (did the /invite work?)"
grep -q "walks off now" "$f" || fail "no \"/dialogue walkoff Shempenstein\" answer in the recording; is the server's GameServer.dll the new one? See $f"

log=$(notes "$start")
echo "  server.log:"; [[ -n $log ]] && echo "$log" | sed -E 's/^.*PERSONALITY_ORDER/      PERSONALITY_ORDER/'

# 1. Assist with no fight.
echo "  \"$ASSIST\":"
noted "$log" Persoper Assist NoTarget True && pass "Persoper had nothing to assist on" || fail "no Assist/NoTarget note for Persoper"
answered Persoper "$ASSIST" "~ reply ordernotarget" && pass "she said an ordernotarget line" ||
  fail "no ordernotarget line from Persoper within 10 s (a casting bot drops a reply; run it again). See $f"

# 2. Rest: full, or a real rest.
echo "  \"$REST\":"
if noted "$log" Shempenstein Rest Full True; then
  pass "Shempenstein was full"
  answered Shempenstein "$REST" "~ reply orderfull" && pass "he said an orderfull line" || fail "no orderfull line from Shempenstein within 10 s. See $f"
elif noted "$log" Shempenstein Rest Yes True; then
  pass "Shempenstein rests"
  answered Shempenstein "$REST" "~ reply orderrest" && pass "he said an orderrest line" || fail "no orderrest line from Shempenstein within 10 s. See $f"
else
  fail "no Rest/Full or Rest/Yes note for Shempenstein"
fi

# 3. An open "leave the group" goes to nobody.
echo "  \"$OPEN_LEAVE\" (to nobody):"
grep -q 'PERSONALITY_ORDER open .*order=Leave' <<<"$log" && pass "it was noted as open" || fail "no open Leave note"
grep -qF "order=Leave answer=Yes named=False" <<<"$log" && fail "a bot was told to leave by an open order" || pass "nobody was told to leave"

# 4. A companion never leaves.
echo "  \"$LEAVE_PERSOPER\":"
noted "$log" Persoper Leave No True && pass "Persoper said no" || fail "no Leave/No note for Persoper"
answered Persoper "$LEAVE_PERSOPER" "~ reply orderno" && pass "she said an orderno line" ||
  fail "no orderno line from Persoper within 10 s. See $f"

# 5. Stormed off: refuses in character, not "not yours".
echo "  \"$AWAY\" (while he has stormed off, in /say):"
noted "$log" Shempenstein Follow Away True && pass "Shempenstein was noted Away" ||
  fail "no Follow/Away note for Shempenstein ($(grep -F 'order=Follow' <<<"$log" | grep -o 'answer=[A-Za-z]*' | head -1))"
answered Shempenstein "$AWAY" "~ reply orderaway" && pass "he said an orderaway line" ||
  fail "no orderaway line from Shempenstein within 10 s. See $f"

# 6. Back in the group, a world bot leaves when named.
echo "  \"$LEAVE_SHEMP\":"
noted "$log" Shempenstein Leave Yes True && pass "Shempenstein said yes" ||
  fail "no Leave/Yes note for Shempenstein (did he rejoin after the walk-off? see the PERSONALITY_WALKOFF lines in server.log)"
answered Shempenstein "$LEAVE_SHEMP" "~ reply orderleave" && pass "he said an orderleave line" ||
  fail "no orderleave line from Shempenstein within 10 s. See $f"
grep -q 'PERSONALITY_ORDER left bot="Shempenstein' <<<"$log" && pass "he left the group" || fail "no \"left\" note for Shempenstein"

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
