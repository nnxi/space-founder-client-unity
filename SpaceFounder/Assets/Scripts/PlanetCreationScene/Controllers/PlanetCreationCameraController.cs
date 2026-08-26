using UnityEngine;

public class PlanetCreationCameraController : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("중심을 돌 행성 오브젝트를 할당하거나 비워두면 이름으로 자동 검색합니다.")]
    public Transform targetPlanet;
    public string targetName = "Planet"; 

    [Header("Orbit Settings")]
    [SerializeField] private float orbitSpeed = 5f;
    [SerializeField] private float rotationSmoothness = 10f;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 10f;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private float minDistancePadding = 0.5f; // 행성 표면과 카메라 사이의 여유 거리

    private float currentDistance = 2f;
    private float currentRotationX;
    private float currentRotationY;
    private float targetRotationX;
    private float targetRotationY;
    private float dynamicMinDistance;

    private void Start()
    {
        if (targetPlanet == null)
        {
            GameObject foundObj = GameObject.Find(targetName);
            if (foundObj != null)
            {
                targetPlanet = foundObj.transform;
            }
        }

        Vector3 angles = transform.eulerAngles;
        targetRotationX = angles.y;
        targetRotationY = angles.x > 180 ? angles.x - 360 : angles.x;
        
        currentRotationX = targetRotationX;
        currentRotationY = targetRotationY;
    }

    private void Update()
    {
        if (targetPlanet == null) return;

        // 동적 최소 거리 계산: 행성의 로컬 스케일(x) 기반
        dynamicMinDistance = targetPlanet.localScale.x + minDistancePadding;

        // 마우스 스크롤 줌 입력 처리
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput != 0f)
        {
            currentDistance -= scrollInput * zoomSpeed;
        }

        // 거리 제한 적용
        currentDistance = Mathf.Clamp(currentDistance, dynamicMinDistance, maxDistance);

        // 일정한 속도로 X축 회전
        targetRotationX += orbitSpeed * Time.deltaTime;

        // 부드러운 회전 보간
        currentRotationX = Mathf.Lerp(currentRotationX, targetRotationX, rotationSmoothness * Time.deltaTime);
        currentRotationY = Mathf.Lerp(currentRotationY, targetRotationY, rotationSmoothness * Time.deltaTime);

        // 위치 및 회전 업데이트
        Quaternion rotation = Quaternion.Euler(currentRotationY, currentRotationX, 0f);
        Vector3 position = targetPlanet.position - (rotation * Vector3.forward * currentDistance);

        transform.rotation = rotation;
        transform.position = position;
    }
}