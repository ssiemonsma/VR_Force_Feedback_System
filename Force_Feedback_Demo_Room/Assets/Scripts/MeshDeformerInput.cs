using UnityEngine;
using static ForceManager;

/*
 *  This script is a heavily modified version of a script from the deformable ball tutorial that 
 *  originally took a mouse click as input.  Now it takes hand velocity vectors as input so that 
 *  we can create "proximity-based" forces for our system.
 */

public class MeshDeformerInput : MonoBehaviour {
	public float ballForceScaler = 10f;
	public float forceOffset = 0.1f;
	
	void FixedUpdate () {
        HandleInputLeft();
        HandleInputRight();
    }

    void HandleInputLeft() {
        Vector3 ballPosition = GameObject.Find("DeformableBall").transform.position;
        float leftHandDistanceToBall = (ballPosition - ForceManager.GetLeftHandPreviousPosition()).magnitude - 0.5f;
        Vector3 leftHandVelocity = ForceManager.GetLeftHandVelocity();
        Vector3 leftHandPosition = ForceManager.GetLeftHandPreviousPosition();
        float leftHandToBallForce;
        if (!ForceManager.GetGameStarted() ||ForceManager.GetBoundaryOnlyMode())
        {
            leftHandToBallForce = 0;
        }
        else if (leftHandDistanceToBall > 0 && leftHandDistanceToBall <= 1.0)
        {
            leftHandToBallForce = HandleInput(leftHandDistanceToBall, leftHandPosition, leftHandVelocity);
        }
        else
        {
            leftHandToBallForce = 0;
        }
        ForceManager.SetLeftHandToBallForce(leftHandToBallForce);
    }

    void HandleInputRight()
    {
        Vector3 ballPosition = GameObject.Find("DeformableBall").transform.position;
        float rightHandDistanceToBall = (ballPosition - ForceManager.GetRightHandPreviousPosition()).magnitude - 0.5f;
        Vector3 rightHandVelocity = ForceManager.GetRightHandVelocity();
        Vector3 rightHandPosition = ForceManager.GetRightHandPreviousPosition();
        float rightHandToBallForce;
        if (!ForceManager.GetGameStarted() || ForceManager.GetBoundaryOnlyMode())
        {
            rightHandToBallForce = 0;
        }
        else if (rightHandDistanceToBall > 0 && rightHandDistanceToBall <= 1.0)
        {
            rightHandToBallForce = HandleInput(rightHandDistanceToBall, rightHandPosition, rightHandVelocity);
        }
        else
        {
            rightHandToBallForce = 0;
        }
        ForceManager.SetRightHandToBallForce(rightHandToBallForce);
    }

    float HandleInput(float handDistanceToBall, Vector3 handPosition, Vector3 handVelocity)
    {
        RaycastHit hit;
        float handToBallForce = ballForceScaler*(1 - handDistanceToBall)*handVelocity.magnitude;
        if (Physics.SphereCast(handPosition, 0.04f, handVelocity, out hit, 1.0f))
        {
            MeshDeformer deformer = hit.collider.GetComponent<MeshDeformer>();

            if (deformer)
            {
                Vector3 point = hit.point;
                point += hit.normal * forceOffset;
                deformer.AddDeformingForce(point, handToBallForce);
            }
        }

        return ballForceScaler*(1 - handDistanceToBall);  // Since velocity changes so rapidly, the force sent to the Raspberry Pi will depend only on distance
    }
}