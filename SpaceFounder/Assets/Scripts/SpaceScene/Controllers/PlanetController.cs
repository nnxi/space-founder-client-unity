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
    [SerializeField] private float packetInterval = 10f; // 서버 패킷 수신 주기 (초 단위)

    [Header("Visual Effects")]
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private float rotationSpeed = 15f; // 행성 자전 속도

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

    private Vector3 networkPosition;
    private Vector3 networkVelocity;
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

        // 초기화 전이거나 오차가 임계값을 넘으면 즉시 이동
        if (!isInitialized || Vector3.Distance(transform.position, targetPosition) > snapDistance)
        {
            transform.position = targetPosition;
            networkPosition = targetPosition;
            networkVelocity = serverVel;
            isInitialized = true;

            if (trailRenderer != null)
            {
                trailRenderer.Clear();
            }
            return;
        }

        // 예측 위치와 서버 실제 위치 간의 오차 계산
        Vector3 error = targetPosition - networkPosition;

        // 다음 패킷이 올 때까지 오차를 나누어 흡수하도록 속도 보정
        networkVelocity = serverVel + (error / packetInterval);
        
        // 기준 위치는 서버 위치로 갱신
        networkPosition = targetPosition;
    }

    private void Update()
    {
        if (!isInitialized) return;

        float dt = Time.deltaTime;

        // 보정된 속도로 예측 이동 수행
        networkPosition += networkVelocity * dt;
        transform.position = networkPosition;

        // Y축 기준으로 자전 수행 (가로 무늬와 수평 유지)
        transform.Rotate(Vector3.up * rotationSpeed * dt);

        UpdateSatellites(Time.time);
    }

    public void ApplyWorldShift(Vector3 amount)
    {
        networkPosition -= amount;
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