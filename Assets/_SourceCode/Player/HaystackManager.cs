using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HaystackManager : MonoBehaviour {
    private PlayerController player;
    private GameObject haystackSlider;
    private Image slider;
    private float holdTime;
    private void Awake() {
        player = GetComponent<PlayerController>();
    }

    private void Start() {
        haystackSlider = CanvasManager.cm.haystackCircularMeter;
        slider = haystackSlider.GetComponent<Image>();
        holdTime = 0;
        slider.fillAmount = holdTime;
    }

    private void Update() {
        if(haystackSlider.activeSelf) {
            if(Input.GetButton("Sneak")) {
                holdTime += Time.deltaTime;
                if(holdTime >= 1) {
                    player.jumpIntoHaystack();
                    return ;
                }
                slider.fillAmount = holdTime;
            }
            if(Input.GetButtonUp("Sneak")) {
                holdTime = 0;
                slider.fillAmount = holdTime;
            }
        } else if(holdTime != 0) {
            holdTime = 0;
            slider.fillAmount = holdTime;
        }
    }
}