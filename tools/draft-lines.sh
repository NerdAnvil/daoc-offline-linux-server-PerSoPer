#!/usr/bin/env bash
# draft-lines.sh - asks a local model (Ollama, on this machine only) to draft dialogue lines for a bot.
# Run it yourself, outside any sandbox:
#
#   tools/draft-lines.sh <bot> [task-file]          (a few minutes)
#
#   <bot>        the bot's key, e.g. persoper; needs characters/<bot>.md and its .dlg file
#   [task-file]  what to write this time (default drafts/<bot>-task.md): one row per section,
#                "section | lines | what the player just did, and anything extra"
#   MODEL        the Ollama model (default dialogue-writer, made from tools/draft-lines.Modelfile)
#
# Needs Ollama running (sudo systemctl start ollama). It only ever talks to 127.0.0.1.
# For each row the model gets drafts/format.md, the character sheet, the bot's current lines (as
# examples) and that one request, and answers with plain lines. The script cleans them and writes the
# section headings itself. Out: drafts/out/<bot>-<date>-<time>.dlg, the raw answers (.raw.txt), the
# prompts (.prompt.txt) and the checker's report plus warnings (.check.txt). Nothing goes into the
# game's dialogue files: a person reads the draft and copies the good lines over by hand.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BOT="${1:?usage: tools/draft-lines.sh <bot> [task-file]}"
TASK="${2:-$REPO/drafts/$BOT-task.md}"
MODEL="${MODEL:-dialogue-writer}"
FORMAT="$REPO/drafts/format.md"
SHEET="$REPO/characters/$BOT.md"
DLG_FILE="$REPO/src/GameServer/bots/persoper/dialogue/$BOT.dlg"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
CHECKER="$REPO/src/build/DialogueCheck/Release/DialogueCheck.dll"
OUT_DIR="$REPO/drafts/out"
export OLLAMA_HOST=127.0.0.1:11434   # this machine only, whatever the environment says
MAX_SECONDS=600                      # give up on one section after 10 minutes
MAX_ROWS=40                          # sections per run at most
MAX_COUNT=20                         # lines asked per section at most
LONG_WORDS=35                        # longer lines are flagged

die() { echo "draft-lines: $*" >&2; exit 2; }
trim() { local s="$1"; s="${s#"${s%%[![:space:]]*}"}"; printf '%s' "${s%"${s##*[![:space:]]}"}"; }
[[ $BOT =~ ^[a-z]+$ ]] || die "bot key must be lower-case letters only"
for f in "$FORMAT" "$SHEET" "$DLG_FILE" "$TASK"; do [[ -f $f ]] || die "missing $f"; done
command -v ollama >/dev/null || die "ollama isn't installed"
ollama list 2>/dev/null | grep -q "^$MODEL[: ]" ||
  die "model '$MODEL' not found; is Ollama running? Make it with: ollama create $MODEL -f tools/draft-lines.Modelfile"

mkdir -p "$OUT_DIR"
stamp="$(date +%Y%m%d-%H%M%S)"
base="$OUT_DIR/$BOT-$stamp"
draft="$base.dlg"; raw="$base.raw.txt"; prompts="$base.prompt.txt"; report="$base.check.txt"
: > "$raw"; : > "$prompts"; : > "$report"

# What every request starts with. The examples leave out comments, swap-in parts and lines with
# placeholders other than {0}, so the model doesn't copy those.
common="$(
  cat "$FORMAT"
  printf '\nCHARACTER SHEET\n\n'
  cat "$SHEET"
  printf '\nEXAMPLE LINES (her current file; write new lines, never copies of these)\n\n'
  awk '/^~ part /{skip=1; next} /^~ /{skip=0} !skip' "$DLG_FILE" | grep -v '^#' | grep -v '{[^0]'
)"

printf '=== %s\n' "$BOT" > "$draft"
rows=0
while IFS='|' read -r section count what; do
  section="$(trim "$section")"; count="$(trim "$count")"; what="$(trim "$what")"
  [[ -z $section || $section == \#* ]] && continue
  rows=$((rows + 1))
  ((rows <= MAX_ROWS)) || { echo "draft-lines: more than $MAX_ROWS rows, the rest skipped" | tee -a "$report"; break; }
  [[ $section =~ ^[a-z_]+( [a-z_]+)?$ ]] || die "bad section '$section' in $TASK"
  [[ $count =~ ^[0-9]+$ ]] && ((count >= 1 && count <= MAX_COUNT)) || die "bad line count '$count' for $section"

  request="$(printf '\nYOUR TASK\n\nWrite %s new lines for her section "%s": %s.\nAnswer with the %s lines only, one per row.\n' \
    "$count" "$section" "$what" "$count")"
  printf '\n##### %s\n%s\n%s\n' "$section" "$common" "$request" >> "$prompts"

  echo; echo "draft-lines: ~ $section ($count lines)"
  # The answer shows on screen as it's written (pipefail: a failed or timed-out model fails the line).
  answer="$(printf '%s\n%s\n' "$common" "$request" | timeout "$MAX_SECONDS" ollama run --nowordwrap "$MODEL" | tee /dev/stderr)" ||
    die "the model failed or timed out on $section; earlier answers in $raw"
  printf '\n##### %s\n%s\n' "$section" "$answer" >> "$raw"

  # Clean: drop headings, fences and blank rows; strip list marks, quotes and spaces; keep at most twice the count.
  cleaned="$(echo "$answer" |
    grep -v -e '^[[:space:]]*```' -e '^[[:space:]]*~' -e '^[[:space:]]*===' -e '^[[:space:]]*#' -e ':[[:space:]]*$' |
    sed -E 's/^[[:space:]]*([-*•]|[0-9]+[.)])[[:space:]]*//; s/^"(.*)"$/\1/; s/^[[:space:]]+//; s/[[:space:]]+$//' |
    grep -v '^$' | head -n $((count * 2)))"
  printf '\n~ %s\n%s\n' "$section" "$cleaned" >> "$draft"

  got="$(printf '%s\n' "$cleaned" | grep -c .)"
  ((got == count)) || echo "warning: ~ $section: asked for $count lines, got $got" >> "$report"
done < "$TASK"

# Warnings for the reviewer (not errors): long lines and phrases the format rules ban.
awk -v max="$LONG_WORDS" '!/^(~|===)/ && NF > max { printf "warning: line %d has %d words\n", NR, NF }' "$draft" >> "$report"
grep -n -i -E "here to help|here for you|together|i'm not sure|healing services|healing powers|real deal" "$draft" |
  sed 's/^/warning: banned phrase at line /' >> "$report"

echo; echo "draft-lines: draft in $draft"
if [[ -f $CHECKER ]]; then
  "$DOTNET" "$CHECKER" "$draft" >> "$report" 2>&1
  status=$?
  cat "$report"
  [[ $status -eq 0 ]] && echo "draft-lines: checker found no problems" || echo "draft-lines: checker found problems (see $report)"
else
  cat "$report"
  echo "draft-lines: checker not built, skipped (dotnet build src/DaocServer.sln -c Release)"
fi
