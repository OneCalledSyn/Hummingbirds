using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine;

// A hummingbird Machine Learning Agent

public class HummingbirdAgent : Agent
{
    [Tooltip("Force to apply when moving")]
    public float moveForce = 2f;

    [Tooltip("Speed to pitch up or down")]
    public float pitchSpeed = 100f;

    [Tooltip("Speed to rotate around the up axis")]
    public float yawSpeed = 100f;

    [Tooltip("Transform at the tip of the beak")]
    public Transform beakTip;

    [Tooltip("The agent's camera")]
    public Camera agentCamera;

    [Tooltip("Whether this is a training mode or gameplay mode")]
    public bool trainingMode;

    // Rigidbody of the agent
    new private Rigidbody rigidbody;

    // The flower area that the agent is in
    private FlowerArea flowerArea;

    // Nearest flower to the agent
    private Flower nearestFlower;

    // Allows for smoother pitch changes
    private float smoothPitchChange = 0f;

    // Allows for smoother yaw changes
    private float smoothYawChange = 0f;

    // Max angle agent can pitch
    private const float MaxPitchAngle = 80f;

    // Max distance from beak tip to accept nectar collision
    private const float BeakTipRadius = 0.008f;

    // Whether the agent is frozen (intentionally not flying)
    private bool frozen = false;

    // Amount of nectar earned during this episode
    public float NectarObtained {get; private set; }

    // Initialize the agent
    public override void Initialize()
    {
        rigidbody = GetComponent<Rigidbody>();
        flowerArea = GetComponentInParent<FlowerArea>();

        // Play indefinitely if not in training mode
        if (!trainingMode) MaxStep = 0;
    }

    // Reset the agent whenever a new episode starts
    public override void OnEpisodeBegin()
    {
        if (trainingMode)
        {
            // Only reset flowers in training when there is one agent per area
            flowerArea.ResetFlowers();
        }

        // Reset nectar earned
        NectarObtained = 0f;
        
        // Remove velocities from Agent so it isn't moving when the area resets
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;

        // Default to spawning in front of a flower
        bool inFrontOfFlower = true;
        if (trainingMode)
        {
            // Spawn in front of a flower 50% of the time during training
            inFrontOfFlower = UnityEngine.Random.value > .5f;
        }

        // Move the agent to a random new position
        MoveToSafeRandomPosition(inFrontOfFlower);

        // Find new nearest flower
        UpdateNearestFlower();
    }

    // Called when an action is received from either the player input or the NN
    // vectorAction[i]:
    // Index 0: move vector x (+1 = right, -1 = left)
    // Index 1: move vector y (+1 = up, -1 = down)
    // Index 2: move vector z (+1 = forward, -1 = backward)
    // Index 3: pitch angle (+1 = pitch up, -1 = pitch down)
    // Index 4: yaw angle (+1 = turn right, -1 = turn left)

    public override void OnActionReceived(float[] vectorAction)
    {
        // Don't take actions if frozen
        if (frozen) return;

        // Calculate movement vector
        Vector3 move = new Vector3(vectorAction[0], vectorAction[1], vectorAction[2]);

        // Add force in the direction of the move vector
        rigidbody.AddForce(move * moveForce);

        // Get current rotation
        Vector3 rotationVector = transform.rotation.eulerAngles;

        // Calculate pitch and yaw rotations
        float pitchChange = vectorAction[3];
        float yawChange = vectorAction[4];

        // Calculate smooth rotation changes
        smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
        smoothYawChange = Mathf.MoveTowards(smoothYawChange, yawChange, 2f * Time.fixedDeltaTime);

        // Calculate new pitch and yaw based on smoothed values
        // Clamp pitch to avoid flipping the agent upside down
        float pitch = rotationVector.x + smoothPitchChange * Time.fixedDeltaTime * pitchSpeed;
        if (pitch > 180f) pitch -=360f;
        pitch = Mathf.Clamp(pitch, -MaxPitchAngle, MaxPitchAngle);

        float yaw = rotationVector.y + smoothYawChange * Time.fixedDeltaTime * yawSpeed;

        // Apply new rotation
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    // Collect vector observations from the environment for the agent to make decisions
    public override void CollectObservations(VectorSensor sensor)
    {
        // If nearestFlower is null, return an array of 0 observations early to not brick the function
        if (nearestFlower == null)
        {
            sensor.AddObservation(new float[10]);
            return;
        }
        
        // Observe agent's local rotation (4 obs)
        sensor.AddObservation(transform.localRotation.normalized);

        // Get a vector from the beak tip to the nearest flower
        Vector3 toFlower = nearestFlower.FlowerCenterPosition - beakTip.position;

        // Observe a normalized vector pointing to the nearest flower (3 obs)
        sensor.AddObservation(toFlower.normalized);

        // Observe a dot product that indicates whether the beak tip is in front of the flower (1 obs)
        // +1 means beak tip is directly in front of the flower, -1 means directly behind
        sensor.AddObservation(Vector3.Dot(toFlower.normalized, -nearestFlower.FlowerUpVector.normalized));

        // Observe a dot product that indicates whether the beak tip is pointing at the flower (1 obs)
        // +1 means beak is pointing directly at flower, -1 means pointing directly away
        sensor.AddObservation(Vector3.Dot(beakTip.forward.normalized, -nearestFlower.FlowerUpVector.normalized));

        // Observe the relative distance from the beak tip to the flower (1 obs)
        sensor.AddObservation(toFlower.magnitude / FlowerArea.AreaDiameter);

        // 10 total observations
    }

    // When Behavior Type is set to "Heuristic Only" on the agent's Behavior Parameters
    // this function is called. It's return values will be fed in instead of using the NN

    public override void Heuristic(float[] actionsOut)
    {
        // Create placeholders for all movement and turning
        Vector3 forward = Vector3.zero;
        Vector3 left = Vector3.zero;
        Vector3 up = Vector3.zero;
        float pitch = 0f;
        float yaw = 0f;

        // Convert keyboard inputs to in game controls
        if (Input.GetKey(KeyCode.W)) forward = transform.forward;
        else if (Input.GetKey(KeyCode.S)) forward = -transform.forward;

        if (Input.GetKey(KeyCode.A)) left = -transform.right;
        else if (Input.GetKey(KeyCode.D)) left = transform.right;

        if (Input.GetKey(KeyCode.E)) up = transform.up;
        else if (Input.GetKey(KeyCode.C)) up = -transform.up;

        if (Input.GetKey(KeyCode.UpArrow)) pitch = -1f;
        else if (Input.GetKey(KeyCode.DownArrow)) pitch = 1f;

        if (Input.GetKey(KeyCode.LeftArrow)) yaw = -1f;
        else if (Input.GetKey(KeyCode.RightArrow)) yaw = 1f;

        // Combine movement vectors and normalize
        Vector3 combined = (forward + left + up).normalized;

        // Add motion values to array
        actionsOut[0] = combined.x;
        actionsOut[1] = combined.y;
        actionsOut[2] = combined.z;
        actionsOut[3] = pitch;
        actionsOut[4] = yaw;
    }

    // Prevent agent from moving or taking any actions
    public void FreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze not supported in training mode");
        frozen = true;
        rigidbody.Sleep();
    }

    public void UnfreezeAgent()
    {
        Debug.Assert(trainingMode = false, "Freeze/Unfreeze not supported in training mode");
        frozen = false;
        rigidbody.WakeUp();
    }

    // Move the agent to a safe random position
    // If in front of a flower, point beak at flower
    private void MoveToSafeRandomPosition(bool inFrontOfFlower)
    {
        bool safePositionFound = false;
        int attemptsRemaining = 100;
        Vector3 potentialPosition = Vector3.zero;
        Quaternion potentialRotation = new Quaternion();

        // Loop until a safe position is found or no attempts remain
        while(!safePositionFound && attemptsRemaining > 0)
        {
            attemptsRemaining--;
            if (inFrontOfFlower)
            {
                Flower randomFlower = flowerArea.Flowers[UnityEngine.Random.Range(0, flowerArea.Flowers.Count)];
                
                // Position 10-20cm in front of the flower
                float distanceFromFlower = UnityEngine.Random.Range(.1f, .2f);
                potentialPosition = randomFlower.transform.position + randomFlower.FlowerUpVector * distanceFromFlower;

                // Point beak at flower (bird's head is the center of the transform)
                Vector3 toFlower = randomFlower.FlowerCenterPosition - potentialPosition;
                potentialRotation = Quaternion.LookRotation(toFlower, Vector3.up);
            }

            else
            {
                // Pick a random height from the ground
                float height = UnityEngine.Random.Range(1.2f, 2.5f);

                // Pick a random radius from the center of the island
                float radius = UnityEngine.Random.Range(2f, 7f);

                // Pick a random direction rotated around the y axis
                Quaternion direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-180f, 180f), 0f);

                // Combine height, radius, and direction to pick a potential position
                potentialPosition = flowerArea.transform.position + Vector3.up * height + direction * Vector3.forward * radius;

                // Set random starting pitch and yaw
                float pitch = UnityEngine.Random.Range(-60f, 60f);
                float yaw = UnityEngine.Random.Range(-180f, 180f);
                potentialRotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            // Check to see if there is a collision between the agent and another object
            Collider[] colliders = Physics.OverlapSphere(potentialPosition, 0.05f);

            // No overlap = safety
            safePositionFound = colliders.Length == 0;
        }

        Debug.Assert(safePositionFound, "Could not find a safe position to spawn");

        // Set positoin and rotation if successful
        transform.position = potentialPosition;
        transform.rotation = potentialRotation;
    }

    private void UpdateNearestFlower()
    {
        foreach (Flower flower in flowerArea.Flowers)
        {
            if (nearestFlower == null && flower.HasNectar)
            {
                // No current nearest flower and this flower has nectar, set to this flower
                nearestFlower = flower;
            }

            else if (flower.HasNectar)
            {
                float distanceToFlower = Vector3.Distance(flower.transform.position, beakTip.position);
                float distanceToCurrentNearestFlower = Vector3.Distance(nearestFlower.transform.position, beakTip.position);

                // If current nearest flower is empty or this flower is closer, update 
                if (!nearestFlower.HasNectar || distanceToFlower < distanceToCurrentNearestFlower)
                {
                    nearestFlower = flower;
                }
            }
        }
    }

    // Called when the agent's collider enters a trigger collider
    private void OnTriggerEnter(Collider other)
    {
        TriggerEnterOrStay(other);

    }

    // Called when the agent's collider stays in a trigger collider
    private void OnTriggerStay(Collider other)
    {
        TriggerEnterOrStay(other);
        
    }


    private void TriggerEnterOrStay(Collider collider)
    {
        // Check if agent is colliding with nectar
        if (collider.CompareTag("nectar"))
        {
            Vector3 closestPointToBeakTip = collider.ClosestPoint(beakTip.position);

            // Check if the closest collision point is close to the tip of the agent's beak
            // Other kinds of collision should not see the agent rewarded
            if (Vector3.Distance(beakTip.position, closestPointToBeakTip) < BeakTipRadius)
            {
                // Look up flower for this nectar collider
                Flower flower = flowerArea.GetFlowerFromNectar(collider);

                // Try to take .01 units of nectar per unit time
                // 50 times per second in this case
                float nectarReceived = flower.Feed(.01f);

                // Keep track of the received nectar
                NectarObtained += nectarReceived;

                if (trainingMode)
                {
                    // Calculate reward for getting nectar
                    float bonus = .02f * Mathf.Clamp01(Vector3.Dot(transform.forward.normalized, -nearestFlower.FlowerUpVector.normalized));
                    AddReward(.01f + bonus);
                }

                // If flower is empty, update the nearest flower
                if (!flower.HasNectar)
                {
                    UpdateNearestFlower();
                }
            }
        }
    }

    // Called when the agent collides with a solid object
    private void OnCollisionEnter(Collision collision)
    {
        if (trainingMode && collision.collider.CompareTag("boundary"))
        {
            AddReward(-.5f);
        }
    }

    // Called every frame
    private void Update()
    {
        if (nearestFlower != null)
        {
            Debug.DrawLine(beakTip.position, nearestFlower.FlowerCenterPosition, Color.green);
        }
    }

    // Called every .02 seconds
    private void FixedUpdate()
    {
        if (nearestFlower != null && !nearestFlower.HasNectar)
        {
            // Avoids scenario where nearest flower is stolen by another hummingbird
            UpdateNearestFlower();
        }
    }
}
