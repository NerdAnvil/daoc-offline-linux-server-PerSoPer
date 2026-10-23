#!/usr/bin/env bash
# test-named-delete.sh - check "bots delete" on a named bot against a test server folder.
#
#   tools/test-named-delete.sh <server-folder> [bot-name]     (default bot: Shnacksize)
#
# Steps: delete the bot, wait until it is gone, restart the server and check it stays gone, then
# remake it with "bots create --personality" and check it is back at level 1 and on.
# Needs the server RUNNING when you start it. It stops and starts the server itself, so use it on
# a test folder only. Exit code 0 = every check passed.
set -uo pipefail

SERVER=${1:?usage: $0 <server-folder> [bot-name]}
NAME=${2:-Shnacksize}
ADMIN="$SERVER/daoc-admin"
CTL="$SERVER/daoc-server.sh"
WAIT=60   # seconds, for every wait below

[[ -x $ADMIN && -x $CTL ]] || { echo "not a server folder: $SERVER" >&2; exit 2; }

fails=0
check() { # check <description> <command...>
  local what=$1; shift
  if "$@"; then echo "PASS  $what"; else echo "FAIL  $what"; fails=$((fails + 1)); fi
}

# The State and Lv columns of the bot's row in "bots named list".
row() { "$ADMIN" bots named list 2>&1 | awk -v n="$NAME" 'tolower($1) == tolower(n) { print }'; }
# Lv is blank for a deleted bot, so the columns shift: State is the field after Lv when Lv is a number.
state() { row | awk '{ print ($5 ~ /^[0-9]+$/) ? $6 : $5 }'; }
level() { row | awk '{ print ($5 ~ /^[0-9]+$/) ? $5 : 0 }'; }
in_roster() { "$ADMIN" bots list 2>&1 | grep -iw -- "$NAME" >/dev/null; }

wait_for() { # wait_for <command...>: true as soon as the command succeeds, false after $WAIT seconds
  local i
  for ((i = 0; i < WAIT; i++)); do
    "$@" && return 0
    sleep 1
  done
  return 1
}

is_deleted() { [[ $(state) == deleted ]] && ! in_roster; }
is_on() { [[ $(state) == on ]]; }
admin_answers() { "$ADMIN" bots named list >/dev/null 2>&1; }

echo "Bot: $NAME   Server: $SERVER"
before=$(state)
[[ -n $before ]] || { echo "No such named bot: $NAME" >&2; exit 2; }
echo "Before: state=$before level=$(level)"
[[ $before == on || $before == off ]] || { echo "$NAME must exist first (state is '$before'). Run: bots create --personality $NAME" >&2; exit 2; }

echo "--- 1. delete"
"$ADMIN" bots delete "$NAME"
check "$NAME is marked deleted and leaves the roster" wait_for is_deleted

echo "--- 2. restart"
"$CTL" stop
(cd "$SERVER" && setsid "$CTL" start --console </dev/null >"$SERVER/logs/console.out" 2>&1 &)
check "server is back and answering" wait_for admin_answers
check "$NAME stays deleted after the restart" is_deleted

echo "--- 3. remake"
"$ADMIN" bots create --personality "$NAME"
check "$NAME is back and on" wait_for is_on
check "$NAME is level 1" test "$(level)" = 1
check "$NAME is in the roster" in_roster

echo
if ((fails == 0)); then echo "ALL PASSED"; else echo "$fails CHECK(S) FAILED"; fi
exit $((fails > 0))
