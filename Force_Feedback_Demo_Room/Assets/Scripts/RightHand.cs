using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ForceManager;
using System;

/*
 *  This script coordinates calculations related to the right hand.
 */

public class RightHand : MonoBehaviour { 
    private Rigidbody rb;
    public GameObject rightShoulder;

    // Debug HUDs
    public TextMesh rightBallForceHUD;
    public TextMesh rightCollisionForceHUD;
    public TextMesh rightBoundaryForceHUD;
    public TextMesh rightAngleHUD;
    public TextMesh rightRetractionHUD;
    public TextMesh rightCollisionPredictionHUD;

    // Use this for initialization    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        ForceManager.SetRightHandPreviousPosition(rb.position);
    }

    void OnCollisionEnter(Collision collision)
    {
        ForceManager.ClearRightForceLog();
        float rightForce = CalculateEffectiveForce(collision);
        ForceManager.RightCollisionForceUpdate(rightForce, true);
    }

    void OnCollisionStay(Collision collision)
    {
        float rightForce = CalculateEffectiveForce(collision);
        ForceManager.RightCollisionForceUpdate(rightForce, false);
    }

    void OnCollisionExit(Collision collision)
    {
        ForceManager.ClearRightForceLog();
        ForceManager.RightCollisionForceUpdate(0, true);
    }

    float CalculateEffectiveForce(Collision collision)
    {
        Vector3 shoulderToHand = Vector3.Normalize(gameObject.transform.position - rightShoulder.transform.position);
        return Vector3.Dot(shoulderToHand, collision.impulse) / Time.fixedDeltaTime;
    }

    public void updateRightForces()
    {
        rb = GetComponent<Rigidbody>();

        if (ForceManager.RightRetractionCheck(ref rightShoulder, rb.position))
        {
            rightRetractionHUD.text = "R. Retraction?: TRUE";
        }
        else
        {
            rightRetractionHUD.text = "R. Retraction?: FALSE";
        }

        ForceManager.RightBoundaryForceUpdate(ref rightShoulder, rb.position);

        // Debug HUD refreshing
        if (Time.fixedTime < ForceManager.GetHoldRightUntil())
        {
            rightCollisionPredictionHUD.text = "L.C.Pred.: TRUE";
        }
        else
        {
            rightCollisionPredictionHUD.text = "L.C.Pred.: FALSE";
        }

        rightBallForceHUD.text = "R. Ball Force: " + ForceManager.GetRightHandToBallForce().ToString("0.00") + "lbs";
        rightCollisionForceHUD.text = "R.D. Force: " + ForceManager.GetRightDominantForce() + "lbs";
        rightBoundaryForceHUD.text = "R.B. Force:" + ForceManager.GetRightBoundaryForce();
        rightAngleHUD.text = "R. Angle: " + ForceManager.GetRightAngle() + "°";
    }
}
