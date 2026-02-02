using UnityEngine;

public class GameManager : MonoBehaviour {
    public static GameManager Instance;
    public GameObject lowTier;
    public GameObject mediumTier;
    public GameObject highTier;

    void Awake() {
        Instance = this;
    }

    public void SelectLocation(SpaceEnvironment env) {
        DisableAllTiers();

        if(env == SpaceEnvironment.Earth) {
            highTier.SetActive(true);
        }
        else if(env == SpaceEnvironment.Mars) {
            mediumTier.SetActive(true);
        }
        else if(env == SpaceEnvironment.DeepSpace) {
            lowTier.SetActive(true);
        }
    }

    void DisableAllTiers() {
        lowTier.SetActive(false);
        mediumTier.SetActive(false);
        highTier.SetActive(false);
    }
}
