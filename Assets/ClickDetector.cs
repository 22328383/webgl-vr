using UnityEngine;
using UnityEngine.EventSystems;

public class ClickDetector : MonoBehaviour {
    [SerializeField] private SystemMapUI systemMapUI;

    void Update() {
        if(Input.GetMouseButtonDown(0)) {
            if(EventSystem.current != null && IsPointerOverUIElement())
                return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if(Physics.Raycast(ray, out hit)) {
                SubLocationMarker moon = hit.collider.GetComponent<SubLocationMarker>();
                if(moon != null) {
                    GameManager.Instance.SelectLocation(moon.parentEnvironment);
                    GameManager.Instance.SelectSubLocation(moon.subLocationName);
                    return;
                }

                SpaceLocation location = hit.collider.GetComponent<SpaceLocation>();
                if(location != null) {
                    GameManager.Instance.SelectLocation(location.environment);

                    if(systemMapUI != null) {
                        systemMapUI.ExpandPlanet(location.environment);
                    }
                }
            }
        }
    }

    private bool IsPointerOverUIElement() {
        UnityEngine.EventSystems.PointerEventData eventData = new UnityEngine.EventSystems.PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;
        System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        foreach(var r in results) {
            if(r.gameObject.layer == LayerMask.NameToLayer("UI"))
                return true;
        }
        return false;
    }
}
