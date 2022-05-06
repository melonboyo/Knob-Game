using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class MainCamera : MonoBehaviour {

    static string TAG = "MAIN_CAMERA";

    // Public variables, tweakable in inspector 
	[SerializeField]
	Transform mainFocus = default;		// What area is in focus

    [SerializeField]
	Transform secondaryFocus = default;	// Whatever moves around in that area

	[SerializeField]
	Transform startingControlPoint = default;

	[SerializeField, Range(0f,0.5f)]
	float railTransitionRatio = 0.1f;

	[SerializeField, Range(0.1f,8f)]
	float transitionSpeed = 3f;

	/* [SerializeField]
	LayerMask obstructionMask = -1; */

    // Private variables
	Camera mainCamera;

    Vector3 mainFocusPoint;
	Vector3 secFocusPoint;
	List<Transform> controlPoints = new List<Transform>();
	List<Vector3> areaConstraints = new List<Vector3>(); 	// In the order: Left, Right, Up, Down
	Vector3 targetPos;

	float minVerticalAngle = 0f, maxVerticalAngle = 0f; 
	float minHorizontalAngle = 0f, maxHorizontalAngle = 0f;
	
	bool ignorePlayer, onRail, inTransition, hasAwoken;

	void Awake() {
		mainCamera = GetComponent<Camera>();
		if (startingControlPoint != null) {
			targetPos = startingControlPoint.position;
			controlPoints.Add(startingControlPoint);
		} else {
			targetPos = transform.position;
		}
		if (mainFocus != null) {
			mainFocusPoint = mainFocus.position;
		} else {
			mainFocusPoint = transform.position - Vector3.one;
		}
		if (secondaryFocus != null) {
			secFocusPoint = secondaryFocus.position;
		} else {
			ignorePlayer = true;
		}
		
		inTransition = true;
		hasAwoken = true;
        OnValidate();
	}

    void OnValidate() {
		if (maxVerticalAngle < minVerticalAngle) {
			maxVerticalAngle = minVerticalAngle;
		} 
		if (maxHorizontalAngle < minHorizontalAngle) {
			maxHorizontalAngle = minHorizontalAngle;
		} 
	}

	public void setFocus(Transform newMainFocus, List<Vector3> newAreaConstraints, Transform newControlPoint) {
		inTransition = true;
		mainFocusPoint = newMainFocus.position;
		areaConstraints = newAreaConstraints;
		controlPoints.Clear();
		controlPoints.Add(newControlPoint);
		targetPos = newControlPoint.position;
	}

	public void setFocus(Transform newMainFocus, List<Vector3> newAreaConstraints, List<Transform> newControlPoints, Vector3 startingPos) {
		inTransition = true;
		mainFocusPoint = newMainFocus.position;
		areaConstraints = newAreaConstraints;
		controlPoints = newControlPoints;
		targetPos = startingPos;
	}

    // Adjust camera position and rotation in LateUpdate, in case focus
    // position is moved in Update
	void LateUpdate() {
		Vector3 newPos = transform.position;
		Quaternion newRot = transform.rotation;

		float distToTarget = Vector3.Distance(targetPos, transform.position);
		if (distToTarget < 0.1f) {
			inTransition = false;
		}

		// Check if in transition state
		if (inTransition) {
			float t = transitionSpeed * Time.deltaTime;
			// Debug.Log(CustomDebug.Debug(TAG, "t: " + t));
			if (t > 0.04f) {
				t = 0.01f;
			}
			newPos = Vector3.Lerp(transform.position, targetPos, t);
			newRot = Quaternion.Lerp(transform.rotation, controlPoints[0].rotation, t);
		}

		transform.SetPositionAndRotation(newPos, newRot);
	}
}
