#!/usr/bin/env bash
# check-pull-ingame.sh - in-game check of the named pull ("eagle, pull this") on a running test server. Run it
# yourself, outside any sandbox:
#
#   tools/check-pull-ingame.sh <server-folder> [character]       (about 3.5 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a GM test character in Midgard (default Headlesstest)
#   PULL_SPOT        where the character jumps to first (default: the nip mephit camp in Midgard, region 100, levels
#                    23 to 27 and barely aggressive, so not grey to Eaglefang (26) and Shempenstein (25))
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's dialogue files for intents, Eaglefang, Gruffdoc, Shempenstein and Casual to be the repo's,
# and its GameServer.dll to have "/dialogue target". Invites Eaglefang (a healer) and Shempenstein (a warrior, the
# tank), brings them over with /dialogue gather and turns on "/dialogue orders always". The headless client can't
# target, so "/dialogue target nearest|clear" sets the character's target on the server. Then, 10 s apart:
#   1. no target, "/g eagle, pull it": Pull/NoTarget and an Eaglefang "~ reply orderpullwhat" line;
#   2. no target, "/g pull that" (to nobody): Pull/NoTarget for the group and a "pull what?" line from one of them;
#   3. a mob targeted, "/g eagle, pull this": Pull/Yes, "pull ... pulled=True attackers=1" (Shempenstein sent in at
#      once) and an orderpull line; later a "NAMED_PULL support done" note says if the healer made contact;
#   4. a new mob targeted, "/g shemp, pull this one": Pull/Yes and pulled=True for Shempenstein, an orderpull line;
#   5. a new mob targeted, "/g pull the mob" (to nobody): the plain /pull ("PERSONALITY_ORDER open pull", with
#      Shempenstein leading it).
# The run ends with "/dialogue orders normal" and "/dialogue target clear". The bots fight for real; a fight that
# isn't over by the next pull (about 50 s later) makes that bot busy: it says so, and the check fails; run it again.
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-pull-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
DIR="src/GameServer/bots/persoper/dialogue"
PULL_SPOT="${PULL_SPOT:-695700 717600 5705 100}"
RECORDS="$SERVER/logs"   # the recording stays there after the run
LOG="$SERVER/logs/server.log"

die() { echo "check-pull-ingame: $*" >&2; exit 2; }
if [[ -z ${DAOC_PASSWORD:-} ]]; then
  [[ -t 0 ]] || die "set DAOC_PASSWORD (the test account's password) first"
  read -rsp "Password for $ACCOUNT: " DAOC_PASSWORD; echo
  export DAOC_PASSWORD
fi
[[ -f $CLIENT ]] || die "build first: dotnet build src/DaocServer.sln -c Release"
for file in intents.dlg eaglefang.dlg gruffdoc.dlg shempenstein.dlg casual.dlg; do
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
# A bot's rows of one section: its own file's over its type's.
rows() { if [[ $1 == Eaglefang ]]; then section gruffdoc "$2"; section eaglefang "$2"; else section casual "$2"; section shempenstein "$2"; fi; }
# True when $1 is one of the rows on stdin, alone or with a mood tint before or after it.
one_of() { local said="$1" row; while IFS= read -r row; do
  [[ -z $row ]] && continue
  local glob="${row//\{[a-z]*\}/*}"
  # shellcheck disable=SC2053
  [[ $said == $glob || $said == *" "$glob || $said == $glob" "* ]] && return 0
done; return 1; }
# What one bot said in a recording: party chat ("Name: "..."") or /say ("Name says, "...").
said_by() { sed -nE "s/.*\\] ([Tt]he )?$1[^:\"]*: \"(.*)\"\$/\\2/p; s/.*\\] ([Tt]he )?$1( [A-Za-z]+)? says, \"(.*)\"\$/\\3/p" "$2" |
  sed -E 's/[[:space:]]+$//'; }
# The chat lines from the moment the player said $2 in group chat until $3 (default 10) seconds later.
within() { awk -v pat="$2" -v me="$CHARACTER" -v span="${3:-10}" '
  function secs(t) { split(t, a, ":"); return a[1] * 3600 + a[2] * 60 + a[3] }
  !seen && index($0, "[Party] " me ": \"" pat) { start = secs($1); seen = 1; next }
  seen && secs($1) - start <= span' "$1"; }
# This run's server.log lines (after line $1). server.log rolls over at 100 MB (to server.<date>.<n>.log); when it
# did during the run, the rest of the old one comes first.
run_log() { if (( $(wc -l <"$LOG") + 1 < $1 )); then
    tail -n +"$1" "$(ls -t "$SERVER"/logs/server.*.log 2>/dev/null | head -1)"; cat "$LOG"
  else tail -n +"$1" "$LOG"; fi; }
# This run's order notes for the character.
notes() { run_log "$1" | grep -F "PERSONALITY_ORDER" | grep -F "player=\"$CHARACTER\""; }
# True when a note says bot $2 got order $3 with answer $4 (named True/False $5).
noted() { grep -F "bot=\"$2" <<<"$1" | grep -qF "order=$3 answer=$4 named=$5"; }
# The check for one answer line: who, the line the player said, the section.
answered() { local who="$1" line="$2" sect="$3" said one hit=""
  said=$(said_by "$who" <(within "$f" "$line"))
  [[ -n $said ]] && echo "$said" | sed "s/^/      $who: /"
  while IFS= read -r one; do [[ -n $one ]] && rows "$who" "$sect" | one_of "$one" && hit="$one"; done <<<"$said"
  [[ -n $hit ]]; }

WHAT_EAGLE="eagle, pull it"
WHAT_OPEN="pull that"
PULL_EAGLE="eagle, pull this"
PULL_SHEMP="shemp, pull this one"
PULL_OPEN="pull the mob"
start=$(( $(wc -l <"$LOG") + 1 ))
f="$RECORDS/pull-check.txt"
echo "== Named pull: no target, a healer, a tank, and the plain /pull (about 3.5 minutes)"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/jump to $PULL_SPOT" --send="/invite Eaglefang" --send="/invite Shempenstein" --send="/dialogue gather" --listen=5 \
  --wait-for=Shempenstein --send-late-gap=10 \
  --send-late="/dialogue gather" --send-late="/dialogue forget" --send-late="/dialogue orders always" --send-late="/dialogue target clear" \
  --send-late="/g $WHAT_EAGLE" --send-late="/g $WHAT_OPEN" \
  --send-late="/dialogue gather" --send-late="/dialogue target nearest" --send-late="/g $PULL_EAGLE" \
  --send-late="/dialogue state Eaglefang" --send-late="/dialogue state Shempenstein" \
  --send-late="/dialogue forget" --send-late="/dialogue gather" --send-late="/dialogue target nearest" --send-late="/g $PULL_SHEMP" \
  --send-late="/dialogue forget" --send-late="/dialogue gather" --send-late="/dialogue target nearest" --send-late="/g $PULL_OPEN" \
  --send-late="/dialogue orders normal" --send-late="/dialogue target clear" \
  --record="$f" --record-seconds=15 --overwrite >"$f.log" 2>&1
grep -q "Shempenstein" "$f" || fail "Shempenstein never showed up in the recording; see $f (did the /invite work?)"
grep -q "Eaglefang" "$f" || fail "Eaglefang never showed up in the recording; see $f (did the /invite work?)"
grep -q "Target: nothing" "$f" || fail "no \"/dialogue target\" answer in the recording; is the server's GameServer.dll the new one? See $f"
grep -q "Target: no mob" "$f" && fail "no mob within range that isn't grey to the bots; try another PULL_SPOT. See $f"

log=$(notes "$start")
echo "  server.log:"; [[ -n $log ]] && echo "$log" | sed -E 's/^.*PERSONALITY_ORDER/      PERSONALITY_ORDER/'
support=$(run_log "$start" | grep -F 'NAMED_PULL support done bot="Eaglefang')
[[ -n $support ]] && echo "$support" | sed -E 's/^.*NAMED_PULL/      NAMED_PULL/'

# 1. Named, no target.
echo "  \"$WHAT_EAGLE\" (no target):"
noted "$log" Eaglefang Pull NoTarget True && pass "Eaglefang had nothing to pull" || fail "no Pull/NoTarget note for Eaglefang"
answered Eaglefang "$WHAT_EAGLE" "~ reply orderpullwhat" && pass "he said an orderpullwhat line" ||
  fail "no orderpullwhat line from Eaglefang within 10 s (a casting bot drops a reply; run it again). See $f"

# 2. Open, no target.
echo "  \"$WHAT_OPEN\" (to nobody, no target):"
grep -qF "order=Pull answer=NoTarget named=False" <<<"$log" && pass "noted as nothing to pull" || fail "no open Pull/NoTarget note"
{ answered Eaglefang "$WHAT_OPEN" "~ reply orderpullwhat" || answered Shempenstein "$WHAT_OPEN" "~ reply orderpullwhat"; } &&
  pass "someone asked \"pull what?\"" || fail "no orderpullwhat line from either bot within 10 s. See $f"

# 3. The healer pulls; the tank attacks at once.
echo "  \"$PULL_EAGLE\" (a mob targeted):"
noted "$log" Eaglefang Pull Yes True && pass "Eaglefang said yes" || fail "no Pull/Yes note for Eaglefang ($(grep -F 'bot="Eaglefang' <<<"$log" | grep -F 'order=Pull' | grep -o 'answer=[A-Za-z]*' | tail -1))"
grep -qE 'PERSONALITY_ORDER pull bot="Eaglefang.* pulled=True attackers=[1-9]' <<<"$log" &&
  pass "the healer pulled and the others were sent in at once" || fail "no \"pull bot=Eaglefang ... pulled=True attackers>0\" note"
answered Eaglefang "$PULL_EAGLE" "~ reply orderpull" && pass "he said an orderpull line" || fail "no orderpull line from Eaglefang within 10 s. See $f"
grep -q 'contact=True' <<<"$support" && pass "the healer made contact, then went back to healing" ||
  fail "no \"NAMED_PULL support done ... contact=True\" note for Eaglefang (did his swing or spell land?)"

# 4. The tank, named.
echo "  \"$PULL_SHEMP\" (a new mob targeted):"
noted "$log" Shempenstein Pull Yes True && pass "Shempenstein said yes" ||
  fail "no Pull/Yes note for Shempenstein ($(grep -F 'bot="Shempenstein' <<<"$log" | grep -F 'order=Pull' | grep -o 'answer=[A-Za-z]*' | tail -1); Busy means the last fight wasn't over)"
grep -qE 'PERSONALITY_ORDER pull bot="Shempenstein.* pulled=True' <<<"$log" && pass "the tank pulled" || fail "no \"pull bot=Shempenstein ... pulled=True\" note"
answered Shempenstein "$PULL_SHEMP" "~ reply orderpull" && pass "he said an orderpull line" || fail "no orderpull line from Shempenstein within 10 s. See $f"

# 5. No name: the plain /pull.
echo "  \"$PULL_OPEN\" (to nobody, a new mob targeted):"
open=$(grep -F "PERSONALITY_ORDER open pull" <<<"$log")
[[ -n $open ]] && pass "it ran the plain /pull: ${open##*result=}" || fail "no \"open pull\" note"

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
