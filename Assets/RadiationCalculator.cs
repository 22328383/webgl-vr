using System.Collections.Generic;
using UnityEngine;

public struct FidelityResult {
    public int tierLevel;
    public string tierName;
    public string tierShortName;
    public float lifespanYears;
    public float effectiveTID;
    public float totalMissionDose;
    public int missionDurationYears;
    public float effectiveGFLOPS;
    public bool hardwareSurvives;
    public string hardwareName;
    public string locationName;
}

public static class RadiationCalculator {

    [System.Serializable] private class DosimetryEntry {
        public string id;
        public string name;
        public float tid;
        public float tid_min;
        public float tid_max;
        public string dominant_source;
        public string confidence;
    }

    [System.Serializable] private class DosimetryTable {
        public DosimetryEntry[] locations;
    }

    [System.Serializable] private class ShieldingEntry {
        public string id;
        public string name;
        public string label;
        public float areal_density_gcm2;
        public float gcr;
        public float spe;
        public float trapped_electrons;
    }

    [System.Serializable] private class ShieldingTable {
        public ShieldingEntry[] levels;
    }

    [System.Serializable] private class HardwareEntry {
        public string id;
        public string name;
        public string short_name;
        public string examples;
        public float gflops;
        public float tid_tolerance_krad;
        public float overhead_factor;
    }

    [System.Serializable] private class HardwareTable {
        public HardwareEntry[] classes;
    }

    [System.Serializable] private class FidelityEntry {
        public int tier;
        public string name;
        public string short_name;
        public float gflops_min;
    }

    [System.Serializable] private class FidelityTable {
        public FidelityEntry[] tiers;
    }

    private static Dictionary<string, DosimetryEntry> dosimetryMap;
    private static ShieldingEntry[] shieldingLevels;
    private static HardwareEntry[] hardwareClasses;
    private static FidelityEntry[] fidelityTiers;
    private static bool loaded = false;
    private static Dictionary<string, string> locationIdMap;

    public static readonly string[] HardwareIds = { "radhard_legacy", "radhard_modern", "fpga", "cots" };
    public static readonly string[] HardwareNames = { "Legacy RH", "Modern RH", "FPGA", "COTS" };

    private static void EnsureLoaded() {
        if(loaded) return;

        try {
            TextAsset dosText = Resources.Load<TextAsset>("RadData/dosimetry_table");
            DosimetryTable dosTable = JsonUtility.FromJson<DosimetryTable>(dosText.text);
            dosimetryMap = new Dictionary<string, DosimetryEntry>();
            foreach(var entry in dosTable.locations)
                dosimetryMap[entry.id] = entry;

            TextAsset shieldText = Resources.Load<TextAsset>("RadData/shielding_table");
            ShieldingTable shieldTable = JsonUtility.FromJson<ShieldingTable>(shieldText.text);
            shieldingLevels = shieldTable.levels;

            TextAsset hwText = Resources.Load<TextAsset>("RadData/hardware_classes");
            HardwareTable hwTable = JsonUtility.FromJson<HardwareTable>(hwText.text);
            hardwareClasses = hwTable.classes;

            TextAsset fidText = Resources.Load<TextAsset>("RadData/fidelity_tiers");
            FidelityTable fidTable = JsonUtility.FromJson<FidelityTable>(fidText.text);
            fidelityTiers = fidTable.tiers;

            locationIdMap = new Dictionary<string, string> {
                { "Mercury", "mercury_orbit" },
                { "Venus", "venus_orbit" },
                { "Earth", "leo_iss" },
                { "Mars", "mars_orbit" },
                { "Jupiter", "jupiter_orbit" },
                { "Saturn", "saturn_orbit" },
                { "Uranus", "uranus_orbit" },
                { "Neptune", "neptune_orbit" },
                { "Moon", "lunar_surface" },
                { "ISS", "leo_iss" },
                { "Io", "io" },
                { "Europa", "europa" },
                { "Ganymede", "ganymede" }
            };

            loaded = true;
        } catch(System.Exception e) {
            Debug.LogError("[RadiationCalculator] Failed to load data: " + e.Message);
        }
    }

    public static string GetDosimetryId(SpaceEnvironment env, string moonName = null) {
        EnsureLoaded();
        string key = string.IsNullOrEmpty(moonName) ? env.ToString() : moonName;
        if(locationIdMap.TryGetValue(key, out string id)) return id;
        return null;
    }

    public static FidelityResult Calculate(SpaceEnvironment env, string moonName, int shieldingIndex, int hardwareIndex, int missionYears) {
        EnsureLoaded();

        FidelityResult result = new FidelityResult();
        result.tierLevel = 0;
        result.tierName = "Infeasible";
        result.tierShortName = "NONE";
        result.lifespanYears = -1f;
        result.effectiveTID = 0f;
        result.totalMissionDose = 0f;
        result.missionDurationYears = missionYears;
        result.effectiveGFLOPS = 0f;
        result.hardwareSurvives = false;
        result.hardwareName = HardwareNames[hardwareIndex];

        string dosId = GetDosimetryId(env, moonName);
        if(dosId == null || !dosimetryMap.ContainsKey(dosId)) {
            result.tierName = "No Data";
            result.locationName = string.IsNullOrEmpty(moonName) ? env.ToString() : moonName;
            return result;
        }

        DosimetryEntry dos = dosimetryMap[dosId];
        result.locationName = dos.name;

        if(dos.tid < 0) {
            result.tierName = "Unknown Radiation";
            return result;
        }

        shieldingIndex = Mathf.Clamp(shieldingIndex, 0, shieldingLevels.Length - 1);
        ShieldingEntry shield = shieldingLevels[shieldingIndex];

        float reductionFactor = 1f;
        switch(dos.dominant_source) {
            case "gcr":               reductionFactor = shield.gcr; break;
            case "spe":               reductionFactor = shield.spe; break;
            case "trapped_electrons": reductionFactor = shield.trapped_electrons; break;
        }

        float effectiveTID = dos.tid * reductionFactor;
        result.effectiveTID = effectiveTID;

        float totalMissionDose = effectiveTID * missionYears;
        result.totalMissionDose = totalMissionDose;

        hardwareIndex = Mathf.Clamp(hardwareIndex, 0, hardwareClasses.Length - 1);
        HardwareEntry hw = hardwareClasses[hardwareIndex];

        if(effectiveTID > 0f) {
            result.lifespanYears = hw.tid_tolerance_krad / effectiveTID;
        } else {
            result.lifespanYears = float.MaxValue;
        }

        if(totalMissionDose > hw.tid_tolerance_krad) {
            result.tierName = "Hardware Fails";
            result.hardwareSurvives = false;
            return result;
        }

        result.hardwareSurvives = true;

        float effectiveGFLOPS = hw.gflops / hw.overhead_factor;
        result.effectiveGFLOPS = effectiveGFLOPS;

        for(int i = fidelityTiers.Length - 1; i >= 0; i--) {
            if(fidelityTiers[i].gflops_min >= 0 && effectiveGFLOPS >= fidelityTiers[i].gflops_min) {
                result.tierLevel = fidelityTiers[i].tier;
                result.tierName = fidelityTiers[i].name;
                result.tierShortName = fidelityTiers[i].short_name;
                break;
            }
        }

        return result;
    }

    public static string FormatLifespan(float years) {
        if(years < 0) return "N/A";
        if(years >= float.MaxValue * 0.5f) return "Indefinite";
        if(years >= 1000f) return string.Format("~{0:N0} yrs", years);
        if(years >= 1f) return string.Format("~{0:F1} yrs", years);
        float days = years * 365.25f;
        if(days >= 1f) return string.Format("~{0:F0} days", days);
        float hours = days * 24f;
        return string.Format("~{0:F0} hrs", hours);
    }
}
