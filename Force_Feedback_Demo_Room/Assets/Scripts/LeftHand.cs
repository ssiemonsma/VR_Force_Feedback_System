using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ForceManager;
using static UDPManager;
using System;
using UnityEngine.UI;

/*
 *  This script coordinates calculations related to the left hand.
 */

public class LeftHand : MonoBehaviour {
    private Rigidbody rb;
    public GameObject leftShoulder;

    // Debug HUDs
    public TextMesh leftBallForceHUD;
    public TextMesh leftCollisionForceHUD;
    public TextMesh leftBoundaryForceHUD;
    public TextMesh leftAngleHUD;
    public TextMesh leftRetractionHUD;
    public TextMesh leftCollisionPredictionHUD;

    // Use this for initialization    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        ForceManager.SetLeftHandPreviousPosition(rb.position);
    }

    void OnCollisionEnter(Collision collision)
    {
        ForceManager.ClearLeftForceLog();
        float leftForce = CalculateEffectiveForce(collision);
        ForceManager.LeftCollisionForceUpdate(leftForce, true);
    }

    void OnCollisionStay(Collision collision)
    {
        float leftForce = CalculateEffectiveForce(collision);
        ForceManager.LeftCollisionForceUpdate(leftForce, false);
    }

    void OnCollisionExit(Collision collision)
    {
        ForceManager.ClearLeftForceLog();
        ForceManager.LeftCollisionForceUpdate(0, true);
    }

    float CalculateEffectiveForce(Collision collision)
    {
        Vector3 shoulderToHand = Vector3.Normalize(gameObject.transform.position - leftShoulder.transform.position);
        return Vector3.Dot(shoulderToHand, collision.impulse)/Time.fixedDeltaTime;
    }

    public void updateLeftForces()
    {
        rb = GetComponent<Rigidbody>();

        if (ForceManager.LeftRetractionCheck(ref leftShoulder, rb.position))
        {
            leftRetractionHUD.text = "L. Retraction?: TRUE";
        }
        else
        {
            leftRetractionHUD.text = "L. Retraction?: FALSE";
        }
        
        ForceManager.LeftBoundaryForceUpdate(ref leftShoulder, rb.position);

        // Debug HUD refreshing
        if (Time.fixedTime < ForceManager.GetHoldLeftUntil())
        {
            leftCollisionPredictionHUD.text = "L.C.Pred.: TRUE";
        }
        else
        {
            leftCollisionPredictionHUD.text = "L.C.Pred.: FALSE";
        }

        leftBallForceHUD.text = "L. Ball Force: " + ForceManager.GetLeftHandToBallForce().ToString("0.00") + "lbs";
        leftCollisionForceHUD.text = "L.D. Force: " + ForceManager.GetLeftDominantForce().ToString("0.00") + "lbs";
        leftBoundaryForceHUD.text = "L.B. Force:" + ForceManager.GetLeftBoundaryForce().ToString("0.00");
        leftAngleHUD.text = "L. Angle: " + ForceManager.GetLeftAngle() + "°";
    }
}
