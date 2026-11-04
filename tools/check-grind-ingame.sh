#!/usr/bin/env bash
# check-grind-ingame.sh - in-game check of /grind with the PerSoPers ("let's grind" / "stop grinding") on a running test
# server. Run it yourself, outside any sandbox:
#
#   tools/check-grind-ingame.sh <server-folder> [character]       (about 9 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a GM test character in Midgard (default Headlesstest)
#   GRIND_SPOT       where the character grinds (default: the frosty scuttlebugs in Midgard, region 100, level 25, as
#                    in check-pull-ingame.sh; no aggressive mob within 2500 units)
#   GREY_SPOT        where it checks the grey stop (default: the implings in Midgard, level 5, never aggressive)
#   GRIND_MINUTES    how long the first run grinds (1 to 10, default 4)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's GameServer.dll to have the GRIND notes in server.log, and its dialogue files for intents,
# Persoper, Eaglefang, Gruffdoc, Shempenstein and Casual to be the repo's. One login, two parts:
#   1. At GRIND_SPOT: summons Persoper, invites Eaglefang (a healer) and Shempenstein (a warrior, the tank), brings them
#      over with /dialogue gather, says "/g let's grind", waits GRIND_MINUTES, then says "/g stop grinding". Checks:
#      "GRIND start" (and the "Grind mode active" message), at least three "GRIND pull" notes, at least one
#      "GRIND rest" note, and "GRIND stop ... reason="stopped from chat"". When a pull was red, purple or deep
#      purple to the character, a "GRIND_LINE" note of that kind and its line in party chat from the bot's section;
#      with no such pull the con-line check is skipped (the con depends on the character's level).
#   2. In the same login (bots in a logged-out player's group stay tied to it for a while, so a second login can't
#      invite them), jumps to GREY_SPOT, gathers the bots and says "/g let's grind" again. When the implings are grey
#      to the character, grind stops by itself ("everything left nearby is grey") with a "~ reply grindgrey" line;
#      when they aren't (or nothing there is in reach), that check is skipped, and the run ends with "/g stop grinding".
# The bots fight for real. Prints PASS, FAIL or SKIP per check; exits 0 only when no check failed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-grind-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
DIR="src/GameServer/bots/persoper/dialogue"
GRIND_SPOT="${GRIND_SPOT:-770570 655518 5981 100}"
GREY_SPOT="${GREY_SPOT:-765300 742300 5204 100}"
GRIND_MINUTES="${GRIND_MINUTES:-4}"
RECORDS="$SERVER/logs"   # the recordings stay there after the run
LOG="$SERVER/logs/server.log"
BOTS=(Persoper Eaglefang Shempenstein)

die() { echo "check-grind-ingame: $*" >&2; exit 2; }
[[ $GRIND_MINUTES =~ ^[0-9]+$ ]] && (( GRIND_MINUTES >= 1 && GRIND_MINUTES <= 10 )) || die "GRIND_MINUTES must be 1 to 10"
if [[ -z ${DAOC_PASSWORD:-} ]]; then
  [[ -t 0 ]] || die "set DAOC_PASSWORD (the test account's password) first"
  read -rsp "Password for $ACCOUNT: " DAOC_PASSWORD; echo
  export DAOC_PASSWORD
fi
[[ -f $CLIENT ]] || die "build first: dotnet build src/DaocServer.sln -c Release"
for file in intents.dlg persoper.dlg eaglefang.dlg gruffdoc.dlg shempenstein.dlg casual.dlg; do
  cmp -s "$REPO/$DIR/$file" "$SERVER/dialogue/$file" || die "$SERVER/dialogue/$file isn't the repo's; copy it over first"
done
pid=$(cat "$SERVER/run/server.pid" 2>/dev/null || true)
[[ $pid =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null || die "the server in $SERVER isn't running; start it first"
[[ -f $LOG ]] || die "no $LOG"
login=$("$DOTNET" "$CLIENT" --account="$ACCOUNT" --login-only 2>&1) ||
  die "can't log in as $ACCOUNT ($(grep -m1 FAIL <<<"$login")); check DAOC_PASSWORD"
grep -q " $CHARACTER, level" <<<"$login" || die "$ACCOUNT has no character $CHARACTER"
level=$(grep -o " $CHARACTER, level [0-9]*" <<<"$login" | grep -o '[0-9]*$')

failures=0
pass() { echo "PASS  $*"; }
fail() { echo "FAIL  $*"; failures=$((failures + 1)); }
skip() { echo "SKIP  $*"; }

# The notes before (part 1) and after (part 2) the first chat stop.
part1() { awk '{print} /GRIND stop/ && /stopped from chat/ {exit}' <<<"$1"; }
part2() { awk 'f {print} /GRIND stop/ && /stopped from chat/ {f=1}' <<<"$1"; }
# The rows of one section of a bot's file, tags off, {0} filled in with the character's name.
section() { awk -v want="$2" '/^~ /{f=($0==want); next} /^===/{f=0} f&&NF&&!/^#/' "$REPO/$DIR/$1.dlg" |
  sed -E "s/[[:space:]]*\[(if|once|cooldown|allow)[^]]*\]//g; s/[[:space:]]+\$//; s/\{0\}/$CHARACTER/g"; }
# A bot's rows of one section: its own file's over its type's.
rows() { case $1 in
  Eaglefang) section gruffdoc "$2"; section eaglefang "$2" ;;
  Shempenstein) section casual "$2"; section shempenstein "$2" ;;
  *) section persoper "$2" ;;
esac; }
# True when $1 is one of the rows on stdin, alone or with a mood tint before or after it.
one_of() { local said="$1" row; while IFS= read -r row; do
  [[ -z $row ]] && continue
  local glob="${row//\{[a-z]*\}/*}"
  # shellcheck disable=SC2053
  [[ $said == $glob || $said == *" "$glob || $said == $glob" "* ]] && return 0
done; return 1; }
# What one bot said in party chat in a recording.
said_by() { sed -nE "s/.*\\] ([Tt]he )?$1[^:\"]*: \"(.*)\"\$/\\2/p" "$2" | sed -E 's/[[:space:]]+$//'; }
# This run's server.log lines (after line $1). server.log rolls over at 100 MB (to server.<date>.<n>.log); when it
# did during the run, the rest of the old one comes first.
run_log() { if (( $(wc -l <"$LOG") + 1 < $1 )); then
    tail -n +"$1" "$(ls -t "$SERVER"/logs/server.*.log 2>/dev/null | head -1)"; cat "$LOG"
  else tail -n +"$1" "$LOG"; fi; }
# This run's GRIND and GRIND_LINE notes for the character.
notes() { run_log "$1" | grep -E 'GRIND(_LINE)? ' | grep -F "player=\"$CHARACTER\""; }
# True when one of the bots said a line of section $2 in recording $1 (prints what they said).
said_section() { local f="$1" sect="$2" who said one hit=""
  for who in "${BOTS[@]}"; do
    said=$(said_by "$who" "$f")
    while IFS= read -r one; do
      [[ -n $one ]] && rows "$who" "$sect" | one_of "$one" && { echo "      $who: $one"; hit=1; }
    done <<<"$said"
  done
  [[ -n $hit ]]; }

# The waits: --send-late-gap is the same after every late line, so a minute of grinding is one harmless state line.
fillers=()
for (( i = 1; i < GRIND_MINUTES; i++ )); do fillers+=(--send-late="/dialogue state Shempenstein"); done

echo "== /grind with the PerSoPers, $CHARACTER (level ${level:-?}): $GRIND_MINUTES minutes at GRIND_SPOT, then GREY_SPOT"
start=$(( $(wc -l <"$LOG") + 1 ))
f="$RECORDS/grind-check.txt"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/jump to $GRIND_SPOT" --send="/persoper" --send="/invite Eaglefang" --send="/invite Shempenstein" \
  --send="/dialogue gather" --listen=5 --wait-for=Shempenstein --send-late-gap=60 \
  --send-late="/dialogue gather" --send-late="/g let's grind" "${fillers[@]}" --send-late="/g stop grinding" \
  --send-late="/jump to $GREY_SPOT" --send-late="/dialogue gather" --send-late="/g let's grind" \
  --send-late="/g stop grinding" --record="$f" --record-seconds=15 --overwrite >"$f.log" 2>&1
for who in "${BOTS[@]}"; do grep -q "$who" "$f" || fail "$who never showed up in the recording; see $f"; done
all=$(notes "$start")
log=$(part1 "$all")
echo "  server.log:"; [[ -n $log ]] && echo "$log" | sed -E 's/^.*(GRIND(_LINE)? )/      \1/'

grep -q 'GRIND start' <<<"$log" && pass "\"let's grind\" started grind mode" ||
  fail "no \"GRIND start\" note (is the server's GameServer.dll the new one? see $f for the refusal)"
grep -q "Grind mode active" "$f" && pass "the player was told grind mode is on" || fail "no \"Grind mode active\" message in $f"
pulls=$(grep -c 'GRIND pull' <<<"$log")
(( pulls >= 3 )) && pass "$pulls pulls on their own" || fail "only $pulls pull(s) in $GRIND_MINUTES minutes (at least 3 expected)"
rests=$(grep -c 'GRIND rest' <<<"$log")
(( rests >= 1 )) && pass "the bots rested $rests time(s) between pulls" || fail "no \"GRIND rest\" note (nobody rested between pulls)"
grep -q 'GRIND stop.*reason="stopped from chat"' <<<"$log" && pass "\"stop grinding\" ended it" ||
  fail "no \"GRIND stop ... stopped from chat\" note ($(grep -o 'GRIND stop.*' <<<"$log" | head -1))"

# The con lines: only when a pull was red, purple or deep purple to the character.
kinds=$(grep -o 'con=\(Red\|Purple\|Deep\)' <<<"$log" | sort -u | sed 's/con=//')
if [[ -z $kinds ]]; then
  skip "no red or purple pull at GRIND_SPOT for a level ${level:-?} character ($(grep -o 'con=[A-Za-z]*' <<<"$log" | sort | uniq -c | paste -sd, -))"
else
  for kind in $kinds; do
    sect="~ reply grind${kind,,}"
    if grep -q "GRIND_LINE .*kind=Grind$kind" <<<"$log"; then
      said_section "$f" "$sect" && pass "a $kind pull got a \"$sect\" line" || fail "a GRIND_LINE $kind note but no line of \"$sect\" in $f"
    else
      fail "a $kind pull but no GRIND_LINE note for it (one line a minute at most; the first one should have one)"
    fi
  done
fi

echo
echo "== The grey stop at GREY_SPOT"
g="$f"
log=$(part2 "$all")
echo "  server.log:"; [[ -n $log ]] && echo "$log" | sed -E 's/^.*(GRIND(_LINE)? )/      \1/'
if grep -q 'GRIND stop.*reason="everything left nearby is grey' <<<"$log"; then
  pass "grind stopped by itself: everything nearby is grey"
  grep -q 'GRIND_LINE .*kind=GrindGrey' <<<"$log" && said_section "$g" "~ reply grindgrey" &&
    pass "a bot said a \"~ reply grindgrey\" line" || fail "no grindgrey line (note and party chat) in $g"
elif grep -q 'GRIND pull' <<<"$log"; then
  skip "the implings aren't grey to a level ${level:-?} character ($(grep -o 'con=[A-Za-z]*' <<<"$log" | sort -u | paste -sd, -)); set GREY_SPOT"
elif grep -q 'GRIND start' <<<"$log"; then
  # Grind ran but found nothing to pull and nothing all grey: no mob in reach is grey to this character.
  skip "no mob near GREY_SPOT is grey to a level ${level:-?} character; use a higher character or set GREY_SPOT"
else
  fail "neither a grey stop nor a pull at GREY_SPOT ($(grep -o 'GRIND stop.*' <<<"$log" | head -1)); see $g"
fi

(( failures == 0 )) && echo "No check failed." || echo "$failures check(s) failed."
(( failures == 0 ))
