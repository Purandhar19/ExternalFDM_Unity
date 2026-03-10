// ============================================================
//  AircraftVisuals.cs
//  Converts NED state → Unity world-space transform.
//  Also procedurally builds a simple plane mesh if no model
//  is assigned, so the scene works immediately out-of-the-box.
//
//  Coordinate frames:
//    NED  (FDM)  :  +X North,  +Y East,  +Z Down
//    Unity world :  +X East,   +Y Up,    +Z North
// ============================================================
using UnityEngine;

[DisallowMultipleComponent]
public class AircraftVisuals : MonoBehaviour
{
    [Header("Aircraft Model")]
    [Tooltip("Assign your own 3-D model here.  Leave empty to use the built-in placeholder.")]
    public GameObject modelRoot;

    [Tooltip("Scale applied to the mesh (adjust if your model is very large or small).")]
    public float modelScale = 1f;

    [Header("Camera Rig")]
    [Tooltip("How far behind and above the aircraft the chase camera sits (Unity units = metres).")]
    public Vector3 cameraOffset = new Vector3(0f, 3f, -12f);

    [Tooltip("How fast the camera follows attitude changes (lower = more lag).")]
    [Range(1f, 20f)] public float cameraLerp = 5f;

    // ------------------------------------------------------------------ //
    private Camera      _cam;
    private GameObject  _mesh;
    private TrailRenderer _trail;

    // ------------------------------------------------------------------ //
    private void Awake()
    {
        // Build or assign mesh
        if (modelRoot != null)
        {
            _mesh = Instantiate(modelRoot, transform);
        }
        else
        {
            _mesh = BuildPlaceholderPlane();
            _mesh.transform.SetParent(transform, false);
        }
        _mesh.transform.localScale = Vector3.one * modelScale;

        // Chase camera
        _cam = Camera.main;
        if (_cam == null)
        {
            var camGO = new GameObject("ChaseCamera");
            _cam = camGO.AddComponent<Camera>();
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane  = 20000f;
        }

        // Trail
        _trail = _mesh.AddComponent<TrailRenderer>();
        _trail.time = 4f;
        _trail.startWidth = 0.3f;
        _trail.endWidth   = 0f;
        _trail.material   = new Material(Shader.Find("Sprites/Default"));
        _trail.startColor = new Color(1f, 0.8f, 0.2f, 0.8f);
        _trail.endColor   = new Color(1f, 0.5f, 0.1f, 0f);
    }

    // ------------------------------------------------------------------ //
    //  Called every FixedUpdate by ArduPilotBridge                        //
    // ------------------------------------------------------------------ //
    public void UpdateTransform(FixedWingFDM.State s)
    {
        // NED → Unity position
        // Unity: X=East Y=Up Z=North
        Vector3 pos = new Vector3(s.pe, -s.pd, s.pn);
        transform.position = pos;

        // NED Euler → Unity rotation
        // ArduPilot/NED: roll=phi(X), pitch=theta(nose-up+), yaw=psi(CW from N)
        // Unity: pitch around X (nose-up = negative X rotation)
        //        yaw   around Y (CW from above = positive Y)
        //        roll  around Z (right-wing-down = negative Z)
        float rollDeg  =  s.phi   * Mathf.Rad2Deg;
        float pitchDeg = -s.theta * Mathf.Rad2Deg;   // NED pitch-up → Unity -X
        float yawDeg   =  s.psi   * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(pitchDeg, yawDeg, -rollDeg);

        // Chase camera
        UpdateCamera();
    }

    // ------------------------------------------------------------------ //
    private void UpdateCamera()
    {
        Vector3 desired = transform.TransformPoint(cameraOffset);
        _cam.transform.position = Vector3.Lerp(
            _cam.transform.position, desired, cameraLerp * Time.fixedDeltaTime);
        _cam.transform.LookAt(transform.position);
    }

    // ------------------------------------------------------------------ //
    //  Procedural placeholder airplane mesh                               //
    // ------------------------------------------------------------------ //
    private GameObject BuildPlaceholderPlane()
    {
        var root = new GameObject("PlaceholderAircraft");

        // Fuselage
        var fuse = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fuse.transform.SetParent(root.transform, false);
        fuse.transform.localScale    = new Vector3(0.35f, 0.35f, 2.8f);
        fuse.transform.localPosition = Vector3.zero;
        SetColor(fuse, new Color(0.15f, 0.6f, 1f));

        // Wings
        var wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wing.transform.SetParent(root.transform, false);
        wing.transform.localScale    = new Vector3(6f, 0.08f, 0.9f);
        wing.transform.localPosition = new Vector3(0f, 0f, 0.1f);
        SetColor(wing, new Color(0.2f, 0.75f, 1f));

        // Horizontal stabiliser
        var hs = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hs.transform.SetParent(root.transform, false);
        hs.transform.localScale    = new Vector3(2.2f, 0.06f, 0.5f);
        hs.transform.localPosition = new Vector3(0f, 0f, -1.3f);
        SetColor(hs, new Color(0.2f, 0.75f, 1f));

        // Vertical stabiliser
        var vs = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vs.transform.SetParent(root.transform, false);
        vs.transform.localScale    = new Vector3(0.06f, 0.6f, 0.5f);
        vs.transform.localPosition = new Vector3(0f, 0.3f, -1.3f);
        SetColor(vs, new Color(1f, 0.3f, 0.1f));

        // Nose cone (sphere)
        var nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        nose.transform.SetParent(root.transform, false);
        nose.transform.localScale    = new Vector3(0.35f, 0.35f, 0.5f);
        nose.transform.localPosition = new Vector3(0f, 0f, 1.4f);
        SetColor(nose, new Color(1f, 0.3f, 0.1f));

        return root;
    }

    private static void SetColor(GameObject go, Color c)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.color = c;
        go.GetComponent<Renderer>().material = mat;
        Destroy(go.GetComponent<Collider>());   // no physics collision needed
    }
}
