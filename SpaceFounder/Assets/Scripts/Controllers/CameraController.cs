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
    [SerializeField] private float normalSpeed = 100f;
    [SerializeField] private float boostMultiplier = 3f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float zoomSpeed = 500f;

    [Header("Smooth Settings")]
    [SerializeField] private float movementSmoothness = 3.5f;
    [SerializeField] private float rotationSmoothness = 10f;

    [Header("Focus & Zoom Settings")]
    [SerializeField] private float initialDistance = 500f;
    [SerializeField] private float minDistance = 50f;
    [SerializeField] private float maxDistance = 3000f;

    private Transform targetPlanet;
    private float currentDistance;

    private Vector3 targetVelocity;
    private Vector3 currentVelocity;

    private float targetRotationX;
    private float targetRotationY;
    private float currentRotationX;
    private float currentRotationY;

    private bool isCameraPositionSet = false;

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
/*
        int myPlanetId = NetworkManager.Instance.MyPlanetId;
        
        // 1. 아직 player:init을 받지 못했다면 절대 실행하지 않고 대기
        if (myPlanetId == -1) return;

        // 2. 서버에서 mySector 패킷을 정상 수신했는지 확인 후 1회 세팅
        if (!isCameraPositionSet)
        {
            Vector3Int initialSector = WorldManager.Instance.CurrentCameraSector;

            // 카메라 위치를 서버에서 받은 실제 섹터 절대 위치로 이동
            transform.position = new Vector3(
                initialSector.x * 1000f,
                initialSector.y * 1000f,
                initialSector.z * 1000f
            );

            // 이동 완료 후 1차 구독 개시
            CameraChunkTracker tracker = GetComponent<CameraChunkTracker>();
            if (tracker != null)
            {
                tracker.InitializeTracker();
            }

            isCameraPositionSet = true;
        }
*/
        // 3. 내 행성 오브젝트가 생성되면 포커싱
        GameObject myPlanet = WorldManager.Instance.MyPlanet;
        if (myPlanet != null)
        {
            targetPlanet = myPlanet.transform;
            
            targetRotationX = 45f;
            targetRotationY = 30f;
            currentRotationX = targetRotationX;
            currentRotationY = targetRotationY;
            currentDistance = initialDistance;

            UpdateOrbitalPosition(true);
            hasFocusedOnMyPlanet = true;
        }
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