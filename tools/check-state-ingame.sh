#!/usr/bin/env bash
# check-state-ingame.sh - in-game check of bots answering questions about themselves (intent Part B4) on a
# running test server. Run it yourself, outside any sandbox:
#
#   tools/check-state-ingame.sh <server-folder> [character]       (about 4 minutes)
#
#   <server-folder>  a test copy of dist/daoc-server, with the server running (never a live one)
#   [character]      a level 1 GM test character in Midgard (default Headlesstest)
#   DAOC_ACCOUNT     the test account (default persopertest); DAOC_PASSWORD, if set, is its password
#
# Needs the test server's dialogue files for intents, Persoper, Shempenstein and Casual to be the repo's, and
# its GameServer.dll to have "/dialogue state". Summons Persoper, invites Shempenstein and brings him over with
# /dialogue gather (a test shortcut), then asks each bot in group chat where it is, its level or class, whether
# it is in a group and what it is doing. Before each question "/dialogue state <bot>" prints the bot's real
# zone, level, class, task and group; the answer (within 10 s) must be one of that bot's lines for the
# question ("~ reply where", "~ reply level", "~ reply groupstatus", "~ reply activity") that fits that state
# ([if bot_doing ...], [if bot_grouped]), filled with those values: a shrug line only when no other line fits.
# A mood tint before or after the line (Persoper's memory) is allowed. Both bots follow the player here, so
# only the "following" activity lines are seen; the others are unit tested (UT_StateAnswers).
# Prints PASS or FAIL per check; exits 0 only when every check passed.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${1:?usage: tools/check-state-ingame.sh <server-folder> [character]}"
SERVER="$(cd "$SERVER" && pwd)"
CHARACTER="${2:-Headlesstest}"
ACCOUNT="${DAOC_ACCOUNT:-persopertest}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CLIENT="$REPO/src/build/HeadlessClient/Release/HeadlessClient.dll"
DIR="src/GameServer/bots/persoper/dialogue"
RECORDS="$SERVER/logs"   # the recordings stay there after the run

die() { echo "check-state-ingame: $*" >&2; exit 2; }
if [[ -z ${DAOC_PASSWORD:-} ]]; then
  [[ -t 0 ]] || die "set DAOC_PASSWORD (the test account's password) first"
  read -rsp "Password for $ACCOUNT: " DAOC_PASSWORD; echo
  export DAOC_PASSWORD
fi
[[ -f $CLIENT ]] || die "build first: dotnet build src/DaocServer.sln -c Release"
for file in intents.dlg persoper.dlg shempenstein.dlg casual.dlg; do
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

# The raw rows (tags kept) of one section of a bot's file.
raw() { awk -v want="$2" '/^~ /{f=($0==want); next} /^===/{f=0} f&&NF&&!/^#/' "$REPO/$DIR/$1.dlg"; }
# A bot's raw rows of one section: Persoper's file, or Shempenstein's over Casual's.
rows() { if [[ $1 == Persoper ]]; then raw persoper "$2"; else raw casual "$2"; raw shempenstein "$2"; fi; }
# The rows that fit a "/dialogue state" line ($1), filled in: the ones with a slot or a condition that fit,
# else the shrugs (no slot, no condition). A row whose slot is empty, or with another condition, is left out.
fitting() { awk -v state="$1" -v me="$CHARACTER" '
  function put(s, token, value,   i) { while ((i = index(s, token)) > 0) s = substr(s, 1, i - 1) value substr(s, i + length(token)); return s }
  BEGIN {
    n = split(state, parts, "; ")
    for (i = 1; i <= n; i++) { eq = index(parts[i], "="); v[substr(parts[i], 1, eq - 1)] = substr(parts[i], eq + 1) }
    if (v["level"] == "0") v["level"] = ""
    if (v["groupsize"] == "0") v["groupsize"] = ""
    if (tolower(v["leader"]) == tolower(me)) v["leader"] = "you"
    split("zone level class monster destination groupsize leader", slots, " ")
  }
  {
    line = $0; cond = ""
    while (match(line, /[[:space:]]*\[[^]]*\][[:space:]]*$/)) {
      tag = substr(line, RSTART, RLENGTH); line = substr(line, 1, RSTART - 1)
      gsub(/^[[:space:]]*\[|\][[:space:]]*$/, "", tag)
      if (tag ~ /^if /) cond = substr(tag, 4)
    }
    ok = 1; stateful = (cond != "")
    if (cond == "bot_grouped") ok = (v["grouped"] == "yes")
    else if (cond == "not bot_grouped") ok = (v["grouped"] == "no")
    else if (cond ~ /^bot_doing /) ok = (substr(cond, 11) == v["doing"])
    else if (cond != "") ok = 0
    for (s in slots) {
      token = "{" slots[s] "}"
      if (index(line, token)) { stateful = 1; if (v[slots[s]] == "") ok = 0; else line = put(line, token, v[slots[s]]) }
    }
    line = put(line, "{0}", me)
    if (!ok) next
    if (stateful) fit[++nf] = line; else shrug[++ns] = line
  }
  END { if (nf) for (i = 1; i <= nf; i++) print fit[i]; else for (i = 1; i <= ns; i++) print shrug[i] }'; }
# What one bot said in a recording: party chat ("Name: "..."") or /say ("Name says, "..."").
said_by() { sed -nE "s/.*\\] ([Tt]he )?$1[^:\"]*: \"(.*)\"\$/\\2/p; s/.*\\] ([Tt]he )?$1( [A-Za-z]+)? says, \"(.*)\"\$/\\3/p" "$2" |
  sed -E 's/[[:space:]]+$//'; }
# The chat lines from the moment the player said $2 in group chat until $3 (default 10) seconds later.
within() { awk -v pat="$2" -v me="$CHARACTER" -v span="${3:-10}" '
  function secs(t) { split(t, a, ":"); return a[1] * 3600 + a[2] * 60 + a[3] }
  index($0, "[Party] " me ": \"" pat "\"") || index($0, "[Party] " me ": \"" pat " \"") { start = secs($1); seen = 1; next }
  seen && secs($1) - start <= span' "$1"; }
# The last "/dialogue state" line for a bot before the player asked $3, without "State <name>: ".
state_before() { awk -v who="$2" -v pat="$3" -v me="$CHARACTER" '
  index($0, "State " who) { s = $0; sub(/.*State [^:]*: /, "", s) }
  index($0, "[Party] " me ": \"" pat) { print s; exit }' "$1"; }
# True when $1 is one of the lines on stdin, alone or with a mood tint before or after it.
one_of() { local said="$1" line; while IFS= read -r line; do
  [[ -n $line ]] && { [[ $said == "$line" || $said == *" $line" || $said == "$line "* ]] && return 0; }; done; return 1; }

# The test: the line, who must answer, its "/dialogue state" name, and which section.
QUESTIONS=(
  "shemp, where are you?|Shempenstein|Shempenstein|~ reply where"
  "shemp, what level are you?|Shempenstein|Shempenstein|~ reply level"
  "shemp, are you in a group?|Shempenstein|Shempenstein|~ reply groupstatus"
  "shemp, what are you up to?|Shempenstein|Shempenstein|~ reply activity"
  "persoper, where are you?|Persoper|persoper|~ reply where"
  "persoper, what class are you?|Persoper|persoper|~ reply level"
  "persoper, are you grouped?|Persoper|persoper|~ reply groupstatus"
  "persoper, what are you doing?|Persoper|persoper|~ reply activity"
)

f="$RECORDS/state-check.txt"
late=(); for row in "${QUESTIONS[@]}"; do IFS='|' read -r line _ name _ <<<"$row"; late+=("--send-late=/dialogue state $name" "--send-late=/g $line"); done
echo "== Questions about the bots themselves, in group chat: ${#QUESTIONS[@]} lines (about 3.5 minutes)"
"$DOTNET" "$CLIENT" --account="$ACCOUNT" --character="$CHARACTER" \
  --send="/invite Shempenstein" --send="/dialogue gather" --send="/persoper" --listen=5 \
  --wait-for=Shempenstein --follow=Shempenstein --send-late-gap=11 "${late[@]}" \
  --record="$f" --record-seconds=15 --overwrite >"$f.log" 2>&1
grep -q "Shempenstein" "$f" || fail "Shempenstein never showed up in the recording; see $f (did the /invite work?)"
grep -q "State " "$f" || fail "no \"/dialogue state\" answer in the recording; is the server's GameServer.dll the new one?"

for row in "${QUESTIONS[@]}"; do
  IFS='|' read -r line who name section <<<"$row"
  state=$(state_before "$f" "$([[ $who == Persoper ]] && echo Persoper || echo "$who")" "$line")
  echo "  \"$line\""
  echo "      state: ${state:-none}"
  said=$(said_by "$who" <(within "$f" "$line"))
  [[ -n $said ]] && echo "$said" | sed "s/^/      $who: /"
  if [[ -z $state ]]; then fail "\"$line\": no state line for $who before it; see $f"; continue; fi
  expected=$(rows "$who" "$section" | fitting "$state")
  answered=""
  while IFS= read -r one; do [[ -n $one ]] && one_of "$one" <<<"$expected" && answered="$one"; done <<<"$said"
  if [[ -n $answered ]]; then pass "\"$line\": $who answered from \"$section\" with its real state"
  else fail "\"$line\": no fitting \"$section\" answer from $who within 10 s (a casting bot drops a reply; run it again). See $f"; fi
done

echo
(( failures == 0 )) && echo "All checks passed." || echo "$failures check(s) failed."
(( failures == 0 ))
