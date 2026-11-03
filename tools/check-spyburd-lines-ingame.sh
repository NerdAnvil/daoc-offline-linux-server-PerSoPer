#!/usr/bin/env bash
# check-spyburd-lines-ingame.sh - in-game check of Spyburd's new reviewed lines (dialogue step 7) on a running
# test server. The sibling of check-kobaldjovi-lines-ingame.sh. Run it yourself, outside any sandbox:
#
#   tools/check-spyburd-lines-ingame.sh <server-folder> [character] [bot]       (about 4 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a level 1 GM test character in Spyburd's realm, Midgard (default Headlesstest)
#   [bot] or BOT     Spyburd (default), Spyburde or Spyburdagh; the character must be in that realm
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's dialogue/spyburd.dlg and dialogue/herbaldoctor.dlg to be the repo's. "New lines"
# are the lines his character file has that his old blocks in herbaldoctor.dlg didn't (commit 7c8ab59).
# Part 1 (no server needed) has the checker confirm the file and that a new greeting can be picked.
# Part 2 teleports to him and invites him (so he stands still and isn't fighting), says hello three times,
# thanks him twice and asks for help once, and checks that at least one of his answers is a new line and
# that no raw {placeholder} or [tag] reached the chat.
# His own lines are mixed with the Herbal Doctor type's and picked at random, so a run where he happens to
# pick only old lines fails part 2: run it again.
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-spyburd-lines-ingame.sh <server-folder> [character] [bot]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
BOT="${3:-${BOT:-Spyburd}}"   # or Spyburde (Albion) or Spyburdagh (Hibernia), with a character in that realm
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
CHECKER="$REPO/src/build/DialogueCheck/Release/DialogueCheck.dll"
DIR="src/GameServer/bots/persoper/dialogue"
BASELINE=7c8ab59   # before his character file: his old lines were blocks in herbaldoctor.dlg
RECORDS="$SERVER/logs"   # the recordings stay there after the run

die() { echo "check-spyburd-lines-ingame: $*" >&2; exit 2; }
if [[ -z ${DAOC_PASSWORD:-} ]]; then
  [[ -t 0 ]] || die "set DAOC_PASSWORD (the test account's password) first"
  read -rsp "Password for $ACCOUNT: " DAOC_PASSWORD; echo
  export DAOC_PASSWORD
fi
[[ -f $CLIENT && -f $CHECKER ]] || die "build first: dotnet build src/DaocServer.sln -c Release"
for file in spyburd.dlg herbaldoctor.dlg; do
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

# Dialogue rows only (no comments, sections, blank rows or ask answers), with their tags taken off.
rows() { grep -v -e '^#' -e '^~' -e '^===' -e '^->' -e '^names:' -e '^[[:space:]]*$' |
  sed -E 's/[[:space:]]*\[(if|once|cooldown|allow)[^]]*\]//g; s/[[:space:]]+$//' | sort -u; }
new_lines=$(comm -13 <(git -C "$REPO" show "$BASELINE:$DIR/herbaldoctor.dlg" | rows) <(rows < "$REPO/$DIR/spyburd.dlg") |
  sed "s/{0}/$CHARACTER/g")
[[ -n $new_lines ]] || die "no new lines since $BASELINE"
echo "($(grep -c . <<<"$new_lines") new lines since $BASELINE)"

echo "== 1. The checker accepts the file and offers a new greeting (no server needed)"
out=$("$DOTNET" "$CHECKER" "$SERVER/dialogue/spyburd.dlg" 2>&1)
grep -q "no problems" <<<"$out" && pass "checker: spyburd.dlg has no problems" ||
  { fail "checker found problems:"; echo "$out" | sed 's/^/      /'; }
out=$("$DOTNET" "$CHECKER" --preview spyburd greeting --player "$CHARACTER" --folder "$SERVER/dialogue" 2>&1)
grep -qF 'Looking a bit under-buffed there' <<<"$out" && pass "preview: the \"under-buffed\" greeting can be picked" ||
  { fail "preview: no \"under-buffed\" greeting. It said:"; echo "$out" | sed 's/^/      /'; }

echo "== 2. In game he answers with new lines (about 3 minutes)"
f="$RECORDS/spyburd-lines-check-1.txt"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/tele $BOT" --send="/invite $BOT" --listen=6 --goto="$BOT" --follow="$BOT" --send-late-gap=11 \
  --send-late="$BOT, hello" --send-late="$BOT, thanks" --send-late="$BOT, hello again" \
  --send-late="$BOT, help" --send-late="$BOT, thank you" --send-late="$BOT, hello there" \
  --record="$f" --record-seconds=85 --overwrite >"$f.log" 2>&1
# His replies are "Spyburd: "..."" in party chat once grouped, or "Spyburd says, "..."" in /say.
# A line counts as new when it contains a new line, so a mood word added before or after doesn't hide it.
said=$(sed -nE "s/.*\\] $BOT[^:\"]*: \"(.*)\"\$/\\1/p; s/.*\\] $BOT( [A-Za-z]+)? says, \"(.*)\"\$/\\2/p" "$f")
if [[ -z $said ]]; then
  fail "$BOT said nothing; see $f (is $CHARACTER in $BOT's realm, is $BOT turned on, and did the /tele and /invite work?)"
else
  heard=$(grep -cFf <(printf '%s\n' "$new_lines") <<<"$said")
  (( heard >= 1 )) && pass "he said $heard new line(s)" ||
    fail "none of his answers was a new line (his lines are random; run it again). See $f"
  grep -Ff <(printf '%s\n' "$new_lines") <<<"$said" | sed 's/^/      new: /'
  grep -qE '\{[a-z0-9]+\}|\[(if|once|cooldown)' <<<"$said" && fail "a raw {placeholder} or [tag] reached the chat" ||
    pass "no raw {placeholder} or [tag] in his chat"
fi

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
