using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class PlayerCamera : MonoBehaviour {

    static string TAG = "PLAYER_CAMERA";

    // Public variables, tweakable in inspector 
    [SerializeField]
	Transform focus = default;

	[SerializeField, Range(1f, 20f)]
	float distance = 5f;

    [SerializeField, Min(0f)]
	float focusRadius = 1f;

    [SerializeField, Range(0f, 1f)]
	float focusCentering = 0.5f;

    [SerializeField, Range(1f, 360f)]
	float rotationSpeed = 90f;

    [SerializeField, Range(-89f, 89f)]
	float minVerticalAngle = -15f, maxVerticalAngle = 70f;

	[SerializeField, Min(0f)]
	float upAlignmentSpeed = 360f;

	[SerializeField]
    bool invertY, invertX;

	[SerializeField]
	LayerMask obstructionMask = -1;

    // Private variables
	InputAction lookAction;
	Camera regularCamera;

    Vector3 focusPoint;
    Vector2 orbitAngles = new Vector2(45f, 0f);
	Quaternion gravityAlignment = Quaternion.identity;
	Quaternion orbitRotation;

	void Start() {
        var map = new InputActionMap("Camera");

        lookAction = map.AddAction("look", binding: "<Mouse>/delta");

        lookAction.AddBinding("<Gamepad>/rightStick")
			.WithProcessor("scaleVector2(x=5, y=5)");

        lookAction.Enable();
	}

	void Awake() {
		regularCamera = GetComponent<Camera>();
		focusPoint = focus.position;
		transform.localRotation = orbitRotation = Quaternion.Euler(orbitAngles);
        OnValidate();
	}

    void OnValidate() {
		if (maxVerticalAngle < minVerticalAngle) {
			maxVerticalAngle = minVerticalAngle;
		}
	}

    // Adjust camera position in late update, in case something focus
    // position is moved in update
	void LateUpdate() {
		UpdateGravityAlignment();
		UpdateFocusPoint();

        if (ManualRotation()) {
			ConstrainAngles();
			orbitRotation = Quaternion.Euler(orbitAngles);
		}
		Quaternion lookRotation = gravityAlignment * orbitRotation;

		Vector3 lookDirection = lookRotation * Vector3.forward;
		Vector3 lookPosition = focusPoint - lookDirection * distance;

		Vector3 rectOffset = lookDirection * regularCamera.nearClipPlane;
		Vector3 rectPosition = lookPosition + rectOffset;
		Vector3 castFrom = focus.position;
		Vector3 castLine = rectPosition - castFrom;
		float castDistance = castLine.magnitude;
		Vector3 castDirection = castLine / castDistance;

		if (Physics.BoxCast(
			castFrom, CameraHalfExtents, castDirection, out RaycastHit hit,
			lookRotation, castDistance, obstructionMask
		)) {
			rectPosition = castFrom + castDirection * hit.distance;
			lookPosition = rectPosition - rectOffset;
		}

		transform.SetPositionAndRotation(lookPosition, lookRotation);
	}

	void UpdateGravityAlignment() {
		Vector3 fromUp = gravityAlignment * Vector3.up;
		Vector3 toUp = CustomGravity.GetUpAxis(focusPoint);
		float dot = Mathf.Clamp(Vector3.Dot(fromUp, toUp), -1f, 1f);
		float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
		float maxAngle = upAlignmentSpeed * Time.deltaTime;

		Quaternion newAlignment =
			Quaternion.FromToRotation(fromUp, toUp) * gravityAlignment;
		if (angle <= maxAngle) {
			gravityAlignment = newAlignment;
		} else {
			gravityAlignment = Quaternion.SlerpUnclamped(
				gravityAlignment, newAlignment, maxAngle / angle
			);
		}
	}

	void UpdateFocusPoint() {
		Vector3 targetPoint = focus.position;
		if (focusRadius > 0f) {
			float distance = Vector3.Distance(targetPoint, focusPoint);
			float t = 1f;
			if (distance > 0.01f && focusCentering > 0f) {
				t = Mathf.Pow(1f - focusCentering, Time.unscaledDeltaTime);
			}
			if (distance > focusRadius) {
				t = Mathf.Min(t, focusRadius / distance);
			}
			focusPoint = Vector3.Lerp(targetPoint, focusPoint, t);
		}
		else {
			focusPoint = targetPoint;
		}
	}

    bool ManualRotation() {
		float lookX, lookY;
		if (!invertX) {
			lookX = lookAction.ReadValue<Vector2>().y;
		} else {
			lookX = -lookAction.ReadValue<Vector2>().y;
		}
		if (!invertY) {
			lookY = -lookAction.ReadValue<Vector2>().x;
		} else {
			lookY = lookAction.ReadValue<Vector2>().x;
		}
		Vector2 lookInput = new Vector2(lookX, lookY);
		const float e = 0.001f;
		if (lookInput.x < -e || lookInput.x > e || lookInput.y < -e || lookInput.y > e) {
			orbitAngles += rotationSpeed * Time.unscaledDeltaTime * lookInput;
			return true;
		}
		return false;
	}

    void ConstrainAngles() {
		orbitAngles.x =
			Mathf.Clamp(orbitAngles.x, minVerticalAngle, maxVerticalAngle);

		if (orbitAngles.y < 0f) {
			orbitAngles.y += 360f;
		}
		else if (orbitAngles.y >= 360f) {
			orbitAngles.y -= 360f;
		}
	}

	Vector3 CameraHalfExtents {
		get {
			Vector3 halfExtents;
			halfExtents.y =
				regularCamera.nearClipPlane *
				Mathf.Tan(0.5f * Mathf.Deg2Rad * regularCamera.fieldOfView);
			halfExtents.x = halfExtents.y * regularCamera.aspect;
			halfExtents.z = 0f;
			return halfExtents;
		}
	}
}
