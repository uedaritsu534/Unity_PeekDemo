# Peek Demo

A first-person sandbox where the monitor behaves like a window. WASD moves, the mouse aims, and on top of
that your real head position (from a webcam) shifts the view, so you can lean around cover and look over walls.

## Setup (Unity 6)

1. Create a new 3D project (URP or Built-in both work).
2. Make sure the **Input System** package is installed and active. It is by default in Unity 6. If you see
   errors about `UnityEngine.InputSystem`, install it from Package Manager and set
   *Project Settings → Player → Active Input Handling* to **Input System Package** or **Both**.
3. Copy the `PeekDemo` folder into `Assets/`.
4. Open an empty scene (or the sample scene), create an empty GameObject, add **PeekDemoBootstrap**, and press Play.
   It builds the player and the level at runtime and disables any existing cameras.

**Building a player with the Built-in render pipeline:** the level is made from runtime primitives, which
Unity strips the `Standard` shader from, so everything renders magenta. Add `Standard` to
*Project Settings → Graphics → Always Included Shaders*. URP projects and play mode in the editor aren't affected.

With no tracker running, the **keyboard simulator** stands in for your head:
arrow keys = lean left/right/up/down, Page Up/Down = lean toward/away from the screen.

## Head tracking on macOS (or anywhere without OpenTrack)

Run the Python tracker in `HeadTracker/` at the repo root. It sends the same UDP packets as OpenTrack.
See `HeadTracker/README.md`.

## Head tracking with a webcam (OpenTrack, Windows)

1. Install [OpenTrack](https://github.com/opentrack/opentrack/releases).
2. **Input:** `neuralnet tracker` (webcam face tracking, no markers needed). AITrack → OpenTrack also works.
3. **Output:** `UDP over network`, remote IP `127.0.0.1`, port `4242`.
4. Click **Start** in OpenTrack, then press Play in Unity. The HUD should switch to `OpenTrack UDP :4242`.
5. Sit normally and press **C** to recenter.
6. Lean right, up, and toward the screen. The HUD offsets should go **+x, +y, +z**. If one is backwards,
   tick `invertX/Y/Z` on the Player's **OpenTrackUdpSource** component (or invert the axis in OpenTrack).
7. In OpenTrack's filter settings, keep smoothing light. Unity already smooths, and latency hurts the effect more than jitter.

Only position is used. Head rotation is ignored, because turning your head doesn't change what a window shows.

## Controls

| Key | Action |
| --- | --- |
| WASD / mouse | Move / look (click to lock the cursor, Esc to release) |
| Shift / Space | Sprint / jump |
| C | Recenter head |
| H | Toggle head tracking (good for before/after comparisons) |
| Arrows, PgUp/PgDn | Simulated head when no tracker is connected |

## The stations

Stand on a colored pad, face the same way you spawned, and find the 4-digit code without stepping off the pad.

- **Red, doorway.** Lean left to read the code on the room's right wall. Lean right to spot who's hiding on the left.
- **Yellow, low wall.** Raise your head to see over it.
- **Green, slit.** Only a sliver shows through the gap. Sweep your head sideways to read the whole code.
- **Blue, pole.** Lean past the pole.

The sight lines were tuned for the default camera settings and 10–20 cm of real head movement. From far across
the arena you can glimpse some codes; the challenge is on the pads.

## Tuning (on the camera's **OffAxisWindowCamera** component)

- `screenHeightMeters`, `viewingDistanceMeters`: measure your monitor and your sitting distance.
- `neutralVerticalFov`: 0 is physically exact, but usually only about 28°, which feels zoomed in for an FPS.
  60 is a good compromise. The window illusion still holds because the frustum is always rebuilt from the eye position.
- `parallaxGain`: 1 changes the view angle exactly as a real window the size of your monitor would. That's the
  most convincing illusion, but seated leans barely reveal anything. 2.5 (the default) makes 10–15 cm leans useful,
  and higher values feel more "gamey". Head motion is also scaled by the FOV setting, so the two stay consistent.
- `smoothing`: higher is snappier, lower is smoother.

## How it works

- `FirstPersonController` handles yaw on the Player and pitch on `EyePivot`. It's a normal FPS controller.
- `EyePivot` is the neutral eye. `OffAxisWindowCamera` treats a rectangle a fixed distance in front of it as the
  monitor. Each frame it moves the camera to the tracked eye position (head offset × gain) and builds an asymmetric
  frustum (`Matrix4x4.Frustum`) through that rectangle. This is the generalized perspective projection from
  Kooima's paper, simplified because the screen never rotates relative to the eye pivot.
- A sphere cast keeps the eye from leaning through walls.
- `HeadPoseSource` is the input abstraction: `OpenTrackUdpSource` reads OpenTrack's 48-byte packets
  (6 doubles: x, y, z in cm, then yaw, pitch, roll), and `KeyboardHeadSimulator` is the fallback.
  To add another tracker (MediaPipe, a depth camera, and so on), subclass `HeadPoseSource`.
