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
    [SerializeField] private float smoothTime = 1.2f;   // 시각적 보간 시간 (틱 주기의 약 25% 권장)

    [Header("Visual Effects")]
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private float rotationSpeed = 15f; 

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

            if (trailRenderer != null)
            {
                trailRenderer.Clear();
            }
            return;
        }

        // 새로운 패킷 도착 시 논리적 좌표와 속도를 최신 서버 값으로 덮어씌움 (Visual Ghost 보간)
        logicalPosition = targetPosition;
        networkVelocity = serverVel;
    }

    private void Update()
    {
        if (!isInitialized) return;

        float dt = Time.deltaTime;

        // 1. 논리적 위치는 서버가 부여한 속도(networkVelocity)로만 정직하게 직선 예측 이동
        logicalPosition += networkVelocity * dt;
        
        // 2. 실제 시각적 객체(transform)는 논리적 위치를 스프링처럼 부드럽게 따라감
        transform.position = Vector3.SmoothDamp(transform.position, logicalPosition, ref visualVelocity, smoothTime);

        // Y축 기준으로 자전 수행 (가로 무늬와 수평 유지)
        transform.Rotate(Vector3.up * rotationSpeed * dt);

        UpdateSatellites(Time.time);
    }

    public void ApplyWorldShift(Vector3 amount)
    {
        // 월드 쉬프트 시 논리적 위치와 시각적 위치 모두 보정
        logicalPosition -= amount;
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
    }
}