using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Follow Target")]
    [SerializeField] private Transform target;

    [Header("Follow Settings")]
    [Min(0.01f)]
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Map Bounds")]
    [SerializeField] private bool limitToMapBounds = true;
    [SerializeField] private Vector2 mapMin = new Vector2(-25f, -25f);
    [SerializeField] private Vector2 mapMax = new Vector2(25f, 25f);

    private Camera cameraComponent;
    private Vector3 velocity = Vector3.zero;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = new Vector3(
            target.position.x,
            target.position.y,
            transform.position.z
        );

        if (limitToMapBounds &&
            cameraComponent != null &&
            cameraComponent.orthographic)
        {
            targetPosition = ClampToMapBounds(targetPosition);
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            smoothTime
        );
    }

    private Vector3 ClampToMapBounds(Vector3 targetPosition)
    {
        float cameraHalfHeight = cameraComponent.orthographicSize;
        float cameraHalfWidth = cameraHalfHeight * cameraComponent.aspect;

        float minCameraX = mapMin.x + cameraHalfWidth;
        float maxCameraX = mapMax.x - cameraHalfWidth;
        float minCameraY = mapMin.y + cameraHalfHeight;
        float maxCameraY = mapMax.y - cameraHalfHeight;

        // 地图宽度小于相机视野宽度时，让相机固定在地图水平中心。
        if (minCameraX > maxCameraX)
        {
            targetPosition.x = (mapMin.x + mapMax.x) * 0.5f;
        }
        else
        {
            targetPosition.x = Mathf.Clamp(
                targetPosition.x,
                minCameraX,
                maxCameraX
            );
        }

        // 地图高度小于相机视野高度时，让相机固定在地图垂直中心。
        if (minCameraY > maxCameraY)
        {
            targetPosition.y = (mapMin.y + mapMax.y) * 0.5f;
        }
        else
        {
            targetPosition.y = Mathf.Clamp(
                targetPosition.y,
                minCameraY,
                maxCameraY
            );
        }

        return targetPosition;
    }
}