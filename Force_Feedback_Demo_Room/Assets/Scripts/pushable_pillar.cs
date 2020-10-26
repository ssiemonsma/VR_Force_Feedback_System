using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 *  This script initializes the pushable pillars to have a spring constant proportional to volume and mass.
 */

public class pushable_pillar : MonoBehaviour {
    Rigidbody rb;

	// Use this for initialization
	void Start() {
        rb = GetComponent<Rigidbody>();
        rb.SetDensity((float)40);
        SpringJoint spring_joint = GetComponent<SpringJoint>();
    }

    private void FixedUpdate()
    {
        Vector3 velocity = rb.velocity;
        if (velocity.z < -0.01)
        {
            velocity.z = -0.01f;
            rb.velocity = velocity;
        }
    }
}
