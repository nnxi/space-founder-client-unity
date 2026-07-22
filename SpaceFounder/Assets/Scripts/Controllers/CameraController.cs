using UnityEngine;

public enum CameraMode
{
    Follow,
    Orbit,
    Free
}

public class CameraController : MonoBehaviour
{
    [Header("Mode Settings")]
    public CameraMode currentMode = CameraMode.Follow;
    [SerializeField] private float orbitSpeed = 15f; // Orbit 모드 시 자동 회전 속도

    [Header("Speed Settings")]
    [SerializeField] private float normalSpeed = 50f;
    [SerializeField] private float boostMultiplier = 3f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float zoomSpeed = 500f;

    [Header("Smooth Settings")]
    [SerializeField] private float movementSmoothness = 3.5f;
    [SerializeField] private float rotationSmoothness = 10f;

    [Header("Focus & Zoom Settings")]
    [SerializeField] private float initialDistance = 10f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 70f;

    private Transform targetPlanet;
    private float currentDistance;

    private Vector3 targetVelocity;
    private Vector3 currentVelocity;

    private float targetRotationX;
    private float targetRotationY;
    private float currentRotationX;
    private float currentRotationY;

    private bool hasFocusedOnMyPlanet = false;

    private void Start()
    {
        currentDistance = initialDistance;
        SyncRotationVariables();
    }

    private void Update()
    {
        if (!hasFocusedOnMyPlanet)
        {
            TryFocusOnMyPlanet();
            return;
        }

        HandleModeSwitch();

        if (currentMode == CameraMode.Free)
        {
            HandleFreeRotation();
            HandleFreeMovement();
        }
        else
        {
            HandleOrbitalMovement();
        }
    }

    private void TryFocusOnMyPlanet()
    {
        if (WorldManager.Instance.MyPlanet == null) return;

        GameObject myPlanet = WorldManager.Instance.MyPlanet;
        
        Vector3 planetPos = myPlanet.transform.position;
        Vector3Int currentSector = WorldManager.Instance.CurrentCameraSector;

        // 유니티 스케일이 적용된 섹터 크기 (100000 * 0.01 = 1000f)
        float scaledSectorSize = 1000f;

        // 섹터의 중심 좌표 계산
        Vector3 sectorCenter = new Vector3(
            currentSector.x * scaledSectorSize,
            currentSector.y * scaledSectorSize,
            currentSector.z * scaledSectorSize
        );

        // 행성 위치에서 섹터 중심을 향하는 방향 벡터 계산
        Vector3 directionToCenter = (sectorCenter - planetPos).normalized;

        // 행성이 정확히 섹터 중심에 위치해 방향 벡터가 0이 될 경우의 방어 로직
        if (directionToCenter.sqrMagnitude == 0f)
        {
            directionToCenter = Vector3.one.normalized;
        }

        // 카메라가 행성을 바라봐야 하므로 시선 방향은 반대(-directionToCenter)
        Quaternion lookRotation = Quaternion.LookRotation(-directionToCenter);

        targetPlanet = myPlanet.transform;
        
        // 수동으로 좌표를 지정하는 대신, 계산된 방향을 오르빗 카메라의 회전값으로 변환
        targetRotationX = lookRotation.eulerAngles.y;
        targetRotationY = lookRotation.eulerAngles.x;

        // Unity의 Euler 각도 보정 (180도를 넘어가면 음수로 변환하여 제한 범위와 맞춤)
        if (targetRotationY > 180f) targetRotationY -= 360f;

        currentRotationX = targetRotationX;
        currentRotationY = targetRotationY;
        currentDistance = initialDistance;

        // UpdateOrbitalPosition이 위에서 설정한 각도와 거리를 바탕으로 카메라를 정확한 위치에 세팅함
        UpdateOrbitalPosition(true);
        hasFocusedOnMyPlanet = true;
    }

    private void HandleModeSwitch()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchMode(CameraMode.Follow);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchMode(CameraMode.Orbit);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchMode(CameraMode.Free);
    }

    private void SwitchMode(CameraMode newMode)
    {
        currentMode = newMode;
        
        if (newMode == CameraMode.Free)
        {
            // Free 모드 진입 시 현재 회전값을 동기화하여 시점 튀는 현상 방지
            SyncRotationVariables();
            targetVelocity = Vector3.zero;
            currentVelocity = Vector3.zero;
        }
    }

    private void HandleOrbitalMovement()
    {
        if (targetPlanet == null) return;

        // 마우스 스크롤 줌 인/아웃
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentDistance -= scroll * zoomSpeed;
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
        }

        // 우클릭 드래그로 각도 조절
        if (Input.GetMouseButton(1))
        {
            Cursor.lockState = CursorLockMode.Confined;
            targetRotationX += Input.GetAxis("Mouse X") * mouseSensitivity;
            targetRotationY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            targetRotationY = Mathf.Clamp(targetRotationY, -89f, 89f);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            
            // Orbit 모드일 때 입력이 없으면 자동 공전
            if (currentMode == CameraMode.Orbit)
            {
                targetRotationX += orbitSpeed * Time.deltaTime;
            }
        }

        UpdateOrbitalPosition(false);
    }

    private void UpdateOrbitalPosition(bool immediate)
    {
        if (immediate)
        {
            currentRotationX = targetRotationX;
            currentRotationY = targetRotationY;
        }
        else
        {
            currentRotationX = Mathf.Lerp(currentRotationX, targetRotationX, rotationSmoothness * Time.deltaTime);
            currentRotationY = Mathf.Lerp(currentRotationY, targetRotationY, rotationSmoothness * Time.deltaTime);
        }

        Quaternion rotation = Quaternion.Euler(currentRotationY, currentRotationX, 0f);
        Vector3 position = targetPlanet.position - (rotation * Vector3.forward * currentDistance);

        transform.rotation = rotation;
        transform.position = position;
    }

    private void HandleFreeRotation()
    {
        if (Input.GetMouseButton(1))
        {
            Cursor.lockState = CursorLockMode.Confined;
            targetRotationX += Input.GetAxis("Mouse X") * mouseSensitivity;
            targetRotationY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            targetRotationY = Mathf.Clamp(targetRotationY, -90f, 90f);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
        }

        currentRotationX = Mathf.Lerp(currentRotationX, targetRotationX, rotationSmoothness * Time.deltaTime);
        currentRotationY = Mathf.Lerp(currentRotationY, targetRotationY, rotationSmoothness * Time.deltaTime);

        transform.rotation = Quaternion.Euler(currentRotationY, currentRotationX, 0f);
    }

    private void HandleFreeMovement()
    {
        float currentSpeed = normalSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            currentSpeed *= boostMultiplier;
        }

        Vector3 moveInput = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) moveInput += transform.forward;
        if (Input.GetKey(KeyCode.S)) moveInput -= transform.forward;
        if (Input.GetKey(KeyCode.A)) moveInput -= transform.right;
        if (Input.GetKey(KeyCode.D)) moveInput += transform.right;
        if (Input.GetKey(KeyCode.E)) moveInput += transform.up;
        if (Input.GetKey(KeyCode.Q)) moveInput -= transform.up;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            moveInput += transform.forward * scroll * (zoomSpeed / normalSpeed);
        }

        targetVelocity = moveInput.normalized * currentSpeed;
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, movementSmoothness * Time.deltaTime);

        transform.position += currentVelocity * Time.deltaTime;
    }

    private void SyncRotationVariables()
    {
        Vector3 angles = transform.eulerAngles;
        targetRotationX = angles.y;
        targetRotationY = angles.x > 180 ? angles.x - 360 : angles.x;
        currentRotationX = targetRotationX;
        currentRotationY = targetRotationY;
    }
}