using UnityEngine;

public class Buoyant : MonoBehaviour {
    private Rigidbody rb;

    void Awake() {
        rb = GetComponent<Rigidbody>();
    }
}
