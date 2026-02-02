using UnityEngine;

public class ClickDetector : MonoBehaviour {
    void Update() {
        if(Input.GetMouseButtonDown(0)) {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if(Physics.Raycast(ray, out hit)) {
                SpaceLocation location = hit.collider.GetComponent<SpaceLocation>();

                if(location != null) {
                    GameManager.Instance.SelectLocation(location.environment);
                }
            }
        }
    }
}
