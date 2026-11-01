#!/usr/bin/env bash
# check-misses-ingame.sh - in-game check of the intent misses seen 2026-10-30 (Part A of the intent work) on a
# running test server. Run it yourself, outside any sandbox:
#
#   tools/check-misses-ingame.sh <server-folder> [character]       (about 3 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a level 1 GM test character in Midgard (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's dialogue files for intents, Persoper, Kobaldjovi, Shempenstein and Casual to be the repo's.
# Part 1 summons Persoper and tells her the troll and Pluto jokes: at least one answer must come from her
#   "~ reply joke" lines within 10 s, and none from her "~ fallback" (the "you've lost me" lines).
# Part 2 teleports to Kobaldjovi, invites him and says "jovie you're fat": he must answer, and not with a
#   "~ fallback" line (it used to be "You seem to have spoken in tongues").
# Part 3 summons Persoper, teleports to Shempenstein, invites him and says "shemp i have swords taller than you":
#   Shempenstein must answer, and Persoper must not answer before him.
# Bots that are casting drop a due reply, and their lines are random: if a part is short, run it again.
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-misses-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
DIR="src/GameServer/bots/persoper/dialogue"
RECORDS="$SERVER/logs"   # the recordings stay there after the run

die() { echo "check-misses-ingame: $*" >&2; exit 2; }
if [[ -z ${DAOC_PASSWORD:-} ]]; then
  [[ -t 0 ]] || die "set DAOC_PASSWORD (the test account's password) first"
  read -rsp "Password for $ACCOUNT: " DAOC_PASSWORD; echo
  export DAOC_PASSWORD
fi
[[ -f $CLIENT ]] || die "build first: dotnet build src/DaocServer.sln -c Release"
for file in intents.dlg persoper.dlg kobaldjovi.dlg shempenstein.dlg casual.dlg; do
  cmp -s "$REPO/$DIR/$file" "$SERVER/dialogue/$file" || die "$SERVER/dialogue/$file isn't the repo's; copy it over first"
done
pid=$(cat "$SERVER/run/server.pid" 2>/dev/null || true)
[[ $pid =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null || die "the server in $SERVER isn't running; start it first"
login=$("$DOTNET" "$CLIENT" --account="$ACCOUNT" --login-only 2>&1) ||
  die "can't log in as $ACCOUNT ($(grep -m1 FAIL <<<"$login")); check DAOC_PASSWORD"
grep -q " $CHARACTER, level" <<<"$login" || die "$ACCOUNT has no character $CHARACTER"

failures=0
pass() { echo "PASS  $*"; }
fail() { echo "FAIL  $*"; failures=$((failures + 1)); }

# The rows of one section of a bot's file, tags off, {0} filled in with the character's name.
section() { awk -v want="$2" '/^~ /{f=($0==want); next} /^===/{f=0} f&&NF&&!/^#/' "$REPO/$DIR/$1.dlg" |
  sed -E "s/[[:space:]]*\[(if|once|cooldown|allow)[^]]*\]//g; s/[[:space:]]+\$//; s/\{0\}/$CHARACTER/g"; }
# What one bot said in a recording: party chat ("Name: "..."") or /say ("Name says, "..."").
said_by() { sed -nE "s/.*\\] ([Tt]he )?$1[^:\"]*: \"(.*)\"\$/\\2/p; s/.*\\] ([Tt]he )?$1( [A-Za-z]+)? says, \"(.*)\"\$/\\3/p" "$2"; }

# The chat lines in a recording from the moment the player said $2 until 10 seconds later (a reply, not idle chatter).
within() { awk -v pat="$2" '
  function secs(t) { split(t, a, ":"); return a[1] * 3600 + a[2] * 60 + a[3] }
  index($0, "You say, \"" pat) { start = secs($1); next }
  start && secs($1) - start <= 10' "$1"; }

echo "== 1. Persoper reads the troll and Pluto jokes as jokes (about 60 seconds)"
f="$RECORDS/misses-check-1.txt"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/persoper" --listen=25 --send-late-gap=11 \
  --send-late="Persoper, why did the troll cross the road?" \
  --send-late="Persoper, why did the astronaut leave Pluto? It had no atmosphere" \
  --record="$f" --record-seconds=55 --overwrite >"$f.log" 2>&1
for joke in "Persoper, why did the troll" "Persoper, why did the astronaut"; do
  said=$(said_by Persoper <(within "$f" "$joke"))
  if [[ -z $said ]]; then
    fail "no answer within 10 s to \"$joke...\" (she may have been casting; run it again). See $f"
  else
    echo "$said" | sed 's/^/      Persoper: /'
    grep -qFf <(section persoper "~ reply joke") <<<"$said" && pass "a joke-line answer to \"$joke...\"" ||
      fail "her answer to \"$joke...\" wasn't one of her joke lines; see $f"
    grep -qFf <(section persoper "~ fallback") <<<"$said" && fail "a \"you've lost me\" fallback line reached the chat"
  fi
done

echo "== 2. Kobaldjovi reads \"jovie you're fat\" as banter (about 40 seconds)"
f="$RECORDS/misses-check-2.txt"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/tele Kobaldjovi" --send="/invite Kobaldjovi" --listen=6 --goto=Kobaldjovi --follow=Kobaldjovi \
  --send-late-gap=11 --send-late="jovie you're fat" --send-late="jovie you're fat, I said" \
  --record="$f" --record-seconds=35 --overwrite >"$f.log" 2>&1
said=$(said_by Kobaldjovi <(within "$f" "jovie you"))
if [[ -z $said ]]; then
  fail "Kobaldjovi said nothing; see $f (did the /tele and /invite work?)"
else
  echo "$said" | sed 's/^/      Kobaldjovi: /'
  pass "he answered"
  grep -qFf <(section kobaldjovi "~ fallback") <<<"$said" && fail "a fallback line (\"spoken in tongues\"...) reached the chat" ||
    pass "no fallback line in his answers"
fi

echo "== 3. Shempenstein, not Persoper, answers \"shemp i have swords taller than you\" (about 50 seconds)"
f="$RECORDS/misses-check-3.txt"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/tele Shempenstein" --send="/invite Shempenstein" --send="/persoper" --listen=10 \
  --goto=Shempenstein --follow=Shempenstein --send-late-gap=11 \
  --send-late="shemp i have swords taller than you" \
  --record="$f" --record-seconds=30 --overwrite >"$f.log" 2>&1
# Only replies within 10 s of the line count; Persoper greets on arrival and bots chatter on their own.
after=$(within "$f" "shemp i have swords")
shemp=$(said_by Shempenstein <(echo "$after"))
first=$(sed -nE 's/.*\] ([Tt]he )?(Shempenstein|Persoper)[^:"]*: ".*/\2/p; s/.*\] ([Tt]he )?(Shempenstein|Persoper)( [A-Za-z]+)? says, ".*/\2/p' <<<"$after" | head -1)
[[ -n $shemp ]] && { echo "$shemp" | sed 's/^/      Shempenstein: /'; pass "Shempenstein answered"; } ||
  fail "Shempenstein didn't answer within 10 s; see $f (he may have been casting; run it again)"
[[ $first != Persoper ]] && pass "Persoper didn't answer before him" || fail "Persoper answered first; see $f"

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
