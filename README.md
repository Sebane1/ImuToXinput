An experimental project that takes IMU data from SlimeVR and attempts to translate it into usable XInput controller data. The project simulates an Xbox controller, or a dance pad for games such as Stepmania with some basic autodetection of which style of game is running.

Proof of concept video:
https://www.youtube.com/watch?v=ynCTgu7mtcQ

Presently the project has been experimentally tested with Halo Combat Evolved, Mirrors Edge, and Stepmania. Over time this project aims to improve gesture controls and inputs for interfacing with games in a way that doesn't require hands or fingers,

Current configurations have a head tracker for looking around, hip tracker for forwards/backwards movement and strafing, elbow trackers for melee, shooting, and throwing in game explosives, weapon switching and pickups mapped to feet, etc.

Stepmania controls give you a virtual invisible dance pad laid out like the following, relying on hip, knee, ankle, and foot trackers to inform body placement (You will have to likely bind the actions in Stepmania):

<img width="185" height="149" alt="image" src="https://github.com/user-attachments/assets/3b67304e-48f4-4558-a213-60100c6cd37d" />


### Configuration (one .json per game)

Game-specific IMU-to-XInput mappings live in a **configs** folder next to the executable. **One JSON file per game** so configs are easy to download and share.

- **configs/default.json** — Used when no game process is detected (e.g. generic FPS). Required if you use the configs folder.
- **configs/&lt;game&gt;.json** — Any other file, e.g. `portal.json`, `MirrorsEdge.json`, `ffxiv_dx11.json`. The app matches the running process name (no `.exe`) to **processNames** inside each file.

Each JSON file is a single profile with:

- **name**: Display identifier.
- **processNames**: Executable names that select this config when running (e.g. `["portal", "portal2"]`).
- **axisMappings**: Tracker rotation/position → thumbsticks (e.g. HEAD → RightThumb for look).
- **buttonMappings** / **triggerMappings**: Conditions (euler threshold, euler diff/sum, position threshold) that drive buttons and triggers.
- **trackers**: Optional (e.g. `["LEFT_FOOT","RIGHT_FOOT"]`) — trackers used to update floor height for floor-relative position.

Condition types: `euler_threshold`, `euler_diff`, `euler_sum`, `position_threshold`. To add a game: add a new `configs/SomeGame.json` and set **processNames** to the game's process name(s). Share that single file for others to drop into their **configs** folder.

**Config Editor:** The solution includes **ImuToXInput.ConfigEditor**, a Windows Forms app to create and edit game configs without editing JSON by hand. Run it from Visual Studio (set as startup project) or run `ImuToXInput.ConfigEditor.exe` from the editor’s build output. Use **Browse** to point to the `configs` folder next to your ImuToXInput executable, then **New** / **Edit** / **Delete** to manage profiles and their axis, button, and trigger mappings. The **Script** tab shows an optional C#-style text representation of the same rules (e.g. `axis HEAD.EulerX * 2 invert -> RightThumbY`, `button when LEFT_FOOT.Euler.X < -20 -> A`); edit there and click **Apply script** to update the form, or **Refresh from form** to generate script from the form. Script and JSON stay in sync through the editor.

**StepMania / DDR pad mode** is unchanged and not driven by config; it stays a dedicated hardcoded mode when the `stepmania` process is detected.

*Future goal: a version for ESP32 that reads from the SlimeVR server over WiFi and could work with real Xbox or other consoles; the same per-game JSON format can be reused there.*

How to use:

Install the VIGEM bus driver:
https://vigembusdriver.com/

Install SlimeVR:
https://slimevr.dev/

Connect and calibrate trackers in the SlimeVR software, and follow calibration steps.

Run ImuToXInput after calibrating in SlimeVR, remain in neutral position until fully loaded (Re-calibrating SlimeVR requires re-starting ImuToXInput afterwards):
https://github.com/Sebane1/ImuToXinput/releases
