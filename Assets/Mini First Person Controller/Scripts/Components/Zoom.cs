using UnityEngine;
using UnityEngine.InputSystem;

[ExecuteInEditMode]
public class Zoom : MonoBehaviour
{
    Camera camera;
    public float defaultFOV = 60;
    public float maxZoomFOV = 15;
    [Range(0, 1)]
    public float currentZoom;
    public float sensitivity = 1;


    void Awake()
    {
        // Get the camera on this gameObject and the defaultZoom.
        camera = GetComponent<Camera>();
        if (camera && defaultFOV <= 0)
        {
            defaultFOV = camera.fieldOfView;
        }
    }

    void Update()
    {
        if (Mouse.current == null) return;

        // Update the currentZoom and the camera's fieldOfView.
        currentZoom += Mouse.current.scroll.ReadValue().y * sensitivity * .05f;
        currentZoom = Mathf.Clamp01(currentZoom);
        camera.fieldOfView = Mathf.Lerp(defaultFOV, maxZoomFOV, currentZoom);
    }
}
