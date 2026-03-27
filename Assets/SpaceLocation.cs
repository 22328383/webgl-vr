using System.Collections.Generic;
using UnityEngine;

public enum SpaceEnvironment {
    Mercury,
    Venus,
    Earth,
    Mars,
    Jupiter,
    Saturn,
    Uranus,
    Neptune
}

[System.Serializable]
public class SubLocation {
    public string name;
    public SpaceEnvironment parent;

    public SubLocation(string name, SpaceEnvironment parent) {
        this.name = name;
        this.parent = parent;
    }
}

public static class SubLocationDatabase {
    private static Dictionary<SpaceEnvironment, List<SubLocation>> _data;

    public static Dictionary<SpaceEnvironment, List<SubLocation>> Data {
        get {
            if(_data == null) BuildDatabase();
            return _data;
        }
    }

    private static void BuildDatabase() {
        _data = new Dictionary<SpaceEnvironment, List<SubLocation>> {
            { SpaceEnvironment.Earth, new List<SubLocation> {
                new SubLocation("Moon", SpaceEnvironment.Earth),
                new SubLocation("ISS", SpaceEnvironment.Earth)
            }},
            { SpaceEnvironment.Jupiter, new List<SubLocation> {
                new SubLocation("Io", SpaceEnvironment.Jupiter),
                new SubLocation("Europa", SpaceEnvironment.Jupiter),
                new SubLocation("Ganymede", SpaceEnvironment.Jupiter)
            }}
        };
    }

    public static List<SubLocation> GetSubLocations(SpaceEnvironment env) {
        return Data.ContainsKey(env) ? Data[env] : new List<SubLocation>();
    }
}

public class SpaceLocation : MonoBehaviour {
    public SpaceEnvironment environment;
}

public class SubLocationMarker : MonoBehaviour {
    public string subLocationName;
    public SpaceEnvironment parentEnvironment;
}
