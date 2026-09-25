using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    public Camera targetCamera;
    public InteractionUI interactionUI;
    public float maxDistance = 4f;
    public Key interactKey = Key.E;

    public IInteractable Current { get; private set; }

    void Awake()
    {
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
    }

    void Update()
    {
        if (interactionUI == null)
            interactionUI = FindObjectOfType<InteractionUI>();

        IInteractable best = null;
        float bestDist = float.MaxValue;
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        Vector3 origin = cam != null ? cam.transform.position : transform.position;

        for (int i = 0; i < 3; i++)
        {
            Vector3 dir = cam != null ? cam.transform.forward : transform.forward;
            if (i > 0)
                dir = Quaternion.AngleAxis(-i * 8f, cam != null ? cam.transform.right : transform.right) * dir;

            RaycastHit hit;
            if (Physics.Raycast(origin, dir, out hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                IInteractable intr = hit.collider.GetComponentInParent<IInteractable>();
                if (intr != null && intr.CanInteract)
                {
                    float d = (hit.point - origin).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = intr;
                    }
                }
            }
        }

        Current = best;

        if (interactionUI != null)
        {
            if (Current != null)
                interactionUI.ShowPrompt(Current.InteractionPrompt);
            else
                interactionUI.HidePrompt();
        }

        if (Current != null && Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame)
        {
            Current.Interact(gameObject);
        }
    }
}