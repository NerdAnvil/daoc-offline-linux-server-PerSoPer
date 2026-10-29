#!/usr/bin/env bash
# check-hunterson-lines-ingame.sh - in-game check of Hunterson's new reviewed lines (dialogue step 7) on a running
# test server. The sibling of check-muckaduck-lines-ingame.sh. Run it yourself, outside any sandbox:
#
#   tools/check-hunterson-lines-ingame.sh <server-folder> [character]       (about 4 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a level 1 GM test character in Hunterson's realm, Midgard (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's dialogue/hunterson.dlg and dialogue/roleplayer.dlg to be the repo's. "New lines"
# are the lines the character file has that the old blocks in roleplayer.dlg didn't (commit 5f018e8).
# Part 1 (no server needed) has the checker confirm the file and that a new greeting can be picked.
# Part 2 teleports to Hunterson and invites him (so he stands still and isn't fighting), says hello three times,
# thanks them twice and asks for help once, and checks that at least one of their answers is a new line and
# that no raw {placeholder} or [tag] reached the chat.
# Their own lines are mixed with the Roleplayer type's and picked at random, so a run where they happen to
# pick only old lines fails part 2: run it again.
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-hunterson-lines-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
BOT=Hunterson
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
CHECKER="$REPO/src/build/DialogueCheck/Release/DialogueCheck.dll"
DIR="src/GameServer/bots/persoper/dialogue"
BASELINE=5f018e8   # before the character file: the old lines were blocks in roleplayer.dlg
RECORDS="$SERVER/logs"   # the recordings stay there after the run

die() { echo "check-hunterson-lines-ingame: $*" >&2; exit 2; }
if [[ -z ${DAOC_PASSWORD:-} ]]; then
  [[ -t 0 ]] || die "set DAOC_PASSWORD (the test account's password) first"
  read -rsp "Password for $ACCOUNT: " DAOC_PASSWORD; echo
  export DAOC_PASSWORD
fi
[[ -f $CLIENT && -f $CHECKER ]] || die "build first: dotnet build src/DaocServer.sln -c Release"
for file in hunterson.dlg roleplayer.dlg; do
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
  sed -E 's/[[:space:]]*\[(if|once|cooldown)[^]]*\]//g; s/[[:space:]]+$//' | sort -u; }
new_lines=$(comm -13 <(git -C "$REPO" show "$BASELINE:$DIR/roleplayer.dlg" | rows) <(rows < "$REPO/$DIR/hunterson.dlg") |
  sed "s/{0}/$CHARACTER/g")
[[ -n $new_lines ]] || die "no new lines since $BASELINE"
echo "($(grep -c . <<<"$new_lines") new lines since $BASELINE)"

echo "== 1. The checker accepts the file and offers a new greeting (no server needed)"
out=$("$DOTNET" "$CHECKER" "$SERVER/dialogue/hunterson.dlg" 2>&1)
grep -q "no problems" <<<"$out" && pass "checker: hunterson.dlg has no problems" ||
  { fail "checker found problems:"; echo "$out" | sed 's/^/      /'; }
out=$("$DOTNET" "$CHECKER" --preview hunterson greeting --player "$CHARACTER" --folder "$SERVER/dialogue" 2>&1)
grep -qF "Heroes are being watched" <<<"$out" && pass "preview: the \"Heroes are being watched\" greeting can be picked" ||
  { fail "preview: no \"Heroes are being watched\" greeting. It said:"; echo "$out" | sed 's/^/      /'; }

echo "== 2. In game they answer with new lines (about 3 minutes)"
f="$RECORDS/hunterson-lines-check-1.txt"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/tele $BOT" --send="/invite $BOT" --listen=6 --goto="$BOT" --follow="$BOT" --send-late-gap=11 \
  --send-late="Hunterson, hello" --send-late="Hunterson, thanks" --send-late="Hunterson, hello again" \
  --send-late="Hunterson, help" --send-late="Hunterson, thank you" --send-late="Hunterson, hello there" \
  --record="$f" --record-seconds=85 --overwrite >"$f.log" 2>&1
# Their replies are "Hunterson: "..."" in party chat once grouped, or "Hunterson says, "..."" in /say.
# A line counts as new when it contains a new line, so a mood word added before or after doesn't hide it.
said=$(sed -nE "s/.*\\] ([Tt]he )?$BOT[^:\"]*: \"(.*)\"\$/\\2/p; s/.*\\] ([Tt]he )?$BOT( [A-Za-z]+)? says, \"(.*)\"\$/\\3/p" "$f")
if [[ -z $said ]]; then
  fail "$BOT said nothing; see $f (is $CHARACTER in Midgard, is Hunterson turned on, and did the /tele and /invite work?)"
else
  heard=$(grep -cFf <(printf '%s\n' "$new_lines") <<<"$said")
  (( heard >= 1 )) && pass "they said $heard new line(s)" ||
    fail "none of their answers was a new line (their lines are random; run it again). See $f"
  grep -Ff <(printf '%s\n' "$new_lines") <<<"$said" | sed 's/^/      new: /'
  grep -qE '\{[a-z0-9]+\}|\[(if|once|cooldown)' <<<"$said" && fail "a raw {placeholder} or [tag] reached the chat" ||
    pass "no raw {placeholder} or [tag] in their chat"
fi

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
