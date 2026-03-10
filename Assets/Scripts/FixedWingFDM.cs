// ============================================================
//  FixedWingFDM.cs
//  Nonlinear 6-DoF Fixed-Wing Flight Dynamics Model
//  Based on Beard & McLain, "Small Unmanned Aircraft", 2012
//  Equations 5.4 – 5.12  |  Gamma constants Eq. 3.13
// ============================================================
using UnityEngine;

public class FixedWingFDM
{
    // ------------------------------------------------------------------ //
    //  Aircraft Parameters  (Aerosonde-like defaults)                     //
    // ------------------------------------------------------------------ //
    [System.Serializable]
    public class AircraftParams
    {
        [Header("Mass & Inertia")]
        public float mass = 11.0f;      // kg
        public float Jx   = 0.8244f;   // kg·m²
        public float Jy   = 1.1350f;
        public float Jz   = 1.7590f;
        public float Jxz  = 0.1204f;

        [Header("Geometry")]
        public float S = 0.55f;         // m²  wing area
        public float b = 2.8956f;       // m   wingspan
        public float c = 0.18994f;      // m   mean chord

        [Header("Propulsion")]
        public float Sprop  = 0.2027f;  // m²  prop disc area
        public float Cprop  = 1.0f;
        public float kmotor = 80.0f;    // motor constant

        [Header("Longitudinal Aero")]
        public float CL0    =  0.28f;
        public float CL_a   =  3.45f;
        public float CL_q   =  0.00f;
        public float CL_de  = -0.36f;

        public float CD_p   =  0.043f;  // parasitic drag
        public float CD_q   =  0.00f;
        public float CD_de  =  0.00f;
        public float e_oswald = 0.9f;   // Oswald efficiency

        public float CM0    = -0.02338f;
        public float CM_a   = -0.38f;
        public float CM_q   = -3.60f;
        public float CM_de  = -0.50f;

        public float M_blend =  50.0f;  // stall sharpness
        public float alpha0  =  0.4712f;// stall angle (rad) ~27°

        [Header("Lateral Aero")]
        public float CY_b  = -0.98f;  public float CY_p = 0f; public float CY_r = 0f;
        public float CY_da =  0.00f;  public float CY_dr = -0.17f;

        public float Cl_b  = -0.12f;  public float Cl_p = -0.26f; public float Cl_r = 0.14f;
        public float Cl_da =  0.08f;  public float Cl_dr =  0.105f;

        public float Cn_b  =  0.25f;  public float Cn_p =  0.022f; public float Cn_r = -0.35f;
        public float Cn_da =  0.06f;  public float Cn_dr = -0.032f;
    }

    // ------------------------------------------------------------------ //
    //  State  [u v w  p q r  phi theta psi  pn pe pd]                     //
    // ------------------------------------------------------------------ //
    public struct State
    {
        public float u, v, w;           // body-frame velocity (m/s)
        public float p, q, r;           // body-frame angular rates (rad/s)
        public float phi, theta, psi;   // Euler angles (rad)
        public float pn, pe, pd;        // NED position (m)
        public float time;
    }

    public struct Controls
    {
        public float de;  // elevator  [-1, +1]
        public float da;  // aileron   [-1, +1]
        public float dr;  // rudder    [-1, +1]
        public float dt;  // throttle  [ 0,  1]
    }

    // ------------------------------------------------------------------ //
    //  Private fields                                                      //
    // ------------------------------------------------------------------ //
    private readonly AircraftParams _p;
    private State _state;

    // Gamma constants (computed once)
    private readonly float G1, G2, G3, G4, G5, G6, G7, G8;

    // ------------------------------------------------------------------ //
    //  Public API                                                          //
    // ------------------------------------------------------------------ //
    public State  CurrentState => _state;
    public float  Airspeed => Mathf.Sqrt(_state.u*_state.u + _state.v*_state.v + _state.w*_state.w);
    public float  Altitude => -_state.pd;

    public FixedWingFDM(AircraftParams p, State init)
    {
        _p     = p;
        _state = init;

        float Jx = p.Jx, Jy = p.Jy, Jz = p.Jz, Jxz = p.Jxz;
        float Gamma = Jx * Jz - Jxz * Jxz;

        G1 = Jxz * (Jx - Jy + Jz) / Gamma;
        G2 = (Jz * (Jz - Jy) + Jxz * Jxz) / Gamma;
        G3 = Jz / Gamma;
        G4 = Jxz / Gamma;
        G5 = (Jz - Jx) / Jy;
        G6 = Jxz / Jy;
        G7 = ((Jx - Jy) * Jx + Jxz * Jxz) / Gamma;
        G8 = Jx / Gamma;
    }

    // ------------------------------------------------------------------ //
    //  RK4 Integration Step                                               //
    // ------------------------------------------------------------------ //
    public void Step(Controls ctrl, float dt, float rho = 1.225f)
    {
        float[] x0 = Pack(_state);
        float[] k1 = Deriv(x0, ctrl, rho);
        float[] k2 = Deriv(AddScaled(x0, k1, dt * 0.5f), ctrl, rho);
        float[] k3 = Deriv(AddScaled(x0, k2, dt * 0.5f), ctrl, rho);
        float[] k4 = Deriv(AddScaled(x0, k3, dt),        ctrl, rho);

        float[] xn = new float[12];
        for (int i = 0; i < 12; i++)
            xn[i] = x0[i] + (dt / 6f) * (k1[i] + 2f*k2[i] + 2f*k3[i] + k4[i]);

        _state      = Unpack(xn);
        _state.time = _state.time; // kept in Unpack

        // Ground constraint – simple hard floor at pd = 0
        if (_state.pd > 0f)
        {
            _state.pd = 0f;
            if (_state.w > 0f) _state.w = 0f;   // no sinking below ground
            // Kill rotation when on ground
            _state.p *= 0.95f;
            _state.q *= 0.95f;
            _state.r *= 0.95f;
        }

        // Wrap psi to [-π, π]
        while (_state.psi >  Mathf.PI) _state.psi -= 2f * Mathf.PI;
        while (_state.psi < -Mathf.PI) _state.psi += 2f * Mathf.PI;

        _state.time += dt;
    }

    // ------------------------------------------------------------------ //
    //  Equations of Motion                                                //
    // ------------------------------------------------------------------ //
    private float[] Deriv(float[] x, Controls c, float rho)
    {
        float u=x[0], v=x[1], w=x[2];
        float p=x[3], q=x[4], r=x[5];
        float phi=x[6], th=x[7], psi=x[8];

        float Va = Mathf.Sqrt(u*u + v*v + w*w);
        Va = Mathf.Max(Va, 1f); // prevent div-zero at standstill

        float alpha = Mathf.Atan2(w, u);
        float beta  = Mathf.Asin(Mathf.Clamp(v / Va, -1f, 1f));

        // ---- Blended lift / drag (flat-plate stall model) ----------------
        float sig   = Sigma(alpha);
        float sgn   = alpha >= 0f ? 1f : -1f;
        float CL_al = (1f - sig) * (_p.CL0 + _p.CL_a * alpha)
                     + sig * 2f * sgn * Mathf.Sin(alpha)*Mathf.Sin(alpha)*Mathf.Cos(alpha);

        float AR     = _p.b * _p.b / _p.S;
        float CD_al  = _p.CD_p + CL_al * CL_al / (Mathf.PI * _p.e_oswald * AR);

        // Stability → body axes
        float ca = Mathf.Cos(alpha), sa = Mathf.Sin(alpha);
        float CX    = -CD_al * ca + CL_al * sa;
        float CZ    = -CD_al * sa - CL_al * ca;
        float CXde  = -_p.CD_de * ca + _p.CL_de * sa;
        float CZde  = -_p.CD_de * sa - _p.CL_de * ca;
        float CXq   = -_p.CD_q  * ca + _p.CL_q  * sa;
        float CZq   = -_p.CD_q  * sa - _p.CL_q  * ca;

        float qbarS_2m = 0.5f * rho * Va * Va * _p.S / _p.mass;
        float c2Va     = _p.c * q / (2f * Va);
        float b2Va_p   = _p.b * p / (2f * Va);
        float b2Va_r   = _p.b * r / (2f * Va);

        // ---- Propulsion --------------------------------------------------
        float vProp2   = _p.kmotor * c.dt;
        float FpropX   = 0.5f * rho * _p.Sprop * _p.Cprop * (vProp2*vProp2 - Va*Va) / _p.mass;

        // ---- Translational EoM (Eq 5.4–5.6) ------------------------------
        float g = 9.81f;
        float u_dot = r*v - q*w - g*Mathf.Sin(th)
                    + qbarS_2m*(CX + CXq*c2Va + CXde*c.de) + FpropX;

        float v_dot = p*w - r*u + g*Mathf.Cos(th)*Mathf.Sin(phi)
                    + qbarS_2m*(_p.CY_b*beta + _p.CY_p*b2Va_p + _p.CY_r*b2Va_r
                                + _p.CY_da*c.da + _p.CY_dr*c.dr);

        float w_dot = q*u - p*v + g*Mathf.Cos(th)*Mathf.Cos(phi)
                    + qbarS_2m*(CZ + CZq*c2Va + CZde*c.de);

        // ---- Euler kinematics (Eq 5.7–5.9) --------------------------------
        float tth = Mathf.Tan(th), cph = Mathf.Cos(phi), sph = Mathf.Sin(phi);
        float phi_dot   = p + (q*sph + r*cph) * tth;
        float theta_dot = q*cph - r*sph;
        float psi_dot   = (q*sph + r*cph) / Mathf.Cos(th);

        // ---- Angular EoM (Eq 5.10–5.12) -----------------------------------
        float bv2S = 0.5f*rho*Va*Va*_p.S*_p.b;
        float cv2S = 0.5f*rho*Va*Va*_p.S*_p.c;

        float l_a = bv2S*(_p.Cl_b*beta + _p.Cl_p*b2Va_p + _p.Cl_r*b2Va_r + _p.Cl_da*c.da + _p.Cl_dr*c.dr);
        float m_a = cv2S*(_p.CM0 + _p.CM_a*alpha + _p.CM_q*(_p.c*q/(2f*Va)) + _p.CM_de*c.de);
        float n_a = bv2S*(_p.Cn_b*beta + _p.Cn_p*b2Va_p + _p.Cn_r*b2Va_r + _p.Cn_da*c.da + _p.Cn_dr*c.dr);

        float p_dot = G1*p*q - G2*q*r + G3*l_a + G4*n_a;
        float q_dot = G5*p*r - G6*(p*p - r*r) + m_a / _p.Jy;
        float r_dot = G7*p*q - G1*q*r + G4*l_a + G8*n_a;

        // ---- NED position kinematics (body → NED via DCM) -----------------
        float cth  = Mathf.Cos(th), sth = Mathf.Sin(th);
        float cpsi = Mathf.Cos(psi), spsi = Mathf.Sin(psi);

        float pn_dot = (cth*cpsi)*u + (sph*sth*cpsi - cph*spsi)*v + (cph*sth*cpsi + sph*spsi)*w;
        float pe_dot = (cth*spsi)*u + (sph*sth*spsi + cph*cpsi)*v + (cph*sth*spsi - sph*cpsi)*w;
        float pd_dot = (-sth)*u + (sph*cth)*v + (cph*cth)*w;

        return new float[] { u_dot, v_dot, w_dot, p_dot, q_dot, r_dot,
                             phi_dot, theta_dot, psi_dot, pn_dot, pe_dot, pd_dot };
    }

    // ---- Sigmoid blending for stall model --------------------------------
    private float Sigma(float alpha)
    {
        float M  = _p.M_blend, a0 = _p.alpha0;
        float ep = Mathf.Exp(-M*(alpha - a0));
        float en = Mathf.Exp( M*(alpha + a0));
        return (1f + ep + en) / ((1f + ep) * (1f + en));
    }

    // ---- Packing helpers -------------------------------------------------
    private static float[] Pack(State s)
        => new[] { s.u, s.v, s.w, s.p, s.q, s.r, s.phi, s.theta, s.psi, s.pn, s.pe, s.pd };

    private static State Unpack(float[] x) => new State
    {
        u=x[0], v=x[1], w=x[2], p=x[3], q=x[4], r=x[5],
        phi=x[6], theta=x[7], psi=x[8], pn=x[9], pe=x[10], pd=x[11]
    };

    private static float[] AddScaled(float[] a, float[] b, float s)
    {
        var r = new float[a.Length];
        for (int i = 0; i < a.Length; i++) r[i] = a[i] + b[i]*s;
        return r;
    }

    // ------------------------------------------------------------------ //
    //  Body-to-NED rotation (public utility used by bridge)               //
    // ------------------------------------------------------------------ //
    public static (float vN, float vE, float vD) BodyToNED(
        float u, float v, float w, float phi, float theta, float psi)
    {
        float cp=Mathf.Cos(phi), sp=Mathf.Sin(phi);
        float ct=Mathf.Cos(theta), st=Mathf.Sin(theta);
        float cy=Mathf.Cos(psi), sy=Mathf.Sin(psi);
        float vN= (ct*cy)*u + (sp*st*cy - cp*sy)*v + (cp*st*cy + sp*sy)*w;
        float vE= (ct*sy)*u + (sp*st*sy + cp*cy)*v + (cp*st*sy - sp*cy)*w;
        float vD=    (-st)*u +          (sp*ct)*v +          (cp*ct)*w;
        return (vN, vE, vD);
    }
}
