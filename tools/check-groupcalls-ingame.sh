#!/usr/bin/env bash
# check-groupcalls-ingame.sh - in-game check of bots answering "who wants to group?" (intent Part B5) on a running test
# server. Run it yourself, outside any sandbox:
#
#   tools/check-groupcalls-ingame.sh <server-folder> [character]       (about 1.5 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a level 1 GM test character in Midgard (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's dialogue files for intents, Shempenstein and Casual to be the repo's, and its GameServer.dll
# to have "/dialogue groupcall". Invites Shempenstein, brings him over with /dialogue gather, follows him and turns on
# the GM test shortcut "/dialogue groupcall always" (every personality bot that hears a call answers; the cooldown and
# his real state still decide) and holds him with "/dialogue hold" (his own plans pause, so he is still there after
# /disband instead of teleporting off). Then, in /say, 11 s apart:
#   1. "who wants to group?" while he is in the group: server.log weighs him Quiet and he says nothing;
#   2. /disband, then "anyone up for a dungeon?": server.log weighs him (yes, no, busy, grouped or level) and he says
#      a line from that answer's section ("~ reply groupyes" and so on; a yes may use "~ reply grouping", a reason
#      without lines his "~ reply groupno"), filled from his state;
#   3. "lfg" 11 s later, within the 20 s cooldown: server.log notes the cooldown and he says nothing.
# The run ends with "/dialogue groupcall normal". Which answer he gives depends on his level and state (a level 1
# character usually gets the level answer); the yes/no odds per type are unit tested (UT_GroupCalls).
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-groupcalls-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
DIR="src/GameServer/bots/persoper/dialogue"
RECORDS="$SERVER/logs"   # the recordings stay there after the run
LOG="$SERVER/logs/server.log"

die() { echo "check-groupcalls-ingame: $*" >&2; exit 2; }
if [[ -z ${DAOC_PASSWORD:-} ]]; then
  [[ -t 0 ]] || die "set DAOC_PASSWORD (the test account's password) first"
  read -rsp "Password for $ACCOUNT: " DAOC_PASSWORD; echo
  export DAOC_PASSWORD
fi
[[ -f $CLIENT ]] || die "build first: dotnet build src/DaocServer.sln -c Release"
for file in intents.dlg shempenstein.dlg casual.dlg; do
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
# Shempenstein's rows of one section: his own over Casual's.
rows() { section casual "$1"; section shempenstein "$1"; }
# The rows a weighed answer may be said from: its section, then its fallback.
answer_rows() { case "$1" in
  Yes) rows "~ reply groupyes"; rows "~ reply grouping" ;;
  No) rows "~ reply groupno" ;;
  Busy) rows "~ reply groupbusy"; rows "~ reply groupno" ;;
  Grouped) rows "~ reply grouphave"; rows "~ reply groupno" ;;
  Level) rows "~ reply grouplevel"; rows "~ reply groupno" ;;
esac; }
# True when $1 is one of the rows on stdin, each slot ({level}, {zone}, ...) standing for some words.
fits() { awk -v said="$1" '
  function match_row(row, s,   n, parts, i, at, pos, lit) {
    n = split(row, parts, /\{[a-z]+\}/)
    if (n == 1) return row == s
    if (substr(s, 1, length(parts[1])) != parts[1]) return 0
    pos = length(parts[1]) + 1
    for (i = 2; i < n; i++) {
      lit = parts[i]; at = index(substr(s, pos + 1), lit)
      if (lit == "" || at == 0) return 0
      pos = pos + at + length(lit)
    }
    lit = parts[n]
    return length(s) - pos + 1 > length(lit) && substr(s, length(s) - length(lit) + 1) == lit
  }
  match_row($0, said) { found = 1; exit }
  END { exit !found }'; }
# What Shempenstein said in /say in a recording.
said_by() { sed -nE "s/.*\\] ([Tt]he )?$1( [A-Za-z]+)? says, \"(.*)\"\$/\\3/p" "$2" | sed -E 's/[[:space:]]+$//'; }
# The chat lines from the moment the player said $2 in /say until $3 (default 10) seconds later.
within() { awk -v pat="$2" -v span="${3:-10}" '
  function secs(t) { split(t, a, ":"); return a[1] * 3600 + a[2] * 60 + a[3] }
  !seen && index($0, "You say, \"" pat "\"") { start = secs($1); seen = 1; next }
  seen && secs($1) - start <= span' "$1"; }
# This run's group-call notes for the character in server.log (lines after $1).
notes() { tail -n +"$1" "$LOG" | grep -F "PERSONALITY_GROUPCALL" | grep -F "player=\"$CHARACTER\""; }

IN_GROUP="who wants to group?"
CALL="anyone up for a dungeon?"
AGAIN="lfg"
start=$(( $(wc -l <"$LOG") + 1 ))
f="$RECORDS/groupcalls-check.txt"
echo "== Group calls: one in the group, one after /disband, one in the cooldown (about 1.5 minutes)"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/invite Shempenstein" --send="/dialogue gather" --listen=5 \
  --wait-for=Shempenstein --follow=Shempenstein --send-late-gap=11 \
  --send-late="/dialogue forget" --send-late="/dialogue groupcall always" --send-late="/dialogue hold Shempenstein 90" \
  --send-late="$IN_GROUP" \
  --send-late="/disband" --send-late="$CALL" --send-late="$AGAIN" --send-late="/dialogue groupcall normal" \
  --record="$f" --record-seconds=12 --overwrite >"$f.log" 2>&1
grep -q "Shempenstein" "$f" || fail "Shempenstein never showed up in the recording; see $f (did the /invite work?)"
grep -q "Group calls: every" "$f" || fail "no \"/dialogue groupcall\" answer in the recording; is the server's GameServer.dll the new one?"
grep -q "Holding Shempenstein" "$f" || fail "no \"/dialogue hold\" answer in the recording; is the server's GameServer.dll the new one?"

log=$(notes "$start")
echo "  server.log:"; [[ -n $log ]] && echo "$log" | sed -E 's/^.*PERSONALITY_GROUPCALL/      PERSONALITY_GROUPCALL/'
weighed=$(grep -F 'weighed bot="Shempenstein"' <<<"$log" | grep -oE "answer=[A-Za-z]+" | cut -d= -f2)
first=$(sed -n 1p <<<"$weighed"); second=$(sed -n 2p <<<"$weighed")

# 1. In the group: quiet.
said=$(said_by Shempenstein <(within "$f" "$IN_GROUP"))
echo "  \"$IN_GROUP\" (in the group):"; [[ -n $said ]] && echo "$said" | sed 's/^/      Shempenstein: /'
[[ $first == Quiet ]] && pass "in the group he was weighed Quiet" || fail "in the group he was weighed \"${first:-not at all}\", not Quiet"
[[ -z $said ]] && pass "in the group he said nothing" || fail "in the group he answered in /say"

# 2. After /disband: an answer from the weighed section.
said=$(said_by Shempenstein <(within "$f" "$CALL"))
echo "  \"$CALL\" (after /disband, weighed ${second:-nothing}):"; [[ -n $said ]] && echo "$said" | sed 's/^/      Shempenstein: /'
if [[ -z $second || $second == Quiet ]]; then
  fail "after /disband he was weighed \"${second:-not at all}\" (still in a group? out of earshot? did the hold work?)"
else
  pass "after /disband he was weighed $second"
  answered=""
  while IFS= read -r one; do [[ -n $one ]] && answer_rows "$second" | fits "$one" && answered="$one"; done <<<"$said"
  [[ -n $answered ]] && pass "he said a line for $second, filled from his state" ||
    fail "no line for $second from him within 10 s (a casting bot drops a reply; run it again). See $f"
fi

# 3. Within the cooldown: nothing.
said=$(said_by Shempenstein <(within "$f" "$AGAIN"))
echo "  \"$AGAIN\" (11 s later):"; [[ -n $said ]] && echo "$said" | sed 's/^/      Shempenstein: /'
grep -q 'PERSONALITY_GROUPCALL cooldown' <<<"$log" && pass "the second call was held by the cooldown" ||
  fail "no cooldown note for the call 11 s after the last"
[[ -z $said ]] && pass "within the cooldown he said nothing" || fail "within the cooldown he answered in /say"

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
