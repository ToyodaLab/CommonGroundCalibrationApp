using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlythroughCamera : MonoBehaviour
{
    public float normalSpeed = 5.0f;
    public float fastSpeed = 15.0f;
    public float mouseSensitivity = 3.0f;
    public float climbSpeed = 5.0f;
    public float scrollSpeedMultiplier = 1f;

    private float currentSpeed;
    private bool active = true;

    void Update()
    {
        if (!active) return;

        // Enable look around when holding right mouse
        if (Input.GetMouseButton(1))
        {
            float rotX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float rotY = -Input.GetAxis("Mouse Y") * mouseSensitivity;
            transform.eulerAngles += new Vector3(rotY, rotX, 0);
        }

        // Speed control
        currentSpeed = Input.GetKey(KeyCode.LeftShift) ? fastSpeed : normalSpeed;
        currentSpeed += Input.GetAxis("Mouse ScrollWheel") * scrollSpeedMultiplier;

        // Movement
        Vector3 direction = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) direction += transform.forward;
        if (Input.GetKey(KeyCode.S)) direction -= transform.forward;
        if (Input.GetKey(KeyCode.A)) direction -= transform.right;
        if (Input.GetKey(KeyCode.D)) direction += transform.right;
        if (Input.GetKey(KeyCode.E)) direction += transform.up;
        if (Input.GetKey(KeyCode.Q)) direction -= transform.up;

        transform.position += direction.normalized * currentSpeed * Time.deltaTime;
    }

    public void SetActive(bool isActive)
    {
        active = isActive;
    }
}
