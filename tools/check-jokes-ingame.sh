#!/usr/bin/env bash
# check-jokes-ingame.sh - in-game check of the joke flow (intent Part B3a) on a running test server.
# Run it yourself, outside any sandbox:
#
#   tools/check-jokes-ingame.sh <server-folder> [character]       (about 6 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a level 1 GM test character in Midgard (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's dialogue files for intents, Persoper, Shempenstein and Casual to be the repo's.
# Summons Persoper, invites Shempenstein and brings him over with the GM command /dialogue gather (a test
# shortcut: in play only a bot in another region pops to the player), waits until he is near, then tells jokes
# in group chat. Each line's
# answer (within 10 s) must come from the right bot's right lines:
#   a riddle gets a "~ reply jokeguess" line, its punchline a "~ reply groan" or "~ reply jokelaugh" line;
#   a joke told in full gets a groan or laugh straight away; "knock knock" gets "Who's there?", the name
#   "Boo who?", the punchline a groan or laugh; "tell me a joke" gets a "~ reply jokeask" line (Persoper);
#   a riddle naming nobody gets one bot's guess, and that bot answers its punchline.
# No "you've lost me" fallback line may reach the chat. Other chatter in the window is shown and ignored.
# Part 2 (B3b) tells Shempenstein 15 jokes in full: now and then (about 1 in 5) he answers his groan with a
#   "~ reply jokeback" lead-in and then a "~ reply jokeask" joke; never two lead-ins to one joke, and not every time.
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-jokes-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
DIR="src/GameServer/bots/persoper/dialogue"
RECORDS="$SERVER/logs"   # the recordings stay there after the run

die() { echo "check-jokes-ingame: $*" >&2; exit 2; }
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
pass() { echo "PASS  $*"; }
fail() { echo "FAIL  $*"; failures=$((failures + 1)); }

# The rows of one section of a bot's file, tags off, {0} filled in with the character's name.
section() { awk -v want="$2" '/^~ /{f=($0==want); next} /^===/{f=0} f&&NF&&!/^#/' "$REPO/$DIR/$1.dlg" |
  sed -E "s/[[:space:]]*\[(if|once|cooldown|allow)[^]]*\]//g; s/[[:space:]]+\$//; s/\{0\}/$CHARACTER/g"; }
# A bot's rows of one section: Persoper's file, or Shempenstein's over Casual's.
rows() { if [[ $1 == Persoper ]]; then section persoper "$2"; else section casual "$2"; section shempenstein "$2"; fi; }
# What one bot said in a recording: party chat ("Name: "..."") or /say ("Name says, "..."").
said_by() { sed -nE "s/.*\\] ([Tt]he )?$1[^:\"]*: \"(.*)\"\$/\\2/p; s/.*\\] ([Tt]he )?$1( [A-Za-z]+)? says, \"(.*)\"\$/\\3/p" "$2" |
  sed -E 's/[[:space:]]+$//'; }
# The chat lines from the moment the player said $2 in group chat until $3 (default 10) seconds later.
within() { awk -v pat="$2" -v me="$CHARACTER" -v span="${3:-10}" '
  function secs(t) { split(t, a, ":"); return a[1] * 3600 + a[2] * 60 + a[3] }
  index($0, "[Party] " me ": \"" pat "\"") || index($0, "[Party] " me ": \"" pat " \"") { start = secs($1); seen = 1; next }
  seen && secs($1) - start <= span' "$1"; }

# The test: the line, who must answer (Persoper, Shempenstein, or "first" for the bot that answered the
# last unnamed riddle), and what with (sections, or the exact line).
LINES=(
  "shemp, why did the troll cross the road?|Shempenstein|~ reply jokeguess"
  "to get to the alb!|Shempenstein|~ reply groan;~ reply jokelaugh"
  "persoper, why did the astronaut leave Pluto? It had no atmosphere|Persoper|~ reply groan;~ reply jokelaugh"
  "shemp, knock knock|Shempenstein|=Who's there?"
  "boo|Shempenstein|=Boo who?"
  "don't cry, it's only a joke|Shempenstein|~ reply groan;~ reply jokelaugh"
  "persoper, tell me a joke|Persoper|~ reply jokeask"
  "what do you call a kobold with a sword?|either|~ reply jokeguess"
  "sir kobold, of course|first|~ reply groan;~ reply jokelaugh"
)

f="$RECORDS/jokes-check.txt"
late=(); for row in "${LINES[@]}"; do late+=("--send-late=/g ${row%%|*}"); done
echo "== Joke flow in group chat: ${#LINES[@]} lines (about 2.5 minutes)"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/invite Shempenstein" --send="/dialogue gather" --send="/persoper" --listen=5 \
  --wait-for=Shempenstein --follow=Shempenstein --send-late-gap=11 "${late[@]}" \
  --record="$f" --record-seconds=15 --overwrite >"$f.log" 2>&1
grep -q "Shempenstein" "$f" || fail "Shempenstein never showed up in the recording; see $f (did the /invite work?)"

fallbacks=$(section persoper "~ fallback"; section casual "~ fallback"; section shempenstein "~ fallback")
first=""
for row in "${LINES[@]}"; do
  IFS='|' read -r line who want <<<"$row"
  after=$(within "$f" "$line")
  echo "  \"$line\":"
  for bot in Persoper Shempenstein; do said_by "$bot" <(echo "$after") | sed "s/^/      $bot: /"; done
  if grep -qxFf <(echo "$fallbacks") <(said_by Persoper <(echo "$after"); said_by Shempenstein <(echo "$after")); then
    fail "\"$line\" got a fallback line"
  fi
  case $who in either) bots=(Persoper Shempenstein) ;; first) bots=("${first:-none}") ;; *) bots=("$who") ;; esac
  answered=""
  for bot in "${bots[@]}"; do
    [[ $bot == none ]] && continue
    if [[ $want == =* ]]; then expected="${want#=}"; else expected=$(IFS=';'; for s in $want; do rows "$bot" "$s"; done); fi
    if said_by "$bot" <(echo "$after") | grep -qxFf <(echo "$expected"); then answered="$bot"; break; fi
  done
  [[ $who == either ]] && first="$answered"
  if [[ -n $answered ]]; then pass "\"$line\": $answered answered from the right lines"
  else fail "\"$line\": no fitting answer from ${bots[*]} within 10 s (a casting bot drops a reply; run it again). See $f"; fi
done

echo "== 2. Joke back: 15 jokes told in full to Shempenstein (about 3.5 minutes)"
f="$RECORDS/jokes-check-2.txt"
THINGS=(troll kobold dwarf skeleton wolf bear spider imp ghost giant goblin seal frog yeti boar)
late=(); for thing in "${THINGS[@]}"; do late+=("--send-late=/g shemp, why did the $thing cross the road? to get to the inn"); done
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/invite Shempenstein" --send="/dialogue gather" --listen=5 \
  --wait-for=Shempenstein --follow=Shempenstein --send-late-gap=13 "${late[@]}" \
  --record="$f" --record-seconds=15 --overwrite >"$f.log" 2>&1
leadins=$(rows Shempenstein "~ reply jokeback")
backs=0
for thing in "${THINGS[@]}"; do
  said=$(said_by Shempenstein <(within "$f" "shemp, why did the $thing cross the road? to get to the inn" 12))
  echo "  the $thing:"; [[ -n $said ]] && echo "$said" | sed 's/^/      Shempenstein: /'
  count=$(grep -cxFf <(echo "$leadins") <<<"$said")
  if (( count > 1 )); then fail "the $thing got $count joke-back lead-ins (one at most)"
  elif (( count == 1 )); then
    backs=$((backs + 1))
    after=$(awk -v lead="$(grep -xFf <(echo "$leadins") <<<"$said" | head -1)" 'found { print } $0 == lead { found = 1 }' <<<"$said")
    grep -qxFf <(rows Shempenstein "~ reply jokeask") <<<"$after" && pass "the $thing: a lead-in, then one of his jokes" ||
      fail "the $thing: a lead-in with no joke of his after it; see $f"
  fi
done
(( backs > 0 )) && pass "$backs of ${#THINGS[@]} jokes got one back" ||
  fail "no joke came back (about a 1 in 30 chance by luck; run it again)"
(( backs < ${#THINGS[@]} )) && pass "not every joke got one back" || fail "every joke got one back (should be now and then)"

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
