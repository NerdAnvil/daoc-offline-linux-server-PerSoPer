#!/usr/bin/env bash
# check-guildvoices-ingame.sh - in-game check that named bots in your guild talk in guild chat in their own voice,
# not with the stock guild lines, on a running test server. Run it yourself, outside any sandbox:
#
#   tools/check-guildvoices-ingame.sh <server-folder> [character]      (about 1.5 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a GM test character in Midgard, in a guild, with the right to invite (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's GameServer.dll to have "/dialogue guildchat". It tries to invite Eaglefang and Shempenstein
# into the character's guild (/gc invite; a bot already in another guild is refused, which is fine: the check needs
# at least one bot with a personality in the guild, joined now or before, such as Kobaldjovi), has a member start guild chat twice ("/dialogue guildchat", one other member
# answers each time), then says three lines in guild chat that name nobody, 13 s apart (guild replies wait 10 s per
# guild). Every member line writes a "BOT_GUILD_CHAT ... personality=True own=True|False" note to server.log. It
# passes when both openers are in the bot's own voice, at least one member answered the player in its own voice,
# and no member with a personality fell back to a stock line where it should have its own. An answer to an opener
# may still be stock (a bot has a topic reply only for some topics), so those are counted, not judged.
# Bots the run invited are removed again at the end (/gc remove); a bot already in the guild stays.
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-guildvoices-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
RECORD="$SERVER/logs/guildvoices-check.txt"   # the recording stays there after the run
LOG="$SERVER/logs/server.log"
BOTS=(Eaglefang Shempenstein)

die() { echo "check-guildvoices-ingame: $*" >&2; exit 2; }
if [[ -z ${DAOC_PASSWORD:-} ]]; then
  [[ -t 0 ]] || die "set DAOC_PASSWORD (the test account's password) first"
  read -rsp "Password for $ACCOUNT: " DAOC_PASSWORD; echo
  export DAOC_PASSWORD
fi
[[ -f $CLIENT ]] || die "build first: dotnet build src/DaocServer.sln -c Release"
pid=$(cat "$SERVER/run/server.pid" 2>/dev/null || true)
[[ $pid =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null || die "the server in $SERVER isn't running; start it first"
login=$("$DOTNET" "$CLIENT" --account="$ACCOUNT" --login-only 2>&1) ||
  die "can't log in as $ACCOUNT ($(grep -m1 FAIL <<<"$login")); check DAOC_PASSWORD"
grep -q " $CHARACTER, level" <<<"$login" || die "$ACCOUNT has no character $CHARACTER"

failures=0
pass() { echo "PASS  $*"; }
fail() { echo "FAIL  $*"; failures=$((failures + 1)); }
# This run's server.log lines (after line $1). server.log rolls over at 100 MB (to server.<date>.<n>.log); when it
# did during the run, the rest of the old one comes first.
run_log() { if (( $(wc -l <"$LOG") + 1 < $1 )); then
    tail -n +"$1" "$(ls -t "$SERVER"/logs/server.*.log 2>/dev/null | head -1)"; cat "$LOG"
  else tail -n +"$1" "$LOG"; fi; }
count() { grep -cE "$1" <<<"$notes"; }

start=$(( $(wc -l <"$LOG") + 1 ))
invites=(); for bot in "${BOTS[@]}"; do invites+=("--send-late=/gc invite $bot"); done
echo "== Inviting ${BOTS[*]}, two guild chats, three lines to nobody (about 1.5 minutes)"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" --listen=3 --send-late-gap=13 \
  "${invites[@]}" \
  "--send-late=/dialogue guildchat" \
  "--send-late=/dialogue guildchat" \
  "--send-late=/gu anyone up for a hunt tonight" \
  "--send-late=/gu the roads are quiet today" \
  "--send-late=/gu what is everyone working on" \
  --record="$RECORD" --record-seconds=110 --overwrite >"$RECORD.log" 2>&1
notes=$(run_log "$start" | grep -F "BOT_GUILD_CHAT")

# The invites only set things up: a bot already in another guild (most world bots are) is refused, which is fine.
joined=()
for bot in "${BOTS[@]}"; do
  if grep -q "$bot has joined the guild" "$RECORD"; then joined+=("$bot"); echo "      $bot joined the guild"
  else echo "      $bot didn't join: $(grep -m1 -oE "$bot (is already in [a-z ]*guild|cannot [a-z ]*)" "$RECORD" || echo "no answer")"; fi
done
talkers=$(grep -oE 'bot="[^"]+"[^"]*"[^"]*" kind=[a-z]+ personality=True' <<<"$notes" | cut -d'"' -f2 | sort -u | paste -sd, -)
[[ -n $talkers ]] && pass "bots with a personality talked in the guild: $talkers" ||
  fail "no bot with a personality talked in the guild; invite one by hand (/gc invite <name>) and run it again"
grep -q "Guild chat: .* starts it" "$RECORD" || fail "no \"Guild chat: ... starts it\"; does the server have /dialogue guildchat? See $RECORD"

openers=$(count 'kind=opener personality=True own=True')
(( openers >= 2 )) && pass "$openers opener(s) in the bot's own voice (2 asked)" ||
  fail "$openers own-voice opener(s), 2 asked ($(count 'kind=opener') opener notes). See $RECORD"
answers=$(count 'kind=answer personality=True own=True')
(( answers >= 1 )) && pass "$answers answer(s) to the player in the bot's own voice" ||
  fail "no own-voice answer to the three lines ($(count 'kind=answer') answer notes; a bot that is casting drops a reply, run it again)"
stock=$(count 'kind=(opener|answer) personality=True own=False')
(( stock == 0 )) && pass "no stock opener or answer from a bot with a personality" ||
  fail "$stock stock opener(s)/answer(s) from a bot with a personality"
echo "      replies to openers: $(count 'kind=reply personality=True own=True') own, $(count 'kind=reply personality=True own=False') stock (not judged)"
echo "      guild lines heard:"; grep -F '[Guild] ' "$RECORD" | grep -vF "[Guild] $CHARACTER:" | sed 's/^/        /'

if (( ${#joined[@]} > 0 )); then
  removes=(); for bot in "${joined[@]}"; do removes+=("--send-late=/gc remove $bot"); done
  echo "== Removing ${joined[*]} from the guild again"
  "$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" --listen=3 --send-late-gap=3 "${removes[@]}" \
    --record="$RECORD.remove" --record-seconds=12 --overwrite >"$RECORD.remove.log" 2>&1
fi

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
