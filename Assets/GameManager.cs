using System;
using UnityEngine;

public class GameManager : MonoBehaviour {
    public static GameManager Instance;

    public SpaceEnvironment? CurrentSelection { get; private set; }
    public string CurrentSubLocation { get; private set; }
    public int ShieldingLevel { get; private set; }
    public int HardwareClassIndex { get; private set; }
    public int MissionDuration { get; private set; }
    public bool VRPreviewEnabled { get; private set; }
    public FidelityResult CurrentResult { get; private set; }

    public static readonly string[] ShieldingNames = { "LOW", "MEDIUM", "HIGH" };

    public event Action<SpaceEnvironment> OnLocationChanged;
    public event Action<string> OnSubLocationChanged;
    public event Action<int> OnShieldingChanged;
    public event Action<int> OnHardwareChanged;
    public event Action<int> OnMissionDurationChanged;
    public event Action<bool> OnVRPreviewChanged;
    public event Action<FidelityResult> OnFidelityChanged;

    void Awake() {
        Instance = this;
        HardwareClassIndex = 3;
        MissionDuration = 1;
    }

    public void SelectLocation(SpaceEnvironment env) {
        CurrentSelection = env;
        CurrentSubLocation = null;
        OnLocationChanged?.Invoke(env);
        RecalculateFidelity();
    }

    public void SelectSubLocation(string subLocationName) {
        CurrentSubLocation = subLocationName;
        OnSubLocationChanged?.Invoke(subLocationName);
        RecalculateFidelity();
    }

    public void SetShieldingLevel(int level) {
        ShieldingLevel = Mathf.Clamp(level, 0, 2);
        OnShieldingChanged?.Invoke(ShieldingLevel);
        RecalculateFidelity();
    }

    public void SetHardwareClass(int index) {
        HardwareClassIndex = Mathf.Clamp(index, 0, 3);
        OnHardwareChanged?.Invoke(HardwareClassIndex);
        RecalculateFidelity();
    }

    public void SetMissionDuration(int years) {
        MissionDuration = Mathf.Clamp(years, 1, 20);
        OnMissionDurationChanged?.Invoke(MissionDuration);
        RecalculateFidelity();
    }

    public void SetVRPreview(bool enabled) {
        VRPreviewEnabled = enabled;
        OnVRPreviewChanged?.Invoke(VRPreviewEnabled);
    }

    private void RecalculateFidelity() {
        if(!CurrentSelection.HasValue) return;

        CurrentResult = RadiationCalculator.Calculate(
            CurrentSelection.Value,
            CurrentSubLocation,
            ShieldingLevel,
            HardwareClassIndex,
            MissionDuration
        );

        OnFidelityChanged?.Invoke(CurrentResult);
    }
}
