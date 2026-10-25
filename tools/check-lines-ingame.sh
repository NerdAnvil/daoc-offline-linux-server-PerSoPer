#!/usr/bin/env bash
# check-lines-ingame.sh - in-game check of Persoper's new reviewed lines (dialogue step 7) on a running test server.
# Run it yourself, outside any sandbox:
#
#   tools/check-lines-ingame.sh <server-folder> [character]       (about 2 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a level 1 GM test character in Persoper's realm (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's dialogue/persoper.dlg to be the new one. "New lines" are the reply lines that
# persoper.dlg has now and didn't have after the step 7 pilot (commit 6cf5620).
# Part 1 (no server needed) has the checker confirm the file and that the new greetings can be picked.
# Part 2 summons her, says hello three times, thanks her twice and asks for help once, and checks that
# at least one of her answers is a new line and that no raw {placeholder} or [tag] reached the chat.
# Her lines are random, so a run where she happens to pick only old lines fails part 2: run it again.
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-lines-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
CHECKER="$REPO/src/build/DialogueCheck/Release/DialogueCheck.dll"
DLG="src/GameServer/bots/persoper/dialogue/persoper.dlg"
BASELINE=6cf5620   # after the pilot: lines added since then (passes 4 on) are the new ones
RECORDS="$SERVER/logs"   # the recordings stay there after the run

die() { echo "check-lines-ingame: $*" >&2; exit 2; }
if [[ -z ${DAOC_PASSWORD:-} ]]; then
  [[ -t 0 ]] || die "set DAOC_PASSWORD (the test account's password) first"
  read -rsp "Password for $ACCOUNT: " DAOC_PASSWORD; echo
  export DAOC_PASSWORD
fi
[[ -f $CLIENT && -f $CHECKER ]] || die "build first: dotnet build src/DaocServer.sln -c Release"
cmp -s "$REPO/$DLG" "$SERVER/dialogue/persoper.dlg" || die "$SERVER/dialogue/persoper.dlg isn't the repo's new one; copy it over first"
pid=$(cat "$SERVER/run/server.pid" 2>/dev/null || true)
[[ $pid =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null || die "the server in $SERVER isn't running; start it first"
login=$("$DOTNET" "$CLIENT" --account="$ACCOUNT" --login-only 2>&1) ||
  die "can't log in as $ACCOUNT ($(grep -m1 FAIL <<<"$login")); check DAOC_PASSWORD"
grep -q " $CHARACTER, level" <<<"$login" || die "$ACCOUNT has no character $CHARACTER"

failures=0
pass() { echo "PASS  $*"; }
fail() { echo "FAIL  $*"; failures=$((failures + 1)); }

# Dialogue rows only (no comments, sections, blank rows or ask answers), with their tags taken off.
rows() { grep -v -e '^#' -e '^~' -e '^===' -e '^->' -e '^names:' -e '^[[:space:]]*$' |
  sed -E 's/[[:space:]]*\[(if|once|cooldown)[^]]*\]//g; s/[[:space:]]+$//' | sort -u; }
new_lines=$(comm -13 <(git -C "$REPO" show "$BASELINE:$DLG" | rows) <(rows < "$REPO/$DLG") | sed "s/{0}/$CHARACTER/g")
[[ -n $new_lines ]] || die "no new lines since $BASELINE"
echo "($(grep -c . <<<"$new_lines") new lines since $BASELINE)"

echo "== 1. The checker accepts the file and offers the new greetings (no server needed)"
out=$("$DOTNET" "$CHECKER" "$SERVER/dialogue/persoper.dlg" 2>&1)
grep -q "no problems" <<<"$out" && pass "checker: persoper.dlg has no problems" ||
  { fail "checker found problems:"; echo "$out" | sed 's/^/      /'; }
out=$("$DOTNET" "$CHECKER" --preview persoper greeting --player "$CHARACTER" --folder "$SERVER/dialogue" 2>&1)
grep -qF 'Hellooo, nurse' <<<"$out" && pass "preview: the \"Hellooo, nurse\" greeting can be picked" ||
  { fail "preview: no \"Hellooo, nurse\" greeting. It said:"; echo "$out" | sed 's/^/      /'; }

echo "== 2. In game she answers with new lines (about 90 seconds)"
f="$RECORDS/lines-check-1.txt"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/persoper" --listen=10 --send-late-gap=11 \
  --send-late="Persoper, hello" --send-late="Persoper, thanks" --send-late="Persoper, hello again" \
  --send-late="Persoper, help" --send-late="Persoper, thank you" --send-late="Persoper, hello there" \
  --record="$f" --record-seconds=85 --overwrite >"$f.log" 2>&1
# Her replies are "Persoper ... says, "..."" (her summon lines go to party chat). Her mood may add
# "*smiles*" or "Hmph." to a line; that's taken off before comparing.
said=$(sed -nE 's/.*\[Party\] Persoper[^:]*: "(.*)"$/\1/p; s/.*\] Persoper [A-Za-z]+ says, "(.*)"$/\1/p' "$f" |
  sed -E 's/ \*smiles\*$//; s/^Hmph\. //; s/ Hmph\.$//')
if [[ -z $said ]]; then
  fail "Persoper said nothing; see $f (is $CHARACTER in her realm, and is she sulking?)"
else
  heard=$(grep -cxFf <(printf '%s\n' "$new_lines") <<<"$said")
  (( heard >= 1 )) && pass "she said $heard new line(s)" ||
    fail "none of her answers was a new line (her lines are random; run it again). See $f"
  grep -xFf <(printf '%s\n' "$new_lines") <<<"$said" | sed 's/^/      new: /'
  grep -qE '\{[a-z0-9]+\}|\[(if|once|cooldown)' <<<"$said" && fail "a raw {placeholder} or [tag] reached the chat" ||
    pass "no raw {placeholder} or [tag] in her chat"
fi

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
