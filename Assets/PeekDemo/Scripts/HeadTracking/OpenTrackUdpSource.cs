using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PeekDemo
{
    /// <summary>
    /// Receives head pose from OpenTrack's "UDP over network" output.
    /// Packet = 6 little-endian doubles: x, y, z (cm), yaw, pitch, roll (degrees). Only position is used.
    /// </summary>
    public class OpenTrackUdpSource : HeadPoseSource
    {
        public int port = 4242;
        [Tooltip("Seconds without a packet before tracking is considered lost.")]
        public float timeoutSeconds = 0.5f;
        [Tooltip("OpenTrack's axis signs depend on the tracker. Lean right/up/toward the screen and check the HUD shows +x/+y/+z.")]
        public bool invertX, invertY, invertZ;

        readonly object gate = new object();
        UdpClient client;
        Thread thread;
        volatile bool running;
        Vector3 latestCm;
        long lastPacketTicks = long.MinValue;

        public override string DisplayName => $"OpenTrack UDP :{port}";

        public override bool IsTracking
        {
            get
            {
                long last;
                lock (gate) last = lastPacketTicks;
                if (last == long.MinValue) return false;
                return (Stopwatch.GetTimestamp() - last) / (double)Stopwatch.Frequency < timeoutSeconds;
            }
        }

        public override Vector3 RawPosition
        {
            get
            {
                Vector3 p;
                lock (gate) p = latestCm;
                p *= 0.01f;
                if (invertX) p.x = -p.x;
                if (invertY) p.y = -p.y;
                if (invertZ) p.z = -p.z;
                return p;
            }
        }

        void OnEnable()
        {
            try
            {
                client = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[PeekDemo] Could not listen on UDP {port}: {e.Message}");
                return;
            }

            running = true;
            thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "OpenTrackUdp" };
            thread.Start();
        }

        void OnDisable()
        {
            running = false;
            client?.Close();
            thread?.Join(200);
            client = null;
            thread = null;
        }

        void ReceiveLoop()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                byte[] data;
                try
                {
                    data = client.Receive(ref remote);
                }
                catch (SocketException)
                {
                    if (!running) break;
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (data.Length < 24) continue;
                var cm = new Vector3(
                    (float)BitConverter.ToDouble(data, 0),
                    (float)BitConverter.ToDouble(data, 8),
                    (float)BitConverter.ToDouble(data, 16));

                lock (gate)
                {
                    latestCm = cm;
                    lastPacketTicks = Stopwatch.GetTimestamp();
                }
            }
        }
    }
}
