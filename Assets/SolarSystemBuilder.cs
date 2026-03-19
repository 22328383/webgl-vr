using System.Collections.Generic;
using UnityEngine;

public class SolarSystemBuilder : MonoBehaviour {

    public static SolarSystemBuilder Instance;

    [Header("Optional: assign existing planet objects to skip spawning them")]
    [SerializeField] private GameObject existingSun;
    [SerializeField] private GameObject existingEarth;
    [SerializeField] private GameObject existingMars;

    private struct PlanetDef {
        public SpaceEnvironment env;
        public float distance;
        public float radius;
        public Color color;
        public float spinSpeed; // degrees per second

        public PlanetDef(SpaceEnvironment env, float distance, float radius, Color color, float spinSpeed) {
            this.env = env;
            this.distance = distance;
            this.radius = radius;
            this.color = color;
            this.spinSpeed = spinSpeed;
        }
    }

    private struct MoonDef {
        public string name;
        public SpaceEnvironment parent;
        public float radius;
        public Color color;

        public MoonDef(string name, SpaceEnvironment parent, float radius, Color color) {
            this.name = name;
            this.parent = parent;
            this.radius = radius;
            this.color = color;
        }
    }

    public Dictionary<SpaceEnvironment, GameObject> PlanetObjects { get; private set; }
        = new Dictionary<SpaceEnvironment, GameObject>();

    private Dictionary<SpaceEnvironment, GameObject> moonContainers
        = new Dictionary<SpaceEnvironment, GameObject>();

    // Spin tracking
    private Dictionary<SpaceEnvironment, float> planetSpinSpeeds
        = new Dictionary<SpaceEnvironment, float>();

    // Animation
    private SpaceEnvironment? expandedPlanet;
    private SpaceEnvironment? animatingPlanet;
    private bool animatingOpen;
    private float animProgress; // 0 to 1
    private float animDuration = 0.35f; // seconds

    void Awake() {
        Instance = this;
    }

    void Start() {
        BuildSolarSystem();
    }

    void Update() {
        // Spin all planets
        foreach(var kvp in PlanetObjects) {
            if(planetSpinSpeeds.ContainsKey(kvp.Key)) {
                kvp.Value.transform.Rotate(Vector3.up, planetSpinSpeeds[kvp.Key] * Time.deltaTime, Space.Self);
            }
        }

        // Spin all visible moons
        foreach(var kvp in moonContainers) {
            if(kvp.Value.activeSelf) {
                foreach(Transform child in kvp.Value.transform) {
                    child.Rotate(Vector3.up, 20f * Time.deltaTime, Space.Self);
                }
            }
        }

        // Animate dropdown
        if(animatingPlanet.HasValue) {
            animProgress += Time.deltaTime / animDuration;

            if(animProgress >= 1f) {
                animProgress = 1f;
                FinishAnimation();
            } else {
                UpdateAnimation();
            }
        }
    }

    public bool HasMoons(SpaceEnvironment env) {
        return moonContainers.ContainsKey(env);
    }

    public bool IsExpanded(SpaceEnvironment env) {
        return expandedPlanet == env;
    }

    public void ToggleMoons(SpaceEnvironment env) {
        // Don't interrupt an ongoing animation
        if(animatingPlanet.HasValue) return;

        if(expandedPlanet == env) {
            // Collapse current
            StartAnimation(env, false);
        } else {
            // Collapse previous instantly if any
            if(expandedPlanet.HasValue && moonContainers.ContainsKey(expandedPlanet.Value)) {
                SetMoonContainerScale(expandedPlanet.Value, 0f);
                moonContainers[expandedPlanet.Value].SetActive(false);
                expandedPlanet = null;
            }
            // Open new
            StartAnimation(env, true);
        }
    }

    private void StartAnimation(SpaceEnvironment env, bool opening) {
        if(!moonContainers.ContainsKey(env)) return;

        animatingPlanet = env;
        animatingOpen = opening;
        animProgress = 0f;

        if(opening) {
            moonContainers[env].SetActive(true);
            SetMoonContainerScale(env, 0f);
            expandedPlanet = env;
        }
    }

    private void UpdateAnimation() {
        if(!animatingPlanet.HasValue) return;

        // Smooth ease-out curve
        float t = 1f - Mathf.Pow(1f - animProgress, 3f);

        if(animatingOpen) {
            SetMoonContainerScale(animatingPlanet.Value, t);
        } else {
            SetMoonContainerScale(animatingPlanet.Value, 1f - t);
        }
    }

    private void FinishAnimation() {
        if(!animatingPlanet.HasValue) return;

        if(animatingOpen) {
            SetMoonContainerScale(animatingPlanet.Value, 1f);
        } else {
            SetMoonContainerScale(animatingPlanet.Value, 0f);
            moonContainers[animatingPlanet.Value].SetActive(false);
            if(expandedPlanet == animatingPlanet.Value) {
                expandedPlanet = null;
            }
        }

        animatingPlanet = null;
    }

    private void SetMoonContainerScale(SpaceEnvironment env, float t) {
        if(!moonContainers.ContainsKey(env)) return;
        GameObject container = moonContainers[env];

        // Scale Y from 0 to 1 and fade alpha via renderer
        container.transform.localScale = new Vector3(1f, t, 1f);

        // Also set individual moon alpha for a fade-in effect
        foreach(Transform child in container.transform) {
            Renderer rend = child.GetComponent<Renderer>();
            if(rend != null) {
                Color c = rend.material.color;
                c.a = t;
                rend.material.color = c;
            }
        }
    }

    private void BuildSolarSystem() {
        GameObject solarSystem = new GameObject("SolarSystem_Generated");

        PlanetDef[] planets = new PlanetDef[] {
            new PlanetDef(SpaceEnvironment.Sun,      0f,    3.0f,  new Color(1.0f, 0.85f, 0.3f),  2f),
            new PlanetDef(SpaceEnvironment.Mercury,   5f,    0.3f,  new Color(0.7f, 0.65f, 0.6f),  15f),
            new PlanetDef(SpaceEnvironment.Venus,     8f,    0.6f,  new Color(0.9f, 0.75f, 0.5f),  -8f),
            new PlanetDef(SpaceEnvironment.Earth,    12f,    0.65f, new Color(0.2f, 0.5f, 0.9f),   12f),
            new PlanetDef(SpaceEnvironment.Mars,     16f,    0.45f, new Color(0.8f, 0.35f, 0.2f),  11f),
            new PlanetDef(SpaceEnvironment.Jupiter,  22f,    1.8f,  new Color(0.8f, 0.7f, 0.5f),   25f),
            new PlanetDef(SpaceEnvironment.Saturn,   30f,    1.5f,  new Color(0.9f, 0.8f, 0.55f),  22f),
            new PlanetDef(SpaceEnvironment.Uranus,   38f,    1.0f,  new Color(0.6f, 0.85f, 0.9f),  -18f),
            new PlanetDef(SpaceEnvironment.Neptune,  45f,    0.95f, new Color(0.3f, 0.4f, 0.9f),   16f),
        };

        MoonDef[] moons = new MoonDef[] {
            new MoonDef("Moon",      SpaceEnvironment.Earth,   0.45f, new Color(0.75f, 0.75f, 0.75f)),
            new MoonDef("ISS",       SpaceEnvironment.Earth,   0.20f, new Color(0.9f, 0.9f, 0.9f)),
            new MoonDef("Phobos",    SpaceEnvironment.Mars,    0.25f, new Color(0.6f, 0.5f, 0.4f)),
            new MoonDef("Deimos",    SpaceEnvironment.Mars,    0.20f, new Color(0.65f, 0.55f, 0.45f)),
            new MoonDef("Io",        SpaceEnvironment.Jupiter, 0.38f, new Color(0.9f, 0.8f, 0.3f)),
            new MoonDef("Europa",    SpaceEnvironment.Jupiter, 0.35f, new Color(0.85f, 0.8f, 0.7f)),
            new MoonDef("Ganymede",  SpaceEnvironment.Jupiter, 0.45f, new Color(0.7f, 0.65f, 0.6f)),
            new MoonDef("Callisto",  SpaceEnvironment.Jupiter, 0.40f, new Color(0.5f, 0.45f, 0.4f)),
            new MoonDef("Titan",     SpaceEnvironment.Saturn,  0.50f, new Color(0.85f, 0.7f, 0.35f)),
            new MoonDef("Enceladus", SpaceEnvironment.Saturn,  0.25f, new Color(0.95f, 0.95f, 0.95f)),
            new MoonDef("Titania",   SpaceEnvironment.Uranus,  0.35f, new Color(0.75f, 0.75f, 0.8f)),
            new MoonDef("Miranda",   SpaceEnvironment.Uranus,  0.22f, new Color(0.7f, 0.7f, 0.7f)),
            new MoonDef("Triton",    SpaceEnvironment.Neptune, 0.38f, new Color(0.7f, 0.75f, 0.85f)),
        };

        // --- Spawn planets ---
        foreach(PlanetDef p in planets) {
            GameObject planetGO = null;

            if(p.env == SpaceEnvironment.Sun && existingSun != null) {
                planetGO = existingSun;
            } else if(p.env == SpaceEnvironment.Earth && existingEarth != null) {
                planetGO = existingEarth;
            } else if(p.env == SpaceEnvironment.Mars && existingMars != null) {
                planetGO = existingMars;
            } else {
                planetGO = CreateSphere(p.env.ToString(), p.radius, p.color);
                planetGO.transform.SetParent(solarSystem.transform, false);
                planetGO.transform.position = new Vector3(p.distance, 0, 0);
            }

            SpaceLocation loc = planetGO.GetComponent<SpaceLocation>();
            if(loc == null) loc = planetGO.AddComponent<SpaceLocation>();
            loc.environment = p.env;

            if(planetGO.GetComponent<Collider>() == null) {
                planetGO.AddComponent<SphereCollider>();
            }

            PlanetObjects[p.env] = planetGO;
            planetSpinSpeeds[p.env] = p.spinSpeed;
        }

        // --- Group moons by parent ---
        Dictionary<SpaceEnvironment, List<MoonDef>> moonsByPlanet = new Dictionary<SpaceEnvironment, List<MoonDef>>();
        foreach(MoonDef m in moons) {
            if(!moonsByPlanet.ContainsKey(m.parent))
                moonsByPlanet[m.parent] = new List<MoonDef>();
            moonsByPlanet[m.parent].Add(m);
        }

        // --- Build moon containers (hidden by default) ---
        foreach(var kvp in moonsByPlanet) {
            SpaceEnvironment parentEnv = kvp.Key;
            List<MoonDef> parentMoons = kvp.Value;

            if(!PlanetObjects.ContainsKey(parentEnv)) continue;
            GameObject parentPlanet = PlanetObjects[parentEnv];
            float parentX = parentPlanet.transform.position.x;

            GameObject container = new GameObject("Moons_" + parentEnv);
            container.transform.SetParent(solarSystem.transform, false);
            container.transform.position = new Vector3(parentX, 0, 0);

            float spacing = 1.4f;
            float planetRadius = parentPlanet.transform.localScale.x * 0.5f;
            float startY = -(planetRadius + 3.0f);

            for(int i = 0; i < parentMoons.Count; i++) {
                MoonDef m = parentMoons[i];
                GameObject moonGO = CreateSphere(m.name, m.radius, m.color);
                moonGO.transform.SetParent(container.transform, false);

                float yPos = startY - (i * spacing);
                moonGO.transform.localPosition = new Vector3(0, yPos, 0);

                SubLocationMarker marker = moonGO.AddComponent<SubLocationMarker>();
                marker.subLocationName = m.name;
                marker.parentEnvironment = m.parent;

                if(moonGO.GetComponent<Collider>() == null) {
                    moonGO.AddComponent<SphereCollider>();
                }
            }

            container.SetActive(false);
            moonContainers[parentEnv] = container;
        }
    }

    private GameObject CreateSphere(string name, float radius, Color color) {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.localScale = Vector3.one * radius * 2f;

        Renderer rend = sphere.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;

        if(name == "Sun") {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 2f);
        }

        rend.material = mat;
        return sphere;
    }
}
