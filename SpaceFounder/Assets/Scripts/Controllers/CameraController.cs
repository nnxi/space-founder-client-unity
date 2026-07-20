using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Speed Settings")]
    [SerializeField] private float normalSpeed = 100f;
    [SerializeField] private float boostMultiplier = 3f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float zoomSpeed = 2000f;

    [Header("Smooth Settings")]
    [SerializeField] private float movementSmoothness = 3.5f;
    [SerializeField] private float rotationSmoothness = 10f;

    [Header("Focus Settings")]
    [SerializeField] private float initialDistance = 200f;

    private Vector3 targetVelocity;
    private Vector3 currentVelocity;

    private float targetRotationX;
    private float targetRotationY;
    private float currentRotationX;
    private float currentRotationY;

    private bool hasFocusedOnMyPlanet = false;

    private void Start()
    {
        SyncRotationVariables();
    }

    private void Update()
    {
        if (!hasFocusedOnMyPlanet)
        {
            TryFocusOnMyPlanet();
        }

        HandleRotation();
        HandleMovement();
    }

    private void TryFocusOnMyPlanet()
    {
        if (NetworkManager.Instance == null || WorldManager.Instance == null) return;

        int myPlanetId = NetworkManager.Instance.MyPlanetId;
        if (myPlanetId == -1) return;

        GameObject myPlanet = WorldManager.Instance.GetPlanet(myPlanetId);
        if (myPlanet != null)
        {
            Vector3 targetPos = myPlanet.transform.position;
            
            transform.position = targetPos + new Vector3(0, initialDistance * 0.5f, -initialDistance);
            transform.LookAt(targetPos);
            
            SyncRotationVariables();
            
            hasFocusedOnMyPlanet = true;
        }
    }

    private void SyncRotationVariables()
    {
        Vector3 angles = transform.eulerAngles;
        targetRotationX = angles.y;
        targetRotationY = angles.x;
        currentRotationX = angles.y;
        currentRotationY = angles.x;
    }

    private void HandleRotation()
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

    private void HandleMovement()
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
}