using System;
using System.Collections.Generic;
using UnityEngine;

public class SolarSystemBuilder : MonoBehaviour {

    public static SolarSystemBuilder Instance;

    [Serializable] private class PlanetJson {
        public string name;
        public float distance;
        public float radius;
        public float[] color;
        public float spin_speed;
    }

    [Serializable] private class MoonJson {
        public string name;
        public string parent;
        public float radius;
        public float[] color;
    }

    [Serializable] private class SolarSystemJson {
        public PlanetJson[] planets;
        public MoonJson[] moons;
    }

    public Dictionary<SpaceEnvironment, GameObject> PlanetObjects { get; private set; }
        = new Dictionary<SpaceEnvironment, GameObject>();

    private Dictionary<SpaceEnvironment, GameObject> moonContainers
        = new Dictionary<SpaceEnvironment, GameObject>();

    private Dictionary<SpaceEnvironment, float> planetSpinSpeeds
        = new Dictionary<SpaceEnvironment, float>();

    private SpaceEnvironment? expandedPlanet;
    private SpaceEnvironment? animatingPlanet;
    private bool animatingOpen;
    private float animProgress;
    private float animDuration = 0.35f;

    void Awake() {
        Instance = this;
    }

    void Start() {
        BuildSolarSystem();
    }

    void Update() {
        foreach(var kvp in PlanetObjects) {
            if(planetSpinSpeeds.ContainsKey(kvp.Key)) {
                kvp.Value.transform.Rotate(Vector3.up, planetSpinSpeeds[kvp.Key] * Time.deltaTime, Space.Self);
            }
        }

        foreach(var kvp in moonContainers) {
            if(kvp.Value.activeSelf) {
                foreach(Transform child in kvp.Value.transform) {
                    child.Rotate(Vector3.up, 20f * Time.deltaTime, Space.Self);
                }
            }
        }

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
        if(animatingPlanet.HasValue) return;

        if(expandedPlanet == env) {
            StartAnimation(env, false);
        } else {
            if(expandedPlanet.HasValue && moonContainers.ContainsKey(expandedPlanet.Value)) {
                SetMoonContainerScale(expandedPlanet.Value, 0f);
                moonContainers[expandedPlanet.Value].SetActive(false);
                expandedPlanet = null;
            }
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

        container.transform.localScale = new Vector3(1f, t, 1f);

        foreach(Transform child in container.transform) {
            Renderer rend = child.GetComponent<Renderer>();
            if(rend != null) {
                Color c = rend.material.color;
                c.a = t;
                rend.material.color = c;
            }
        }
    }

    private SpaceEnvironment ParseEnv(string name) {
        return (SpaceEnvironment)Enum.Parse(typeof(SpaceEnvironment), name);
    }

    private Color ParseColor(float[] c) {
        return new Color(c[0], c[1], c[2]);
    }

    private void BuildSolarSystem() {
        TextAsset json = Resources.Load<TextAsset>("RadData/solar_system");
        SolarSystemJson data = JsonUtility.FromJson<SolarSystemJson>(json.text);

        GameObject solarSystem = new GameObject("SolarSystem_Generated");

        foreach(PlanetJson p in data.planets) {
            SpaceEnvironment env = ParseEnv(p.name);
            Color color = ParseColor(p.color);

            GameObject planetGO = CreateSphere(p.name, p.radius, color);
            planetGO.transform.SetParent(solarSystem.transform, false);
            planetGO.transform.position = new Vector3(p.distance, 0, 0);

            SpaceLocation loc = planetGO.AddComponent<SpaceLocation>();
            loc.environment = env;

            if(planetGO.GetComponent<Collider>() == null) {
                planetGO.AddComponent<SphereCollider>();
            }

            PlanetObjects[env] = planetGO;
            planetSpinSpeeds[env] = p.spin_speed;
        }

        Dictionary<SpaceEnvironment, List<MoonJson>> moonsByPlanet = new Dictionary<SpaceEnvironment, List<MoonJson>>();
        foreach(MoonJson m in data.moons) {
            SpaceEnvironment parentEnv = ParseEnv(m.parent);
            if(!moonsByPlanet.ContainsKey(parentEnv))
                moonsByPlanet[parentEnv] = new List<MoonJson>();
            moonsByPlanet[parentEnv].Add(m);
        }

        foreach(var kvp in moonsByPlanet) {
            SpaceEnvironment parentEnv = kvp.Key;
            List<MoonJson> parentMoons = kvp.Value;

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
                MoonJson m = parentMoons[i];
                Color color = ParseColor(m.color);
                GameObject moonGO = CreateSphere(m.name, m.radius, color);
                moonGO.transform.SetParent(container.transform, false);

                float yPos = startY - (i * spacing);
                moonGO.transform.localPosition = new Vector3(0, yPos, 0);

                SubLocationMarker marker = moonGO.AddComponent<SubLocationMarker>();
                marker.subLocationName = m.name;
                marker.parentEnvironment = parentEnv;

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

        rend.material = mat;
        return sphere;
    }
}
