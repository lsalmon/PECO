using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCollisions : MonoBehaviour {

    [HideInInspector] public Vector3 hitNormal;
    [HideInInspector] public bool haystackNearby = false;
    [HideInInspector] public GameObject haystack = null;

    private void OnControllerColliderHit(ControllerColliderHit hit) {
        hitNormal = hit.normal;
        //Debug.Log(hit.gameObject.name);
    }

    // Detecting bush cover objects
    private void OnTriggerEnter(Collider coll) {
        if(coll.CompareTag("HayStack")) {
            haystackNearby = true;
            haystack = coll.gameObject;
        }
    }
    private void OnTriggerExit(Collider coll) {
        if(coll.CompareTag("HayStack")) {
            haystackNearby = false;
            haystack = null;
        }
    }
}
