#!/usr/bin/env bash
# check-groans-ingame.sh - in-game check that named bots answer each other with situation lines
# ([if nearby shnacksize] groans) on a running test server. Run it yourself, outside any sandbox:
#
#   tools/check-groans-ingame.sh <server-folder> [character] [joker]      (about 2 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a GM test character (default Headlesstest, Midgard; Headlessalb is Albion,
#                    Headlesshib Hibernia: the realm's own versions of the bots are invited)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#   [joker]          shnacksize (default), or spyburd: Spyburd tells the jokes with Shnacksize
#                    standing by, so the groans name her, and it checks they use the realm's name
#                    for her ({bot:...} in the files)
#
# It uses the headless client: it summons Persoper, invites Shnacksize, Kobaldjovi, Spyburd and
# Eaglefang (they must be online), brings them beside the character with "/dialogue gather" (GM),
# then has Shnacksize tell jokes with "/dialogue joke Shnacksize", each after "/dialogue forget" so
# the 30-minute cooldowns don't hide the groans. The groan lines are read from the server's own
# dialogue/ folder, so new ones are picked up. Grouped with an idle leader the bots stand still.
# Not every groupmate answers every joke, so it passes when at least one groan is heard, and says who.
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-groans-ingame.sh <server-folder> [character] [joker]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
RECORD="$SERVER/logs/groan-check.txt"   # the recording stays there after the run
# The bots by character file name, and as the character's realm calls them.
case "$CHARACTER" in
  Headlessalb) declare -A AS=([shnacksize]=Shnacksizer [spyburd]=Spyburde [eaglefang]=Eaglefange) ;;
  Headlesshib) declare -A AS=([shnacksize]=Shnacksizey [spyburd]=Spyburdagh [eaglefang]=Eaglefangh) ;;
  *)           declare -A AS=([shnacksize]=Shnacksize [spyburd]=Spyburd [eaglefang]=Eaglefang [kobaldjovi]=Kobaldjovi) ;;
esac
JOKER_KEY="${3:-shnacksize}"
[[ $JOKER_KEY == shnacksize || $JOKER_KEY == spyburd ]] || { echo "check-groans-ingame: the joker is shnacksize or spyburd" >&2; exit 2; }
JOKER="${AS[$JOKER_KEY]}"
SHNACK="${AS[shnacksize]}"
GROANERS=(persoper)   # Kobaldjovi lives in Midgard only
for key in kobaldjovi spyburd eaglefang; do [[ -n ${AS[$key]:-} && $key != "$JOKER_KEY" ]] && GROANERS+=("$key"); done
JOKES=4

die() { echo "check-groans-ingame: $*" >&2; exit 2; }
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

# A groan line's longest piece without {0} (the speaker's name) or {bot:...} (a realm's bot name),
# from a character file's "~ reply joke" section.
groans() {
  awk '/^~ /{section=$0} section=="~ reply joke" && /\[if nearby shnacksize\]/' "$SERVER/dialogue/$1.dlg" 2>/dev/null |
    sed 's/ *\[[^]]*\]//g' |
    awk -F'[{](0|bot:[a-z]+)[}]' '{best=""; for (i = 1; i <= NF; i++) if (length($i) > length(best)) best = $i; gsub(/^[ ,.]+|[ ,.]+$/, "", best); print best}'
}
name() { [[ $1 == persoper ]] && echo Persoper || echo "${AS[$1]}"; }

late=()
invites=("$JOKER")
[[ $JOKER_KEY != shnacksize ]] && invites+=("$SHNACK")
for key in "${GROANERS[@]:1}"; do invites+=("$(name "$key")"); done
for bot in "${invites[@]}"; do late+=("--send-late=/invite $bot"); done
late+=("--send-late=/dialogue gather")
for ((i = 0; i < JOKES; i++)); do late+=("--send-late=/dialogue forget" "--send-late=/dialogue joke $JOKER"); done

echo "== Grouping the nameds and letting $JOKER tell $JOKES jokes (about 2 minutes)"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/persoper" --listen=10 --send-late-gap=6 "${late[@]}" \
  --record="$RECORD" --record-seconds=110 --overwrite >"$RECORD.log" 2>&1

told=$(grep -c "$JOKER tells a joke" "$RECORD")
(( told > 0 )) && pass "$JOKER told $told joke(s)" ||
  fail "$JOKER told no jokes; is the bot online, and $CHARACTER in its realm? See $RECORD and $RECORD.log"

heard=()
for key in "${GROANERS[@]}"; do
  pieces=$(groans "$key")
  [[ -n $pieces ]] || { fail "$(name "$key") has no groan lines in $SERVER/dialogue"; continue; }
  grep -qF -f <(echo "$pieces") "$RECORD" && heard+=("$(name "$key")")
done
(( ${#heard[@]} > 0 )) && pass "groans at $JOKER's jokes from: ${heard[*]}" ||
  fail "nobody groaned at $JOKER's jokes; see $RECORD (did the groaners join the group?)"

if [[ $JOKER_KEY != shnacksize ]]; then
  # Party chat only (not "has joined the group"); the groans are the lines of neither the joker nor her.
  party=$(grep -F '[Party] ' "$RECORD")
  grep -vE "\[Party\] ($JOKER|$SHNACK):" <<<"$party" | grep -qw "$SHNACK" && pass "a groan names her $SHNACK" ||
    fail "no groan named $SHNACK; see $RECORD"
  wrong=$(grep -owE 'Shnacksize[ry]?' <<<"$party" | grep -vx "$SHNACK" | sort -u | tr '\n' ' ')
  [[ -z $wrong ]] && pass "no other realm's name for her" || fail "other realms' names heard: $wrong"
  grep -qF '{bot:' "$RECORD" && fail "a {bot:...} reached the chat unfilled; see $RECORD" || pass "no unfilled {bot:...}"
fi

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
