#!/bin/bash
# Helpers for driving the open Unity Editor (moba-test) from Git Bash through the `unity` CLI.
#   source .claude/skills/unity-playtest/scripts/unity.sh
# Every function works from the moba-test folder (this script cd's there).

MOBA_TEST="$(cd "$(dirname "${BASH_SOURCE[0]}")" && git rev-parse --show-toplevel)"
cd "$MOBA_TEST" || return 1

# ---------- Running code in the Editor ----------

# Runs C# in the Editor and prints the returned value (or the error). The snippet must `return` something.
# The Editor's main thread gives up after ~5 s: queue long work (see game-server-deploy) instead.
ev() { unity command eval "$1" --result-only --no-banner 2>&1 | grep -oE '"result": ".*"|"message": ".*"' | head -3; }

# Prints just the returned value, unquoted (for use in variables).
evv() { ev "$1" | sed -E 's/^"(result|message)": "(.*)"$/\2/'; }

# Waits until the Editor finished compiling. $1 = C# bool expression that is true once the new code is in
# (e.g. 'System.Type.GetType("Moba.Net.NetItems, Assembly-CSharp") != null'); default: true.
compiled() {
  local expr="${1:-true}" r e
  for i in $(seq 1 60); do
    sleep 3
    r=$(ev "return \$\"{UnityEditor.EditorApplication.isCompiling}|{($expr)}\";")
    echo "$r" | grep -q 'False|True' && { echo "compiled"; return 0; }
    if [ $((i % 4)) -eq 0 ]; then
      e=$(unity command recompile_status --result-only --no-banner 2>&1 | grep -E 'error CS' | head -12)
      [ -n "$e" ] && { echo "$e"; return 1; }
    fi
  done
  echo "timeout: $r"; return 1
}

# Picks up changed scripts and waits for the compile. $1 as for compiled.
recompile() {
  ev 'UnityEditor.AssetDatabase.Refresh(); UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(); return "refresh";' >/dev/null
  compiled "$1"
}

clearconsole() { unity command clear_console --no-banner >/dev/null 2>&1; }

# Console warnings and errors (logs left out), counted.
errors() {
  unity command console --no-banner 2>&1 | grep -oE '"(level|message)":"[^"]{0,250}' | paste - - \
    | grep -vi '"level":"log' | sort | uniq -c | head -${1:-15}
}

# ---------- Play mode ----------

openscene() { ev "UnityEditor.SceneManagement.EditorSceneManager.OpenScene(\"Assets/_Project/Scenes/${1:-Frontend}.unity\"); return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;"; }

# Enters play mode from a scene (default Frontend) with a clean console, and lets test input through.
play() {
  openscene "${1:-Frontend}" >/dev/null
  clearconsole
  unity command editor_play --no-banner >/dev/null 2>&1
  sleep 8
  testinput >/dev/null
}

# Leaves play mode and puts the Editor back the way the user expects it (Frontend open, normal input).
stop() {
  unity command editor_stop --no-banner >/dev/null 2>&1
  sleep 2
  restoreinput >/dev/null
  openscene Frontend
}

# Input reaches the game even when the Game view isn't focused (needed for key/uiclick from the shell).
testinput() { ev 'UnityEngine.Application.runInBackground = true; var s = UnityEngine.InputSystem.InputSystem.settings; s.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; s.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus; return "test input on";'; }
restoreinput() { ev 'var s = UnityEngine.InputSystem.InputSystem.settings; s.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus; s.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices; return "input restored";'; }

# ---------- The user's login (never print it, never log it out) ----------

# Moves the user's session token to a backup key so tests can't use their account.
token_aside() { ev 'var t = UnityEngine.PlayerPrefs.GetString("moba.auth.token", ""); if (t.Length > 0 && !UnityEngine.PlayerPrefs.HasKey("moba.auth.token.userbackup")) UnityEngine.PlayerPrefs.SetString("moba.auth.token.userbackup", t); UnityEngine.PlayerPrefs.DeleteKey("moba.auth.token"); UnityEngine.PlayerPrefs.Save(); return "token set aside (backup: " + UnityEngine.PlayerPrefs.HasKey("moba.auth.token.userbackup") + ")";'; }

# Puts the user's token back and removes the backup (also clears any test account's token).
token_restore() { ev 'var b = UnityEngine.PlayerPrefs.GetString("moba.auth.token.userbackup", ""); if (b.Length == 0) { UnityEngine.PlayerPrefs.DeleteKey("moba.auth.token"); UnityEngine.PlayerPrefs.Save(); return "no backup: test token cleared"; } UnityEngine.PlayerPrefs.SetString("moba.auth.token", b); UnityEngine.PlayerPrefs.DeleteKey("moba.auth.token.userbackup"); UnityEngine.PlayerPrefs.Save(); return "user token restored";'; }

# Logs the game in as a test account: token_use "$TOKEN" (from the backend's /auth/register or /auth/login).
# Call token_aside first; the Frontend reads the token when it starts, so call this before play.
token_use() { ev "UnityEngine.PlayerPrefs.SetString(\"moba.auth.token\", \"$1\"); UnityEngine.PlayerPrefs.Save(); return \"test token in place\";"; }

# ---------- Input ----------

# Presses and releases one key: key Q  (Input System Key names).
key() {
  ev "UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.$1)); return \"ok\";" >/dev/null
  sleep 0.3
  ev "UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current, new UnityEngine.InputSystem.LowLevel.KeyboardState()); return \"ok\";" >/dev/null
  sleep 0.4
}

# Clicks a UI Toolkit element. $1 = GameObject with the UIDocument (e.g. HUD, Frontend), $2 = C# Vector2 in panel
# space (use byname/byclass), $3 = mouse button (0 left, 1 right).
uiclick() {
  ev "var root = UnityEngine.GameObject.Find(\"$1\").GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement; var vt = root.panel.visualTree; UnityEngine.Vector2 a = $2; int btn = ${3:-0};
    void Send(UnityEngine.EventType t) { var e = new UnityEngine.Event { type = t, mousePosition = a, button = btn, clickCount = 1 }; UnityEngine.UIElements.EventBase pe = t == UnityEngine.EventType.MouseDown ? UnityEngine.UIElements.PointerDownEvent.GetPooled(e) : t == UnityEngine.EventType.MouseUp ? UnityEngine.UIElements.PointerUpEvent.GetPooled(e) : UnityEngine.UIElements.PointerMoveEvent.GetPooled(e); using (pe) vt.SendEvent(pe); }
    Send(UnityEngine.EventType.MouseMove); Send(UnityEngine.EventType.MouseDown); Send(UnityEngine.EventType.MouseUp); return \"clicked\";"
}

# Panel-space centre of an element: by name, or by USS class and index.
byname() { echo "UnityEngine.UIElements.UQueryExtensions.Q(UnityEngine.GameObject.Find(\"$1\").GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement, \"$2\").worldBound.center"; }
byclass() { echo "UnityEngine.UIElements.UQueryExtensions.Query(UnityEngine.GameObject.Find(\"$1\").GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement, null, \"$2\").AtIndex(${3:-0}).worldBound.center"; }

# ---------- Screenshots ----------

# Game view to Assets/Temp/<name>.png (read it with the Read tool). Remove with cleanshots before committing.
shot() { unity command capture_game_view --save_path "Assets/Temp/$1.png" --width 1600 --height 900 --no-banner >/dev/null 2>&1; }
cleanshots() { rm -rf "$MOBA_TEST/Assets/Temp" "$MOBA_TEST/Assets/Temp.meta"; }

# ---------- Probes (frame-by-frame measurements) ----------
# CLI calls take ~1 s each, too slow to watch anything quick. A probe runs inside the Editor instead: it hooks
# EditorApplication.update, writes its findings to SessionState "probe", and sets it to anything but "running" when done.
#   ev "$(cat my-probe.cs)"; probe_wait 60
probe_wait() {
  local r
  for i in $(seq 1 $(( ${1:-60} / 3 ))); do
    sleep 3
    r=$(ev 'return UnityEditor.SessionState.GetString("probe", "none");')
    echo "$r" | grep -q '"running"' || { echo "$r"; return 0; }
  done
  echo "still running: $r"; return 1
}

# Settings Unity flips by itself (Unity Services toggle on builds/play). Never commit them.
revert_noise() { git -C "$MOBA_TEST" checkout -q -- ProjectSettings/UnityConnectSettings.asset 2>/dev/null; git -C "$MOBA_TEST" status --short ProjectSettings; }
