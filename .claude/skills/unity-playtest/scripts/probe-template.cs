// Probe template: measures something every editor frame for a few seconds, inside the Editor.
// Copy, adapt the marked parts, then:  ev "$(cat my-probe.cs)"; probe_wait 30
// Rules for eval snippets: plain statements, fully qualified type names, end with `return ...;`.
var h = Moba.GameManager.Instance.hero;                      // <- what to watch
var log = new System.Text.StringBuilder();
double t0 = UnityEditor.EditorApplication.timeSinceStartup, next = 0;
UnityEditor.EditorApplication.CallbackFunction cb = null;
cb = () =>
{
    double t = UnityEditor.EditorApplication.timeSinceStartup - t0;
    if (t >= next)                                            // <- sample every 0.15 s (or log only changes)
    {
        next += 0.15;
        log.Append(t.ToString("0.00") + ":" + h.transform.position.ToString("0.0") + " ");
    }
    if (t > 3.0)                                              // <- how long to watch
    {
        UnityEditor.EditorApplication.update -= cb;
        UnityEditor.SessionState.SetString("probe", log.ToString());
    }
};
UnityEditor.SessionState.SetString("probe", "running");
UnityEditor.EditorApplication.update += cb;
h.OrderMove(h.transform.position + h.transform.forward * 8f); // <- the action to measure (optional)
return "probing";
