#!/usr/bin/env bash
# check-night-lines-ingame.sh - in-game check that a named bot's ambient lines follow the game clock
# ([if time_of_day night] lines at night only) on a running test server. Run it yourself, outside
# any sandbox:
#
#   tools/check-night-lines-ingame.sh <server-folder> [character]      (a few minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a GM test character in Midgard (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# It uses the headless client: it invites Shnacksize (she must be online), brings her beside the
# character with "/dialogue gather", reads the game clock with "/time", then has her say ambient
# lines with "/dialogue ambient Shnacksize", each after "/dialogue forget". At night (22:00 to
# 06:00 game time) it expects night lines, by day none. It doesn't change the clock (that needs an
# admin account): a game day lasts about an hour, so run it once by day and once at night. Night
# lines are read from the server's own dialogue/ folder. Prints PASS or FAIL per check; exits 0
# only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-night-lines-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
RECORD="$SERVER/logs/night-lines-check.txt"   # the recording stays there after the run
BOT=Shnacksize
SAYS=4

die() { echo "check-night-lines-ingame: $*" >&2; exit 2; }
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

# The bot's night lines, tags taken off, from the "~ ambient" section of its file.
night_lines() {
  awk '/^~ /{section=$0} section=="~ ambient" && /\[if time_of_day night\]/' "$SERVER/dialogue/${1,,}.dlg" 2>/dev/null |
    sed 's/ *\[[^]]*\]//g'
}

late=("--send-late=/invite $BOT" "--send-late=/dialogue gather" "--send-late=/time")
for ((i = 0; i < SAYS; i++)); do late+=("--send-late=/dialogue forget" "--send-late=/dialogue ambient $BOT"); done

echo "== $BOT says $SAYS ambient lines (a few minutes)"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --listen=10 --send-late-gap=4 "${late[@]}" \
  --record="$RECORD" --record-seconds=20 --overwrite >"$RECORD.log" 2>&1

lines=$(night_lines "$BOT")
[[ -n $lines ]] || die "$BOT has no night lines in $SERVER/dialogue"

# The game hour (0-23) from the "It is 2:24:00 am" message; empty when there is none.
hour=$(grep -m1 -oE 'It is [0-9]+:[0-9]+:[0-9]+ [ap]m' "$RECORD" |
  awk '{split($3, t, ":"); h = t[1] % 12; if ($4 == "pm") h += 12; print h}')

said=$(grep -c "$BOT says an ambient line" "$RECORD")
(( said > 0 )) && pass "$BOT said $said ambient line(s)" ||
  fail "$BOT said no ambient lines; is she online and in Midgard? See $RECORD and $RECORD.log"

n=$(grep -cF -f <(echo "$lines") "$RECORD")
if [[ -z $hour ]]; then
  fail "no game time in the recording; see $RECORD"
elif (( hour >= 6 && hour < 22 )); then
  (( n == 0 )) && pass "by day (hour $hour) $BOT said no night lines" ||
    fail "by day (hour $hour) $BOT said $n night line(s); see $RECORD"
else
  (( n > 0 )) && pass "at night (hour $hour) $BOT said $n night line(s)" ||
    fail "at night (hour $hour) $BOT said no night lines; see $RECORD"
fi

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
