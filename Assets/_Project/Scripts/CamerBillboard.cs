using UnityEngine;

public class CameraBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            return;

        transform.rotation = targetCamera.transform.rotation;
    }
}