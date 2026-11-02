#!/usr/bin/env bash
# check-orders-ingame.sh - in-game check of bots obeying "stay here", "come here" and "follow me" (intent Part B6) on a
# running test server. Run it yourself, outside any sandbox:
#
#   tools/check-orders-ingame.sh <server-folder> [character]       (about 2 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a level 1 GM test character in Midgard (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's dialogue files for intents, Persoper, Shempenstein and Casual to be the repo's, and its
# GameServer.dll to have "/dialogue orders". Invites Shempenstein (a world bot), summons Persoper (a companion), brings
# them over with /dialogue gather and turns on "/dialogue orders always" (no refusal of "stay" by type, and a named
# order is always answered; their real state still decides). Then, 10 s apart:
#   1. "/g shemp, stay here": server.log notes Stay/Yes for him and he says a "~ reply orderstay" line;
#   2. "/g persoper, stay here": the same for her (a companion obeys her owner);
#   3. "/jump rel 800 0 0" (the GM moves away), then "/dialogue state" for both: each still holds (staying > 0 s)
#      and stands 600 or more away;
#   4. "/g come here" (open, to the group): both are noted Come/Yes, one of them says a "~ reply ordercome" line, and
#      10 s later both are back within 500 and no longer staying;
#   5. "/dialogue hold Shempenstein 60", "/disband", "/s shemp, follow me": he is no longer in the GM's group, so
#      server.log notes Follow/NotYours and he says a "~ reply ordernot" line.
# The run ends with "/dialogue orders normal". How often a type refuses "stay", the cooldown and who may order whom are
# unit tested (UT_Orders). Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-orders-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
DIR="src/GameServer/bots/persoper/dialogue"
RECORDS="$SERVER/logs"   # the recording stays there after the run
LOG="$SERVER/logs/server.log"

die() { echo "check-orders-ingame: $*" >&2; exit 2; }
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
# The "/dialogue state" lines for a bot ($2), in order, without "State <name>: ".
states() { grep -F "State $2" "$1" | sed -E 's/.*State [^:]*: //'; }
field() { sed -nE "s/.*(^|; )$1=([^;]*).*/\\2/p" <<<"$2"; }
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

STAY_SHEMP="shemp, stay here"
STAY_PERSOPER="persoper, stay here"
COME="come here"
FOLLOW="shemp, follow me"
start=$(( $(wc -l <"$LOG") + 1 ))
f="$RECORDS/orders-check.txt"
echo "== Orders: stay, walk away, come, then an order to a bot outside the group (about 2 minutes)"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/invite Shempenstein" --send="/persoper" --send="/dialogue gather" --listen=5 \
  --wait-for=Shempenstein --send-late-gap=10 \
  --send-late="/dialogue forget" --send-late="/dialogue orders always" \
  --send-late="/g $STAY_SHEMP" --send-late="/g $STAY_PERSOPER" \
  --send-late="/jump rel 800 0 0" --send-late="/dialogue state Shempenstein" --send-late="/dialogue state persoper" \
  --send-late="/g $COME" --send-late="/dialogue state Shempenstein" --send-late="/dialogue state persoper" \
  --send-late="/dialogue hold Shempenstein 60" --send-late="/disband" --send-late="/s $FOLLOW" \
  --send-late="/dialogue orders normal" \
  --record="$f" --record-seconds=5 --overwrite >"$f.log" 2>&1
grep -q "Shempenstein" "$f" || fail "Shempenstein never showed up in the recording; see $f (did the /invite work?)"
grep -q "Orders: no bot refuses" "$f" || fail "no \"/dialogue orders\" answer in the recording; is the server's GameServer.dll the new one?"

log=$(notes "$start")
echo "  server.log:"; [[ -n $log ]] && echo "$log" | sed -E 's/^.*PERSONALITY_ORDER/      PERSONALITY_ORDER/'

# 1 and 2. Stay, named.
for pair in "Shempenstein|$STAY_SHEMP" "Persoper|$STAY_PERSOPER"; do
  IFS='|' read -r who line <<<"$pair"
  echo "  \"$line\":"
  noted "$log" "$who" Stay Yes True && pass "$who was told to stay and said yes" || fail "no Stay/Yes note for $who"
  answered "$who" "$line" "~ reply orderstay" && pass "$who said an orderstay line" ||
    fail "no orderstay line from $who within 10 s (a casting bot drops a reply; run it again). See $f"
done

# 3. After the GM moved away, both still hold.
for who in Shempenstein Persoper; do
  state=$(states "$f" "$who" | sed -n 1p)
  echo "  after /jump, $who: ${state:-no state line}"
  staying=$(field staying "$state"); distance=$(field distance "$state")
  [[ ${staying%s} =~ ^[0-9]+$ && ${staying%s} -gt 0 && $distance =~ ^[0-9]+$ && $distance -ge 600 ]] &&
    pass "$who stayed behind ($distance away, ${staying} left)" || fail "$who didn't stay (staying=$staying, distance=$distance)"
done

# 4. Come, open: both obey, one or two answer, both come back.
echo "  \"$COME\" (to the group):"
spoke=0
for who in Shempenstein Persoper; do
  noted "$log" "$who" Come Yes False && pass "$who was told to come and said yes" || fail "no Come/Yes note for $who"
  answered "$who" "$COME" "~ reply ordercome" && spoke=$((spoke + 1))
done
(( spoke >= 1 )) && pass "$spoke of them said an ordercome line" || fail "nobody said an ordercome line within 10 s. See $f"
for who in Shempenstein Persoper; do
  state=$(states "$f" "$who" | sed -n 2p)
  echo "  after \"$COME\", $who: ${state:-no state line}"
  staying=$(field staying "$state"); distance=$(field distance "$state")
  [[ $staying == 0s && $distance =~ ^[0-9]+$ && $distance -le 500 ]] &&
    pass "$who came back ($distance away) and no longer stays" || fail "$who didn't come back (staying=$staying, distance=$distance)"
done

# 5. A bot outside the group won't take an order.
echo "  \"$FOLLOW\" (after /disband, in /say):"
noted "$log" Shempenstein Follow NotYours True && pass "outside the group, Shempenstein was noted NotYours" ||
  fail "no Follow/NotYours note for Shempenstein after /disband"
answered Shempenstein "$FOLLOW" "~ reply ordernot" && pass "he said an ordernot line" ||
  fail "no ordernot line from Shempenstein within 10 s. See $f"

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
