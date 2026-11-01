#!/usr/bin/env bash
# check-walkoff-ingame.sh - in-game check of the walk-off gag after a bad joke (intent Part B3c) on a running test server.
# Run it yourself, outside any sandbox:
#
#   tools/check-walkoff-ingame.sh <server-folder> [character]       (about 3.5 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a level 1 GM test character in Midgard (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's dialogue files for intents, Shempenstein and Casual to be the repo's.
# Invites Shempenstein (the character leads the group), brings him over with /dialogue gather and turns on the
# GM test shortcut "/dialogue walkoff always" (every groan gets the gag; the guards stay on). The character stands
# still. Four jokes told in full in group chat, 20 s apart (he groans at about 6 in 10, so all four laughed at is
# a 1 in 40 miss: run it again). Then:
#   1. a groan gets a "~ reply jokequit" line, and server.log shows PERSONALITY_WALKOFF start (mode=quit),
#      rejoined, end at least 200 away, and returned (back at his spot); a "~ reply jokereturn" line follows;
#      while he is out of the group his own plans are paused: no town teleport, no "moved" (out of reach) note;
#   2. with "/dialogue walkoff normal" again, a fifth joke gets no "I'm out" line (the 24 h cooldown the gag saved).
# The run ends with "/dialogue walkoff reset", so the test doesn't use up the character's real 24 h.
# Prints PASS or FAIL per check; exits 0 only when every check passed. The look of the walk is a hand check.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-walkoff-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
DIR="src/GameServer/bots/persoper/dialogue"
RECORDS="$SERVER/logs"   # the recordings stay there after the run
LOG="$SERVER/logs/server.log"

die() { echo "check-walkoff-ingame: $*" >&2; exit 2; }
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
# What Shempenstein said in a recording: party chat or /say.
said_by() { sed -nE "s/.*\\] ([Tt]he )?$1[^:\"]*: \"(.*)\"\$/\\2/p; s/.*\\] ([Tt]he )?$1( [A-Za-z]+)? says, \"(.*)\"\$/\\3/p" "$2" |
  sed -E 's/[[:space:]]+$//'; }
# The chat lines from the moment the player said $2 in group chat until $3 (default 20) seconds later.
within() { awk -v pat="$2" -v me="$CHARACTER" -v span="${3:-20}" '
  function secs(t) { split(t, a, ":"); return a[1] * 3600 + a[2] * 60 + a[3] }
  index($0, "[Party] " me ": \"" pat "\"") || index($0, "[Party] " me ": \"" pat " \"") { start = secs($1); seen = 1; next }
  seen && secs($1) - start <= span' "$1"; }
# This run's walk-off notes for Shempenstein in server.log (lines after $1).
notes() { tail -n +"$1" "$LOG" | grep -F "PERSONALITY_WALKOFF" | grep -F "player=\"$CHARACTER\""; }

THINGS=(troll kobold skeleton yeti)
joke() { echo "shemp, why did the $1 cross the road? to get to the inn"; }
LAST="$(joke goblin)"
start=$(( $(wc -l <"$LOG") + 1 ))
f="$RECORDS/walkoff-check.txt"
late=(--send-late="/dialogue walkoff reset" --send-late="/dialogue walkoff always")
for thing in "${THINGS[@]}"; do late+=("--send-late=/g $(joke "$thing")"); done
late+=(--send-late="/dialogue walkoff normal" "--send-late=/g $LAST" --send-late="/dialogue walkoff reset")
echo "== Walk-off gag: four jokes with the test shortcut on, one on the normal chance (about 3.5 minutes)"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/invite Shempenstein" --send="/dialogue gather" --listen=5 \
  --wait-for=Shempenstein --send-late-gap=20 "${late[@]}" \
  --record="$f" --record-seconds=20 --overwrite >"$f.log" 2>&1
grep -q "Shempenstein" "$f" || fail "Shempenstein never showed up in the recording; see $f (did the /invite work?)"

quits=$(rows "~ reply jokequit"); backs=$(rows "~ reply jokereturn")
for thing in "${THINGS[@]}"; do
  said=$(said_by Shempenstein <(within "$f" "$(joke "$thing")"))
  echo "  the $thing:"; [[ -n $said ]] && echo "$said" | sed 's/^/      Shempenstein: /'
done
all=$(said_by Shempenstein "$f")
grep -qxFf <(echo "$quits") <<<"$all" && pass "he said an \"I'm out\" line" ||
  fail "no \"~ reply jokequit\" line (four laughs in a row is a 1 in 40 chance: run it again); see $f"
grep -qxFf <(echo "$backs") <<<"$all" && pass "he said an \"I'm back\" line" || fail "no \"~ reply jokereturn\" line; see $f"

log=$(notes "$start")
echo "  server.log:"; [[ -n $log ]] && echo "$log" | sed -E 's/^.*PERSONALITY_WALKOFF/      PERSONALITY_WALKOFF/'
grep -q 'PERSONALITY_WALKOFF start bot="Shempenstein".* mode=quit' <<<"$log" && pass "he left the group" ||
  fail "no walk-off start (mode=quit) for Shempenstein ($(grep -o 'reason="[^"]*"' <<<"$log" | head -1))"
grep -q 'PERSONALITY_WALKOFF rejoined bot="Shempenstein"' <<<"$log" && pass "he rejoined the group" ||
  fail "no rejoin ($(grep -F norejoin <<<"$log" | grep -o 'reason="[^"]*"' | head -1))"
far=$(grep -F 'PERSONALITY_WALKOFF end bot="Shempenstein"' <<<"$log" | grep -oE "distance=[0-9]+" | cut -d= -f2 | sort -n | tail -1)
if [[ -n $far ]] && (( far >= 200 )); then pass "he walked off: $far away when he rejoined"
elif [[ -n $far ]]; then fail "he only got $far away (200 or more expected)"
else fail "no walk-off end for Shempenstein"; fi
grep -q 'PERSONALITY_WALKOFF moved bot="Shempenstein"' <<<"$log" &&
  fail "he was out of reach at the rejoin and had to be moved back (his own plans weren't paused)" ||
  pass "he stayed in reach while he was out of the group"
teleports=$(tail -n +"$start" "$LOG" | grep -c "AUTONOMOUS_TOWN_TELEPORT bot=Shempenstein ")
(( teleports == 0 )) && pass "no town teleport during the run" || fail "$teleports town teleport(s) by Shempenstein during the run"
grep -q 'PERSONALITY_WALKOFF returned bot="Shempenstein"' <<<"$log" && pass "he came back to his spot" ||
  fail "no \"returned\" note ($(grep -oE 'PERSONALITY_WALKOFF notback.*distance=[0-9]+' <<<"$log" | head -1))"

said=$(said_by Shempenstein <(within "$f" "$LAST"))
echo "  the goblin (normal chance, after the gag):"; [[ -n $said ]] && echo "$said" | sed 's/^/      Shempenstein: /'
grep -qxFf <(echo "$quits") <<<"$said" && fail "the fifth joke got an \"I'm out\" line (the 24 h cooldown didn't hold)" ||
  pass "no gag on the normal chance after one (cooldown held)"

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
