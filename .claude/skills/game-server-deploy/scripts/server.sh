#!/bin/bash
# Build the Linux game server from the open Editor and (re)start it in Docker.
#   source .claude/skills/game-server-deploy/scripts/server.sh
# Needs the unity-playtest helpers (sourced below).

source "$(dirname "${BASH_SOURCE[0]}")/../../unity-playtest/scripts/unity.sh" || return 1

SERVER_DLL="Builds/LinuxServer/MobaServer_Data/Managed/Assembly-CSharp.dll"

# Builds the server and waits until the build output is new. The build runs on the Editor's main thread: the `ev`
# call itself times out after 5 s, but the build carries on. (Queueing it with delayCall is unreliable: when the
# Editor idles in the background, queued work may never run.)
build_server() {
  local before now
  before=$(stat -c %Y "$SERVER_DLL" 2>/dev/null || echo 0)
  clearconsole
  ev 'Moba.EditorTools.GameBuilder.BuildLinuxServer(out string m); UnityEngine.Debug.Log(m); return m;' >/dev/null
  for i in $(seq 1 72); do
    now=$(stat -c %Y "$SERVER_DLL" 2>/dev/null || echo 0)
    [ "$now" -gt "$before" ] && break
    sleep 5
  done
  sleep 5
  unity command console --no-banner 2>&1 | grep -oE 'Server (built|build)[^"]{0,120}' | tail -1
  [ "$(stat -c %Y "$SERVER_DLL" 2>/dev/null || echo 0)" -gt "$before" ] || { echo "BUILD OUTPUT UNCHANGED: check the console (errors)"; return 1; }
  # Must be back on Windows/Player, or the Editor compiles as a dedicated server (UNITY_SERVER).
  ev 'return "editor target: " + UnityEditor.EditorUserBuildSettings.activeBuildTarget + "/" + UnityEditor.EditorUserBuildSettings.standaloneBuildSubtarget + ", http: " + UnityEditor.PlayerSettings.insecureHttpOption;'
  revert_noise
}

# (Re)starts the container with the current build and waits for the map to load. Extra args are extra compose
# files, e.g.:  deploy "$(nobots_override)"
deploy() {
  local files=(-f docker-compose.yml)
  for f in "$@"; do files+=(-f "$f"); done
  docker compose "${files[@]}" up -d --build 2>&1 | tail -1
  for i in $(seq 1 20); do
    sleep 2
    docker compose logs --tail 30 game-server 2>&1 | grep -q "Map loaded" && break
  done
  server_log 4
}

# The game server's own log lines ([GameServer], [Matches]) plus real errors, without Unity's headless noise.
server_log() {
  docker compose logs --tail "${2:-400}" game-server 2>&1 | grep -E "\[GameServer\]|\[Matches\]|Exception|NullReference" \
    | grep -v "Built-in Resource" | tail -"${1:-15}"
}

# A compose override with no bots (bots with 1-gold test items end matches within a minute or two).
# Normal server again: deploy (with no override).
nobots_override() {
  local f="${TMPDIR:-/tmp}/moba-nobots.yml"
  printf 'services:\n  game-server:\n    environment:\n      MOBA_BOTS: "0"\n' > "$f"
  echo "$f"
}
