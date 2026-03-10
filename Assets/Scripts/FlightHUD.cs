// ============================================================
//  FlightHUD.cs
//  On-screen flight data display.
//  Attach to any GameObject in the scene.
// ============================================================
using UnityEngine;

public class FlightHUD : MonoBehaviour
{
    public ArduPilotBridge bridge;

    // Cached GUI style (created once)
    private GUIStyle _boxStyle, _labelStyle, _warningStyle;
    private bool     _stylesReady;

    private void OnGUI()
    {
        if (bridge == null) return;
        EnsureStyles();

        float W = Screen.width, H = Screen.height;

        // ---- Connection banner -------------------------------------------
        if (!bridge.Connected)
        {
            string msg = "WAITING FOR ARDUPILOT SITL\n" +
                         "Run:  sim_vehicle.py -v ArduPlane -f JSON:127.0.0.1 --console --map";
            GUI.Box(new Rect(W * 0.1f, H * 0.05f, W * 0.8f, 70), msg, _warningStyle);
        }

        // ---- Primary flight data -----------------------------------------
        float panelW = 230f, panelH = 230f;
        GUI.Box(new Rect(10, 10, panelW, panelH), "", _boxStyle);

        float y = 18f;
        DrawRow("AIRSPEED",  $"{bridge.Airspeed:F1} m/s",    14f, y); y += 30f;
        DrawRow("ALTITUDE",  $"{bridge.Altitude:F1} m",      14f, y); y += 30f;
        DrawRow("ROLL",      $"{bridge.RollDeg:F1}°",        14f, y); y += 30f;
        DrawRow("PITCH",     $"{bridge.PitchDeg:F1}°",       14f, y); y += 30f;
        DrawRow("HEADING",   $"{bridge.YawDeg % 360f:F1}°",  14f, y); y += 30f;
        DrawRow("THROTTLE",  $"{bridge.ThrottleNorm * 100f:F0}%", 14f, y); y += 30f;
        DrawRow("SIM T",     $"{bridge.SimTime:F1} s",       14f, y);

        // ---- Attitude indicator (artificial horizon) ---------------------
        DrawAttitudeIndicator(W - 160f, 10f, 150f, 150f);

        // ---- Controls legend (bottom-left) -------------------------------
        GUI.Box(new Rect(10, H - 80f, 300f, 70f), "", _boxStyle);
        GUI.Label(new Rect(18, H - 74f, 290f, 64f),
            "SPACE  Reset sim\n" +
            "ESC    Quit\n" +
            "Mouse  (reserved for GCS)", _labelStyle);
    }

    // ------------------------------------------------------------------ //
    private void DrawRow(string label, string value, float x, float y)
    {
        GUI.Label(new Rect(x,       y, 110f, 26f), label,  _labelStyle);
        GUI.Label(new Rect(x + 115f, y, 100f, 26f), value, _labelStyle);
    }

    // ------------------------------------------------------------------ //
    //  Minimal artificial horizon drawn with GL                           //
    // ------------------------------------------------------------------ //
    private void DrawAttitudeIndicator(float px, float py, float w, float h)
    {
        if (bridge == null) return;

        float cx = px + w / 2f, cy = py + h / 2f;
        float roll  = bridge.RollDeg  * Mathf.Deg2Rad;
        float pitch = bridge.PitchDeg; // degrees; 1 deg ≈ h/60 pixels

        // Background box
        GUI.Box(new Rect(px, py, w, h), "", _boxStyle);

        // We use the immediate GUI drawing with a Texture2D horizon
        // (Simple 2-colour fill, rotated via matrix)
        GUI.BeginGroup(new Rect(px, py, w, h));

        // Sky / ground split (pitch offset)
        float pitchOffset = Mathf.Clamp(pitch * (h / 60f), -h / 2f, h / 2f);

        // Draw using a labelled box so the colours read correctly
        float half = h / 2f + pitchOffset;
        var savedColor = GUI.color;

        // Sky
        GUI.color = new Color(0.2f, 0.5f, 0.9f, 0.85f);
        GUI.DrawTexture(new Rect(0, 0, w, Mathf.Clamp(half, 0, h)), Texture2D.whiteTexture);

        // Ground
        GUI.color = new Color(0.5f, 0.3f, 0.1f, 0.85f);
        GUI.DrawTexture(new Rect(0, Mathf.Clamp(half, 0, h), w, h - Mathf.Clamp(half, 0, h)),
                        Texture2D.whiteTexture);

        GUI.color = savedColor;

        // Horizon line
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(0, half - 1f, w, 2f), Texture2D.whiteTexture);
        GUI.color = savedColor;

        // Roll chevron (fixed triangle at bottom)
        GUI.Label(new Rect(w / 2f - 12f, h - 24f, 26f, 22f), "▲", _labelStyle);

        GUI.EndGroup();

        // Roll text below
        GUI.Label(new Rect(cx - 30f, py + h + 2f, 60f, 20f),
                  $"R {bridge.RollDeg:+0.0;-0.0}°", _labelStyle);
    }

    // ------------------------------------------------------------------ //
    private void EnsureStyles()
    {
        if (_stylesReady) return;

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = MakeTex(4, 4, new Color(0f, 0f, 0f, 0.65f)) }
        };

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 14,
            normal    = { textColor = Color.white },
            fontStyle = FontStyle.Bold
        };

        _warningStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize  = 15,
            fontStyle = FontStyle.Bold,
            normal    =
            {
                background = MakeTex(4, 4, new Color(0.8f, 0.3f, 0f, 0.9f)),
                textColor  = Color.white
            },
            alignment = TextAnchor.MiddleCenter
        };

        _stylesReady = true;
    }

    private static Texture2D MakeTex(int w, int h, Color c)
    {
        var t = new Texture2D(w, h);
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = c;
        t.SetPixels(pix);
        t.Apply();
        return t;
    }
}
