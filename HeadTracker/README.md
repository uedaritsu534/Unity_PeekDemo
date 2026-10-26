# Head Tracker (webcam, macOS / Windows / Linux)

A stand-in for OpenTrack. It tracks your face with the webcam and sends your head position to the
Unity project over UDP (port 4242), in the format `OpenTrackUdpSource` already understands.
Nothing in Unity needs to change.

## Setup (once)

MediaPipe needs Python 3.9–3.12. On a Mac with Homebrew:

```bash
brew install python@3.12
cd HeadTracker
python3.12 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
```

The first run downloads MediaPipe's face model (~4 MB) next to the script.

## Run

```bash
cd HeadTracker
source .venv/bin/activate
python head_tracker.py --preview
```

1. The first time, macOS asks for camera permission for your terminal app (Terminal, iTerm, or VS Code).
   Allow it, then quit and reopen the terminal. If you dismissed the prompt, turn it on in
   *System Settings → Privacy & Security → Camera*.
2. The preview window shows your eye corners and the position it's sending.
3. Press Play in Unity. The HUD should switch from `Keyboard simulator` to `OpenTrack UDP :4242`.
4. Sit normally and press **C** in Unity to recenter.
5. Lean right, up, and toward the screen. The HUD should show **+x, +y, +z**.

Quit with **q** in the preview window, or Ctrl+C without `--preview`.

## Options

| Flag | Default | What it does |
| --- | --- | --- |
| `--preview` | off | Shows the camera with a tracking overlay. Leave it off once things work, which saves a little CPU. |
| `--camera N` | 0 | Picks another camera, such as an external webcam or an iPhone through Continuity Camera. |
| `--hfov DEG` | 65 | Camera field of view. If a 10 cm lean shows as noticeably more or less than 10 cm, adjust this. |
| `--eye-width-cm` | 9.0 | Distance between your outer eye corners. Mainly affects the z (toward/away) scale. |
| `--min-cutoff` | 0.5 | Smoothing. Lower is steadier but laggier; `0` turns it off. |
| `--beta` | 0.1 | Higher reduces lag during fast movements but lets more jitter through. |
| `--host`, `--port` | 127.0.0.1, 4242 | Where to send. Must match `OpenTrackUdpSource`. |

Unity also smooths the motion (`smoothing` on `OffAxisWindowCamera`). If movement feels floaty, raise that
value or lower `--min-cutoff` here, but not both at once.

## How it works

MediaPipe Face Landmarker finds 478 face points per frame. The script uses the two outer eye corners, which
don't move when you look around:

- **Distance from the camera:** a pinhole camera model plus the known real-world eye-corner spacing.
- **Left/right and up/down:** where the midpoint between your eyes sits in the image, scaled by that distance.

It sends those three numbers, in centimeters, 30 times a second. Rotation is sent as zero, because the
window effect only depends on where your eyes are.
