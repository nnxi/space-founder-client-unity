using UnityEngine;
using System.Collections.Generic;

public class PlanetController : MonoBehaviour
{
    [Header("Planet Info")]
    [SerializeField] private string planetName;
    [SerializeField] private string ownerName;
    [SerializeField] private bool isDefaultPlanet;

    [Header("Network Settings")]
    [SerializeField] private float snapDistance = 300f; 
    [SerializeField] private float smoothTime = 1.2f;   // 시각적 보간 시간

    [Header("Visual Effects")]
    private bool isStaticPlanet = false;
    [SerializeField] private float rotationSpeed = 15f; 

    [SerializeField] private TrailRenderer trailRenderer;

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

    // 보간을 위한 논리적 좌표 및 내부 속도 변수
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

        // 초기화 전이거나 오차가 임계값을 넘으면 즉시 이동 (Snap)
        if (!isInitialized || Vector3.Distance(transform.position, targetPosition) > snapDistance)
        {
            logicalPosition = targetPosition;
            transform.position = targetPosition;
            networkVelocity = serverVel;
            visualVelocity = Vector3.zero;
            isInitialized = true;

            return;
        }

        // 새로운 패킷 도착 시 논리적 좌표와 속도를 최신 서버 값으로 덮어씌움
        logicalPosition = targetPosition;
        networkVelocity = serverVel;

        if (!isStaticPlanet) 
        {
            // 속도 벡터의 크기(magnitude) 출력
            Debug.Log($"[Gravity Check] Velocity Magnitude: {networkVelocity.magnitude}");
        }
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

        // 정적 행성(배경 행성)이 아닐 때만 위치 보간 연산 수행
        if (!isStaticPlanet)
        {
            // 1. 논리적 위치는 서버가 부여한 속도로 직선 예측 이동
            logicalPosition += networkVelocity * dt;
            
            // 2. 실제 시각적 객체(transform)는 논리적 위치를 부드럽게 따라감
            transform.position = Vector3.SmoothDamp(transform.position, logicalPosition, ref visualVelocity, smoothTime);

            if (networkVelocity.sqrMagnitude > 0.01f)
            {
                // 속도 벡터를 정규화하여 순수한 방향만 추출
                Vector3 moveDirection = networkVelocity.normalized;
                
                // 씬 뷰에서 이동 방향을 가리키는 붉은 선 출력 (길이 1000)
                Debug.DrawRay(transform.position, moveDirection * 1000f, Color.red);
            }
        }

        // Y축 기준으로 자전 수행 (정적 행성도 자전 수행)
        transform.Rotate(Vector3.up * rotationSpeed * dt);

        UpdateSatellites(Time.time);
    }

    public void ApplyWorldShift(Vector3 amount)
    {
        // 정적 행성은 트레일 보간 없이 실제 오브젝트 위치만 즉시 이동 후 종료
        if (isStaticPlanet)
        {
            transform.position -= amount;
            return;
        }

        // 유저 행성의 논리적 목표 좌표 이동 및 보간 가속도 초기화
        logicalPosition -= amount;
        visualVelocity = Vector3.zero;

        // 트레일 렌더러 점 배열 통째로 시프트
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

        // 유저 행성의 실제 오브젝트 위치 이동
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

    public void AddSatellite(Transform satTransform, float radius, float speed, float inclination)
    {
        satellites.Add(new SatelliteData
        {
            satelliteTransform = satTransform,
            radius = radius,
            speed = speed,
            inclination = inclination
        });
    }

    private void OnMouseEnter()
    {
        PlanetInfoUIManager.Instance?.ShowPlanetInfo(planetName, ownerName, isDefaultPlanet);
    }

    private void OnMouseExit()
    {
        PlanetInfoUIManager.Instance?.HidePlanetInfo();
    }

    public void SetPlanetData(string name, string owner, bool isDefault)
    {
        this.planetName = name;
        this.ownerName = owner;
        this.isDefaultPlanet = isDefault;
        
        // 정적 행성 상태 저장
        this.isStaticPlanet = isDefault;
    }
}