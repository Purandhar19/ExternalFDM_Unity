
# Fixed-Wing FDM — Unity + Mission Planner SITL
### Step-by-step guide. No Linux. No terminal. Everything on Windows.

---

## What You Need (download these first)

| Software | Where to get it | Notes |
|---|---|---|
| **Mission Planner** | https://ardupilot.org/planner/docs/mission-planner-installation.html | Free. Windows only. |
| **Unity Hub** | https://unity.com/download | Free. |
| **Unity 2022.3 LTS** | Install from Unity Hub after installing it | Free. Choose "3D (Built-In Render Pipeline)" template. |

---

## Part 1 — Unity Project Setup 

### Step 1 — Create the project

1. Open **Unity Hub**
2. Click **New Project** (top right)
3. Select template: **3D (Core)** — make sure it says "Built-In Render Pipeline" underneath
4. Name it anything, e.g. `FDM_Sim`
5. Click **Create Project** — Unity opens

### Step 2 — Add the scripts

1. In the **Project panel** at the bottom of Unity, find the `Assets` folder
2. Right-click `Assets` → **Create** → **Folder** → name it `Scripts`
3. Open File Explorer and navigate to that `Scripts` folder
   - You can right-click the `Scripts` folder in Unity → **Show in Explorer**
4. Copy **all 7 `.cs` files** from this zip into that folder:
   ```
   FixedWingFDM.cs
   ArduPilotBridge.cs
   AircraftVisuals.cs
   FlightHUD.cs
   UnityMainThreadDispatcher.cs
   KeyboardHandler.cs
   SceneBootstrap.cs
   ```
5. Click back into Unity — it will **compile automatically** (spinner bottom-right)
6. Wait until the spinner stops. Check the **Console** panel at the bottom has **no red errors**

> ⚠️ If you see red errors, make sure all 7 files are in `Assets/Scripts/` and no files are missing.

### Step 3 — Create the Bootstrap object

1. In the **Hierarchy** panel (left side), right-click in empty space → **Create Empty**
2. Rename it `Bootstrap` (click the name twice slowly, or press F2)
3. With `Bootstrap` selected, look at the **Inspector** panel (right side)
4. Click **Add Component** at the bottom of Inspector
5. Type `SceneBootstrap` in the search box → click it when it appears

You will now see these settings appear in the Inspector under SceneBootstrap:

```
Sitl Port:          9002    ← leave this
Start Altitude:     100     ← plane starts 100m in the air (safe!)
Start Airspeed:     25      ← 25 m/s cruise speed
Start Heading Deg:  0       ← facing North
```

**Leave all values at their defaults.**

### Step 4 — Physics rate

1. Go to the menu: **Edit** → **Project Settings**
2. Click **Time** in the left list
3. Set **Fixed Timestep** to `0.0025`
4. Set **Maximum Allowed Timestep** to `0.05`
5. Close Project Settings

### Step 5 — Done with Unity!

Do **not** press Play yet. Set up Mission Planner first (Part 2 below).

---

## Part 2 — Mission Planner SITL Setup (one-time, ~3 minutes)

### Step 1 — Open Mission Planner

Launch Mission Planner. You do **not** need to connect to any hardware.

### Step 2 — Go to the Simulation tab

Click the **Simulation** tab in the top menu bar.

> If you don't see it: Menu bar → **Config** → tick **Advanced Mode** → restart Mission Planner

### Step 3 — Configure the simulation

You will see a screen with vehicle icons. Do the following **in order**:

1. **Model dropdown** (center of screen): select **Plane**

2. **Extra command line box** (a text field, usually labeled "Extra command line" or "Sim Options"):
   Type exactly this (copy-paste recommended):
   ```
   --model json:127.0.0.1
   ```

   > This one command is the key — it tells ArduPilot to use Unity as the physics engine instead of its built-in model.

3. Leave everything else at defaults.

4. Click the **ArduPlane** icon or the **Start Simulation** button

Mission Planner will:
- Download the ArduPlane SITL binary (~30MB, first time only)
- Launch it automatically
- Show a console window with ArduPilot boot messages
- The console will print: `Waiting for JSON... ` — **this is normal and expected**

ArduPilot is now waiting for Unity to connect and send physics data.

---

## Part 3 — Running the Simulation (every time)

**Order matters. Always do Mission Planner first, then Unity.**

### Every session:

```
1. Open Mission Planner
2. Simulation tab → Extra command: --model json:127.0.0.1 → click ArduPlane icon
3. Wait until you see "Waiting for JSON..." in the console
4. Switch to Unity → press ▶ Play
5. The HUD "WAITING" banner will disappear → simulation is live
```

You will see:
- The aircraft flying in Unity's viewport
- Mission Planner's map updating with the aircraft's position
- The HUD showing live airspeed, altitude, roll, pitch

---

## Part 4 — Flying the Plane

Mission Planner is your ground station. The plane flies itself in `FBWA` mode (stabilised).

### Change flight mode

In Mission Planner, **Flight Data** tab → bottom left panel → **Actions** tab:
- Click the mode dropdown → choose a mode → click **Set Mode**

| Mode | What it does |
|------|---|
| **FBWA** | Fly-By-Wire A — stabilised, you control roll/pitch attitude with RC |
| **AUTO** | Flies a pre-planned mission |
| **RTL** | Returns to launch point and circles |
| **LOITER** | Circles at current position and altitude |
| **GUIDED** | Fly to a GPS point you click on the map |

### Make the plane do something

Right-click anywhere on the Mission Planner map:
- **Fly to here** → plane flies to that point (GUIDED mode)
- **Takeoff** → sends takeoff command

### Arm and fly a mission

1. Go to **Flight Plan** tab → click on map to add waypoints → click **Write WPs** to upload
2. Go back to **Flight Data** → Actions → set mode to **AUTO** → click **Arm/Disarm**

---

## Keyboard Shortcuts (Unity window must be in focus)

| Key | Action |
|---|---|
| **SPACE** | Reset FDM (plane returns to start altitude, speed, heading) |
| **ESC** | Quit Unity |

---

## Troubleshooting

### "WAITING FOR ARDUPILOT SITL" never disappears in Unity

**Most likely cause:** The `--model json:127.0.0.1` command is missing or wrong.

Checklist:
- [ ] Did you type `--model json:127.0.0.1` in the Extra command box **before** clicking the plane icon?
- [ ] Is ArduPilot SITL actually running? (You should see a black console window with boot text)
- [ ] Does the console say `Waiting for JSON...`? If not, the `--model json` flag wasn't picked up.
- [ ] Is Windows Firewall blocking UDP port 9002?
  - Open Windows Defender Firewall → Advanced Settings → Inbound Rules → New Rule
  - Rule type: Port → UDP → Specific port: 9002 → Allow → name it "ArduPilot FDM"

### ArduPilot console says "Failed to connect to JSON" immediately

This means ArduPilot tried to connect to 127.0.0.1:9002 and Unity wasn't ready.
- Make sure Unity is already in Play mode **before** the timeout (it's about 10 seconds)
- Solution: Start Mission Planner SITL, then **immediately** switch to Unity and press Play

### Unity shows compile errors (red text in Console)

- All 7 `.cs` files must be in the **same folder** (`Assets/Scripts/`)
- None of the files should be in a subfolder
- Try: **Assets** menu → **Reimport All**

### Mission Planner map shows plane in Australia (weird location)

That's the default SITL home position (near Canberra). To change it, add this to the Extra command line (after the `--model` part):
```
--model json:127.0.0.1 --home=28.6139,77.2090,200,0
```
Replace `28.6139,77.2090` with your lat/lon, `200` with altitude in metres, `0` with heading.

### The plane crashes immediately after starting

The plane starts at 100m altitude with 25 m/s airspeed in `FBWA` mode.
In FBWA with no RC input, it should fly level.
If it crashes: press **SPACE** in Unity to reset. Then check Mission Planner is in `FBWA` mode.

### Mission Planner Simulation tab is missing

Go to: **Config** tab → **Planner** → tick the **Layout: Advanced** checkbox → restart Mission Planner.

---

## File Reference

All 7 scripts must be in `Assets/Scripts/`. You never need to edit any of them.

| File | What it does |
|---|---|
| `SceneBootstrap.cs` | Builds the scene (ground, aircraft, camera, HUD) automatically at Play |
| `FixedWingFDM.cs` | The 6-DoF flight physics math (from Beard & McLain 2012) |
| `ArduPilotBridge.cs` | Receives PWM from SITL via UDP → runs FDM → sends JSON state back |
| `AircraftVisuals.cs` | Moves and rotates the 3D aircraft, runs the chase camera |
| `FlightHUD.cs` | On-screen display (airspeed, altitude, attitude indicator) |
| `UnityMainThreadDispatcher.cs` | Internal threading helper (do not touch) |
| `KeyboardHandler.cs` | SPACE=reset, ESC=quit |

---

## How It All Works (for the curious)

```
Mission Planner SITL (ArduPlane firmware on your PC)
        │
        │  Every frame: sends 16 servo PWM values (binary UDP → port 9002)
        │  e.g.  CH1=1520µs (aileron), CH2=1480µs (elevator) ...
        ▼
Unity (ArduPilotBridge.cs running in background thread)
        │
        │  Converts PWM → normalised controls (-1 to +1)
        │  Runs 6-DoF equations of motion (RK4, 400Hz)
        │  Computes new position, velocity, attitude
        ▼
Unity sends JSON back to Mission Planner:
        {
          "timestamp": 1.234,
          "imu": { "gyro": [p,q,r], "accel_body": [ax,ay,az] },
          "position": [North, East, Down],
          "velocity": [vN, vE, vD]
        }
        │
        ▼
Mission Planner SITL receives sensor data, runs its PID controllers,
produces new servo commands → loop continues at 400 Hz
```
errors : 
New-NetFirewallRule -DisplayName "SITL_IN" -Direction Inbound -Protocol UDP -LocalPort 9002 -Action Allow
New-NetFirewallRule -DisplayName "SITL_OUT" -Direction Outbound -Protocol UDP -LocalPort 9002 -Action Allow
```

---

### Outcome B — You see 9002 listed twice:
```
UDP    0.0.0.0:9002    *:*    1234
UDP    0.0.0.0:9002    *:*    8328
---

*Physics based on: Beard, R.W. & McLain, T.W., Small Unmanned Aircraft: Theory and Practice, Princeton University Press, 2012.*
