#!/usr/bin/env bash
# draft-lines.sh - asks a local model (Ollama, on this machine only) to draft dialogue lines for a bot.
# Run it yourself, outside any sandbox:
#
#   tools/draft-lines.sh <bot> [task-file]          (a few minutes)
#
#   <bot>        the bot's key, e.g. persoper; needs characters/<bot>.md, drafts/<bot>-style.md and its .dlg file
#   [task-file]  what to write this time (default drafts/<bot>-task.md): one row per section,
#                "section | lines | what the player just did, and anything extra [| long]";
#                "long" asks for long heartfelt lines (20 to 35 words) instead of short ones
#   MODEL        the Ollama model (default dialogue-writer, made from tools/draft-lines.Modelfile)
#   FORMAT       the format rules file (default drafts/format.md), e.g. to compare two rule sets
#   STYLE        the bot's style rules file (default drafts/<bot>-style.md), e.g. to test new examples
#   MAX_EXAMPLES how many of the bot's current lines to show per section (default 10, 1 to 40)
#   LONG_MIN, LONG_MAX  words asked for in "long" lines (default 20 and 35); longer lines are flagged
#
# Needs Ollama running (sudo systemctl start ollama). It only ever talks to 127.0.0.1.
# For each row the model gets drafts/format.md, the bot's style rules, the character sheet, the bot's current lines for that
# section (as examples) and that one request, and answers with plain lines. The script cleans them and writes the
# section headings itself. Out: drafts/out/<bot>-<date>-<time>.dlg, the raw answers (.raw.txt), the
# prompts (.prompt.txt) and the checker's report plus warnings (.check.txt). Nothing goes into the
# game's dialogue files: a person reads the draft and copies the good lines over by hand.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BOT="${1:?usage: tools/draft-lines.sh <bot> [task-file]}"
TASK="${2:-$REPO/drafts/$BOT-task.md}"
MODEL="${MODEL:-dialogue-writer}"
FORMAT="${FORMAT:-$REPO/drafts/format.md}"
STYLE="${STYLE:-$REPO/drafts/$BOT-style.md}"
SHEET="$REPO/characters/$BOT.md"
DLG_FILE="$REPO/src/GameServer/bots/persoper/dialogue/$BOT.dlg"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CHECKER="$REPO/src/build/DialogueCheck/Release/DialogueCheck.dll"
OUT_DIR="$REPO/drafts/out"
export OLLAMA_HOST=127.0.0.1:11434   # this machine only, whatever the environment says
MAX_SECONDS=600                      # give up on one section after 10 minutes
MAX_ROWS=40                          # sections per run at most
MAX_COUNT=20                         # lines asked per section at most
MAX_EXAMPLES="${MAX_EXAMPLES:-10}"   # current lines shown per section at most (picked at random); more makes the model copy them
MAX_PROMPT_CHARS=24000               # about 6,000 tokens: fits the Modelfile's num_ctx 8192 with room for the answer
LONG_MIN="${LONG_MIN:-20}"           # words asked for in a "long" line
LONG_MAX="${LONG_MAX:-35}"
LONG_WORDS=$LONG_MAX                 # longer lines are flagged
SHORT_LONG_WORDS=$((LONG_MIN - 2))   # a "long" line shorter than this is flagged
MAX_LINE_CHARS=293                   # the game shows a line this long in full; longer is flagged
# Phrases the format rules ban (drafts/format.md); a draft line with one is flagged.
BANNED="here to help|here for you|\btogether\b|i'm not sure|appreciate your input|healing services|healing powers|real deal|how may i assist|\blog ?in\b|\blog ?out\b|\blog back\b|\blogg(ing|ed)\b"

die() { echo "draft-lines: $*" >&2; exit 2; }
trim() { local s="$1"; s="${s#"${s%%[![:space:]]*}"}"; printf '%s' "${s%"${s##*[![:space:]]}"}"; }
[[ $BOT =~ ^[a-z]+$ ]] || die "bot key must be lower-case letters only"
[[ $LONG_MIN =~ ^[0-9]+$ && $LONG_MAX =~ ^[0-9]+$ ]] && ((LONG_MIN >= 2 && LONG_MIN < LONG_MAX && LONG_MAX <= 60)) ||
  die "LONG_MIN and LONG_MAX must be numbers with 2 <= LONG_MIN < LONG_MAX <= 60"
[[ $MAX_EXAMPLES =~ ^[0-9]+$ ]] && ((MAX_EXAMPLES >= 1 && MAX_EXAMPLES <= 40)) ||
  die "MAX_EXAMPLES must be a number from 1 to 40"
for f in "$FORMAT" "$STYLE" "$SHEET" "$DLG_FILE" "$TASK"; do [[ -f $f ]] || die "missing $f"; done
command -v ollama >/dev/null || die "ollama isn't installed"
ollama list 2>/dev/null | grep -q "^$MODEL[: ]" ||
  die "model '$MODEL' not found; is Ollama running? Make it with: ollama create $MODEL -f tools/draft-lines.Modelfile"

mkdir -p "$OUT_DIR"
stamp="$(date +%Y%m%d-%H%M%S)"
base="$OUT_DIR/$BOT-$stamp"
draft="$base.dlg"; raw="$base.raw.txt"; prompts="$base.prompt.txt"; report="$base.check.txt"
: > "$raw"; : > "$prompts"; : > "$report"

# What every request starts with.
common="$(
  cat "$FORMAT"
  printf '\n'
  cat "$STYLE"
  printf '\nCHARACTER SHEET\n\n'
  cat "$SHEET"
)"

# The bot's current lines for one section (with or without an [if ...] on its heading), as examples. Only
# that section's, so the prompt stays small as her file grows. Leaves out comments and lines with
# placeholders other than {0}, so the model doesn't copy those. A big section shows MAX_EXAMPLES of
# them at random, so the prompt stays small (the copy check below still compares with all of them).
examples() {
  awk -v sec="$1" '/^~ /{ on = ($0 == "~ " sec || index($0, "~ " sec " [") == 1); next }
    on && NF && !/^#/' "$DLG_FILE" | grep -v '{[^0]' | shuf -n "$MAX_EXAMPLES"
}

# The draft collects each section's lines under one heading, in the order the sections first appear
# (the checker rejects a heading that appears twice).
declare -A body=()
order=()
rows=0
while IFS='|' read -r section count what kind; do
  section="$(trim "$section")"; count="$(trim "$count")"; what="$(trim "$what")"; kind="$(trim "$kind")"
  [[ -z $section || $section == \#* ]] && continue
  rows=$((rows + 1))
  ((rows <= MAX_ROWS)) || { echo "draft-lines: more than $MAX_ROWS rows, the rest skipped" | tee -a "$report"; break; }
  [[ $section =~ ^[a-z_]+( [a-z_]+)?$ ]] || die "bad section '$section' in $TASK"
  [[ $count =~ ^[0-9]+$ ]] && ((count >= 1 && count <= MAX_COUNT)) || die "bad line count '$count' for $section"
  [[ -z $kind || $kind == long ]] || die "bad kind '$kind' for $section (only 'long')"

  if [[ $kind == long ]]; then
    request="$(printf '\nYOUR TASK: LONG LINES\n\nWrite %s new LONG lines for the section "%s": %s.\nEvery line is %s to %s words, two or three sentences, with real feeling, each on one row. Short lines are wrong here, and so is any line over %s words.\nAnswer with the %s lines only, one per row.\n' \
      "$count" "$section" "$what" "$LONG_MIN" "$LONG_MAX" "$LONG_MAX" "$count")"
  else
    request="$(printf '\nYOUR TASK\n\nWrite %s new lines for the section "%s": %s.\nAnswer with the %s lines only, one per row.\n' \
      "$count" "$section" "$what" "$count")"
  fi
  example_lines="$(examples "$section")"
  prompt="$(printf '%s\n\nEXAMPLE LINES (the current lines for this section; write new lines, never copies of these)\n\n%s\n%s\n' \
    "$common" "${example_lines:-(none yet)}" "$request")"
  printf '\n##### %s\n%s\n' "$section" "$prompt" >> "$prompts"
  # A prompt longer than the model's context would be cut off at the start (the rules), so stop instead.
  size=${#prompt}
  ((size <= MAX_PROMPT_CHARS)) ||
    die "the prompt for $section is $size characters, more than $MAX_PROMPT_CHARS; raise num_ctx in the Modelfile and MAX_PROMPT_CHARS here"

  echo; echo "draft-lines: ~ $section ($count lines)"
  # The answer shows on screen as it's written (pipefail: a failed or timed-out model fails the line).
  answer="$(printf '%s\n' "$prompt" | timeout "$MAX_SECONDS" ollama run --nowordwrap "$MODEL" | tee /dev/stderr)" ||
    die "the model failed or timed out on $section; earlier answers in $raw"
  printf '\n##### %s\n%s\n' "$section" "$answer" >> "$raw"

  # Clean: drop headings, fences, the model's own notes ("5 new lines", "Here are...") and blank rows;
  # strip list marks, quotes and spaces; make curly quotes and long dashes plain (the file is plain
  # ASCII); keep at most twice the count. A song ("~ words ~") gets its leading \~ so it isn't read as a heading.
  cleaned="$(echo "$answer" |
    sed -E 's/^[[:space:]]*~([^~]+~)/\\~\1/' |
    grep -v -e '^[[:space:]]*```' -e '^[[:space:]]*~' -e '^[[:space:]]*===' -e '^[[:space:]]*#' -e ':[[:space:]]*$' |
    grep -v -i -E '^[[:space:]]*([0-9]+ (new |more |long )*"?[a-z_ ]*"? ?lines?\b|here (are|is) .*\blines?\b)' |
    sed -E "s/[‘’]/'/g; s/[“”]/\"/g; s/[[:space:]]*[—–][[:space:]]*/ - /g; s/…/.../g" |
    sed -E 's/^[[:space:]]*([-*•]|[0-9]+[.)])[[:space:]]*//; s/^"(.*)"$/\1/; s/^[[:space:]]+//; s/[[:space:]]+$//' |
    grep -v '^$' | head -n $((count * 2)))"
  [[ -v body[$section] ]] || order+=("$section")
  body[$section]+="$(printf '# %s lines\n%s' "${kind:-short}" "$cleaned")"$'\n'

  got="$(printf '%s\n' "$cleaned" | grep -c .)"
  ((got == count)) || echo "warning: ~ $section: asked for $count lines, got $got" >> "$report"
done < "$TASK"

printf '=== %s\n' "$BOT" > "$draft"
for section in "${order[@]}"; do printf '\n~ %s\n%s' "$section" "${body[$section]}" >> "$draft"; done

# Warnings for the reviewer (not errors): lines too long, "long" lines too short, banned phrases,
# lines that copy one of her current lines or an earlier draft line (ignoring case, punctuation
# and [tags]), and near copies: lines whose first five words match one of hers.
awk -v max="$LONG_WORDS" -v min="$SHORT_LONG_WORDS" -v chars="$MAX_LINE_CHARS" '
  /^~ / { long = 0; next }
  /^# long lines/ { long = 1; next }
  /^# short lines/ { long = 0; next }
  /^(#|===)/ || NF == 0 { next }
  NF > max { printf "warning: line %d has %d words\n", NR, NF }
  length($0) > chars { printf "warning: line %d has %d characters\n", NR, length($0) }
  long && NF < min { printf "warning: line %d is a long line with only %d words\n", NR, NF }' "$draft" >> "$report"
grep -n -i -P "$BANNED" "$draft" | sed 's/^/warning: banned phrase at line /' >> "$report"
grep -n -P '\{(?!0\})[^}]*\}' "$draft" | sed 's/^/warning: placeholder other than {0} at line /' >> "$report"
grep -n -P '[^\x00-\x7F]' "$draft" | sed 's/^/warning: unusual character at line /' >> "$report"
awk '
  function key(s) { s = tolower(s); gsub(/\[[^]]*\]/, "", s); gsub(/[^a-z0-9{}]/, "", s); return s }
  function start(s,   w, n, i, out) { n = split(tolower(s), w, " "); if (n < 6) return ""
    for (i = 1; i <= 5; i++) out = out key(w[i]) " "; return out }
  /^(~|#|===|->)/ { next }
  { k = key($0); if (k == "") next; st = start($0) }
  FNR == NR { old[k] = 1; if (st != "") oldstart[st] = 1; next }
  k in old { printf "warning: line %d copies one of her current lines\n", FNR; next }
  k in seen { printf "warning: line %d repeats draft line %d\n", FNR, seen[k]; next }
  st != "" && st in oldstart { printf "warning: line %d starts like one of her current lines\n", FNR }
  { seen[k] = FNR }' "$DLG_FILE" "$draft" >> "$report"

# The end summary, centred so it stands out from the scrolling answers above it.
centre() {
  local width pad
  width="$(tput cols 2>/dev/null || echo 80)"; [[ $width =~ ^[0-9]+$ ]] || width=80
  pad=$(( (width - ${#1}) / 2 )); ((pad < 0)) && pad=0
  printf '%*s%s\n' "$pad" '' "$1"
}

if [[ -f $CHECKER ]]; then
  "$DOTNET" "$CHECKER" "$draft" >> "$report" 2>&1
  status=$?
  cat "$report"
  [[ $status -eq 0 ]] && result="checker found no problems" || result="checker found problems"
else
  cat "$report"
  result="checker not built, skipped (dotnet build src/DaocServer.sln -c Release)"
fi
warnings="$(grep -c '^warning:' "$report")"
echo
centre "=================================================="
centre "$BOT: $(basename "$TASK")"
centre "$result, $warnings warnings"
centre "draft:  ${draft#"$REPO"/}"
centre "report: ${report#"$REPO"/}"
centre "=================================================="
