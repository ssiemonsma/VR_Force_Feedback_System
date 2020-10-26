using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static ForceManager;
using static UDPManager;

/*
 * This is a central script manager that coordinates different functions in the correct ordering.
 */

public class ScriptManager : MonoBehaviour {
    public LeftHand leftHand;
    public RightHand rightHand;
    public GameObject head;
    public Slider ceilingHeightSlider;
    public Toggle lowForceModeToggle;
    public Toggle boundaryOnlyModeToggle;
    public GameObject ceiling;
    public Text ceilingHeightText;
    public GameObject connectedNotificationHUD;
    public GameObject disconnectedAlertHUD;
    public GameObject lowVoltageAlertHUD;
    public GameObject extendedUseAlertHUD;
    private float timeOfLastLowVoltageWarning = -120;
    private float lowVoltageThreshold = 7.0f;
    private bool disconnectAlertAlreadySent = true;
    private bool connectionNotificationAlreadySent = false;
    private Vector3 previousHeadPosition = new Vector3(0, 0, 0);

    // Use this for initialization    
    void Start()
    {
        UDPManager.Start();
    }

    private void FixedUpdate()
    {
        // Update ceiling height according to in-game mini menu slider if it has been updated recently
        if (ForceManager.GetCeilingHeight() != ceilingHeightSlider.value)
        {
            ForceManager.SetCeilingHeight(ceilingHeightSlider.value);
            ceiling.transform.position = new Vector3(ceiling.transform.position.x, ceilingHeightSlider.value, ceiling.transform.position.z);
            ceilingHeightText.text = ceilingHeightSlider.value.ToString("0.00") + " m";
        }

        UDPManager.SetCurrentTime(Time.fixedTime);      // The UDP receiving thread cannot directly access Time.fixedTime, so we must update an accessible member variable

        // If the PC recently connected to the Raspberry Pi and the game as already started, notify the user
        if (UDPManager.CheckConnection())
        {
            if (!connectionNotificationAlreadySent && ForceManager.GetGameStarted())
            {
                connectedNotificationHUD.SetActive(true);
                connectionNotificationAlreadySent = true;
            }
            disconnectAlertAlreadySent = false;
        }

        // Check for connection timeouts and alert the user of any disconnection
        if (UDPManager.CheckForTimeout() && !disconnectAlertAlreadySent && ForceManager.GetGameStarted())
        {
            disconnectedAlertHUD.SetActive(true);
            disconnectAlertAlreadySent = true;
            connectionNotificationAlreadySent = false;
        }
        
        // Request a new Force Feedback hardware voltage level, if it is time to update that value
        UDPManager.UpdateVoltage();

        // If the voltage is low, alert the user
        if (UDPManager.GetLastVoltageReading() <= lowVoltageThreshold && (Time.fixedTime - timeOfLastLowVoltageWarning) > 120 && UDPManager.GetLastVoltageReading() != 0 && ForceManager.GetGameStarted())
        {
            lowVoltageAlertHUD.SetActive(true);
            timeOfLastLowVoltageWarning = Time.fixedTime;
        }

        // This section checks if the user is falling
        Vector3 headPosition = head.transform.position;
        Vector3 headVelocity = (headPosition - previousHeadPosition) / Time.fixedDeltaTime;
        float downwardHeadVelocity = -Vector3.Dot(headVelocity, new Vector3(0, 1, 0));
        previousHeadPosition = headPosition;
        if (headPosition.y < 1.1 || (downwardHeadVelocity > 0.5 && headPosition.y < 1.5))
        {
            ForceManager.SetIsFalling(true);
        }
        else
        {
            ForceManager.SetIsFalling(false);
        }

        // Update the boundary forces for the left and right hands
        leftHand.updateLeftForces();
        rightHand.updateRightForces();

        // Send a packet based on the current system state
        ForceManager.SendPacket();
    }

    // Enables/disables Full-Force Mode
    public void updateFullForceMode()
    {
        ForceManager.SetFullForceMode(lowForceModeToggle.isOn);
    }

    // Enables/disables Boundary-Only Mode
    public void updateBoundaryOnlyMode()
    {
        ForceManager.SetBoundaryOnlyMode(boundaryOnlyModeToggle.isOn);
    }

    // Sets a Boolean to let the program know that the user has agreed to intitial terms of use
    public void beginGame()
    {
        ForceManager.SetGameStarted(true);
    }
}

