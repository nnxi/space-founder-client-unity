using UnityEngine;

public class LoginCameraController : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("중심을 돌 행성 오브젝트를 할당하거나 비워두면 이름으로 자동 검색합니다.")]
    public Transform targetPlanet;
    public string targetName = "Planet"; // 하이라키에 생성되는 행성 이름

    [Header("Orbit Settings")]
    [SerializeField] private float orbitSpeed = 15f;
    [SerializeField] private float currentDistance = 10f;
    [SerializeField] private float rotationSmoothness = 10f;

    [Header("Zoom Settings (Optional)")]
    [SerializeField] private float zoomSpeed = 10f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 30f;

    private float currentRotationX;
    private float currentRotationY;
    private float targetRotationX;
    private float targetRotationY;

    private void Start()
    {
        // 1. 타겟이 안 들어가 있다면 하이라키에서 이름으로 찾기
        if (targetPlanet == null)
        {
            GameObject foundObj = GameObject.Find(targetName);
            if (foundObj != null)
            {
                targetPlanet = foundObj.transform;
            }
        }

        // 2. 초기 각도 세팅 (카메라가 바라보는 방향 기준)
        Vector3 angles = transform.eulerAngles;
        targetRotationX = angles.y;
        targetRotationY = angles.x > 180 ? angles.x - 360 : angles.x;
        
        currentRotationX = targetRotationX;
        currentRotationY = targetRotationY;
    }

    private void Update()
    {
        if (targetPlanet == null) return;

        // 항상 일정한 속도로 X축 회전 (Orbit)
        targetRotationX += orbitSpeed * Time.deltaTime;

        // 부드러운 회전 보간
        currentRotationX = Mathf.Lerp(currentRotationX, targetRotationX, rotationSmoothness * Time.deltaTime);
        currentRotationY = Mathf.Lerp(currentRotationY, targetRotationY, rotationSmoothness * Time.deltaTime);

        // 3. 계산된 각도와 거리를 바탕으로 카메라 위치/회전 업데이트
        Quaternion rotation = Quaternion.Euler(currentRotationY, currentRotationX, 0f);
        Vector3 position = targetPlanet.position - (rotation * Vector3.forward * currentDistance);

        transform.rotation = rotation;
        transform.position = position;
    }
}