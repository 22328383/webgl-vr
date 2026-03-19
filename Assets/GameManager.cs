using System;
using UnityEngine;

public class GameManager : MonoBehaviour {
    public static GameManager Instance;
    public GameObject lowTier;
    public GameObject mediumTier;
    public GameObject highTier;

    public SpaceEnvironment? CurrentSelection { get; private set; }
    public string CurrentSubLocation { get; private set; }
    public int ShieldingLevel { get; private set; } // 0=Off, 1=Low, 2=Medium, 3=High
    public bool VRPreviewEnabled { get; private set; }

    public static readonly string[] ShieldingNames = { "OFF", "LOW", "MEDIUM", "HIGH" };

    public event Action<SpaceEnvironment> OnLocationChanged;
    public event Action<string> OnSubLocationChanged;
    public event Action<int> OnShieldingChanged;
    public event Action<bool> OnVRPreviewChanged;

    void Awake() {
        Instance = this;
    }

    public void SelectLocation(SpaceEnvironment env) {
        DisableAllTiers();

        string tier = GetTierName(env);
        if(tier == "High" && highTier != null) highTier.SetActive(true);
        else if(tier == "Medium" && mediumTier != null) mediumTier.SetActive(true);
        else if(tier == "Low" && lowTier != null) lowTier.SetActive(true);

        CurrentSelection = env;
        CurrentSubLocation = null;
        OnLocationChanged?.Invoke(env);
    }

    public void SelectSubLocation(string subLocationName) {
        CurrentSubLocation = subLocationName;
        OnSubLocationChanged?.Invoke(subLocationName);
    }

    public void SetShieldingLevel(int level) {
        ShieldingLevel = Mathf.Clamp(level, 0, 3);
        OnShieldingChanged?.Invoke(ShieldingLevel);
    }

    public void SetVRPreview(bool enabled) {
        VRPreviewEnabled = enabled;
        OnVRPreviewChanged?.Invoke(VRPreviewEnabled);
    }

    public string GetTierName(SpaceEnvironment env) {
        switch(env) {
            case SpaceEnvironment.Sun:      return "Extreme";
            case SpaceEnvironment.Mercury:  return "Low";
            case SpaceEnvironment.Venus:    return "Low";
            case SpaceEnvironment.Earth:    return "High";
            case SpaceEnvironment.Mars:     return "Medium";
            case SpaceEnvironment.Jupiter:  return "Low";
            case SpaceEnvironment.Saturn:   return "Low";
            case SpaceEnvironment.Uranus:   return "Low";
            case SpaceEnvironment.Neptune:  return "Low";
            default:                        return "N/A";
        }
    }

    // Returns the max feasible VR tier for a location
    // 0=None, 1=Low, 2=Medium, 3=High
    public int GetVRTierLevel(SpaceEnvironment env) {
        switch(env) {
            case SpaceEnvironment.Sun:      return 0; // too extreme, no VR
            case SpaceEnvironment.Mercury:  return 1;
            case SpaceEnvironment.Venus:    return 1;
            case SpaceEnvironment.Earth:    return 3;
            case SpaceEnvironment.Mars:     return 2;
            case SpaceEnvironment.Jupiter:  return 0; // too extreme, no VR
            case SpaceEnvironment.Saturn:   return 1;
            case SpaceEnvironment.Uranus:   return 1;
            case SpaceEnvironment.Neptune:  return 1;
            default:                        return 0;
        }
    }

    // Moon VR tiers: inherit from parent but can be different
    public int GetMoonVRTierLevel(string moonName, SpaceEnvironment parent) {
        // Moons closer to habitable zones get higher tiers
        switch(moonName) {
            case "Moon":      return 3; // Earth's moon = High
            case "ISS":       return 3; // In Earth orbit = High
            case "Phobos":    return 2; // Mars moon = Medium
            case "Deimos":    return 2;
            case "Io":        return 0; // Volcanic, hostile
            case "Europa":    return 2; // Potential habitability
            case "Ganymede":  return 1;
            case "Callisto":  return 1;
            case "Titan":     return 2; // Atmosphere = Medium
            case "Enceladus": return 1;
            case "Titania":   return 1;
            case "Miranda":   return 1;
            case "Triton":    return 1;
            default:          return GetVRTierLevel(parent);
        }
    }

    void DisableAllTiers() {
        if(lowTier != null) lowTier.SetActive(false);
        if(mediumTier != null) mediumTier.SetActive(false);
        if(highTier != null) highTier.SetActive(false);
    }
}
