using UnityEngine;
using UnityEngine.UI; // UI 컴포넌트 제어를 위해 추가
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

    [Header("UI Settings")]
    [SerializeField] private Button followButton;
    [SerializeField] private Button orbitButton;
    [SerializeField] private Button freeButton;
    [SerializeField] private Image followIcon;
    [SerializeField] private Image orbitIcon;
    [SerializeField] private Image freeIcon;
    
    [SerializeField] private float activeIconAlpha = 1f;
    [SerializeField] private float inactiveIconAlpha = 0.4f;

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

        // UI 버튼 클릭 이벤트 연결
        if (followButton != null) followButton.onClick.AddListener(() => SwitchMode(CameraMode.Follow));
        if (orbitButton != null) orbitButton.onClick.AddListener(() => SwitchMode(CameraMode.Orbit));
        if (freeButton != null) freeButton.onClick.AddListener(() => SwitchMode(CameraMode.Free));

        // 초기 UI 상태 적용
        UpdateUI();
    }

    private void Update()
    {
        HandleModeSwitch();

        if (currentMode == CameraMode.Free)
        {
            HandleFreeRotation();
            HandleFreeMovement();
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

        // 행성이 씬에 존재하지 않는 경우 백엔드에 위치 요청
        if (myPlanet == null)
        {
            if (!isRequestingLocation)
            {
                isRequestingLocation = true;
                WorldManager.Instance.RequestMyPlanetLocation(OnPlanetLocationReceived);
            }
            return; 
        }

        // 행성이 존재하면 정상적으로 포커스 진행
        Vector3 planetPos = myPlanet.transform.position;
        Vector3 sectorCenter = Vector3.zero;

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

    private void OnPlanetLocationReceived(Vector3Int sector, Vector3 localPos)
    {
        // 절대 섹터 위치를 현재 카메라 섹터 기준 상대 오프셋으로 변환
        Vector3Int relativeSector = sector - WorldManager.Instance.CurrentCameraSector;
        
        float scaledSectorSize = 1000f;
        Vector3 relativeSectorCenter = new Vector3(
            relativeSector.x * scaledSectorSize,
            relativeSector.y * scaledSectorSize,
            relativeSector.z * scaledSectorSize
        );

        Vector3 estimatedPlanetPos = relativeSectorCenter + localPos;
        transform.position = estimatedPlanetPos - (transform.forward * initialDistance);

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

        // 모드가 변경될 때 UI 업데이트
        UpdateUI();
    }

    private void UpdateUI()
    {
        UpdateModeUI(CameraMode.Follow, followButton, followIcon);
        UpdateModeUI(CameraMode.Orbit, orbitButton, orbitIcon);
        UpdateModeUI(CameraMode.Free, freeButton, freeIcon);
    }

    private void UpdateModeUI(CameraMode mode, Button btn, Image icon)
    {
        bool isActive = (currentMode == mode);
        
        // 아이콘 알파값 갱신
        if (icon != null)
        {
            Color c = icon.color;
            c.a = isActive ? activeIconAlpha : inactiveIconAlpha;
            icon.color = c;
        }
        
        // 버튼 배경(글로우 이미지) 알파값 갱신
        if (btn != null)
        {
            Image glowImage = btn.GetComponent<Image>();
            if (glowImage != null)
            {
                Color c = glowImage.color;
                c.a = isActive ? 0.1f : 0f; // 선택 시 글로우 100%, 비선택 시 완전 투명(클릭은 가능)
                glowImage.color = c;
            }
        }
    }

    private void HandleOrbitalMovement()
    {
        if (targetPlanet == null) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            // 타겟 행성의 반지름 계산 및 동적 최소 거리 설정
            float planetRadius = targetPlanet.localScale.x * 0.5f;
            float dynamicMinDistance = Mathf.Max(minDistance, planetRadius + 10f); // 10f는 카메라가 표면을 뚫지 않게 하는 여유 공간

            currentDistance -= scroll * zoomSpeed;
            currentDistance = Mathf.Clamp(currentDistance, dynamicMinDistance, maxDistance);
        }

        // 좌클릭 또는 우클릭 드래그 시 회전
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            Cursor.lockState = CursorLockMode.Confined;
            targetRotationX += Input.GetAxis("Mouse X") * mouseSensitivity;
            targetRotationY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            targetRotationY = Mathf.Clamp(targetRotationY, -89f, 89f);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            
            // Orbit 모드일 때 마우스를 놓으면 자동 공전
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
        // Free 모드 마우스 조작
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
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
        if (Input.GetKey(KeyCode.Q)) moveInput += transform.up;
        if (Input.GetKey(KeyCode.E)) moveInput -= transform.up;

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