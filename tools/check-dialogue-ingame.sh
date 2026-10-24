#!/usr/bin/env bash
# check-dialogue-ingame.sh - in-game check of the dialogue folder, reload, fallback and checker
# (dialogue step 3), on a running test server. Run it yourself, outside any sandbox:
#
#   tools/check-dialogue-ingame.sh <server-folder> [character]     (asks for the password)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a level 1 test character in Kobaldjovi's realm (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# It edits dialogue/kobaldjovi.dlg and dialogue/intents.dlg, reloads, and talks to Kobaldjovi with
# the headless client (it teleports to him and invites him into the group, so he stands still and
# isn't fighting). The two files are put back and reloaded at the end, whatever happens.
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-dialogue-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
BOT=Kobaldjovi
TEST_LINE='Grazie mille, {0}! Straight from the folder.'
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
CHECKER="$REPO/src/build/DialogueCheck/Release/DialogueCheck.dll"
DLG="$SERVER/dialogue"

die() { echo "check-dialogue-ingame: $*" >&2; exit 2; }
if [[ -z ${DAOC_PASSWORD:-} ]]; then
  [[ -t 0 ]] || die "set DAOC_PASSWORD (the test account's password) first"
  read -rsp "Password for $ACCOUNT: " DAOC_PASSWORD; echo
  export DAOC_PASSWORD
fi
[[ -f $CLIENT && -f $CHECKER ]] || die "build first: dotnet build src/DaocServer.sln -c Release"
[[ -f $DLG/kobaldjovi.dlg && -f $DLG/intents.dlg ]] || die "no $DLG/kobaldjovi.dlg or intents.dlg"
pid=$(cat "$SERVER/run/server.pid" 2>/dev/null || true)
[[ $pid =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null || die "the server in $SERVER isn't running; start it first"
login=$("$DOTNET" "$CLIENT" --account="$ACCOUNT" --login-only 2>&1) ||
  die "can't log in as $ACCOUNT ($(grep -m1 FAIL <<<"$login")); check DAOC_PASSWORD"
grep -q " $CHARACTER, level" <<<"$login" || die "$ACCOUNT has no character $CHARACTER"

WORK="$(mktemp -d)"
RECORDS="$SERVER/logs"   # the recordings stay there after the run
cp "$DLG/kobaldjovi.dlg" "$DLG/intents.dlg" "$WORK/"
restore() {
  cp "$WORK/kobaldjovi.dlg" "$WORK/intents.dlg" "$DLG/"
  "$SERVER/daoc-admin" dialogue reload >/dev/null 2>&1 || echo "note: couldn't reload after putting the files back; run: $SERVER/daoc-admin dialogue reload"
  rm -rf "$WORK"
}
trap restore EXIT

failures=0
pass() { echo "PASS  $*"; }
fail() { echo "FAIL  $*"; failures=$((failures + 1)); }

# Kobaldjovi's thank-you replies swapped for the test line; "grazie" added to the thank words.
edit_files() {
  awk -v line="$TEST_LINE" '
    skip && ($0 == "" || /^~/ || /^===/) { skip = 0 }
    $0 == "~ reply thank" { print; print line; skip = 1; next }
    !skip { print }' "$WORK/kobaldjovi.dlg" > "$DLG/kobaldjovi.dlg"
  sed -E '0,/^thanks, thank,/{s/^(thanks, thank,.*)$/\1, grazie/}' "$WORK/intents.dlg" > "$DLG/intents.dlg"
}

# What Kobaldjovi said after our line $1 in the recording $2 (party chat when grouped, else /say).
replies() {
  awk -v said="\"$1\"" 'index($0, said) { on = 1; next } on' "$2" | grep -E "$BOT( says,|:) \"" || true
}

# Logs in, teleports to Kobaldjovi, invites him, says $1; the server's messages go to $2.
talk() {
  local say=$1 record=$2 attempt
  for attempt in 1 2; do
    "$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
      --send="/tele $BOT" --send="/invite $BOT" --listen=6 --goto="$BOT" \
      --send-late="/dialogue" --send-late="$say" \
      --record="$record" --record-seconds=10 --overwrite >"$record.log" 2>&1
    [[ -n $(replies "$say" "$record") ]] && return 0
    echo "      (no reply from $BOT on try $attempt; trying again)"
  done
  return 1
}

# The built-in thank-you lines, as Kobaldjovi says them to $CHARACTER.
awk '$0 == "~ reply thank" { on = 1; next } on && ($0 == "" || /^~/) { exit } on' "$WORK/kobaldjovi.dlg" |
  sed "s/{0}/$CHARACTER/g" > "$WORK/builtin-thanks.txt"

echo "== 1. An edited file is used after a reload (daoc-admin and in game)"
edit_files
out=$("$SERVER/daoc-admin" dialogue reload 2>&1)
if grep -q "No problems." <<<"$out" && grep -q "From the folder: .*kobaldjovi" <<<"$out"; then
  pass "daoc-admin dialogue reload: kobaldjovi.dlg from the folder, no problems"
else
  fail "daoc-admin dialogue reload said:"; echo "$out" | sed 's/^/      /'
fi
if talk "Jovi, grazie" "$RECORDS/dialogue-check-1.txt"; then
  grep -q "From the folder: .*kobaldjovi" "$RECORDS/dialogue-check-1.txt" && pass "/dialogue in game lists kobaldjovi from the folder" || fail "/dialogue in game didn't list kobaldjovi"
  replies "Jovi, grazie" "$RECORDS/dialogue-check-1.txt" | grep -q "Straight from the folder" && pass "\"Jovi, grazie\" got the new line" ||
    { fail "\"Jovi, grazie\" didn't get the new line:"; replies "Jovi, grazie" "$RECORDS/dialogue-check-1.txt" | sed 's/^/      /'; }
else
  fail "$BOT never answered (see $RECORDS/dialogue-check-1.txt.log)"
fi

echo "== 2. A broken file is reported and the built-in copy stays"
printf '\n~ reply nonsense\nOops.\n' >> "$DLG/kobaldjovi.dlg"
out=$("$SERVER/daoc-admin" dialogue reload 2>&1)
if grep -q "unknown intent 'nonsense'" <<<"$out" && grep -q "kobaldjovi.dlg: not used; the built-in copy stays" <<<"$out"; then
  pass "daoc-admin names the mistake and keeps the built-in kobaldjovi.dlg"
else
  fail "daoc-admin dialogue reload said:"; echo "$out" | sed 's/^/      /'
fi
grep -q "unknown intent 'nonsense'" "$SERVER/logs/server.log" && pass "server.log has the mistake" || fail "server.log doesn't mention the mistake"
if talk "Jovi, grazie" "$RECORDS/dialogue-check-2.txt"; then
  reply=$(replies "Jovi, grazie" "$RECORDS/dialogue-check-2.txt" | head -1)
  grep -qF -f "$WORK/builtin-thanks.txt" <<<"$reply" && pass "he still answers, with a built-in line" ||
    fail "his answer isn't a built-in thank-you line: $reply"
else
  fail "$BOT never answered"
fi

echo "== 3. The checker and preview"
out=$("$DOTNET" "$CHECKER" "$DLG/kobaldjovi.dlg" 2>&1); rc=$?
(( rc == 1 )) && grep -q "unknown intent 'nonsense'" <<<"$out" && pass "dialogue-check finds the mistake (exit 1)" ||
  { fail "dialogue-check on the broken file (exit $rc):"; echo "$out" | sed 's/^/      /'; }
cp "$WORK/kobaldjovi.dlg" "$WORK/intents.dlg" "$DLG/"
out=$("$DOTNET" "$CHECKER" "$DLG" 2>&1); rc=$?
(( rc == 0 )) && grep -q "no problems" <<<"$out" && pass "dialogue-check passes the restored folder (exit 0)" ||
  { fail "dialogue-check on the restored folder (exit $rc):"; echo "$out" | sed 's/^/      /'; }
out=$("$DOTNET" "$CHECKER" --preview KJ thank --folder "$DLG" 2>&1)
lines=$(wc -l < "$WORK/builtin-thanks.txt")
grep -q "$lines of $lines lines possible" <<<"$out" && pass "preview shows his $lines thank-you lines" ||
  { fail "preview said:"; echo "$out" | sed 's/^/      /'; }
grep -q "not now: \[if player_dead\]" <<<"$("$DOTNET" "$CHECKER" --preview persoper help --folder "$DLG" 2>&1)" &&
  grep -q "YES  Hold still" <<<"$("$DOTNET" "$CHECKER" --preview persoper help player_dead --folder "$DLG" 2>&1)" &&
  pass "preview follows [if player_dead]" || fail "preview ignores [if player_dead]"

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
