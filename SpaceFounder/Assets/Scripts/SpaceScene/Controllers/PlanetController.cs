using UnityEngine;
using System.Collections.Generic;

public class PlanetController : MonoBehaviour
{
    [Header("Planet Info")]
    [SerializeField] private string planetName;
    [SerializeField] private string ownerName;
    [SerializeField] private string planetType;

    [Header("Network Settings")]
    [SerializeField] private float snapDistance = 300f; 
    [SerializeField] private float smoothTime = 1.2f;

    [Header("Visual Effects")]
    private bool isStaticPlanet = false;
    [SerializeField] private float rotationSpeed = 15f; 

    [SerializeField] private TrailRenderer trailRenderer;

    [Header("Collision Optimization")]
    [SerializeField] private LayerMask planetLayer = ~0; // 인스펙터에서 행성 레이어만 할당
    
    // 가비지 컬렉션(GC)을 막기 위해 넉넉한 크기의 충돌체 배열을 미리 한 번만 할당
    private Collider[] hitColliders = new Collider[10];

    // 넓은 반경 사전 검사 주기. 주변에 행성이 없으면 매 프레임 정밀 검사를 생략
    [SerializeField] private float collisionBroadphaseInterval = 0.25f;
    private float nextBroadphaseTime = 0f; // 0이면 다음 Update에서 즉시 사전 검사
    private bool hasNearbyPlanet = true;

    [System.Serializable]
    public class SatelliteData
    {
        public Transform satelliteTransform;
        public float radius;
        public float speed;
        public float inclination;
    }

    [SerializeField] 
    private List<SatelliteData> satellites = new List<SatelliteData>();

    private Vector3 logicalPosition;
    private Vector3 networkVelocity;
    private Vector3 visualVelocity = Vector3.zero;
    
    private bool isInitialized = false;
    private const float SECTOR_SIZE = 1000f; 

    public void UpdateSnapshot(Vector3Int planetSector, Vector3 planetLocalPos, Vector3 serverVel, Vector3Int cameraSector)
    {
        Vector3Int relativeSector = planetSector - cameraSector;
        Vector3 targetPosition = new Vector3(
            (relativeSector.x * SECTOR_SIZE) + planetLocalPos.x,
            (relativeSector.y * SECTOR_SIZE) + planetLocalPos.y,
            (relativeSector.z * SECTOR_SIZE) + planetLocalPos.z
        );

        if (float.IsNaN(targetPosition.x) || float.IsNaN(targetPosition.y) || float.IsNaN(targetPosition.z))
        {
            return;
        }

        if (!isInitialized || Vector3.Distance(transform.position, targetPosition) > snapDistance)
        {
            logicalPosition = targetPosition;
            transform.position = targetPosition;
            networkVelocity = serverVel;
            visualVelocity = Vector3.zero;
            isInitialized = true;
            nextBroadphaseTime = 0f; // 순간이동(스냅) 후에는 주변 상황이 바뀌었으므로 즉시 재검사

            return;
        }

        logicalPosition = targetPosition;
        networkVelocity = serverVel;
    }

    private void Awake()
    {
        if (trailRenderer == null)
        {
            trailRenderer = GetComponent<TrailRenderer>();
        }
    }

    private void Update()
    {
        if (!isInitialized) return;

        float dt = Time.deltaTime;

        if (!isStaticPlanet)
        {
            logicalPosition += networkVelocity * dt;
            
            // 1. 다음 프레임에 이동할 목표 위치 계산
            Vector3 nextPosition = Vector3.SmoothDamp(transform.position, logicalPosition, ref visualVelocity, smoothTime);

            // 2. 클라이언트 방어막: GC가 발생하지 않는 NonAlloc 방식 사용 및 레이어 필터링 적용
            float myRadius = transform.localScale.x * 0.5f;

            // 2-1. 사전 검사: 다음 주기 동안 접근 가능한 거리만큼 넓힌 반경에 다른 행성이 있는지만 확인
            if (Time.time >= nextBroadphaseTime)
            {
                nextBroadphaseTime = Time.time + collisionBroadphaseInterval;
                // shortcut: 상대 행성이 내 속도의 2배 이하로 움직인다고 가정한 여유 거리, 고속 행성이 생기면 주기를 줄이거나 여유를 키울 것
                float mySpeed = Mathf.Max(networkVelocity.magnitude, visualVelocity.magnitude);
                float margin = mySpeed * collisionBroadphaseInterval * 3f + myRadius;
                int nearCount = Physics.OverlapSphereNonAlloc(transform.position, myRadius + margin, hitColliders, planetLayer);
                hasNearbyPlanet = nearCount > 1 || (nearCount == 1 && hitColliders[0].gameObject != gameObject);
            }

            // 2-2. 정밀 검사: 주변에 행성이 있을 때만 매 프레임 수행 (보정 로직은 기존과 동일)
            int hitCount = hasNearbyPlanet
                ? Physics.OverlapSphereNonAlloc(nextPosition, myRadius, hitColliders, planetLayer)
                : 0;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = hitColliders[i];
                if (col.gameObject != this.gameObject)
                {
                    // 3. 충돌 감지 시, 겹치지 않는 외곽 표면 위치로 강제 보정
                    Vector3 directionFromOther = (nextPosition - col.transform.position).normalized;
                    float combinedRadius = myRadius + (col.transform.localScale.x * 0.5f);
                    
                    nextPosition = col.transform.position + (directionFromOther * combinedRadius);
                    
                    // 4. 내부 보간 속도 초기화 (파고드는 관성 상쇄)
                    visualVelocity = Vector3.zero;
                    break; 
                }
            }

            // 5. 최종 안전 위치 적용
            transform.position = nextPosition;

            if (networkVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 moveDirection = networkVelocity.normalized;
                Debug.DrawRay(transform.position, moveDirection * 1000f, Color.red);
            }
        }

        transform.Rotate(Vector3.up * rotationSpeed * dt);
        UpdateSatellites(Time.time);
    }

    public void ApplyWorldShift(Vector3 amount)
    {
        if (isStaticPlanet)
        {
            transform.position -= amount;
            return;
        }

        logicalPosition -= amount;
        visualVelocity = Vector3.zero;

        if (trailRenderer != null && trailRenderer.positionCount > 0)
        {
            Vector3[] trailPoints = new Vector3[trailRenderer.positionCount];
            trailRenderer.GetPositions(trailPoints);

            for (int i = 0; i < trailPoints.Length; i++)
            {
                trailPoints[i] -= amount;
            }

            trailRenderer.SetPositions(trailPoints);
        }

        transform.position -= amount;
    }

    private void UpdateSatellites(float timeSec)
    {
        if (satellites.Count == 0) return;

        foreach (var sat in satellites)
        {
            if (sat.satelliteTransform == null) continue;

            float theta = timeSec * sat.speed;
            float satX = transform.position.x + sat.radius * Mathf.Cos(theta);
            float satY = transform.position.y + sat.radius * Mathf.Sin(theta) * Mathf.Sin(sat.inclination);
            float satZ = transform.position.z + sat.radius * Mathf.Sin(theta) * Mathf.Cos(sat.inclination);

            sat.satelliteTransform.position = new Vector3(satX, satY, satZ);
        }
    }

    private void OnMouseEnter()
    {
        PlanetInfoUIManager.Instance?.ShowPlanetInfo(planetName, ownerName, planetType, isStaticPlanet);
    }

    private void OnMouseExit()
    {
        PlanetInfoUIManager.Instance?.HidePlanetInfo();
    }

    public void SetPlanetData(string name, string owner, string type, bool isDefault)
    {
        this.planetName = name;
        this.ownerName = owner;
        this.planetType = type;
        this.isStaticPlanet = isDefault;
    }
}