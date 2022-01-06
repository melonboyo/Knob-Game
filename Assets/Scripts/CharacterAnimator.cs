using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CharacterAnimator : MonoBehaviour {

    const float locomotionAnimationTimeSmoothTime = .1f;

    [SerializeField, Range(0f,100f)]
    float maxSpeed = 9f;

    Rigidbody body;
    Animator animator;

    void Start() {
        body = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update() {
        float speedPercent = body.velocity.magnitude / maxSpeed;
        animator.SetFloat("speedPercent", speedPercent, locomotionAnimationTimeSmoothTime, Time.deltaTime);
    }
}
