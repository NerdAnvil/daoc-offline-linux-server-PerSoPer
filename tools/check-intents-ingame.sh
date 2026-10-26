#!/usr/bin/env bash
# check-intents-ingame.sh - in-game check of the small-talk intents (joke, grumble, smalltalk, question,
# compliment, agree, quote) on a running test server. Run it yourself, outside any sandbox:
#
#   tools/check-intents-ingame.sh <server-folder> [character]       (about 3 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a level 1 test character in Persoper's realm (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's dialogue files for intents, Persoper, Kobaldjovi, Spyburd and Eaglefang to be the repo's.
# Part 1 (no server needed) has the checker confirm the four bots' files.
# Part 2 summons Persoper and says one line per new intent, 11 seconds apart (a bot that is casting drops a
# due reply). It passes when her answers come from at least 5 of the 7 new sections and none from her
# "~ fallback" (the "you've lost me" lines). Her lines are random and she may be casting: run it again if short.
# Bot-to-bot answers (part 2 of the done-when line) are checked by hand from chat.log after a group session.
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-intents-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
CHECKER="$REPO/src/build/DialogueCheck/Release/DialogueCheck.dll"
DIR="src/GameServer/bots/persoper/dialogue"
RECORDS="$SERVER/logs"   # the recordings stay there after the run
INTENTS=(joke grumble smalltalk question compliment agree quote)
SAY=("lol good one" "ugh this camp sucks" "it's raining here, I need coffee" "have you ever been to the coast"
     "nice heal Persoper" "yeah, agreed" "I'll be back")

die() { echo "check-intents-ingame: $*" >&2; exit 2; }
if [[ -z ${DAOC_PASSWORD:-} ]]; then
  [[ -t 0 ]] || die "set DAOC_PASSWORD (the test account's password) first"
  read -rsp "Password for $ACCOUNT: " DAOC_PASSWORD; echo
  export DAOC_PASSWORD
fi
[[ -f $CLIENT && -f $CHECKER ]] || die "build first: dotnet build src/DaocServer.sln -c Release"
for file in intents.dlg persoper.dlg kobaldjovi.dlg spyburd.dlg eaglefang.dlg; do
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

# The rows of one section of persoper.dlg, tags off, {0} filled in with the character's name.
section() { awk -v want="$1" '/^~ /{f=($0==want); next} /^===/{f=0} f&&NF' "$REPO/$DIR/persoper.dlg" |
  sed -E "s/[[:space:]]*\[(if|once|cooldown)[^]]*\]//g; s/[[:space:]]+\$//; s/\{0\}/$CHARACTER/g"; }

echo "== 1. The checker accepts the four bots' files (no server needed)"
for bot in persoper kobaldjovi spyburd eaglefang; do
  out=$("$DOTNET" "$CHECKER" "$SERVER/dialogue/$bot.dlg" 2>&1)
  grep -q "no problems" <<<"$out" && pass "checker: $bot.dlg has no problems" ||
    { fail "checker found problems in $bot.dlg:"; echo "$out" | sed 's/^/      /'; }
done

echo "== 2. Persoper answers each kind of small talk from its own lines (about 2 minutes)"
f="$RECORDS/intents-check-1.txt"
late=(); for line in "${SAY[@]}"; do late+=("--send-late=$line"); done
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/persoper" --listen=10 --send-late-gap=11 "${late[@]}" \
  --record="$f" --record-seconds=100 --overwrite >"$f.log" 2>&1
said=$(sed -nE 's/.*\] Persoper[^:"]*: "(.*)"$/\1/p; s/.*\] Persoper( [A-Za-z]+)? says, "(.*)"$/\2/p' "$f")
if [[ -z $said ]]; then
  fail "Persoper said nothing; see $f (is $CHARACTER in her realm, and was she summoned?)"
else
  hit=0
  for intent in "${INTENTS[@]}"; do
    if grep -qFf <(section "~ reply $intent") <<<"$said"; then
      hit=$((hit + 1)); echo "      $intent: $(grep -m1 -Ff <(section "~ reply $intent") <<<"$said")"
    else
      echo "      $intent: (no answer from this section)"
    fi
  done
  (( hit >= 5 )) && pass "she answered from $hit of 7 new sections" ||
    fail "she answered from only $hit of 7 new sections (she may have been casting; run it again). See $f"
  grep -qFf <(section "~ fallback") <<<"$said" && fail "a \"you've lost me\" fallback line reached the chat: see $f" ||
    pass "no fallback line in her answers"
  grep -qE '\{[a-z0-9]+\}|\[(if|once|cooldown)' <<<"$said" && fail "a raw {placeholder} or [tag] reached the chat" ||
    pass "no raw {placeholder} or [tag] in her chat"
fi

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
