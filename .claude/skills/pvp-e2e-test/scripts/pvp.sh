#!/bin/bash
# End-to-end PvP tests: the Editor as a client of the Docker game server, with throwaway backend accounts.
#   source .claude/skills/pvp-e2e-test/scripts/pvp.sh
# Loads the unity-playtest and game-server-deploy helpers too.

source "$(dirname "${BASH_SOURCE[0]}")/../../game-server-deploy/scripts/server.sh" || return 1

BACKEND="${BACKEND:-http://localhost:8080}"

# Registers a throwaway account and prints its session token:  T=$(tester_create tester_abc)
# Names must start with tester_ (cleanup only ever deletes those).
tester_create() {
  case "$1" in tester_*) ;; *) echo "test accounts must be named tester_*" >&2; return 1 ;; esac
  curl -s -X POST "$BACKEND/auth/register" -H "Content-Type: application/json" \
    -d "{\"email\":\"$1@example.com\",\"username\":\"$1\",\"password\":\"Testpass123!\"}" \
    | python -c "import sys,json; print(json.load(sys.stdin)['token'])"
}

# Deletes a tester_* account with its matches, sessions and profile (never anything else).
tester_delete() {
  case "$1" in tester_*) ;; *) echo "refusing: only tester_* accounts are deleted" >&2; return 1 ;; esac
  docker exec -i moba-db-1 psql -U moba -d moba -v ON_ERROR_STOP=1 -v name="$1" <<'SQL'
BEGIN;
DELETE FROM matches WHERE id IN (SELECT mp.match_id FROM match_players mp JOIN accounts a ON a.id = mp.account_id WHERE a.username = :'name');
DELETE FROM sessions WHERE account_id IN (SELECT id FROM accounts WHERE username = :'name');
DELETE FROM player_profiles WHERE account_id IN (SELECT id FROM accounts WHERE username = :'name');
DELETE FROM accounts WHERE username = :'name';
COMMIT;
SQL
}

# Lists leftover test accounts.
tester_list() { docker exec moba-db-1 psql -U moba -d moba -tAc "SELECT username FROM accounts WHERE username LIKE 'tester\_%'"; }

# The latest recorded match of a token's account, one line per player.
last_match() {
  curl -s "$BACKEND/me/matches?limit=1" -H "Authorization: Bearer $1" | python -c "
import sys,json
ms=json.load(sys.stdin)['matches']
if not ms: print('no matches'); sys.exit()
m=ms[0]; print(m['mode'], m['mapLayout'], 'won' if m['won'] else 'lost', str(m['durationSeconds'])+'s', 'cheats' if m['cheatsUsed'] else '')
for p in m['players']: print(' ', p['team'], p['heroId'], p['username'] or ('bot' if p['isBot'] else 'anonymous'), 'L'+str(p['level']), '%d/%d/%d'%(p['kills'],p['deaths'],p['assists']))"
}

# Finds a match from the Frontend (play mode), waits through the queue, locks in a hero in champion select, presses
# Start now, and waits until the hero is ours in the match:
#   pvp_join kael "Tester"   (logged in, the account's username is used instead)
# Other players searching within the queue's 15 s land in the same champion select.
pvp_join() {
  ev "Moba.Net.GameClient.FindMatch(\"${2:-Tester}\"); return \"searching\";" >/dev/null
  local r
  for i in $(seq 1 15); do
    sleep 2
    r=$(evv 'var l = Moba.Net.GameClient.Lobby; var me = Moba.Net.GameClient.LocalSeat(l); return l == null ? "none" : l.phase + (me != null ? ":" + me.team + ":" + (me.locked ? "locked" : "open") : "");')
    case "$r" in lobby:*:open) ev "Moba.Net.GameClient.SendPick(\"${1:-kael}\"); Moba.Net.GameClient.SendStartNow(); return \"picked\";" >/dev/null ;; esac
    r=$(ev 'var gm = Moba.GameManager.Instance; return gm != null && gm.hero != null && gm.hero.Remote != null ? "joined as " + gm.hero.name + " on " + gm.hero.Unit.team + ", " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name : "waiting";')
    echo "$r" | grep -q joined && { echo "$r"; return 0; }
  done
  echo "not joined: $(ev 'return "lastError=" + Moba.Net.GameClient.LastError;')"; return 1
}

pvp_leave() { ev 'Moba.Net.GameClient.Leave(); return "left";'; sleep 5; }

# Cheat on the server (needs MOBA_CHEATS on): pvp_cheat Level 5 | Gold | NoManaCost | NoCooldowns
pvp_cheat() { ev "var h = Moba.GameManager.Instance.hero; for (int i = 0; i < ${2:-1}; i++) h.Remote.SendCheat(Moba.Net.CheatAction.$1); return \"sent\";"; }

# Waits for the server to record a match that ended after $1 (an ISO time from `date -u +%Y-%m-%dT%H:%M:%SZ`).
wait_recorded() {
  for i in $(seq 1 ${2:-70}); do
    sleep 6
    r=$(docker compose logs --since "$1" game-server 2>&1 | grep -E "\[Matches\]")
    [ -n "$r" ] && { echo "$r"; return 0; }
  done
  echo "no [Matches] line yet"; return 1
}
