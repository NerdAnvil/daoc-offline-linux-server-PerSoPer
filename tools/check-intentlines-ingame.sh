#!/usr/bin/env bash
# check-intentlines-ingame.sh - in-game sample of the intent lines (B4 to B6b) of the named bots, one per type, on a
# running test server. Run it yourself, outside any sandbox:
#
#   tools/check-intentlines-ingame.sh <server-folder> [character] [set]       (about 5 minutes per set)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a level 1 GM test character in Midgard (default Headlesstest)
#   [set] or SET     1 (default):Shempenstein, xXxNataSehSxXx, TheriousTham, FemurTamer, Redtornader, Shnacksize
#                    2: Muckaduck, Hunterson, Barbalicious, Spyburd, Eaglefang (a group holds the GM and 7 bots)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's dialogue folder to be the repo's (intents, the types and the named files) and its
# GameServer.dll to have "/dialogue orders". Invites the set's bots, brings them over with /dialogue gather and turns on
# "/dialogue orders always" (a named order is always answered). Then, 10 s apart, for each bot in group chat:
#   "<bot>, follow me"          an "orderfollow" line
#   "<bot>, assist me"          an "ordernotarget" line (the character fights nothing)
#   "<bot>, rest up"            an "orderfull" or an "orderrest" line
#   "<bot>, where are you?"     a "where" line
# A said line passes when it is one of that bot's own lines or its type's lines for the section (a slot such as
# {monster} stands for any words; a mood tint before or after the line is allowed). It does not check that the state
# fits the line; check-state-ingame.sh does that for Shempenstein, UT_StateAnswers for the rest. Prints PASS or FAIL per
# line; exits 0 only when every one passed. A casting bot drops a reply: run a failed one again before suspecting a line.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-intentlines-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
SET="${3:-${SET:-1}}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
DIR="src/GameServer/bots/persoper/dialogue"
RECORDS="$SERVER/logs"   # the recording stays there after the run

# nickname|bot name|type file|own file
case "${SET:-1}" in
  1) BOTS=("shemp|Shempenstein|casual|shempenstein" "natas|xXxNataSehSxXx|hardcore|xxxnatasehsxxx"
           "tham|TheriousTham|leeroy|therioustham" "femur|FemurTamer|loner|femurtamer"
           "cannon|Redtornader|minmaxer|redtornader" "shnacki|Shnacksize|joker|shnacksize") ;;
  2) BOTS=("moneybags|Muckaduck|crafter|muckaduck" "hunty|Hunterson|roleplayer|hunterson"
           "barb|Barbalicious|homeowner|barbalicious" "burd|Spyburd|herbaldoctor|spyburd"
           "eagle|Eaglefang|gruffdoc|eaglefang") ;;
  *) echo "check-intentlines-ingame: SET must be 1 or 2" >&2; exit 2 ;;
esac

die() { echo "check-intentlines-ingame: $*" >&2; exit 2; }
if [[ -z ${DAOC_PASSWORD:-} ]]; then
  [[ -t 0 ]] || die "set DAOC_PASSWORD (the test account's password) first"
  read -rsp "Password for $ACCOUNT: " DAOC_PASSWORD; echo
  export DAOC_PASSWORD
fi
[[ -f $CLIENT ]] || die "build first: dotnet build src/DaocServer.sln -c Release"
files=(intents)
for row in "${BOTS[@]}"; do IFS='|' read -r _ _ type own <<<"$row"; files+=("$type" "$own"); done
for file in "${files[@]}"; do
  cmp -s "$REPO/$DIR/$file.dlg" "$SERVER/dialogue/$file.dlg" || die "$SERVER/dialogue/$file.dlg isn't the repo's; copy it over first"
done
pid=$(cat "$SERVER/run/server.pid" 2>/dev/null || true)
[[ $pid =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null || die "the server in $SERVER isn't running; start it first"
login=$("$DOTNET" "$CLIENT" --account="$ACCOUNT" --login-only 2>&1) ||
  die "can't log in as $ACCOUNT ($(grep -m1 FAIL <<<"$login")); check DAOC_PASSWORD"
grep -q " $CHARACTER, level" <<<"$login" || die "$ACCOUNT has no character $CHARACTER"

failures=0
pass() { echo "PASS  $*"; }
fail() { echo "FAIL  $*"; failures=$((failures + 1)); }

# The rows of one section of a file, tags off, {0} filled in with the character's name.
section() { awk -v want="$2" '/^~ /{f=($0==want); next} /^===/{f=0} f&&NF&&!/^#/' "$REPO/$DIR/$1.dlg" |
  sed -E "s/[[:space:]]*\[(if|once|cooldown|allow)[^]]*\]//g; s/[[:space:]]+\$//; s/\{0\}/$CHARACTER/g"; }
# True when $1 is one of the rows on stdin, alone or with a mood tint before or after it ({monster} and the like
# stand for some words).
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

# What to say, and which sections may answer it.
ASKS=("follow me|~ reply orderfollow" "assist me|~ reply ordernotarget" "rest up|~ reply orderfull ~ reply orderrest"
      "where are you?|~ reply where")

first="${BOTS[0]}"; IFS='|' read -r _ firstbot _ _ <<<"$first"
args=(); late=()
for row in "${BOTS[@]}"; do IFS='|' read -r _ bot _ _ <<<"$row"; args+=("--send=/invite $bot"); done
late+=("--send-late=/dialogue gather" "--send-late=/dialogue forget" "--send-late=/dialogue orders always")
for row in "${BOTS[@]}"; do IFS='|' read -r nick _ _ _ <<<"$row"
  for ask in "${ASKS[@]}"; do IFS='|' read -r said _ <<<"$ask"; late+=("--send-late=/g $nick, $said"); done
done
late+=("--send-late=/dialogue orders normal")
total=$(( ${#BOTS[@]} * ${#ASKS[@]} ))
f="$RECORDS/intentlines-check-set${SET:-1}.txt"
echo "== Intent lines of ${#BOTS[@]} named bots (set ${SET:-1}): $total questions, about $(( total / 6 + 1 )) minutes"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" "${args[@]}" --listen=8 \
  --wait-for="$firstbot" --send-late-gap=10 "${late[@]}" \
  --record="$f" --record-seconds=8 --overwrite >"$f.log" 2>&1
for row in "${BOTS[@]}"; do IFS='|' read -r _ bot _ _ <<<"$row"
  grep -q "$bot" "$f" || fail "$bot never showed up in the recording; see $f (did the /invite work?)"
done

for row in "${BOTS[@]}"; do IFS='|' read -r nick bot type own <<<"$row"
  echo "  $bot ($type):"
  for ask in "${ASKS[@]}"; do IFS='|' read -r said sects <<<"$ask"
    line="$nick, $said"
    reply=$(said_by "$bot" <(within "$f" "$line"))
    [[ -n $reply ]] && echo "$reply" | sed "s/^/      /"
    rows=""
    while IFS= read -r name; do [[ -n $name ]] && rows+=$'\n'"$(section "$type" "~ reply $name"; section "$own" "~ reply $name")"; done \
      < <(grep -o 'reply [a-z]*' <<<"$sects" | cut -d' ' -f2)
    hit=""
    while IFS= read -r one; do [[ -n $one ]] && one_of "$one" <<<"$rows" && hit="$one"; done <<<"$reply"
    if [[ -n $hit ]]; then pass "\"$line\""
    else fail "\"$line\": no ${sects//~ reply /} line from $bot within 10 s (a casting bot drops a reply; run it again). See $f"; fi
  done
done

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
