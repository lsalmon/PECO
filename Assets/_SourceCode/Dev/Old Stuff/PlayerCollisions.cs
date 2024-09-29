using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCollisions : MonoBehaviour {

    [HideInInspector] public Vector3 hitNormal;
    [HideInInspector] public bool haystackNearby = false;

    private void OnControllerColliderHit(ControllerColliderHit hit) {
        hitNormal = hit.normal;
        //Debug.Log(hit.gameObject.name);
    }

    // Detecting bush cover objects
    private void OnTriggerEnter(Collider coll) {
        if(coll.CompareTag("HayStack")) {
            Debug.Log("=====> Entered haystack collider");
            haystackNearby = true;
        }
    }
    private void OnTriggerExit(Collider coll) {
        if(coll.CompareTag("HayStack")) {
            Debug.Log("=====> Exited haystack collider");
            haystackNearby = false;
        }
    }
}
