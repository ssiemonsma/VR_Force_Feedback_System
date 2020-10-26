using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using static SendUDP;
using static UDPManager;



public static class ForceManager {
    // Constants
    private const int COLLISION = 0;
    private const int BOUNDARY = 1;
    public const int MAX_ANGLE = 135;
    private const int MAX_BOUNDARY_FORCE = 100;
    private const int NUM_AVERAGED = 5;
    private const float MIN_FORCE_INCREMENT = 3;
    private const float MIN_TIME_INCREMENT = 0.05f;
    public const float GLOBAL_FORCE_SCALER = 0.224809f;
    public const float BOUNDARY_PROXIMITY_LIMIT = 0.15f;
    public const float BOUNDARY_PROXIMITY_CLOSE = 0.5f;
    public const float BOUNDARY_HOLD_TIME = 0.5f;
    public const float BOUNDARY_PREDICTION_TIME = 0.4f;
    public const float BOUNDARY_PREDICTION_RANGE = 0.8f;
    public const float NUM_RETRACTION_AVGS = 10;
    private const float BALL_FORCE_SCALER = 10;
    public const float CEILING_PROXIMITY_LIMIT = 0.15f;
    public const float CEILING_PROXIMITY_CLOSE = 0.25f;
    public const float CEILING_PREDICTION_TIME = 0.4f;
    public const float CEILING_PREDICTION_RANGE = 0.8f;

    // Calibration data and other member variables
    private static int[] calibrationForces = {0, 5, 10, 15, 20, 25, 30, 40, 50, 60, 70, 80, 90, 100};
    // These calibration values correspond to ~0, 10, 20, 30, 40, 50, 60, 70, 80, 90, and 100 pounds of force
    private static int[] leftForceCalibrationAngles = {70, 109, 122, 126, 130, 131, 134, 135, 135, 135, 135, 135, 135, 135};
    private static int[] rightForceCalibrationAngles = {75, 97, 106, 110, 117, 122, 128, 135, 135, 135, 135, 135, 135, 135};
    private static Queue<float> leftCollisionForceLog = new Queue<float>();
    private static Queue<float> rightCollisionForceLog = new Queue<float>();
    private static float leftAvgCollisionForce = 0;
    private static float rightAvgCollisionForce = 0;
    private static float leftBoundaryForce = 0;
    private static float rightBoundaryForce = 0;
    private static float leftHandToBallForce = 0;
    private static float rightHandToBallForce = 0;
    private static float leftDominantForce = 0;
    private static float rightDominantForce = 0;
    private static int leftAngle = 0;
    private static int rightAngle = 0;
    private static float leftTimeLastUpdated = 0;
    private static float rightTimeLastUpdated = 0;
    private static float timeLastPacketSent = 0;
    private static bool leftForced = false;
    private static bool rightForced = false;
    private static int leftOrRightForced = 0;
    private static float holdLeftUntil = 0;
    private static float holdRightUntil = 0;
    private static bool leftHoldHere = false;
    private static bool rightHoldHere = false;
    private static Vector3 leftHandPreviousPosition;
    private static Vector3 rightHandPreviousPosition;
    private static bool leftBoundaryForceIsHeld = false;
    private static bool rightBoundaryForceIsHeld = false;
    private static bool leftBoundaryForceIsDominant = false;
    private static bool rightBoundaryForceIsDominant = false;
    private static bool leftIsRetracting = false;
    private static bool rightIsRetracting = false;
    private static int leftRetractingCount = 0;
    private static int rightRetractingCount = 0;
    private static Queue<float> leftRetractionLog = new Queue<float>();
    private static Queue<float> rightRetractionLog = new Queue<float>();
    private static float ceilingHeight = 2.3f;
    private static bool fullForceModeIsOn = false;
    private static bool boundaryOnlyModeIsOn = false;
    private static bool isFalling = false;
    private static bool gameStarted = false;
    private static Vector3 leftHandVelocity;
    private static Vector3 rightHandVelocity;


    // This is a function decides the collision force that should act on the left hand.
    public static float LeftCollisionForceUpdate(float leftForce, bool initialContactOrExit)
    {
        leftForced = initialContactOrExit;
        return CollisionForceUpdate(leftForce, ref leftCollisionForceLog, ref leftAvgCollisionForce);
    }

    // This is a function decides the collision force that should act on the right hand.
    public static float RightCollisionForceUpdate(float rightForce, bool initialContact)
    {
        rightForced = initialContact;
        return CollisionForceUpdate(rightForce, ref rightCollisionForceLog, ref rightAvgCollisionForce);
    }

    // This is a left/right agnostic function that decides the collision force that should act on a hand.
    public static float CollisionForceUpdate(float force, ref Queue<float> collisionForceLog, ref float avgCollisionForce)
    {
        while (collisionForceLog.Count >= NUM_AVERAGED)
        {
            collisionForceLog.Dequeue();
        }

        collisionForceLog.Enqueue(Math.Abs(force)*GLOBAL_FORCE_SCALER);

        avgCollisionForce = 0;
        foreach (float collisionForce in collisionForceLog)
        {
            avgCollisionForce += collisionForce;
        }
        avgCollisionForce /= collisionForceLog.Count;

        return avgCollisionForce;
    }

    //  This function decides what the boundary force acting the left hand should be.  It is close to an all-or-nothing response.
    public static float LeftBoundaryForceUpdate(ref GameObject shoulder, Vector3 currentHandPosition)
    {
        leftBoundaryForce = BoundaryForceUpdate(ref shoulder, currentHandPosition, ref leftHandPreviousPosition, true);
        return leftBoundaryForce;
    }

    //  This function decides what the boundary force acting the right hand should be.  It is close to an all-or-nothing response.
    public static float RightBoundaryForceUpdate(ref GameObject shoulder, Vector3 currentHandPosition)
    {
        rightBoundaryForce = BoundaryForceUpdate(ref shoulder, currentHandPosition, ref rightHandPreviousPosition, false);
        return rightBoundaryForce;
    }

    //  This function is left/right-agnostic and decides what the boundary force acting on a hand should be.  It is close to an all-or-nothing response.
    public static float BoundaryForceUpdate(ref GameObject shoulder, Vector3 currentHandPosition, ref Vector3 previousHandPosition, bool isLeftHand)
    {
        // Checking the distance from the shoulder to the boundary to get an idea of if the wall is within reach
        OVRPlugin.BoundaryTestResult boundaryResultShoulder = OVRPlugin.TestBoundaryPoint(shoulder.transform.position.ToVector3f(), OVRPlugin.BoundaryType.OuterBoundary);
        float shoulderDistToBoundary = boundaryResultShoulder.ClosestDistance;

        // Also checking that the ceiling is within reach
        float shoulderDistToCeiling = Math.Max(0, ceilingHeight - shoulder.transform.position.y);

        // Checking the distance from the hand to the boundary so that appropriate action can be taken based on proximity alone.
        // The ceiling boundary is not included in Oculus' Guardian system, so it must be calculated manually.
        float distToCeiling = Math.Max(0, ceilingHeight - currentHandPosition.y);
        float distToBoundary;
        OVRPlugin.BoundaryTestResult boundaryResult;
        if (isLeftHand)
        {
            boundaryResult = OVRPlugin.TestBoundaryNode(OVRPlugin.Node.HandLeft, OVRPlugin.BoundaryType.OuterBoundary);
            distToBoundary = boundaryResult.ClosestDistance;
            
        }
        else
        {
            boundaryResult = OVRPlugin.TestBoundaryNode(OVRPlugin.Node.HandRight, OVRPlugin.BoundaryType.OuterBoundary);
            distToBoundary = boundaryResult.ClosestDistance;
        }

        // Manually calculating velocity vector since the built-in velocity property did not work for this object
        Vector3 velocity = (currentHandPosition - previousHandPosition)/Time.fixedDeltaTime;

        // TESTING
        if (isLeftHand)
        {
            leftHandVelocity = velocity;
        }
        else
        {
            rightHandVelocity = velocity;
        }

        // The dot product with a unit vector pointing away to the nearest wall gives us the speed at which we are approaching the boundary
        Vector3 boundaryDirection = boundaryResult.ClosestPointNormal.FromVector3f();
        float speedToBoundary = Vector3.Dot(boundaryDirection, velocity);

        // We can also calculate the speed we are approaching the ceiling at.
        float speedToCeiling = velocity.y;

        // And now we can estimate the time it would take to reach the boundary at the current hand speed.
        float timeToBoundary = distToBoundary/speedToBoundary;

        // We can also calculate that time it would take to reach the ceiling at the current hand speed.
        float timeToCeiling = distToCeiling/speedToCeiling;

        float boundaryForce = 0;
        // If the hand is very close the to boundary, the servo engages resistance since it would otherwise be unable to intercept in time.
        // However, if the hand is retracting, we do not engage this resistance.
        if (distToBoundary < BOUNDARY_PROXIMITY_LIMIT || distToCeiling < CEILING_PROXIMITY_LIMIT)
        {
            if (isLeftHand && !leftIsRetracting)
            {
                boundaryForce = MAX_BOUNDARY_FORCE;
            }
            else if (!isLeftHand && !rightIsRetracting)
            {
                boundaryForce = MAX_BOUNDARY_FORCE;
            }
        }
        // If the hand is somewhat close to the boundary, the servo sets itself to an angle that registers basically no resistant (still ~half way through the angle range) in order to prepare itself to be able to quickly intervene
        else if (distToBoundary < BOUNDARY_PROXIMITY_CLOSE || distToCeiling < CEILING_PROXIMITY_CLOSE)
        {
            boundaryForce = 1;
        }

        // This condition predict if the current hand trajector could exit the play space in a short amount of time.
        // It also requires that both the shoulder and the hand are "within range" of the boundary.
        if ((timeToBoundary < BOUNDARY_PREDICTION_TIME && timeToBoundary > 0 && distToBoundary < BOUNDARY_PREDICTION_RANGE && shoulderDistToBoundary < BOUNDARY_PREDICTION_RANGE) || (timeToCeiling < CEILING_PREDICTION_TIME && timeToCeiling > 0 && distToCeiling < CEILING_PREDICTION_RANGE && shoulderDistToCeiling < CEILING_PREDICTION_RANGE))
        {
            boundaryForce = MAX_BOUNDARY_FORCE;
            if (isLeftHand)
            {
                ForceManager.HoldLeft(Time.fixedTime + BOUNDARY_HOLD_TIME, true);
            }
            else
            {
                ForceManager.HoldRight(Time.fixedTime + BOUNDARY_HOLD_TIME, true);
            }
        }

        // Update hand position to make sure the next time this function runs that the hand velocity is correct
        previousHandPosition = currentHandPosition;

        return boundaryForce;
    }

    // This checks if the left hand is rectracing towards the left shoulder (so that slack can be removed from the rope during this time)
    public static bool LeftRetractionCheck(ref GameObject shoulder, Vector3 currentHandPosition)
    {
        leftIsRetracting = RetractionCheck(ref shoulder, currentHandPosition, ref leftHandPreviousPosition, ref leftRetractionLog, true);
        return leftIsRetracting;
    }

    // This checks if the right hand is rectracing towards the right shoulder (so that slack can be removed from the rope during this time)
    public static bool RightRetractionCheck(ref GameObject shoulder, Vector3 currentHandPosition)
    {
        rightIsRetracting = RetractionCheck(ref shoulder, currentHandPosition, ref rightHandPreviousPosition, ref rightRetractionLog, false);
        return rightIsRetracting;
    }

    // This is a left/right-agnositic function that checks if a hand is retracting towards a shoulder
    public static bool RetractionCheck(ref GameObject shoulder, Vector3 currentHandPosition, ref Vector3 previousHandPosition, ref Queue<float> retractionLog, bool isLeftHand)
    {
        // Manually calculating velocity vector since the built-in property did not work for this object
        Vector3 handVelocity = (currentHandPosition - previousHandPosition) / Time.fixedDeltaTime;

        Vector3 handToShoulderVector = currentHandPosition - shoulder.transform.position;

        bool handIsRetracting = false;

        while (retractionLog.Count >= NUM_RETRACTION_AVGS)
        {
            retractionLog.Dequeue();
        }

        retractionLog.Enqueue(Vector3.Dot(handVelocity, handToShoulderVector));

        float avgRetractionSpeed = 0;
        foreach (float retractionSpeed in retractionLog)
        {
            avgRetractionSpeed += retractionSpeed;
        }
        avgRetractionSpeed /= retractionLog.Count;

        if (avgRetractionSpeed < -0.03)
        {
            handIsRetracting = true;
        }

        // If the hand is retracting, we will disengage an sort force "holds" on that hand
        if (handIsRetracting)
        {
            if (isLeftHand)
            {
                if (holdLeftUntil > Time.fixedTime)
                {
                    holdLeftUntil = Time.fixedTime;
                }
            }
            else
            {
                if (holdRightUntil > Time.fixedTime)
                {
                    holdRightUntil = Time.fixedTime;
                }
            }
        }

        return handIsRetracting;
    }

    // This makes decisions about what to include in the UDP packet sent to the Raspberry Pi that controls the servos.
    public static void SendPacket()
    {
        float currentTime = Time.fixedTime;

        leftBoundaryForceIsDominant = false;
        if (leftBoundaryForceIsHeld == true)
        {
            leftBoundaryForceIsDominant = true;
        }

        if (boundaryOnlyModeIsOn)
        {
            leftDominantForce = leftBoundaryForce;
            leftTimeLastUpdated = currentTime;
            leftForced = true;
            leftBoundaryForceIsDominant = true;
        }
        else if ((leftAvgCollisionForce == 0 && leftBoundaryForce == 0 && leftHandToBallForce == 0) || leftIsRetracting)
        {
            leftDominantForce = 0;
            leftTimeLastUpdated = currentTime;
            leftForced = true;
        }
        else if (leftAvgCollisionForce >= leftBoundaryForce && leftAvgCollisionForce >= leftHandToBallForce)
        {
            if ((Math.Abs(leftDominantForce - leftAvgCollisionForce) >= MIN_FORCE_INCREMENT && (currentTime - leftTimeLastUpdated) >= MIN_TIME_INCREMENT) || leftForced)
            {
                leftDominantForce = leftAvgCollisionForce;
                leftTimeLastUpdated = currentTime;
            }
        }
        else if (leftHandToBallForce >= leftBoundaryForce && leftHandToBallForce >= leftAvgCollisionForce)
        {
            if ((Math.Abs(leftDominantForce - leftHandToBallForce) >= MIN_FORCE_INCREMENT && (currentTime - leftTimeLastUpdated) >= MIN_TIME_INCREMENT) || leftForced)
            {
                leftDominantForce = leftHandToBallForce;
                leftTimeLastUpdated = currentTime;
            }
        }
        else
        {
            leftDominantForce = leftBoundaryForce;
            leftTimeLastUpdated = currentTime;
            leftForced = true;
            leftBoundaryForceIsDominant = true;
        }

        rightBoundaryForceIsDominant = false;
        if (rightBoundaryForceIsHeld == true)
        {
            rightBoundaryForceIsDominant = true;
        }

        if (boundaryOnlyModeIsOn)
        {
            rightDominantForce = rightBoundaryForce;
            rightTimeLastUpdated = currentTime;
            rightForced = true;
        }
        if ((rightAvgCollisionForce == 0 && rightBoundaryForce == 0 && rightHandToBallForce == 0) || rightIsRetracting)
        {
            rightDominantForce = 0;
            rightTimeLastUpdated = currentTime;
            rightForced = true;
            rightBoundaryForceIsDominant = true;
        }
        else if (rightAvgCollisionForce >= rightBoundaryForce && rightAvgCollisionForce >= rightHandToBallForce)
        {
            if ((Math.Abs(rightDominantForce - rightAvgCollisionForce) > MIN_FORCE_INCREMENT && (currentTime - rightTimeLastUpdated) >= MIN_TIME_INCREMENT) || rightForced)
            {
                rightDominantForce = rightAvgCollisionForce;
                rightTimeLastUpdated = currentTime;
            }
        }
        else if (rightHandToBallForce >= rightBoundaryForce && rightHandToBallForce >= rightAvgCollisionForce)
        {
            if ((Math.Abs(rightDominantForce - rightHandToBallForce) > MIN_FORCE_INCREMENT && (currentTime - rightTimeLastUpdated) >= MIN_TIME_INCREMENT) || rightForced)
            {
                rightDominantForce = rightHandToBallForce;
                rightTimeLastUpdated = currentTime;
            }
        }
        else
        {
            rightDominantForce = rightBoundaryForce;
            rightTimeLastUpdated = currentTime;
            rightForced = true;
        }

        if (!fullForceModeIsOn)
        {
            leftDominantForce = Math.Min(leftDominantForce, 20);
            rightDominantForce = Math.Min(rightDominantForce, 20);
        }

        if (!gameStarted)
        {
            leftDominantForce = 0;
            rightDominantForce = 0;
        }

        if (isFalling)
        {
            leftDominantForce = 0;
            rightDominantForce = 0;
            leftForced = true;
            rightForced = true;
        }


        if (leftForced && rightForced)
        {
            leftOrRightForced = 3;
        }
        else if (leftForced)
        {
            leftOrRightForced = 1;
        }
        else if (rightForced)
        {
            leftOrRightForced = 2;
        }
        else
        {
            leftOrRightForced = 0;
        }

        if (leftHoldHere)
        {
            leftAngle = ConvertForceToAngle(leftDominantForce, leftForceCalibrationAngles);
        }
        else if (currentTime >= holdLeftUntil)
        {
            leftAngle = ConvertForceToAngle(leftDominantForce, leftForceCalibrationAngles);
            leftBoundaryForceIsHeld = false;
        }

        if (rightHoldHere)
        {
            rightAngle = ConvertForceToAngle(rightDominantForce, rightForceCalibrationAngles);
        }
        else if (currentTime >= holdRightUntil)
        {
            rightAngle = ConvertForceToAngle(rightDominantForce, rightForceCalibrationAngles);
            rightBoundaryForceIsHeld = false;
        }

        // Update haptic feedback vibrations that alert about boundaries near the left hand
        if (leftBoundaryForceIsHeld)
        {
            OVRInput.SetControllerVibration(1, 1, OVRInput.Controller.LTouch);
        }
        else
        {
            OVRPlugin.BoundaryTestResult boundaryResultLeft = OVRPlugin.TestBoundaryNode(OVRPlugin.Node.HandLeft, OVRPlugin.BoundaryType.OuterBoundary);
            float distToBoundaryLeft = boundaryResultLeft.ClosestDistance;

            if (distToBoundaryLeft < BOUNDARY_PROXIMITY_CLOSE)
            {
                float vibrationAmount = Math.Max(Math.Min(1 - distToBoundaryLeft / BOUNDARY_PROXIMITY_CLOSE, 1), 0);
                OVRInput.SetControllerVibration(vibrationAmount, vibrationAmount, OVRInput.Controller.LTouch);
            }
            else
            {
                OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
            }
        }

        // Update haptic feedback vibrations that alert about boundaries near the right hand
        if (rightBoundaryForceIsHeld)
        {
            OVRInput.SetControllerVibration(1, 1, OVRInput.Controller.RTouch);
        }
        else
        {
            OVRPlugin.BoundaryTestResult boundaryResultRight = OVRPlugin.TestBoundaryNode(OVRPlugin.Node.HandRight, OVRPlugin.BoundaryType.OuterBoundary);
            float distToBoundaryRight = boundaryResultRight.ClosestDistance;

            if (distToBoundaryRight < BOUNDARY_PROXIMITY_CLOSE)
            {
                float vibrationAmount = Math.Max(Math.Min(1 - distToBoundaryRight/BOUNDARY_PROXIMITY_CLOSE, 1), 0);
                OVRInput.SetControllerVibration(vibrationAmount, vibrationAmount, OVRInput.Controller.RTouch);
            }
            else
            {
                OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
            }
        }


        if (UDPManager.GetIsConnected())
        {
            timeLastPacketSent = currentTime;
            UDPManager.SendFormattedPacket(leftAngle, rightAngle, leftOrRightForced, timeLastPacketSent);
        }
        //SendUDP.TransmitPacket(leftAngle, rightAngle, leftOrRightForced, timeLastPacketSent);

        // Reset booleans to flase for the next update interval
        leftForced = false;
        rightForced = false;
        leftHoldHere = false;
        rightHoldHere = false;
    }

    // This performs linear interpolation with the force calibration curves in order to convert an in-game force to a servo angle that closely approximates it.
    public static int ConvertForceToAngle(float scaledForce, int[] calibrationCurve)
    {
        int angle = 0;

        // If the force is very 0, we do not want to wast energy holding the servo at a position at the beginning of the calibration curve
        if (scaledForce > 0)
        {
            for (int i = 1; i < calibrationForces.Length; i++)
            {
                // Find the correct pair of values on the force calibration curves to interpolate between to find the best angle
                if (scaledForce <= calibrationForces[i])
                {
                    // Linear interpolation
                    angle = (int)Math.Round(calibrationCurve[i - 1] + (calibrationCurve[i] - calibrationCurve[i - 1]) * (scaledForce - calibrationForces[i - 1]) / (calibrationForces[i] - calibrationForces[i - 1]));
                    break;
                }
                // Choose the highest angle in the force calibration curve if the force is off the scale
                else if (i == calibrationForces.Length - 1)
                {
                    angle = calibrationCurve[i];
                }
            }
            angle = Math.Min(angle, MAX_ANGLE);    // Just in case.  The angles in the calibration curves should not be "off the scale"
        }
        return angle;
    }

    // This holds the left hand to hold at the current force until the release time is reached.
    public static void HoldLeft(float releaseTime, bool isBoundaryForceHold)
    {
        holdLeftUntil = releaseTime;
        leftHoldHere = true;
        leftBoundaryForceIsHeld = isBoundaryForceHold;
    }

    // This holds the right hand to hold at the current force until the release time is reached.
    public static void HoldRight(float releaseTime, bool isBoundaryForceHold)
    {
        holdRightUntil = releaseTime;
        rightHoldHere = true;
        rightBoundaryForceIsHeld = isBoundaryForceHold;
    }

    // A simmple function to clear the left collision force log after a collision is exited
    public static void ClearLeftForceLog() {
        leftCollisionForceLog.Clear();
    }

    // A simmple function to clear the right collision force log after a collision is exited
    public static void ClearRightForceLog()
    {
        rightCollisionForceLog.Clear();
    }

    // Below this line are some simple get and set functions for use by functions outside of this class

    public static int GetLeftAngle()
    {
        return leftAngle;
    }

    public static int GetRightAngle()
    {
        return rightAngle;
    }

    public static float GetLeftDominantForce()
    {
        return leftDominantForce;
    }

    public static float GetRightDominantForce()
    {
        return rightDominantForce;
    }

    public static float GetLeftAvgCollisionForce()
    {
        return leftAvgCollisionForce;
    }

    public static float GetRightAvgCollisionForce()
    {
        return rightAvgCollisionForce;
    }

    public static float GetLeftBoundaryForce()
    {
        return leftBoundaryForce;
    }

    public static float GetRightBoundaryForce()
    {
        return rightBoundaryForce;
    }

    public static Vector3 GetLeftHandPreviousPosition()
    {
        return leftHandPreviousPosition;
    }

    public static Vector3 GetRightHandPreviousPosition()
    {
        return rightHandPreviousPosition;
    }

    public static void SetLeftHandPreviousPosition(Vector3 position)
    {
        leftHandPreviousPosition = position;
    }

    public static void SetRightHandPreviousPosition(Vector3 position)
    {
        rightHandPreviousPosition = position;
    }

    public static int GetLeftRetractingCount()
    {
        return leftRetractingCount;
    }

    public static int GetRightRetractingCount()
    {
        return rightRetractingCount;
    }

    public static float GetHoldLeftUntil()
    {
        return holdLeftUntil;
    }

    public static float GetHoldRightUntil()
    {
        return holdRightUntil;
    }

    public static Vector3 GetLeftHandVelocity()
    {
        return leftHandVelocity;
    }

    public static Vector3 GetRightHandVelocity()
    {
        return rightHandVelocity;
    }

    public static float GetLeftHandToBallForce()
    {
        return leftHandToBallForce;
    }

    public static float GetRightHandToBallForce()
    {
        return rightHandToBallForce;
    }

    public static void SetLeftHandToBallForce(float unscaledLeftHandToBallForce)
    {
        leftHandToBallForce = unscaledLeftHandToBallForce * BALL_FORCE_SCALER;
    }

    public static void SetRightHandToBallForce(float unscaledRightHandToBallForce)
    {
        rightHandToBallForce = unscaledRightHandToBallForce * BALL_FORCE_SCALER;
    }

    public static void SetCeilingHeight(float heightInMeters)
    {
        ceilingHeight = heightInMeters;
    }

    public static float GetCeilingHeight()
    {
        return ceilingHeight;
    }

    public static void SetFullForceMode(bool fullForceMode)
    {
        fullForceModeIsOn = fullForceMode;
    }

    public static void SetBoundaryOnlyMode(bool boundaryOnlyMode)
    {
        boundaryOnlyModeIsOn = boundaryOnlyMode;
    }

    public static bool GetBoundaryOnlyMode()
    {
        return boundaryOnlyModeIsOn;
    }

    public static void SetGameStarted(bool gameIsStarted)
    {
        gameStarted = gameIsStarted;
    }

    public static bool GetGameStarted()
    {
        return gameStarted;
    }

    public static void SetIsFalling(bool isUserFalling)
    {
        isFalling = isUserFalling;
    }

    public static bool GetIsFalling()
    {
        return isFalling;
    }
}