using UnityEngine;

namespace PeekDemo
{
    /// <summary>
    /// Drop on an empty GameObject in an empty scene and press Play. Builds the player rig and a sandbox
    /// with four peek stations. Each station has a colored pad: stand on it facing +Z (the way you spawn
    /// facing) and a 4-digit code is just out of sight until you move your head.
    ///
    ///   Red    - Doorway: lean left to read the code on the room's right wall (lean right to spot the watcher).
    ///   Yellow - Low wall: raise your head to see over it.
    ///   Green  - Slit: lean right and sweep sideways to read the code through a narrow gap.
    ///   Blue   - Pole: lean either way to see past it.
    ///
    /// Sight lines were tuned for the camera defaults (60° FOV, parallaxGain 2.5) and 10-20 cm of real head movement.
    /// </summary>
    public class PeekDemoBootstrap : MonoBehaviour
    {
        public bool disableExistingCameras = true;
        public Vector3 spawnPosition = new Vector3(0f, 0.05f, -11f);

        static readonly Color Floor1 = new Color(0.55f, 0.56f, 0.58f);
        static readonly Color Floor2 = new Color(0.42f, 0.43f, 0.46f);
        static readonly Color WallColor = new Color(0.82f, 0.80f, 0.76f);
        static readonly Color PanelColor = new Color(0.95f, 0.95f, 0.93f);
        static readonly Color CrateColor = new Color(0.62f, 0.44f, 0.26f);
        static readonly Color CodeColor = new Color(0.10f, 0.12f, 0.16f);

        // 7-segment masks, bits a..g = 0..6.
        static readonly int[] DigitSegments = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };

        void Awake()
        {
            if (disableExistingCameras)
                foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    c.gameObject.SetActive(false);

            EnsureSun();
            var level = new GameObject("Sandbox").transform;
            BuildArena(level);
            BuildClutter(level);
            BuildDoorwayStation(Station(level, "Station Doorway", new Vector3(-10.5f, 0f, -4f), new Color(0.85f, 0.25f, 0.25f)));
            BuildLowWallStation(Station(level, "Station Low Wall", new Vector3(-3.5f, 0f, -4f), new Color(0.95f, 0.80f, 0.20f)));
            BuildSlitStation(Station(level, "Station Slit", new Vector3(3.5f, 0f, -4f), new Color(0.30f, 0.75f, 0.35f)));
            BuildPillarStation(Station(level, "Station Pole", new Vector3(10.5f, 0f, -4f), new Color(0.25f, 0.50f, 0.90f)));
            BuildPlayer();
        }

        void BuildPlayer()
        {
            var player = new GameObject("Player");
            player.layer = 2; // Ignore Raycast, so the eye collision check never hits the player's own capsule
            player.transform.position = spawnPosition;

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            var pivot = new GameObject("EyePivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0f, 1.6f, 0f); // the controller rests ~5 cm up, so eyes end up at ~1.65 m

            var camGo = new GameObject("WindowCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivot, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.02f; // the virtual screen is only ~0.26 m away; geometry may sit in front of it
            cam.farClipPlane = 200f;
            camGo.AddComponent<AudioListener>();

            var window = camGo.AddComponent<OffAxisWindowCamera>();
            window.primarySource = player.AddComponent<OpenTrackUdpSource>();
            window.fallbackSource = player.AddComponent<KeyboardHeadSimulator>();

            var fps = player.AddComponent<FirstPersonController>();
            fps.pitchPivot = pivot;
        }

        static void EnsureSun()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.isActiveAndEnabled) return;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // ---------------------------------------------------------------- level

        static void BuildArena(Transform parent)
        {
            var floor = Box("Floor", parent, new Vector3(0f, -0.1f, 0f), new Vector3(30f, 0.2f, 30f), Color.white);
            var mat = floor.GetComponent<Renderer>().material;
            mat.mainTexture = CheckerTexture(Floor1, Floor2);
            mat.mainTextureScale = new Vector2(15f, 15f); // 1 m squares: strong depth cues for parallax

            const float h = 3f;
            Box("Wall N", parent, new Vector3(0f, h / 2, 15.1f), new Vector3(30.4f, h, 0.2f), WallColor);
            Box("Wall S", parent, new Vector3(0f, h / 2, -15.1f), new Vector3(30.4f, h, 0.2f), WallColor);
            Box("Wall E", parent, new Vector3(15.1f, h / 2, 0f), new Vector3(0.2f, h, 30f), WallColor);
            Box("Wall W", parent, new Vector3(-15.1f, h / 2, 0f), new Vector3(0.2f, h, 30f), WallColor);
        }

        static void BuildClutter(Transform parent)
        {
            // Things near spawn at different depths, so head motion is obvious from the first second.
            Box("Crate", parent, new Vector3(-1.6f, 0.4f, -8.5f), new Vector3(0.8f, 0.8f, 0.8f), CrateColor, 15f);
            Box("Crate", parent, new Vector3(-1.6f, 1.1f, -8.5f), new Vector3(0.6f, 0.6f, 0.6f), CrateColor, -10f);
            Box("Crate", parent, new Vector3(1.8f, 0.5f, -7.2f), new Vector3(1f, 1f, 1f), CrateColor, 40f);
            Box("Crate", parent, new Vector3(2.6f, 0.3f, -9.4f), new Vector3(0.6f, 0.6f, 0.6f), CrateColor, 5f);
            for (int i = 0; i < 5; i++)
                Box("Pole", parent, new Vector3(-0.9f + i * 0.45f, 1.25f, -9.2f + i * 0.9f), new Vector3(0.08f, 2.5f, 0.08f),
                    Color.HSVToRGB(i / 5f, 0.5f, 0.9f));
            Box("Wall Segment", parent, new Vector3(6f, 1.2f, -10f), new Vector3(0.3f, 2.4f, 3f), WallColor);
            Box("Wall Segment", parent, new Vector3(-6f, 1.2f, -10f), new Vector3(3f, 2.4f, 0.3f), WallColor);
        }

        static Transform Station(Transform parent, string name, Vector3 padPosition, Color color)
        {
            var station = new GameObject(name).transform;
            station.SetParent(parent, false);
            station.localPosition = padPosition;
            var pad = Box("Pad (stand here, face +Z)", station, new Vector3(0f, 0.005f, 0f), new Vector3(0.9f, 0.01f, 0.9f), color);
            Object.Destroy(pad.GetComponent<Collider>());
            return station;
        }

        // All station builders work in the station's local space: the pad is at the origin, you look toward +Z.

        // Occluders sit 0.5-0.6 m from the pad (the player's capsule stops ~0.3 m short of walls): close cover is
        // what makes small head movements reveal a lot. Eye height on the pad is 1.65 m.

        static void BuildDoorwayStation(Transform s)
        {
            const float h = 2.6f;
            // Front wall 0.6-0.8 m ahead with a 0.7 m door; room interior x -1.6..1.6, z 0.8..4.5.
            Box("Front L", s, new Vector3(-1.025f, h / 2, 0.7f), new Vector3(1.35f, h, 0.2f), WallColor);
            Box("Front R", s, new Vector3(1.025f, h / 2, 0.7f), new Vector3(1.35f, h, 0.2f), WallColor);
            Box("Lintel", s, new Vector3(0f, 2.4f, 0.7f), new Vector3(0.7f, 0.4f, 0.2f), WallColor);
            Box("Side L", s, new Vector3(-1.7f, h / 2, 2.6f), new Vector3(0.2f, h, 4f), WallColor);
            Box("Side R", s, new Vector3(1.7f, h / 2, 2.6f), new Vector3(0.2f, h, 4f), WallColor);
            Box("Back", s, new Vector3(0f, h / 2, 4.6f), new Vector3(3.6f, h, 0.2f), WallColor);

            // Inner face of the right wall, just behind the door frame: hidden until you lean left ~15 cm.
            Code("4721", s, new Vector3(1.6f, 1.5f, 3.15f), 90f, 0.25f);

            // Someone waiting on the left, hidden behind the frame: lean right to spot them.
            Watcher(s, new Vector3(-1.25f, 0f, 2.2f), lookAt: new Vector3(0f, 1.6f, 0.6f));
        }

        static void BuildLowWallStation(Transform s)
        {
            Box("Low Wall", s, new Vector3(0f, 1.72f / 2, 0.6f), new Vector3(4f, 1.72f, 0.2f), WallColor);
            Box("Panel", s, new Vector3(0f, 0.95f, 2.05f), new Vector3(1.6f, 1.9f, 0.1f), PanelColor);
            // Just below the sight line over the wall: raise your head ~11 cm.
            Code("0385", s, new Vector3(0f, 1.725f, 2f), 0f, 0.2f);
        }

        static void BuildSlitStation(Transform s)
        {
            const float h = 3f;
            // 8 cm vertical slit (x 0.08..0.16) in a wall 0.6-0.8 m ahead.
            Box("Wall L", s, new Vector3(-1.46f, h / 2, 0.7f), new Vector3(3.08f, h, 0.2f), WallColor);
            Box("Wall R", s, new Vector3(1.58f, h / 2, 0.7f), new Vector3(2.84f, h, 0.2f), WallColor);
            Box("Panel", s, new Vector3(0f, 1.2f, 3.55f), new Vector3(3f, 2.4f, 0.1f), PanelColor);
            // Only a narrow strip shows through at once; lean right from ~3 to ~20 cm to sweep across all four digits.
            Code("9164", s, new Vector3(0.12f, 1.65f, 3.5f), 0f, 0.24f);
        }

        static void BuildPillarStation(Transform s)
        {
            Box("Pole", s, new Vector3(0f, 1.5f, 0.575f), new Vector3(0.15f, 3f, 0.15f), WallColor);
            Box("Sign", s, new Vector3(0f, 1.65f, 5.05f), new Vector3(1.2f, 0.5f, 0.1f), PanelColor);
            Box("Sign Post", s, new Vector3(0f, 0.7f, 5.05f), new Vector3(0.08f, 1.4f, 0.08f), CodeColor);
            // Fully hidden behind the pole at rest; lean ~12 cm either way.
            Code("2650", s, new Vector3(0f, 1.65f, 5f), 0f, 0.25f);
        }

        // ---------------------------------------------------------------- props

        static void Watcher(Transform parent, Vector3 feet, Vector3 lookAt)
        {
            var root = new GameObject("Watcher").transform;
            root.SetParent(parent, false);
            root.localPosition = feet;
            Vector3 flat = lookAt - feet;
            flat.y = 0f;
            root.localRotation = Quaternion.LookRotation(flat);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            body.transform.localScale = new Vector3(0.5f, 0.75f, 0.5f);
            body.GetComponent<Renderer>().material.color = new Color(0.75f, 0.2f, 0.2f);

            var head = Sphere("Head", root, new Vector3(0f, 1.65f, 0f), 0.36f, new Color(0.95f, 0.8f, 0.65f));
            Sphere("Eye L", head.transform, new Vector3(-0.2f, 0.1f, 0.42f), 0.18f, Color.black);
            Sphere("Eye R", head.transform, new Vector3(0.2f, 0.1f, 0.42f), 0.18f, Color.black);
        }

        /// <summary>Builds a 7-segment number out of thin boxes, readable by someone looking along yaw.</summary>
        static void Code(string digits, Transform parent, Vector3 center, float yaw, float height)
        {
            var root = new GameObject($"Code {digits}").transform;
            root.SetParent(parent, false);
            root.localPosition = center;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);

            float w = height * 0.5f;
            float t = height * 0.12f;
            float spacing = w * 1.7f;
            float x0 = -(digits.Length - 1) * spacing * 0.5f;
            // Pushed slightly toward the viewer so segments stand proud of the surface behind them.
            float z = -t * 0.5f - 0.005f;

            for (int i = 0; i < digits.Length; i++)
            {
                int mask = DigitSegments[digits[i] - '0'];
                float x = x0 + i * spacing;
                var horizontal = new Vector3(w, t, t);
                var vertical = new Vector3(t, height * 0.5f, t);
                Segment(root, mask, 0, new Vector3(x, height * 0.5f, z), horizontal);       // a
                Segment(root, mask, 1, new Vector3(x + w / 2, height * 0.25f, z), vertical); // b
                Segment(root, mask, 2, new Vector3(x + w / 2, -height * 0.25f, z), vertical); // c
                Segment(root, mask, 3, new Vector3(x, -height * 0.5f, z), horizontal);      // d
                Segment(root, mask, 4, new Vector3(x - w / 2, -height * 0.25f, z), vertical); // e
                Segment(root, mask, 5, new Vector3(x - w / 2, height * 0.25f, z), vertical); // f
                Segment(root, mask, 6, new Vector3(x, 0f, z), horizontal);                  // g
            }
        }

        static void Segment(Transform parent, int mask, int bit, Vector3 position, Vector3 size)
        {
            if ((mask & (1 << bit)) == 0) return;
            var seg = Box("Segment", parent, position, size, CodeColor);
            Object.Destroy(seg.GetComponent<Collider>());
        }

        static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Color color, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().material.color = color;
            return go;
        }

        static GameObject Sphere(string name, Transform parent, Vector3 position, float diameter, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * diameter;
            go.GetComponent<Renderer>().material.color = color;
            return go;
        }

        static Texture2D CheckerTexture(Color a, Color b)
        {
            const int size = 64;
            var tex = new Texture2D(size, size) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = ((x < size / 2) ^ (y < size / 2)) ? a : b;
            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }
    }
}
