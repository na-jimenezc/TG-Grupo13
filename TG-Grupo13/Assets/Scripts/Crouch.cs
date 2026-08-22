using UnityEngine;

public class CrouchAwareController : MonoBehaviour
{
    [SerializeField] private CharacterController playerController;
    [SerializeField] private Transform cameraTransform;      // CenterEyeAnchor

    [SerializeField] private float minHeight = 0.4f;
    [SerializeField] private float maxHeight = 1.9f;
    [SerializeField] private float radius = 0.2f;

    void Update()
    {
        // Con Floor Level, la altura Y de la cámara YA es la altura real sobre el piso
        float headHeight = cameraTransform.position.y;
        float clampedHeight = Mathf.Clamp(headHeight, minHeight, maxHeight);

        playerController.height = clampedHeight;
        playerController.center = new Vector3(0f, clampedHeight / 2f, 0f);
        playerController.radius = radius;
    }
}