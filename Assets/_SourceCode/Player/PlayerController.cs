using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class PlayerController : MonoBehaviour
{
    public enum Form { Test, Human, Bear };
    public enum coverType { Dynamic, Spline };
    public enum peekingUnderCoverType { None, SideLeft, SideRight, Up };

    public static PlayerController pc;

    // PlayerController/movement functionality
    public GameObject controlledPawn;
    private CharacterController pawnController;
    public float rotationSpeed;
    private Vector3 moveNorm;
    [HideInInspector] public bool canAct;
    private float speedMultiplier = 1f;
    public float slideFriction;
    public float coverDistance = 1f;

    // Jumping
    private bool jumpFlag = false;
    [HideInInspector] public bool grounded, groundedFromCast;
    private float jumpTimer = 0f, baseJumpY;
    private Vector3 baseAirMomentum;
    private PlayerCollisions playerColls;

    // Costume/form functionality
    [HideInInspector] public Form currentForm;
    public FormDataBase formData;
    private FormManager formManager;

    // Attacking functionality
    public float attackHoldTimer = 0f;

    // Stealth functionality
    [HideInInspector] public bool isSneaking;
    [HideInInspector] public bool isUndercover;
    private GameObject coverObj;
    private SplineContainer targetSplineContainer;
    private float currentOffsetSpline;
    public coverType cover;
    public peekingUnderCoverType undercoverPeeking = peekingUnderCoverType.None;
    public Vector3 resetPosition;
    public Vector3 currentNormal;

    // Input variables 
    private const float baseSpeed = 12.0f;
    private float splinePercentage;
    public float speed = baseSpeed;
    public float turnSpeed = 2f;
    Vector3 moveDirection;

    // Animation variables
    private Animator anim;

    /**
     * Speed modifiers for now, this will be
     * generalized later on
     */
    //private Dictionary<string, SpeedEffect> _effects = new Dictionary<string, SpeedEffect>();

    //sets the position of the camera behind each pawn once possessed
    //private Vector3 CameraPosition = new Vector3(0.8f, 2.5f, -4.2f);

    private void Awake() {
        if(pc == null) {
            pc = this;
            DontDestroyOnLoad(gameObject);
        } else
            Destroy(gameObject);
        currentForm = Form.Test;
        canAct = true;
        isUndercover = false;
        isSneaking = false;

        // set controlledPawn
        /*if(controlledPawn == null) {
            try {
                controlledPawn = GameObject.FindGameObjectsWithTag("PlayerControllable")[0];
            } catch {
                Debug.LogError("Unable to find object with tag \"PlayarControllable\", unable to set controlled Player.");
                return;
            }
        }*/
        FindNewPawn(false);
        pawnController = controlledPawn.GetComponent<CharacterController>();
        playerColls = controlledPawn.GetComponent<PlayerCollisions>();

        // Find additional components
        anim = controlledPawn.GetComponent<Animator>();
        formManager = GetComponent<FormManager>();
        formManager.humanPawn = controlledPawn;

        HealthSystem.UpdateHP();
    }

    private void Update() {
        if(controlledPawn == null)
            return;

        // Movement
        if(canAct && grounded && !jumpFlag && Input.GetButtonDown("Jump"))
            jumpFlag = true;

        // Attacking
        if(canAct) {
            if(currentForm == Form.Human || currentForm == Form.Test) {
                if(Input.GetButtonDown("AttackStandard") || Input.GetButtonDown("AttackScissor"))
                    anim.SetTrigger("Scissor");
                attackHoldTimer = 0;
            } else {
                if(Input.GetButtonDown("AttackStandard")) {
                    if(!grounded)
                        anim.SetTrigger("AirAttack");
                    else {
                        anim.SetTrigger("Attack");
                    }
                    attackHoldTimer = 0;
                    anim.SetBool("ChargeRelease", false);
                } else if(Input.GetButton("AttackStandard")) {
                    attackHoldTimer += Time.deltaTime;
                    if(attackHoldTimer >= 0.75f)
                        anim.SetTrigger("ChargeAttack");
                } else if(Input.GetButtonUp("AttackStandard")) {
                    attackHoldTimer = 0;
                    anim.SetBool("ChargeRelease", true);
                } else if(Input.GetButtonDown("AttackScissor")) {
                    //anim.SetTrigger("Scissor");
                }
            }
        }

        // Stealth
        if(Input.GetButtonDown("Sneak")) {
            isSneaking = !isSneaking;
            if(isSneaking) {
                speedMultiplier = 0.5f;
                CanvasManager.cm.stealthGradient.SetActive(true);
            } else {
                isUndercover = false;
                speedMultiplier = 1f;
                CanvasManager.cm.stealthGradient.SetActive(false);
            }
        }
    }

    private void LateUpdate() {
        if(currentForm != Form.Human && currentForm != Form.Test) {
            if(grounded)
                anim.SetBool("Grounded", true);
            else
                anim.SetBool("Grounded", false);
        }
    }

    private void FixedUpdate() {
        if (controlledPawn == null)
            return;
        // Check grounded
        groundedFromCast = IsGrounded();
        if(grounded && baseAirMomentum.magnitude > 0) {
            baseAirMomentum = Vector3.zero;
            baseAirMomentum.y = 0;
        }
        else if(!grounded && controlledPawn.transform.position.y < baseJumpY)
            baseAirMomentum = Vector3.zero;
        // Jump functionality
        if(jumpTimer > 0f)
            jumpTimer -= Time.fixedDeltaTime;
        else
            jumpTimer = 0f;
        // Apply movement
        if(canAct)
            Movement();
    }

    private void checkCoverMovement() {
        RaycastHit hit;
        Vector3 castOrigin = controlledPawn.transform.position + pawnController.center;
        float castRadius = pawnController.height / 2;
        Vector3 castDirection = controlledPawn.transform.forward;

        // Move to cover position if sneaking and close enough to object
        if(isSneaking && !isUndercover && 
            Physics.SphereCast(castOrigin, castRadius, castDirection, out hit, coverDistance)) {
            GameObject targetObject = hit.transform.gameObject;
            if(targetObject != null) {
                targetSplineContainer = targetObject.GetComponentInChildren<SplineContainer>();
                Spline target = targetSplineContainer ? targetSplineContainer.Spline : null;
                if(target != null) {
                    using var native = new NativeSpline(target, targetSplineContainer.transform.localToWorldMatrix);
                    SplineUtility.GetNearestPoint(native, controlledPawn.transform.position, out float3 coverPosition, out float splinePercentage);
                    coverPosition.y = controlledPawn.transform.position.y;

                    TeleportPlayer(coverPosition);
                    isUndercover = true;
                    cover = coverType.Spline;
                    currentOffsetSpline = splinePercentage;
                    // Compute direction from up vector
                    var tangent = SplineUtility.EvaluateTangent(native, splinePercentage);
                    Vector3 upDirection = SplineUtility.EvaluateUpVector(native, splinePercentage);
                    currentNormal = Vector3.Normalize(Vector3.Cross(tangent, upDirection));

                    controlledPawn.transform.rotation = Quaternion.LookRotation(tangent, upDirection);
                } else {
                    Vector3 coverPosition = hit.point;
                    currentNormal = hit.normal;
                    coverPosition += currentNormal;
                    coverPosition.y = controlledPawn.transform.position.y;
                    /*
                    // Get size of model to compute (position of cover - size of model)
                    Vector3 coverOffset = controlledPawn.GetComponent<Renderer>().bounds.extents;
                    coverPosition.x -= coverOffset.x;
                    */
                    coverPosition.x -= 0.5f;
                    TeleportPlayer(coverPosition);
                    isUndercover = true;
                    Vector3 directionVector = Quaternion.AngleAxis(90, Vector3.up) * currentNormal;
                    controlledPawn.transform.rotation = Quaternion.FromToRotation(Vector3.forward, directionVector);
                    cover = coverType.Dynamic;
                }
                coverObj = targetObject;
                resetPosition = controlledPawn.transform.position;
            }
        }
    }

    private bool checkCoverHeight() {
        if(!coverObj) {
            return false;
        }

        MeshRenderer renderer = coverObj.GetComponentInChildren<MeshRenderer>();
        if(!renderer) {
            return false;
        }
        Vector3 size = renderer.bounds.size;

        if(size.y - pawnController.height < 1) {
            return true;
        } else {
            return false;
        }
    }

    private void Movement() {
        // Check if player is undercover
        checkCoverMovement();

        // Retrieve inputs
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // If player is moving away from the cover we un-cover it
        if (isSneaking && isUndercover && vertical < 0) {
            isUndercover = false;
            undercoverPeeking = peekingUnderCoverType.None;
            TeleportPlayer(resetPosition);
            // Rotate player to face normal vector (face away from spline)
            controlledPawn.transform.rotation = Quaternion.LookRotation(currentNormal, Vector3.up);
            CameraController cameraControl = (CameraController) Camera.main.GetComponent(typeof(CameraController));
            Vector3 playerRotation = controlledPawn.transform.eulerAngles;
            cameraControl.PlayerExitCover(playerRotation);
            return;
        }

        // Special movements when undercover
        if (isSneaking && isUndercover) {
            if(cover == coverType.Dynamic) {
                float coverFollow = coverDistance*1.5f;
                bool hitFront = Physics.Raycast(controlledPawn.transform.position, Vector3.forward, out RaycastHit checkHitF, coverFollow, LayerMask.GetMask("Terrain"), QueryTriggerInteraction.Ignore);
                bool hitBack = Physics.Raycast(controlledPawn.transform.position, Vector3.back, out RaycastHit checkHitB, coverFollow, LayerMask.GetMask("Terrain"), QueryTriggerInteraction.Ignore);
                if((hitFront || hitBack) && horizontal != 0) {
                    Vector3 directionVector = hitFront ? checkHitF.normal : checkHitB.normal;
                    directionVector = Quaternion.AngleAxis(90 * Mathf.Sign(horizontal), Vector3.up) * directionVector;
                    controlledPawn.transform.rotation = Quaternion.FromToRotation(Vector3.forward, directionVector);

                    currentNormal = directionVector;
                }

                Vector3 moveCover = horizontal * formData.walkSpeed * speedMultiplier * Vector3.right;

                // Apply movement
                pawnController.Move(moveCover * Time.fixedDeltaTime);
            } else {
                Spline target = targetSplineContainer.Spline;
                if(target != null) { 
                    // Default : no panning
                    undercoverPeeking = peekingUnderCoverType.None;

                    if(horizontal != 0f) {
                        var splineLength = target.GetLength();
                        // Compute next position relative to the spline
                        currentOffsetSpline = (currentOffsetSpline - (horizontal * formData.walkSpeed * speedMultiplier) * Time.fixedDeltaTime / splineLength);
                        // If closed spline, go back to the beginning of the spline once we reach the end
                        if(currentOffsetSpline < 0f) {
                            if(target.Closed) {
                                currentOffsetSpline = 1f;
                            } else {
                                currentOffsetSpline = 0f;
                            }
                        }
                        if(currentOffsetSpline > 1f) {
                            if(target.Closed) {
                                currentOffsetSpline = 0f;
                            } else {
                                currentOffsetSpline = 1f;
                            }
                        }

                        // Tell camera to start panning if player moves while at the end of spline
                        if(!target.Closed) {
                            if(currentOffsetSpline == 1f && horizontal < 0) {
                                undercoverPeeking = peekingUnderCoverType.SideLeft;
                            }
                            if(currentOffsetSpline == 0f && horizontal > 0) {
                                undercoverPeeking = peekingUnderCoverType.SideRight;
                            }
                        }

                        var nextPositionOnSplineLocal = SplineUtility.EvaluatePosition(target, currentOffsetSpline);
                        nextPositionOnSplineLocal.y = controlledPawn.transform.position.y;
                        var nextPositionWorld = targetSplineContainer.transform.TransformPoint(nextPositionOnSplineLocal);
                        nextPositionWorld.y = controlledPawn.transform.position.y;
                        Vector3 moveCover = nextPositionWorld - controlledPawn.transform.position;

                        // Compute direction from up vector and tangent
                        var tangent = SplineUtility.EvaluateTangent(target, currentOffsetSpline);
                        Vector3 upDirection = SplineUtility.EvaluateUpVector(target, currentOffsetSpline);
                        currentNormal = Vector3.Normalize(Vector3.Cross(tangent, upDirection));

                        Quaternion lookRotation = Quaternion.LookRotation(tangent, upDirection);
                        if(horizontal > 0f) {
                            lookRotation *= Quaternion.AngleAxis(180f, Vector3.up);
                        }
                        controlledPawn.transform.rotation = lookRotation;

                        // Apply movement
                        pawnController.Move(moveCover);
                    }

                    // Tell camera to start panning up if player moves up but not left or right
                    // (no need to be at the end of the spline)
                    // And object is short enough
                    if(horizontal == 0 && vertical > 0 && checkCoverHeight()) {
                        undercoverPeeking = peekingUnderCoverType.Up;
                    } 
                }
            }
            resetPosition = controlledPawn.transform.position + (0.5f * currentNormal);
        } else {
            // Convert inputs
            moveNorm = Camera.main.transform.TransformVector(Vector3.Normalize(new Vector3(horizontal, 0, vertical)) * formData.walkSpeed * speedMultiplier);
            moveDirection.x = moveNorm.x;
            moveDirection.z = moveNorm.z;

            // Apply gravity and jump
            if(jumpFlag) {
                moveDirection.y = formData.jumpStrength;
                baseJumpY = controlledPawn.transform.position.y;
                baseAirMomentum = moveDirection;
                jumpFlag = false;
                jumpTimer = 0.25f;
            } else if(!grounded) {
                // Apply gravity
                if(moveDirection.y > 0.05f) {
                    if(Input.GetButton("Jump")) {
                        moveDirection.y -= formData.gravityBase;
                    } else {
                        moveDirection.y -= formData.gravityShortHop;
                    }
                } else
                    moveDirection.y -= formData.gravityFalling;
                if(moveDirection.y < 0)
                    moveDirection.y -= formData.gravityFalling;
                else if(moveDirection.y > 0.05f && !Input.GetButton("Jump"))
                    moveDirection.y -= formData.gravityShortHop;
                else
                    moveDirection.y -= formData.gravityBase;

                // Restrict air momentum
                if(moveDirection.y < 0.1f && SlideOffSurface()) {

                } else {
                    RedirectAirMomentum();
                }
            } else if(jumpTimer <= 0f && moveDirection.y <= 0.05f) {
                moveDirection.y = 0;
            }

            moveDirection.y = Mathf.Clamp(moveDirection.y, Mathf.Abs(formData.maxFallSpeed) * -1f, 50f);

            // Apply movement
            pawnController.Move(moveDirection * Time.fixedDeltaTime);
            if(moveNorm.magnitude > 0.05f)
                controlledPawn.transform.rotation = Quaternion.Euler(0, Quaternion.RotateTowards(controlledPawn.transform.rotation, Quaternion.LookRotation(moveNorm, Vector3.up), rotationSpeed).eulerAngles.y, 0);
        }
    }

    /// <summary>
    /// Checks if the player is grounded or not. Returns false if the player is attemting to jump.
    /// </summary>
    private bool IsGrounded() {
        //Debug.DrawRay(controlledPawn.transform.position - new Vector3(0, formData.formHeight / 2), Vector3.down, Color.black, 1f, false);
        return jumpFlag ? false : Physics.BoxCast(controlledPawn.transform.position - new Vector3(0, formData.formHeight / 2), formData.groundedSkin, Vector3.down, controlledPawn.transform.rotation, 0.02f, LayerMask.GetMask("Terrain"));
    }


    private bool SlideOffSurface() {
        if(Vector3.Angle(Vector3.up, playerColls.hitNormal) >= 70f) {
            moveDirection.x = (1 - playerColls.hitNormal.y) * playerColls.hitNormal.x * (1f - slideFriction);
            moveDirection.z = (1 - playerColls.hitNormal.y) * playerColls.hitNormal.z * (1f - slideFriction);
            //Debug.Log("sliding");
            return true;
        }
        return false;
    }

    private void RedirectAirMomentum() {
        //Debug.Log(baseAirMomentum);
        if(baseAirMomentum.magnitude > 0) {
            moveDirection.x = baseAirMomentum.x + (moveDirection.x * 0.4f);
            moveDirection.z = baseAirMomentum.z + (moveDirection.z * 0.4f);
        } else {
            moveDirection.x *= 0.75f;
            moveDirection.z *= 0.75f;
        }
    }

    /*private void ChangeAlpha(float alpha) {
        var mat = controlledPawn.GetComponent<Renderer>().material;
        var oldColor = mat.color;
        var newColor = new Color(oldColor.r, oldColor.g, oldColor.b, alpha);
        mat.SetColor("_Color", newColor);
    }

    private void OnSneakBegin() {
        var sneak = new Sneak();
        _effects.Add("Sneak", sneak);
        ChangeAlpha(0.5f);
    }

    private void OnSneakEnd() {
        _effects.Remove("Sneak");
        ChangeAlpha(1f);
    }*/

    /**
     * Currently only works for speed
     */
    /*private void ApplyEffects() {
        var effects = _effects.Values;
        var newFlat = effects.Aggregate(baseSpeed, (accum, effect) => accum + effect.flat);
        speed = effects.Aggregate(newFlat, (accum, effect) => accum * effect.multiplier);
    }*/

    public void ChangeControlledPawn(Form newForm) {
        if(newForm == currentForm) {
            Debug.Log("Attempting to switch to current form " + newForm + ".");
            return;
        }
        currentForm = newForm;
        switch(newForm) {
            case Form.Human:
                SetNewPawn(formManager.humanPawn);
                return;
            case Form.Bear:
                SetNewPawn(formManager.GetNewPawn(Form.Bear));
                return;
            case Form.Test:
                SetNewPawn(formManager.GetNewPawn(Form.Test));
                return;
            default:
                Debug.Log("Attempting to switch to form that is not set up in PlayerController.");
                return;
        }
    }

    // Sets controlled pawn to new pawn, resets camera on new pawn
    private void SetNewPawn(GameObject newPawn) {
        // set pawn
        GameObject oldPawn = controlledPawn;
        controlledPawn = newPawn;
        pawnController = controlledPawn.GetComponent<CharacterController>();

        // grab new form data
        float oldHeight = formData.spawnHeight;
        formManager.GetNewData(currentForm);

        // retain current position and rotation
        TeleportPlayer(oldPawn.transform.position + new Vector3(0f, -oldHeight + formData.spawnHeight, 0f));
        controlledPawn.transform.rotation = oldPawn.transform.rotation;
        controlledPawn.SetActive(true);
        oldPawn.SetActive(false);

        // set camera reference
        CameraController.player = controlledPawn;

        // grab additional components
        anim = controlledPawn.GetComponent<Animator>();
        playerColls = controlledPawn.GetComponent<PlayerCollisions>();
    }

    // Finds a new base pawn; used when changing scenes
    public void FindNewPawn(bool debug = true) {
        if(controlledPawn == null) {
            try {
                controlledPawn = GameObject.FindGameObjectsWithTag("PlayerControllable")[0];
            } catch {
                Debug.LogError("Unable to find object with tag \"PlayerControllable\", unable to set controlled Player.");
                return;
            }
            pawnController = controlledPawn.GetComponent<CharacterController>();
            playerColls = controlledPawn.GetComponent<PlayerCollisions>();
            anim = controlledPawn.GetComponent<Animator>();
        } else if(debug) {
            //Debug.Log("Attempting to find new pawn for player when one is already set.");
        }
        pawnController = controlledPawn.GetComponent<CharacterController>();
        playerColls = controlledPawn.GetComponent<PlayerCollisions>();
        anim = controlledPawn.GetComponent<Animator>();
        try {
            formManager.GetNewData(currentForm);
        } catch { }
        //Debug.Log(controlledPawn);
    }

    // Teleports player to destination
    public void TeleportPlayer(Vector3 destination) {
        pawnController.enabled = false;
        controlledPawn.transform.position = destination;
        pawnController.enabled = true;
    }
}
