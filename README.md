An experimental project that takes IMU data from SlimeVR and attempts to translate it into usable XInput controller data.
The project simulates an Xbox controller, or a dance pad for games such as Stepmania with some basic autodetecion of which style of game is running.

Presently the project has been experimentally tested with Halo Combat Evolved, Mirrors Edge, and Stepmania.
Over time this project aims to improve gesture controls and inputs for interfacing with games in a way that doesnt require hands or fingers,

Current configurations have a head tracker for looking around, hip tracker for forwards/backwards movement and strafing, elbow trackers for melee, shooting, and throwing in game explosives, weapon switching and pickups mapped to feet, etc.

Stepmania controls give you a virtual invisible dance pad laid out like the following, relying on hip, knee, ankle, and foot trackers to inform body placement (You will have to likely bind the actions in Stepmania):

<img width="185" height="149" alt="image" src="https://github.com/user-attachments/assets/3b67304e-48f4-4558-a213-60100c6cd37d" />


How to use:

Install the VIGEM bus driver:
https://vigembusdriver.com/

Install SlimeVR:
https://slimevr.dev/

Connect and calibrate trackers in the SlimeVR software, and follow calibration steps.

Run ImuToXInput after calibrating in SlimeVR (Re-calibrating SlimeVR requires re-starting ImuToXInput afterwards):
https://github.com/Sebane1/ImuToXinput/releases
