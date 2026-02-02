using UnityEngine;

public enum SpaceEnvironment {
    Earth,
    LEO,
    Mars,
    DeepSpace
}

public class SpaceLocation : MonoBehaviour {
    public SpaceEnvironment environment;
}
