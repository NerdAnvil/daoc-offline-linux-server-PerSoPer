#!/usr/bin/env bash
# check-reactions-ingame.sh - in-game check of the short reactions (intent Part B2) on a running test server.
# Run it yourself, outside any sandbox:
#
#   tools/check-reactions-ingame.sh <server-folder> [character]       (about 4 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a level 1 GM test character in Midgard (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's dialogue files for intents, Persoper, Shempenstein and Casual to be the repo's.
# Summons Persoper, teleports to Shempenstein and invites him, then says lol, sorry, brb, back and nah:
#   part 1 in group chat (five plain, five naming Shemp), part 2 in /say (five plain).
# For each line, in the 10 s after it:
#   - no bot answers with a joke or a "you've lost me" fallback line;
#   - at most one bot gives a short reaction, and it comes from the matching "~ reply laugh" (sorry, away,
#     back, disagree) lines.
# Over the run: some lines get a reaction and some get none (now and then, never every time).
# Other lines in the window (ambient chatter, greetings) are shown and ignored.
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-reactions-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
DIR="src/GameServer/bots/persoper/dialogue"
RECORDS="$SERVER/logs"   # the recordings stay there after the run

die() { echo "check-reactions-ingame: $*" >&2; exit 2; }
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
login=$("$DOTNET" "$CLIENT" --account="$ACCOUNT" --login-only 2>&1) ||
  die "can't log in as $ACCOUNT ($(grep -m1 FAIL <<<"$login")); check DAOC_PASSWORD"
grep -q " $CHARACTER, level" <<<"$login" || die "$ACCOUNT has no character $CHARACTER"

failures=0
reacted=0
silent=0
pass() { echo "PASS  $*"; }
fail() { echo "FAIL  $*"; failures=$((failures + 1)); }

# The rows of one section of a bot's file, tags off, {0} filled in with the character's name.
section() { awk -v want="$2" '/^~ /{f=($0==want); next} /^===/{f=0} f&&NF&&!/^#/' "$REPO/$DIR/$1.dlg" |
  sed -E "s/[[:space:]]*\[(if|once|cooldown|allow)[^]]*\]//g; s/[[:space:]]+\$//; s/\{0\}/$CHARACTER/g"; }
# Persoper's and Shempenstein's (his own file over Casual's) rows of one section.
both() { section persoper "$1"; section casual "$1"; section shempenstein "$1"; }
# What one bot said in a recording: party chat ("Name: "..."") or /say ("Name says, "..."").
said_by() { sed -nE "s/.*\\] ([Tt]he )?$1[^:\"]*: \"(.*)\"\$/\\2/p; s/.*\\] ([Tt]he )?$1( [A-Za-z]+)? says, \"(.*)\"\$/\\3/p" "$2" |
  sed -E 's/[[:space:]]+$//'; }
# The chat lines from the moment the player said $2 (in /say or group chat) until 10 seconds later.
within() { awk -v pat="$2" -v me="$CHARACTER" '
  function secs(t) { split(t, a, ":"); return a[1] * 3600 + a[2] * 60 + a[3] }
  index($0, "You say, \"" pat "\"") || index($0, "[Party] " me ": \"" pat "\"") || index($0, "[Party] " me ": \"" pat " \"") {
    start = secs($1); seen = 1; next }
  seen && secs($1) - start <= 10' "$1"; }
# The reaction section a test line should be answered from.
wanted() { case "$1" in
  *lol*) echo "~ reply laugh" ;; *sorry*) echo "~ reply sorry" ;; *brb*) echo "~ reply away" ;;
  *back*) echo "~ reply back" ;; *nah*) echo "~ reply disagree" ;; esac; }
REACTIONS=$(for name in laugh sorry away back disagree; do both "~ reply $name"; done)
WRONG=$(both "~ reply joke"; both "~ fallback")

# Checks the 10 s after each line of one recording.
judge() {
  local f=$1; shift
  local line after persoper shemp lines count
  for line in "$@"; do
    after=$(within "$f" "$line")
    persoper=$(said_by Persoper <(echo "$after"))
    shemp=$(said_by Shempenstein <(echo "$after"))
    lines=$(printf '%s\n%s\n' "$persoper" "$shemp" | sed '/^$/d')
    echo "  \"$line\":"
    [[ -n $persoper ]] && echo "$persoper" | sed 's/^/      Persoper: /'
    [[ -n $shemp ]] && echo "$shemp" | sed 's/^/      Shempenstein: /'
    if [[ -n $lines ]] && grep -qxFf <(echo "$WRONG") <<<"$lines"; then
      fail "\"$line\" got a joke or fallback line"
    fi
    count=$(grep -cxFf <(echo "$REACTIONS") <<<"$lines")
    if (( count > 1 )); then
      fail "\"$line\" got $count reactions (one bot at most)"
    elif (( count == 1 )); then
      reacted=$((reacted + 1))
      grep -qxFf <(both "$(wanted "$line")") <<<"$lines" && pass "\"$line\": one fitting reaction" ||
        fail "\"$line\": the reaction isn't from the $(wanted "$line") lines"
    else
      silent=$((silent + 1))
      pass "\"$line\": no reaction"
    fi
  done
}

GROUP=("lol" "sorry" "brb" "back" "nah" "shemp lol" "sorry shemp" "brb shemp" "shemp im back" "nah shemp")
SAY=("lol" "sorry" "brb" "back" "nah")

echo "== 1. Group chat: ${#GROUP[@]} short lines (about 2.5 minutes)"
f="$RECORDS/reactions-check-1.txt"
late=(); for line in "${GROUP[@]}"; do late+=("--send-late=/g $line"); done
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/tele Shempenstein" --send="/invite Shempenstein" --send="/persoper" --listen=10 \
  --goto=Shempenstein --follow=Shempenstein --send-late-gap=11 "${late[@]}" \
  --record="$f" --record-seconds=135 --overwrite >"$f.log" 2>&1
grep -q "Shempenstein" "$f" || fail "Shempenstein never showed up in the recording; see $f (did the /tele and /invite work?)"
judge "$f" "${GROUP[@]}"

echo "== 2. /say: ${#SAY[@]} short lines (about 1.5 minutes)"
f="$RECORDS/reactions-check-2.txt"
late=(); for line in "${SAY[@]}"; do late+=("--send-late=$line"); done
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/tele Shempenstein" --send="/persoper" --listen=10 \
  --goto=Shempenstein --follow=Shempenstein --send-late-gap=11 "${late[@]}" \
  --record="$f" --record-seconds=80 --overwrite >"$f.log" 2>&1
judge "$f" "${SAY[@]}"

echo "== 3. Now and then: $reacted reacted, $silent silent"
(( reacted > 0 )) && pass "some lines got a reaction" ||
  fail "no line got a reaction (about a 1 in 100 chance by luck; bots that are casting drop replies; run it again)"
(( silent > 0 )) && pass "some lines got none" || fail "every line got a reaction (should be now and then)"

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
