#!/usr/bin/env bash
# check-misses-ingame.sh - in-game check of the intent misses seen 2026-10-30 (Part A of the intent work) on a
# running test server. Run it yourself, outside any sandbox:
#
#   tools/check-misses-ingame.sh <server-folder> [character]       (about 6 minutes)
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
# Part 3 summons Persoper, teleports to Shempenstein, invites him into the group, and says "shemp i have swords
#   taller than you" three times each in /say, group (/g) and faction chat, with a few words added so each line is
#   told apart: Shempenstein must answer every one, and before any other bot (bug #7: he used to stay silent while
#   riding a stablemaster horse; in faction chat only Persoper is held to that, the channel is busy). A /say line
#   he was too far to hear (the server logs it) is skipped. In guild chat (/gu) "jovi you're fat" three times:
#   Kobaldjovi, a member of the character's guild, must answer first. Another bot may chip in now and then. Then
#   lines for bots that can't hear them (Kobaldjovi in /say and /g, Shempenstein in /gu): the others stay quiet.
# Needs Kobaldjovi in the character's guild and Shempenstein in another (or none).
# A named bot answers even mid-fight; other bots' lines are random: if part 1 or 2 is short, run it again.
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

echo "== 3. Shempenstein answers \"shemp i have swords taller than you\" first, every time, in /say, /g and /faction, Kobaldjovi in /gu, and nobody answers for an absent bot (about 3.5 minutes)"
f="$RECORDS/misses-check-3.txt"
SWORDS=()
# Guild chat goes to Kobaldjovi, a member of the character's guild wherever he is (Shempenstein is in another guild).
GUILD=()
for tail in "" ", really" ", no lie"; do
  for channel in "" "/g " "/faction "; do SWORDS+=("${channel}shemp i have swords taller than you$tail"); done
  GUILD+=("/gu jovi you're fat$tail")
done
# Lines for a bot that can't hear them: Kobaldjovi (far off, not in the group) in /say and /g, Shempenstein (not in
# the guild) in /gu. The others stay quiet.
AWAY=("jovi you're fat" "/g jovi you're fat" "/gu shemp i have swords taller than you")
late=(); for line in "${SWORDS[@]}" "${GUILD[@]}" "${AWAY[@]}"; do late+=("--send-late=$line"); done
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/tele Shempenstein" --send="/invite Shempenstein" --send="/persoper" --listen=10 \
  --goto=Shempenstein --follow=Shempenstein --send-late-gap=11 "${late[@]}" \
  --record="$f" --record-seconds=200 --overwrite >"$f.log" 2>&1
grep -q "Shempenstein" "$f" || fail "Shempenstein never showed up in the recording; see $f (did the /tele and /invite work?)"
# The channel of a test line and its text without the command.
channel() { case "$1" in "/g "*) echo /g ;; "/gu "*) echo /gu ;; "/faction "*) echo /faction ;; *) echo /say ;; esac; }
bare() { sed -E 's#^/(g|gu|faction) ##' <<<"$1"; }
# The bot chat in the 10 s after the player's exact line $2 in channel $3, up to their next line.
after_exact() { awk -v pat="$2" -v where="$3" -v me="$CHARACTER" '
  function secs(t) { split(t, a, ":"); return a[1] * 3600 + a[2] * 60 + a[3] }
  function mine() { return index($0, "You say, \"") || index($0, "[Party] " me ": \"") ||
                           index($0, "[Guild] " me ": \"") || index($0, "(" me ") <Faction> ") }
  function said(p) {   # the client echoes its own group line with a trailing space
    if (where == "/say") return index($0, "You say, \"" p "\"")
    if (where == "/g") return index($0, "[Party] " me ": \"" p "\"") || index($0, "[Party] " me ": \"" p " \"")
    if (where == "/gu") return index($0, "[Guild] " me ": \"" p "\"")
    return $0 ~ /<Faction>/ && substr($0, length($0) - length(p) + 1) == p && index($0, "(" me ") <Faction> ") }
  !seen && said(pat) { start = secs($1); seen = 1; next }
  seen && (secs($1) - start > 10 || mine()) { exit }
  seen && (where == "/faction" ? index($0, "<Faction>") : $2 == (where == "/say" ? "[say]" : where == "/g" ? "[party]" : "[guild]"))' "$1"; }
# Who spoke a chat line: "Name says", "[Party] Name:", "[Guild] Name:" or "(Name) <Faction>".
speakers() { sed -nE 's/^[0-9:]+ \[[a-z]+\] (\[(Party|Guild)\] |\()?([Tt]he )?([A-Za-z]+).*/\4/p'; }
show() { sed -E 's/^[0-9:]+ \[[a-z]+\] (\[(Party|Guild)\] )?/      /'; }
# The server's note that a named bot was out of earshot of the /say line $2 said at $3 (HH:MM:SS).
unheard() { grep -F "$(date +%m/%d) $3 | " "$SERVER/logs/server.log" | grep -F "AUTONOMOUS_CHAT_NAMED_UNHEARD bot=\"$2\"" |
  grep -F "channel=say" | grep -oE "distance=[0-9]+" | head -1; }
# Checks that bot $2 answered the test line $1 first.
judge() {
  local line=$1 bot=$2 where text after who first far
  where=$(channel "$line"); text=$(bare "$line")
  after=$(after_exact "$f" "$text" "$where")
  who=$(speakers <<<"$after")
  # Faction chat is busy with other bots' chatter: there, only Persoper must not beat him.
  [[ $where == /faction ]] && who=$(grep -xE "$bot|Persoper" <<<"$who")
  echo "  $where \"$text\":"
  [[ -n $after ]] && show <<<"$after"
  if grep -qx "$bot" <<<"$who"; then
    pass "$where: $bot answered"
  else
    # A /say line doesn't reach a bot out of earshot (he rides off, the client falls behind): not counted.
    far=$([[ $where == /say ]] && unheard "$text" "$bot" "$(grep -m1 -F "You say, \"$text\"" "$f" | cut -c1-8)")
    [[ -n $far ]] && echo "SKIP  $where: $bot was out of earshot ($far; /say reaches 512)" ||
      fail "$where \"$text\": $bot didn't answer within 10 s; see $f"
  fi
  first=$(head -1 <<<"$who")
  [[ -z $first || $first == "$bot" ]] && pass "$where: nobody answered before him" ||
    fail "$where \"$text\": $first answered before $bot; see $f"
}
for line in "${SWORDS[@]}"; do judge "$line" Shempenstein; done
for line in "${GUILD[@]}"; do judge "$line" Kobaldjovi; done
for line in "${AWAY[@]}"; do
  where=$(channel "$line"); text=$(bare "$line")
  absent=$([[ $where == /gu ]] && echo Shempenstein || echo Kobaldjovi)
  others=$(grep -vE "\] (\[(Party|Guild)\] )?$absent" <<<"$(after_exact "$f" "$text" "$where")" | sed '/^$/d')
  echo "  $where \"$text\" ($absent can't hear it):"
  [[ -n $others ]] && show <<<"$others"
  [[ -z $others ]] && pass "$where: nobody answered in his place" ||
    fail "$where \"$text\": someone answered in $absent's place (or chatted by chance; run it again); see $f"
done

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
