#!/usr/bin/env bash
# check-chunky-ingame.sh - in-game check that NataSehS teases Shempenstein with her chunky lines
# ([if nearby shempenstein] in her "~ reply banter") on a running test server, in any realm.
# Run it yourself, outside any sandbox:
#
#   tools/check-chunky-ingame.sh <server-folder> [character]      (about 1.5 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a GM test character (default Headlesstest, Midgard; Headlessalb is Albion,
#                    Headlesshib Hibernia: the realm's own NataSehS and Shempenstein are invited)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# It uses the headless client: it invites the realm's NataSehS and Shempenstein (they must be
# online), brings them beside the character with "/dialogue gather" (GM), then banters at her by
# her nickname in group chat, each time after "/dialogue forget" so the 1-hour cooldowns don't
# hide the lines. The chunky lines are read from the server's own dialogue/ folder. It passes
# when at least one is heard, none from another realm's block, and every banter's first answer
# is hers (bug 2026-10-31: her nickname wasn't loaded, so a random group bot answered).
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-chunky-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
RECORD="$SERVER/logs/chunky-check.txt"   # the recording stays there after the run
FILE="$SERVER/dialogue/xxxnatasehsxxx.dlg"
BANTER="Natas, come on slowpokes is that all you have"
ROUNDS=6   # with a random pick, 6 rounds all going to her by luck is 1 in 64

case "$CHARACTER" in
  Headlessalb) NATA=xAxNataSehSxAx; SHEMP=Shempenshire; BLOCK=albion ;;
  Headlesshib) NATA=xHxNataSehSxHx; SHEMP=Shempenmore; BLOCK=hibernia ;;
  *)           NATA=xXxNataSehSxXx; SHEMP=Shempenstein; BLOCK=midgard ;;
esac

die() { echo "check-chunky-ingame: $*" >&2; exit 2; }
if [[ -z ${DAOC_PASSWORD:-} ]]; then
  [[ -t 0 ]] || die "set DAOC_PASSWORD (the test account's password) first"
  read -rsp "Password for $ACCOUNT: " DAOC_PASSWORD; echo
  export DAOC_PASSWORD
fi
[[ -f $CLIENT ]] || die "build first: dotnet build src/DaocServer.sln -c Release"
[[ -f $FILE ]] || die "no $FILE"
pid=$(cat "$SERVER/run/server.pid" 2>/dev/null || true)
[[ $pid =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null || die "the server in $SERVER isn't running; start it first"
login=$("$DOTNET" "$CLIENT" --account="$ACCOUNT" --login-only 2>&1) ||
  die "can't log in as $ACCOUNT ($(grep -m1 FAIL <<<"$login")); check DAOC_PASSWORD"
grep -q " $CHARACTER, level" <<<"$login" || die "$ACCOUNT has no character $CHARACTER"

failures=0
pass() { echo "PASS  $*"; }
fail() { echo "FAIL  $*"; failures=$((failures + 1)); }

# The chunky lines' longest piece without {0}, one per line, from one block of her file ("" = her own block).
chunky() {
  awk -v want="$1" '/^=== /{block=$2} /^~ /{section=$0}
    (want == "" ? block !~ /^(midgard|albion|hibernia)$/ : block == want) && section == "~ reply banter" && /\[if nearby shempenstein\]/' "$FILE" |
    sed 's/ *\[[^]]*\]//g' |
    awk -F'[{](0|bot:[a-z]+)[}]' '{best=""; for (i = 1; i <= NF; i++) if (length($i) > length(best)) best = $i; gsub(/^[ ,.]+|[ ,.]+$/, "", best); print best}'
}

late=("--send-late=/invite $SHEMP" "--send-late=/dialogue gather")
for ((i = 0; i < ROUNDS; i++)); do late+=("--send-late=/dialogue forget" "--send-late=/g $BANTER"); done

echo "== Grouping $NATA and $SHEMP and bantering at her $ROUNDS times (about 2 minutes)"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/invite $NATA" --listen=10 --send-late-gap=6 "${late[@]}" \
  --record="$RECORD" --record-seconds=110 --overwrite >"$RECORD.log" 2>&1

grep -q "$SHEMP has joined the group" "$RECORD" && grep -q "$NATA has joined the group" "$RECORD" &&
  pass "$NATA and $SHEMP joined the group" || fail "they didn't both join; are they online? See $RECORD and $RECORD.log"

# "The xXxNataSehSxXx": the game puts "the" before a name with a small first letter (a known bug).
said=$(grep -E "\[Party\] (The )?$NATA:" "$RECORD")
answers=$(grep -c . <<<"$said")
mine=$( { chunky ""; chunky "$BLOCK"; } | grep -v '^$')
heard=$(grep -cF -f <(echo "$mine") <<<"$said")
(( heard > 0 )) && pass "$heard chunky line(s) from $NATA (of $answers party lines)" ||
  fail "no chunky line from $NATA in $answers party lines; see $RECORD"

others=$(for block in midgard albion hibernia; do [[ $block != "$BLOCK" ]] && chunky "$block"; done | grep -v '^$')
grep -qF -f <(echo "$others") <<<"$said" && fail "a chunky line from another realm's block; see $RECORD" ||
  pass "no other realm's chunky line"

# After each banter, the next party line from anyone else must be hers, not Shemp's.
read -r first wrong < <(awk -v me="[Party] $CHARACTER:" -v nata="$NATA:" '
  /\[Party\]/ { if (index($0, me)) { waiting = 1; next }
                if (waiting) { waiting = 0; if (index($0, nata)) ok++; else bad++ } }
  END { print ok + 0, bad + 0 }' "$RECORD")
(( wrong == 0 && first > 0 )) && pass "$NATA answered first all $first time(s) she was named" ||
  fail "someone else answered first $wrong time(s) when $NATA was named; see $RECORD"

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
