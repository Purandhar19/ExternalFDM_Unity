// ============================================================
//  SceneBootstrap.cs  [Mission Planner Edition]
//
//  AUTO-BUILDS the entire simulation scene at runtime.
//  Your friend does NOT need to touch anything else in Unity.
//
//  SETUP (one-time, ~5 min):
//    1. New Unity project  →  3D (Built-In Render Pipeline)
//    2. Copy ALL .cs files into  Assets/Scripts/
//    3. Create empty GameObject  →  name it "Bootstrap"
//    4. Add component  →  search "SceneBootstrap"  →  click it
//    5. Edit > Project Settings > Time > Fixed Timestep = 0.0025
//
//  TO RUN (every time):
//    Step A: Open Mission Planner → Simulation tab → see README
//    Step B: Press Play in Unity  (the "waiting" banner will clear)
// ============================================================
using UnityEngine;

public class SceneBootstrap : MonoBehaviour
{
    [Header("Simulation Settings")]
    [Tooltip("UDP port SITL sends to. MUST match --sim-port-out in SITL (default 9002).")]
    public int sitlPort = 9002;

    [Tooltip("Start altitude in metres above ground. 100 is safe and avoids takeoff complexity.")]
    public float startAltitude = 100f;

    [Tooltip("Starting airspeed m/s. 25 is safe cruise. Do not go below 15 (stall).")]
    public float startAirspeed = 25f;

    [Tooltip("Starting heading in degrees. 0=North 90=East 180=South 270=West.")]
    public float startHeadingDeg = 0f;

    // ------------------------------------------------------------------ //
    private void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount  = 0;

        // Disable Unity's default camera; AircraftVisuals creates its own
        if (Camera.main != null)
            Camera.main.gameObject.SetActive(false);

        // Thread dispatcher must be created before ArduPilotBridge
        new GameObject("_Dispatcher").AddComponent<UnityMainThreadDispatcher>();

        BuildLighting();
        BuildGround();

        // Aircraft: ArduPilotBridge + AircraftVisuals (auto-required)
        var aircraft = new GameObject("Aircraft");
        var bridge   = aircraft.AddComponent<ArduPilotBridge>();
        bridge.listenPort      = sitlPort;
        bridge.startAltitude   = startAltitude;
        bridge.startAirspeed   = startAirspeed;
        bridge.startHeadingDeg = startHeadingDeg;

        // HUD overlay
        var hud = new GameObject("HUD").AddComponent<FlightHUD>();
        hud.bridge = bridge;

        // Keyboard shortcuts
        var kb = new GameObject("Keys").AddComponent<KeyboardHandler>();
        kb.bridge = bridge;

        Debug.Log("[Bootstrap] Scene built. Start Mission Planner SITL, then press Play in Unity.");
    }

    private void BuildLighting()
    {
        RenderSettings.ambientLight     = new Color(0.45f, 0.52f, 0.60f);
        RenderSettings.ambientIntensity = 0.8f;
        var sunGO = new GameObject("Sun");
        var sun   = sunGO.AddComponent<Light>();
        sun.type      = LightType.Directional;
        sun.intensity = 1.1f;
        sun.color     = new Color(1f, 0.96f, 0.85f);
        sunGO.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
    }

    private void BuildGround()
    {
        // Grass (5 km x 5 km)
        var gnd = GameObject.CreatePrimitive(PrimitiveType.Plane);
        gnd.name = "Ground";
        gnd.transform.localScale = new Vector3(500f, 1f, 500f);
        ApplyColor(gnd, new Color(0.22f, 0.42f, 0.14f));
        Destroy(gnd.GetComponent<Collider>());

        // Runway surface
        var rwy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rwy.name = "Runway";
        rwy.transform.localScale    = new Vector3(20f, 0.02f, 300f);
        rwy.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        ApplyColor(rwy, new Color(0.18f, 0.18f, 0.18f));
        Destroy(rwy.GetComponent<Collider>());

        // Centreline dashes
        for (int i = -6; i <= 6; i++)
        {
            var dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dash.transform.localScale    = new Vector3(0.5f, 0.03f, 8f);
            dash.transform.localPosition = new Vector3(0f, 0.02f, i * 20f);
            ApplyColor(dash, Color.white);
            Destroy(dash.GetComponent<Collider>());
        }

        // Threshold stripe
        var thresh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        thresh.transform.localScale    = new Vector3(20f, 0.03f, 1.5f);
        thresh.transform.localPosition = new Vector3(0f, 0.02f, -148f);
        ApplyColor(thresh, Color.white);
        Destroy(thresh.GetComponent<Collider>());
    }

    private static void ApplyColor(GameObject go, Color c)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.color = c;
        go.GetComponent<Renderer>().material = mat;
    }
}
