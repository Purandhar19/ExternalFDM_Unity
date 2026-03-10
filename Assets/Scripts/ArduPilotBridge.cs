// ============================================================
//  ArduPilotBridge.cs
//  UDP bridge between ArduPilot SITL (JSON backend) and
//  the FixedWingFDM.  Attach to a GameObject in your scene.
//
//  ArduPilot → this script : binary PWM packet (port 9002)
//  This script → ArduPilot : JSON sensor state
// ============================================================
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[RequireComponent(typeof(AircraftVisuals))]
public class ArduPilotBridge : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector fields                                                    //
    // ------------------------------------------------------------------ //
    [Header("Network")]
    [Tooltip("UDP port ArduPilot sends PWM packets to (default 9002)")]
    public int listenPort = 9002;

    [Header("Initial Conditions")]
    [Tooltip("Start airborne at this altitude (m). 0 = on runway.")]
    public float startAltitude = 100f;

    [Tooltip("Initial airspeed (m/s). 25 m/s is a safe cruise for Aerosonde.")]
    public float startAirspeed = 25f;

    [Tooltip("Initial heading (degrees, 0 = North)")]
    public float startHeadingDeg = 0f;

    [Header("Channel Mapping (1-indexed, ArduPlane defaults)")]
    public int channelAileron  = 1;
    public int channelElevator = 2;
    public int channelThrottle = 3;
    public int channelRudder   = 4;

    [Header("Aircraft Parameters")]
    public FixedWingFDM.AircraftParams aircraftParams = new FixedWingFDM.AircraftParams();

    // ------------------------------------------------------------------ //
    //  Public read-only state (for UI / HUD)                              //
    // ------------------------------------------------------------------ //
    [HideInInspector] public float Airspeed;
    [HideInInspector] public float Altitude;
    [HideInInspector] public float RollDeg, PitchDeg, YawDeg;
    [HideInInspector] public float ThrottleNorm;
    [HideInInspector] public bool  Connected;
    [HideInInspector] public float SimTime;

    // ------------------------------------------------------------------ //
    //  Private                                                             //
    // ------------------------------------------------------------------ //
    private FixedWingFDM       _fdm;
    private FixedWingFDM.Controls _ctrl;
    private FixedWingFDM.Controls _pendingCtrl;
    private bool _hasNewCtrl;
    private readonly object _lock = new object();

    private UdpClient  _udp;
    private IPEndPoint _remote;
    private Thread     _rxThread;
    private bool       _running;
    private uint       _lastFrameCount;
    private float      _simTime;

    private AircraftVisuals _visuals;

    // ------------------------------------------------------------------ //
    //  Unity Lifecycle                                                     //
    // ------------------------------------------------------------------ //
    private void Awake()
    {
        _visuals = GetComponent<AircraftVisuals>();
    }

    private void Start()
    {
        ResetFDM();
        StartUDP();
        Debug.Log($"[Bridge] Ready – listening on UDP {listenPort}");
        Debug.Log("[Bridge] Start ArduPilot SITL with:");
        Debug.Log("    sim_vehicle.py -v ArduPlane -f JSON:127.0.0.1 --console --map");
    }

    private void FixedUpdate()
    {
        // Pull latest controls from network thread
        lock (_lock)
        {
            if (_hasNewCtrl) { _ctrl = _pendingCtrl; _hasNewCtrl = false; }
        }

        // Step FDM
        _fdm.Step(_ctrl, Time.fixedDeltaTime);
        _simTime += Time.fixedDeltaTime;

        // Cache readable values for HUD
        var s = _fdm.CurrentState;
        Airspeed    = _fdm.Airspeed;
        Altitude    = _fdm.Altitude;
        RollDeg     = s.phi   * Mathf.Rad2Deg;
        PitchDeg    = s.theta * Mathf.Rad2Deg;
        YawDeg      = s.psi   * Mathf.Rad2Deg;
        ThrottleNorm= _ctrl.dt;
        SimTime     = _simTime;

        // Update visuals
        _visuals.UpdateTransform(s);

        // Reply to ArduPilot
        SendState();
    }

    private void OnDestroy()
    {
        _running = false;
        _udp?.Close();
        _rxThread?.Abort();
    }

    // ------------------------------------------------------------------ //
    //  FDM Reset                                                           //
    // ------------------------------------------------------------------ //
    private void ResetFDM()
    {
        float headingRad = startHeadingDeg * Mathf.Deg2Rad;
        var init = new FixedWingFDM.State
        {
            u     = startAirspeed * Mathf.Cos(headingRad),
            v     = 0f,
            w     = 0f,
            p     = 0f, q = 0f, r = 0f,
            phi   = 0f,
            theta = 0f,
            psi   = headingRad,
            pn    = 0f,
            pe    = 0f,
            pd    = -startAltitude,   // NED: down is positive, so altitude = -pd
            time  = 0f
        };
        _fdm     = new FixedWingFDM(aircraftParams, init);
        _simTime = 0f;
        Debug.Log($"[Bridge] FDM reset – alt={startAltitude}m  Va={startAirspeed}m/s  hdg={startHeadingDeg}°");
    }

    // ------------------------------------------------------------------ //
    //  UDP                                                                 //
    // ------------------------------------------------------------------ //
    private void StartUDP()
    {
        _udp     = new UdpClient(listenPort);
        _remote  = new IPEndPoint(IPAddress.Any, 0);
        _running = true;
        _rxThread = new Thread(ReceiveLoop) { IsBackground = true };
        _rxThread.Start();
    }

    private void ReceiveLoop()
    {
        while (_running)
        {
            try
            {
                byte[]     data = _udp.Receive(ref _remote);
                if (data.Length < 10) continue;

                // ---- Parse ArduPilot binary packet ----------------------
                // Offset 0: uint16 magic (must be 29569 / 0x7381)
                // Offset 2: uint16 frame_rate (Hz)
                // Offset 4: uint32 frame_count
                // Offset 8: uint16 pwm[N]  (1000-2000 µs)
                ushort magic = BitConverter.ToUInt16(data, 0);
                if (magic != 29569) continue;

                // uint16 frameRate = BitConverter.ToUInt16(data, 2);  // reserved for future
                uint frameCount  = BitConverter.ToUInt32(data, 4);

                if (frameCount < _lastFrameCount)           // SITL restarted
                    UnityMainThreadDispatcher.Enqueue(ResetFDM);

                _lastFrameCount = frameCount;

                int nCh  = Math.Min((data.Length - 8) / 2, 16);
                var pwm  = new ushort[16];
                for (int i = 0; i < nCh; i++)
                    pwm[i] = BitConverter.ToUInt16(data, 8 + i * 2);

                var ctrl = new FixedWingFDM.Controls
                {
                    da = PwmNorm(pwm[channelAileron  - 1]),
                    de = PwmNorm(pwm[channelElevator - 1]),
                    dt = PwmUnit(pwm[channelThrottle - 1]),
                    dr = PwmNorm(pwm[channelRudder   - 1])
                };

                lock (_lock) { _pendingCtrl = ctrl; _hasNewCtrl = true; }
                Connected = true;
            }
            catch (SocketException) { }
            catch (Exception e) { Debug.LogWarning($"[Bridge] RX: {e.Message}"); }
        }
    }

    // ------------------------------------------------------------------ //
    //  JSON state reply to ArduPilot                                      //
    // ------------------------------------------------------------------ //
    private void SendState()
    {
        if (_remote == null || _remote.Port == 0) return;

        var s = _fdm.CurrentState;
        var (vN, vE, vD) = FixedWingFDM.BodyToNED(s.u, s.v, s.w, s.phi, s.theta, s.psi);

        // Body-frame specific force (gravity component only – EKF needs this)
        float g  = 9.81f;
        float ax =  s.r*s.v - s.q*s.w - g * Mathf.Sin(s.theta);
        float ay =  s.p*s.w - s.r*s.u + g * Mathf.Cos(s.theta)*Mathf.Sin(s.phi);
        float az =  s.q*s.u - s.p*s.v + g * Mathf.Cos(s.theta)*Mathf.Cos(s.phi);

        // Build JSON manually (avoids Unity JSON serialiser overhead in tight loop)
        string json =
            $"{{\"timestamp\":{_simTime:F6}," +
            $"\"imu\":{{\"gyro\":[{s.p:F6},{s.q:F6},{s.r:F6}]," +
            $"\"accel_body\":[{ax:F6},{ay:F6},{az:F6}]}}," +
            $"\"position\":[{s.pn:F4},{s.pe:F4},{s.pd:F4}]," +
            $"\"velocity\":[{vN:F4},{vE:F4},{vD:F4}]," +
            $"\"attitude\":{{\"roll\":{s.phi:F6},\"pitch\":{s.theta:F6},\"yaw\":{s.psi:F6}}}}}";

        try
        {
            byte[] bytes = Encoding.ASCII.GetBytes(json);
            _udp.Send(bytes, bytes.Length, _remote);
        }
        catch (Exception e) { Debug.LogWarning($"[Bridge] TX: {e.Message}"); }
    }

    // ------------------------------------------------------------------ //
    //  Helpers                                                             //
    // ------------------------------------------------------------------ //
    private static float PwmNorm(ushort pwm) => Mathf.Clamp((pwm - 1500f) / 500f, -1f, 1f);
    private static float PwmUnit(ushort pwm) => Mathf.Clamp((pwm - 1000f) / 1000f,  0f, 1f);
}
