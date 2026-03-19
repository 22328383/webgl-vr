using UnityEngine;
using UnityEngine.EventSystems;

public class ClickDetector : MonoBehaviour {
    void Update() {
        if(Input.GetMouseButtonDown(0)) {
            if(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if(Physics.Raycast(ray, out hit)) {
                // Check for moon first
                SubLocationMarker moon = hit.collider.GetComponent<SubLocationMarker>();
                if(moon != null) {
                    GameManager.Instance.SelectLocation(moon.parentEnvironment);
                    GameManager.Instance.SelectSubLocation(moon.subLocationName);
                    return;
                }

                // Check for planet (just selects, doesn't toggle moons)
                SpaceLocation location = hit.collider.GetComponent<SpaceLocation>();
                if(location != null) {
                    GameManager.Instance.SelectLocation(location.environment);
                }
            }
        }
    }
}
