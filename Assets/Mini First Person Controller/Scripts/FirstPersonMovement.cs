using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonMovement : MonoBehaviour
{
    public float speed = 5;

    [Header("Running")]
    public bool canRun = true;
    public bool IsRunning { get; private set; }
    public float runSpeed = 9;
    public Key runningKey = Key.LeftShift;

    Rigidbody rigidbody;
    /// <summary> Functions to override movement speed. Will use the last added override. </summary>
    public List<System.Func<float>> speedOverrides = new List<System.Func<float>>();



    void Awake()
    {
        // Get the rigidbody on this.
        rigidbody = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (Keyboard.current == null) return;

        // Update IsRunning from input.
        IsRunning = canRun && Keyboard.current[runningKey].isPressed;

        // Get targetMovingSpeed.
        float targetMovingSpeed = IsRunning ? runSpeed : speed;
        if (speedOverrides.Count > 0)
        {
            targetMovingSpeed = speedOverrides[speedOverrides.Count - 1]();
        }

        // Get targetVelocity from input.
        Vector2 inputAxis = Vector2.zero;
        if (Keyboard.current.aKey.isPressed) inputAxis.x -= 1;
        if (Keyboard.current.dKey.isPressed) inputAxis.x += 1;
        if (Keyboard.current.sKey.isPressed) inputAxis.y -= 1;
        if (Keyboard.current.wKey.isPressed) inputAxis.y += 1;
        Vector2 targetVelocity = inputAxis * targetMovingSpeed;

        // Apply movement.
        rigidbody.linearVelocity = transform.rotation * new Vector3(targetVelocity.x, rigidbody.linearVelocity.y, targetVelocity.y);
    }
}