using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour {

    static string TAG = "PLAYER_CONTROLLER";
    static string TAG1 = "INPUT";

    // Public variables, tweakable in inspector 
    [SerializeField, Range(0f, 100f)] 
    float maxSpeed = 10f;

    [SerializeField, Range(0f, 100f)] 
    float maxAcceleration = 10f, maxAirAcceleration = 1f;

    [SerializeField, Range(0f, 90f)]
	float maxGroundAngle = 40f;

    [SerializeField, Range(0f, 10f)] 
    float jumpHeight = 1.6f;

    [SerializeField, Range(0f, 100f)] 
    float maxSnapSpeed = 60f;

    [SerializeField, Range(0f, 100f)]
    float maxFallSpeed = 30f;

    [SerializeField, Min(0f)]
	float probeDistance = 1f;

    [SerializeField]
	LayerMask probeMask = -1;

    [SerializeField]
	Transform playerInputSpace = default;

    [SerializeField]
    bool useCustomGravity = false;

    // Private variables
    InputAction moveAction, jumpAction;
    Rigidbody body;

    Vector3 upAxis, rightAxis, forwardAxis;
    Vector3 velocity, desiredVelocity;
    Vector3 contactNormal, steepNormal;
    Quaternion rotation;
    float minGroundDotProduct, fallVelocity;
    int groundContactCount, steepContactCount;
    int stepsSinceLastGrounded, stepsSinceLastJump;
    bool desiredJump, stopJump, jumping;
    bool hasMoved;

    // Readonly properties
    bool OnGround => groundContactCount > 0;
    bool OnSteep => steepContactCount > 0;

    void Start() {
        var gamepad = Gamepad.current;
        if (gamepad == null) {
            Debug.Log(CustomDebug.Debug(TAG1, "No gamepad connected!"));
        }

        var map = new InputActionMap("Player Controller");

        // Create actions
        moveAction = map.AddAction("move", binding: "<Gamepad>/leftStick");
        jumpAction = map.AddAction("jump", binding: "<Gamepad>/buttonSouth");

        // Make extra bindings for the actions
        moveAction.AddCompositeBinding("Dpad")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
        jumpAction.AddBinding("<Keyboard>/space");

        // Enable the action mappings
        moveAction.Enable();
        jumpAction.Enable();
    }

    void Awake() {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        OnValidate();
    }

    void OnValidate() {
		minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
	}

    void Update() {
        // Get the move input and normalize it with clamp
        Vector2 playerInput = moveAction.ReadValue<Vector2>();
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);

        // Get the input space dependent axes
		if (playerInputSpace) {
			rightAxis = ProjectDirectionOnPlane(playerInputSpace.right, upAxis);
			forwardAxis =
				ProjectDirectionOnPlane(playerInputSpace.forward, upAxis);
		} else {
			rightAxis = ProjectDirectionOnPlane(Vector3.right, upAxis);
			forwardAxis = ProjectDirectionOnPlane(Vector3.forward, upAxis);
		}
        // Set the desired velocity to accelerate towards
		desiredVelocity =
			new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;

        // Get the jump input
        jumpAction.performed +=
            ctx => desiredJump = true;

        jumpAction.canceled +=
            ctx => stopJump = true;
	}

    void FixedUpdate() {
        Vector3 gravity;
        if (useCustomGravity) {
            gravity = CustomGravity.GetGravity(body.position, out upAxis);
        } else {
            gravity = Physics.gravity;
            upAxis = Vector3.up;
        }
        
		UpdateState();
        AdjustVelocity();

        if (desiredJump) {
			desiredJump = false;
			Jump(gravity);
		}

        if ((!OnGround || stepsSinceLastJump == 0)
            && fallVelocity < maxFallSpeed) {
            velocity += gravity * Time.deltaTime;
            fallVelocity = Vector3.Dot(velocity, -upAxis);
        }

        if (stopJump) {
            stopJump = false;
            StopJump();
        }

        AdjustRotation();

    	body.velocity = velocity;

        body.MoveRotation(rotation);

        ClearState();
	}

    void UpdateState () {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
		velocity = body.velocity;
        rotation = body.rotation;
        fallVelocity = Vector3.Dot(velocity, -upAxis);
		if (OnGround || SnapToGround() || CheckSteepContacts()) {
            stepsSinceLastGrounded = 0;
            if (groundContactCount > 1) {
                contactNormal.Normalize();
            }
            if (stepsSinceLastJump > 1) {
                jumping = false;
            }
        } else {
			contactNormal = upAxis;
		}
	}

    void ClearState () {
        groundContactCount = steepContactCount = 0;
		contactNormal = steepNormal = Vector3.zero;
	}

    void AdjustVelocity () {
		Vector3 xAxis = ProjectDirectionOnPlane(rightAxis, contactNormal);
		Vector3 zAxis = ProjectDirectionOnPlane(forwardAxis, contactNormal);

        float currentX = Vector3.Dot(velocity, xAxis);
		float currentZ = Vector3.Dot(velocity, zAxis);

        float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
		float maxSpeedChange = acceleration * Time.deltaTime;
        
        float newX =
            Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
        float newZ =
            Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);

        velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
	}

    void Jump(Vector3 gravity) {
        // Only jump if on ground
        if (OnGround) {
            jumping = true;
            Vector3 jumpDirection = contactNormal;
            jumpDirection = (jumpDirection + upAxis).normalized;

            // Calculate jump speed from jump height
			float jumpSpeed = Mathf.Sqrt(2f * gravity.magnitude * jumpHeight);
            stepsSinceLastJump = 0;

            // Limit jump speed
            float alignedSpeed = Vector3.Dot(velocity, jumpDirection);
		    jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);

            Debug.Log(CustomDebug.Debug(TAG1, "Jump speed: " + jumpSpeed));

            velocity += jumpDirection * jumpSpeed;
		}
    }

    void StopJump() {
        if (jumping && fallVelocity < 0f) {
            Debug.Log(CustomDebug.Debug(TAG, "Stop Jump action"));
            jumping = false;
            velocity += upAxis * fallVelocity * 0.5f;
        }
    }

    void AdjustRotation() {
        Quaternion newRotation;
        Vector3 facingDirection = 
            ProjectDirectionOnPlane(velocity, contactNormal);
        if (facingDirection == Vector3.zero) {
            facingDirection = ProjectDirectionOnPlane(body.transform.forward, contactNormal);
        }
        newRotation = Quaternion.LookRotation(facingDirection, contactNormal);
        rotation = Quaternion.Slerp(rotation, newRotation, 0.08f);
    }

    Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal) {
		return (direction - normal * Vector3.Dot(direction, normal)).normalized;
	}

    void OnCollisionEnter(Collision collision) {
		EvaluateCollision(collision);
	}

	void OnCollisionStay(Collision collision) {
		EvaluateCollision(collision);
	}

    void EvaluateCollision(Collision collision) {
        for (int i = 0; i < collision.contactCount; i++) {
			Vector3 normal = collision.GetContact(i).normal;
            float upDot = Vector3.Dot(upAxis, normal);
			if (upDot >= minGroundDotProduct) {
				groundContactCount += 1;
				contactNormal += normal;
			} else if (upDot > -0.01f) {
				steepContactCount += 1;
				steepNormal += normal;
			}
		}
    }

    bool CheckSteepContacts() {
		if (steepContactCount > 1) {
			steepNormal.Normalize();
			if (steepNormal.y >= minGroundDotProduct) {
                steepContactCount = 0;
				groundContactCount = 1;
				contactNormal = steepNormal;
				return true;
			}
		}
		return false;
	}

    bool SnapToGround() {
        if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2) {
			return false;
		} 
        float speed = velocity.magnitude;
		if (speed > maxSnapSpeed) {
			return false;
		}
        if (!Physics.Raycast(
            body.position, -upAxis, out RaycastHit hit, probeDistance, probeMask
        )) {
			return false;
		} 
		float upDot = Vector3.Dot(upAxis, hit.normal);
		if (upDot < minGroundDotProduct) {
			return false;
		}

        groundContactCount = 1;
		contactNormal = hit.normal;
        float dot = Vector3.Dot(velocity, hit.normal);
        if (dot > 0f) {
			velocity = (velocity - hit.normal * dot).normalized * speed;
		}
		return true;
	}

    void OnDrawGizmos() {
        Gizmos.color = Color.red; 
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2.5f);   
    }
}
