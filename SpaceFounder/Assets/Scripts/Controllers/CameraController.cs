using UnityEngine;
using System;

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
    public bool HasFocusedOnMyPlanet => hasFocusedOnMyPlanet;

    [SerializeField] private float orbitSpeed = 15f;

    [Header("Speed Settings")]
    [SerializeField] private float normalSpeed = 50f;
    [SerializeField] private float boostMultiplier = 3f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float zoomSpeed = 10f;

    [Header("Smooth Settings")]
    [SerializeField] private float movementSmoothness = 3f;
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
    private bool isRequestingLocation = false; // 중복 요청 방지 플래그

    private void Start()
    {
        currentDistance = initialDistance;
        SyncRotationVariables();
    }

    private void Update()
    {
        HandleModeSwitch();

        if (currentMode == CameraMode.Free)
        {
            HandleFreeRotation();
            HandleFreeMovement();
            // Free 모드일 때는 포커스를 해제하여 언제든 다시 Follow로 복귀할 수 있도록 설정
            hasFocusedOnMyPlanet = false; 
        }
        else
        {
            if (!hasFocusedOnMyPlanet)
            {
                TryFocusOnMyPlanet();
            }
            else
            {
                HandleOrbitalMovement();
            }
        }
    }

    private void TryFocusOnMyPlanet()
    {
        if (WorldManager.Instance == null) return;

        GameObject myPlanet = WorldManager.Instance.MyPlanet;

        // 행성이 씬에 존재하지 않는 경우 백엔드에 위치를 요청
        if (myPlanet == null)
        {
            if (!isRequestingLocation)
            {
                isRequestingLocation = true;
                // WorldManager.cs에 RequestMyPlanetLocation(Action<Vector3Int, Vector3> callback) 함수 구현 필요
                WorldManager.Instance.RequestMyPlanetLocation(OnPlanetLocationReceived);
            }
            return; 
        }

        // 행성이 존재하면 정상적으로 포커스 진행
        Vector3 planetPos = myPlanet.transform.position;
        Vector3Int currentSector = WorldManager.Instance.CurrentCameraSector;

        float scaledSectorSize = 1000f;
        Vector3 sectorCenter = new Vector3(
            currentSector.x * scaledSectorSize,
            currentSector.y * scaledSectorSize,
            currentSector.z * scaledSectorSize
        );

        Vector3 directionToCenter = (sectorCenter - planetPos).normalized;

        if (directionToCenter.sqrMagnitude == 0f)
        {
            directionToCenter = Vector3.one.normalized;
        }

        Quaternion lookRotation = Quaternion.LookRotation(-directionToCenter);
        targetPlanet = myPlanet.transform;
        
        targetRotationX = lookRotation.eulerAngles.y;
        targetRotationY = lookRotation.eulerAngles.x;

        if (targetRotationY > 180f) targetRotationY -= 360f;

        currentRotationX = targetRotationX;
        currentRotationY = targetRotationY;
        currentDistance = initialDistance;

        UpdateOrbitalPosition(true);
        hasFocusedOnMyPlanet = true;
        isRequestingLocation = false;
    }

    // 위치 정보를 응답받고 카메라를 워프시키는 콜백
    private void OnPlanetLocationReceived(Vector3Int sector, Vector3 localPos)
    {
        float scaledSectorSize = 1000f;
        Vector3 sectorCenter = new Vector3(
            sector.x * scaledSectorSize,
            sector.y * scaledSectorSize,
            sector.z * scaledSectorSize
        );

        Vector3 estimatedPlanetPos = sectorCenter + localPos;
        transform.position = estimatedPlanetPos - (transform.forward * initialDistance);

        // 워프 직후 CameraChunkTracker가 변경된 위치를 기반으로 섹터를 감지하여 구독을 자동 갱신합니다.
        isRequestingLocation = false;
    }

    private void HandleModeSwitch()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchMode(CameraMode.Follow);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchMode(CameraMode.Orbit);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchMode(CameraMode.Free);
    }

    private void SwitchMode(CameraMode newMode)
    {
        if (currentMode == newMode) return;

        currentMode = newMode;
        
        if (newMode == CameraMode.Free)
        {
            SyncRotationVariables();
            targetVelocity = Vector3.zero;
            currentVelocity = Vector3.zero;
        }
    }

    private void HandleOrbitalMovement()
    {
        if (targetPlanet == null) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentDistance -= scroll * zoomSpeed;
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
        }

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