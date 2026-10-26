#!/usr/bin/env python3
"""
Webcam head tracker for the Peek Demo.

Finds your face with MediaPipe Face Landmarker, estimates where your eyes are relative to the
camera, and streams that position to Unity as OpenTrack-style UDP packets
(6 little-endian doubles: x, y, z in cm, then yaw, pitch, roll in degrees).

Axes are sent the way OpenTrackUdpSource expects them, so no invert flags are needed:
  +x = you moved right, +y = you moved up, +z = you moved toward the screen.
Only relative motion matters; press C in Unity to recenter.

Usage:
  python head_tracker.py --preview
"""

import argparse
import math
import socket
import struct
import sys
import time
import urllib.request
from pathlib import Path

import cv2
import mediapipe as mp
from mediapipe.tasks import python as mp_tasks
from mediapipe.tasks.python import vision

MODEL_URL = ("https://storage.googleapis.com/mediapipe-models/face_landmarker/"
             "face_landmarker/float16/1/face_landmarker.task")
MODEL_PATH = Path(__file__).with_name("face_landmarker.task")

# Outer eye corners in MediaPipe's 478-point face mesh. They don't move with gaze,
# unlike the iris points, so their spacing is a stable distance cue.
RIGHT_EYE_OUTER = 33
LEFT_EYE_OUTER = 263


class OneEuroFilter:
    """Low jitter at rest, low lag when moving (Casiez et al. 2012)."""

    def __init__(self, min_cutoff, beta, d_cutoff=1.0):
        self.min_cutoff = min_cutoff
        self.beta = beta
        self.d_cutoff = d_cutoff
        self.x_prev = None
        self.dx_prev = 0.0
        self.t_prev = None

    @staticmethod
    def _alpha(dt, cutoff):
        tau = 1.0 / (2.0 * math.pi * cutoff)
        return 1.0 / (1.0 + tau / dt)

    def __call__(self, x, t):
        if self.t_prev is None:
            self.x_prev, self.t_prev = x, t
            return x
        dt = t - self.t_prev
        if dt <= 0:
            return self.x_prev
        a_d = self._alpha(dt, self.d_cutoff)
        dx = a_d * ((x - self.x_prev) / dt) + (1 - a_d) * self.dx_prev
        a = self._alpha(dt, self.min_cutoff + self.beta * abs(dx))
        x_hat = a * x + (1 - a) * self.x_prev
        self.x_prev, self.dx_prev, self.t_prev = x_hat, dx, t
        return x_hat

    def reset(self):
        self.x_prev = None
        self.t_prev = None
        self.dx_prev = 0.0


def ensure_model():
    if MODEL_PATH.exists():
        return
    print(f"Downloading face model to {MODEL_PATH.name} ...")
    urllib.request.urlretrieve(MODEL_URL, MODEL_PATH)


def parse_args():
    p = argparse.ArgumentParser(description="Webcam head tracker -> OpenTrack UDP for the Peek Demo.")
    p.add_argument("--camera", type=int, default=0, help="Camera index (default 0).")
    p.add_argument("--host", default="127.0.0.1", help="Where Unity is running (default 127.0.0.1).")
    p.add_argument("--port", type=int, default=4242, help="UDP port (default 4242).")
    p.add_argument("--hfov", type=float, default=65.0,
                   help="Camera horizontal field of view in degrees. MacBook cameras are roughly 60-70.")
    p.add_argument("--eye-width-cm", type=float, default=9.0,
                   help="Distance between your outer eye corners. ~9 cm for most adults.")
    p.add_argument("--min-cutoff", type=float, default=0.5,
                   help="Filter cutoff in Hz: lower = steadier at rest, more lag. 0 disables filtering.")
    p.add_argument("--beta", type=float, default=0.1,
                   help="Filter speed response (per cm/s): higher = less lag on fast moves, more jitter.")
    p.add_argument("--preview", action="store_true", help="Show the camera image with tracking overlay.")
    return p.parse_args()


def main():
    args = parse_args()
    ensure_model()

    cap = cv2.VideoCapture(args.camera)
    if not cap.isOpened():
        sys.exit("Could not open the camera. On macOS, allow camera access for your terminal app in "
                 "System Settings > Privacy & Security > Camera, then restart the terminal.")
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)

    options = vision.FaceLandmarkerOptions(
        base_options=mp_tasks.BaseOptions(model_asset_path=str(MODEL_PATH)),
        running_mode=vision.RunningMode.VIDEO,
        num_faces=1,
    )
    landmarker = vision.FaceLandmarker.create_from_options(options)
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    target = (args.host, args.port)

    # Depth from apparent eye spacing is noisier than x/y, so it gets a steadier filter.
    use_filter = args.min_cutoff > 0
    filters = [OneEuroFilter(args.min_cutoff, args.beta),
               OneEuroFilter(args.min_cutoff, args.beta),
               OneEuroFilter(args.min_cutoff * 0.5, args.beta)]

    print(f"Sending head position to {target[0]}:{target[1]}. "
          f"{'Press q in the preview window' if args.preview else 'Press Ctrl+C'} to quit.")

    start = time.monotonic()
    last_ts = -1
    fps, fps_frames, fps_start = 0.0, 0, start
    try:
        while True:
            ok, frame = cap.read()
            if not ok:
                print("Camera stopped delivering frames.")
                break

            now = time.monotonic()
            ts = int((now - start) * 1000)
            if ts <= last_ts:  # MediaPipe needs strictly increasing timestamps
                ts = last_ts + 1
            last_ts = ts

            h, w = frame.shape[:2]
            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            result = landmarker.detect_for_video(mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb), ts)

            pos_cm = None
            if result.face_landmarks:
                lm = result.face_landmarks[0]
                r, l = lm[RIGHT_EYE_OUTER], lm[LEFT_EYE_OUTER]
                ru, rv, lu, lv = r.x * w, r.y * h, l.x * w, l.y * h
                eye_px = math.hypot(lu - ru, lv - rv)

                if eye_px > 1:
                    # Pinhole camera: focal length in pixels from the horizontal FOV.
                    f = (w / 2) / math.tan(math.radians(args.hfov) / 2)
                    dist = f * args.eye_width_cm / eye_px                 # cm from camera
                    u, v = (ru + lu) / 2, (rv + lv) / 2                   # point between the eyes
                    # The camera faces you, so your right is its left: flip x.
                    x = -(u - w / 2) * dist / f
                    y = -(v - h / 2) * dist / f
                    z = -dist                                              # closer to screen = larger z
                    pos_cm = [x, y, z]
                    if use_filter:
                        pos_cm = [flt(val, now) for flt, val in zip(filters, pos_cm)]
                    sock.sendto(struct.pack("<6d", *pos_cm, 0.0, 0.0, 0.0), target)

                    if args.preview:
                        for px, py in ((ru, rv), (lu, lv)):
                            cv2.circle(frame, (int(px), int(py)), 4, (0, 255, 0), -1)
                        cv2.circle(frame, (int(u), int(v)), 6, (0, 200, 255), 2)
            elif use_filter:
                # Face lost: start the filters fresh when it comes back instead of sliding from the old spot.
                for flt in filters:
                    flt.reset()

            fps_frames += 1
            if now - fps_start >= 1.0:
                fps, fps_frames, fps_start = fps_frames / (now - fps_start), 0, now

            if args.preview:
                preview = cv2.flip(frame, 1)  # mirror so it moves like a mirror would
                status = ("x %+6.1f  y %+6.1f  z %+6.1f cm" % tuple(pos_cm)) if pos_cm else "no face"
                cv2.putText(preview, f"{status}   {fps:4.1f} fps", (10, 25),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 0) if pos_cm else (0, 0, 255), 2)
                cv2.imshow("Peek head tracker", preview)
                if cv2.waitKey(1) & 0xFF in (ord("q"), 27):
                    break
    except KeyboardInterrupt:
        pass
    finally:
        cap.release()
        landmarker.close()
        sock.close()
        cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
