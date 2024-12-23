using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class CameraController : MonoBehaviour {
    
    public static GameObject player;

    public float rotateSpeed;
    public float horizOffset, verticalOffset;
    private Vector3 cameraDir = Vector3.forward;
    private Vector3 posOffset, newPos;
    private float angleX, angleY, distance;
    private bool cameraForceStationary = false;
    private float panningSpeed = 1f;
    private float panningLimit = 1.5f;

    // Wall collision detection
    private bool inWall;
    private CameraTargetPos targetPos;
    private Vector3 targetOffset = new Vector3(0, 0, 0.3f);
    private Vector3 setPos;

    private void Start() {
        if(Camera.main != GetComponent<Camera>()) {
            try {
                horizOffset = Camera.main.GetComponent<CameraController>().horizOffset;
                verticalOffset = Camera.main.GetComponent<CameraController>().verticalOffset;
            } catch {
                Debug.Log("Previous main camera did not have CameraController component, unable to retrieve offset");
            }
            Destroy(Camera.main.gameObject);
        }
        if(player == null)
            player = PlayerController.pc.controlledPawn;
        tag = "MainCamera";
        //distance = Mathf.Sqrt(Mathf.Pow(horizOffset, 2f) + Mathf.Pow(verticalOffset, 2f));
        distance = horizOffset;
        targetPos = CameraTargetPos.target.GetComponent<CameraTargetPos>();
    }

    public void PlayerExitCover(Vector3 playerRotation) {
        // Rotate Camera to face the player
        angleX = 180f + playerRotation.y;
        cameraForceStationary = true;
    }

    private void LateUpdate() {
        if(PlayerController.pc.canAct) {
            // Detect if player's controlled pawn changed, and reset reference if so
            if(player != PlayerController.pc.controlledPawn) {
                player = PlayerController.pc.controlledPawn;
            }

            // If player is undercover, camera is fixed 
            if(PlayerController.pc.isUndercover && PlayerController.pc.cover == PlayerController.coverType.Spline) {
                Vector3 standardCameraPos = PlayerController.pc.resetPosition + (PlayerController.pc.currentNormal * horizOffset);
                // Get player input to control panning 
                float horizontal = Input.GetAxis("Horizontal");
                float vertical = Input.GetAxis("Vertical");

                // Camera panning while undercover
                if(horizontal != 0 && PlayerController.pc.undercoverPeeking != PlayerController.peekingUnderCoverType.None
                    || (vertical > 0 && PlayerController.pc.undercoverPeeking == PlayerController.peekingUnderCoverType.Up)) {
                    Vector3 panningPos = Vector3.zero;
                    if(PlayerController.pc.undercoverPeeking == PlayerController.peekingUnderCoverType.SideLeft) {
                        panningPos = -PlayerController.pc.transform.right * Time.deltaTime * panningSpeed * Mathf.Abs(horizontal);
                    } else if(PlayerController.pc.undercoverPeeking == PlayerController.peekingUnderCoverType.SideRight) {
                        panningPos = PlayerController.pc.transform.right * Time.deltaTime * panningSpeed * Mathf.Abs(horizontal);
                    } else if(PlayerController.pc.undercoverPeeking == PlayerController.peekingUnderCoverType.Up) {
                        panningPos = PlayerController.pc.transform.up * Time.deltaTime * panningSpeed * Mathf.Abs(vertical);
                    }
                    Vector3 offset = (transform.position + panningPos) - standardCameraPos;
                    // Clamp panning
                    if(Vector3.Magnitude(offset) < panningLimit) {
                        transform.Translate(panningPos, transform);
                    }
                } else {
                    // Base non-panning position (assumed entered before panning)
                    transform.position = standardCameraPos;
                    transform.LookAt(player.transform);
                }
            // Case of the haystack, 360° camera 
            } else if(PlayerController.pc.isInHaystack) {
                transform.position = player.transform.position;
                angleX += Input.GetAxis("CameraX") * rotateSpeed;
                if(angleX > 360f) {
                    angleX = 0f;
                }
                angleY += Input.GetAxis("CameraY") * rotateSpeed;
                if(angleY > 90f) {
                    angleY = 90f;
                }
                if(angleY < -85f) {
                    angleY = -85f;
                }

                transform.rotation = Quaternion.Euler(-angleY, angleX, 0);
            } else {
                // Convert input into rotation
                // When exiting cover, the angle is fixed until the mouse moves again
                if(cameraForceStationary && Input.GetAxis("CameraX") != 0) {
                    cameraForceStationary = false;
                } else {
                    angleX += Input.GetAxis("CameraX") * rotateSpeed;
                }
                if(angleX > 360f)
                    angleX -= 360f;
                else if(angleX < 0f)
                    angleX += 360f;
                cameraDir = new Vector3(Mathf.Sin(angleX * Mathf.Deg2Rad), 0f, Mathf.Cos(angleX * Mathf.Deg2Rad));

                // Calculate position
                targetPos.transform.position = newPos + transform.TransformVector(targetOffset);
                targetPos.transform.rotation = Quaternion.Euler(5, angleX, 0);
                posOffset = Vector3.Normalize(cameraDir) * -horizOffset;
                posOffset.y = verticalOffset;
                newPos = player.transform.position + posOffset;
                if(targetPos.leftInWall) {
                    /*if(inWall) {
                        Debug.Log("adjusting");
                        newPos += transform.TransformVector(Vector3.right) * targetPos.leftDist;
                        setPos = newPos;
                    }*/
                } else if(targetPos.rightInWall) {

                } else {
                    setPos = newPos;
                }
                
                // Prevent camera from going inside walls
                //Debug.DrawRay(newPos, player.transform.position - newPos, Color.blue, 2f, true);
                if(inWall) {
                    if(Physics.Raycast(player.transform.position, newPos - player.transform.position, out RaycastHit hit, distance, LayerMask.GetMask("Terrain"), QueryTriggerInteraction.Ignore)) {
                        newPos = hit.point;
                        if(targetPos.leftInWall) {
                            if(!targetPos.rightInWall)
                                setPos = newPos + transform.TransformVector(Vector3.right) * targetPos.leftDist;
                        } else if(targetPos.rightInWall) {
                            setPos = newPos + transform.TransformVector(Vector3.left) * targetPos.rightDist;
                        } else {
                            transform.position = newPos;
                            transform.rotation = Quaternion.Euler(5, angleX, 0);
                            return;
                        }
                    }
                }

                // Apply offset position
                transform.position = setPos;
                transform.rotation = Quaternion.Euler(5, angleX, 0);
            }
        }
    }

    private void OnTriggerEnter(Collider other) {
        if(other.CompareTag("Terrain"))
            inWall = true;
    }

    private void OnTriggerExit(Collider other) {
        if(other.CompareTag("Terrain"))
            inWall = false;
    }

}
