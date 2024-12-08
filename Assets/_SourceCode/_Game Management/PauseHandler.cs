using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PauseHandler : MonoBehaviour {

    private static GameObject pauseMenu;
    private float timeMarker;
    [HideInInspector] public static bool paused, canPause;

    void Awake() {
        timeMarker = 1f;
        paused = false;
        canPause = true;
    }

    private void Start() {
        pauseMenu = CanvasManager.cm.pauseMenu;
    }

    void Update() {
        if (Input.GetButtonDown("Pause")) {
            PauseGame();
        }
        MouseLock();
    }

    public void PauseGame() {
        if(!canPause) {
            return;
        }
        paused = !paused;
        try {
            pauseMenu.SetActive(paused);
            if(paused) {
                timeMarker = Time.timeScale;
                Time.timeScale = 0;
                PlayerController.pc.canAct = false;
            } else {
                Time.timeScale = timeMarker;
                PlayerController.pc.canAct = true;
                MouseLock();
            }
        } catch {
            Debug.LogError("Pause Menu reference does not exist. Was the menu deleted, did the scene change, or was the reference not set?");
        }
    }

    public static void SetMenu(bool toggle) {
        pauseMenu.SetActive(toggle);
    }

    private void MouseLock() {
        if (!paused) {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

    }
}
