using UnityEngine;
using System.Collections.Generic;

public class PlanetController : MonoBehaviour
{
    [Header("Smoothing Settings")]
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private float snapDistance = 100f; 

    [Header("Visual Effects")]
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

    private Vector3 networkPosition;
    private Vector3 networkVelocity;
    private Vector3 visualOffset;
    private bool isInitialized = false;

    private const float SECTOR_SIZE = 1000f; 

    // WorldManager로부터 수신한 좌표를 바탕으로 행성의 렌더링 목표 위치 설정
    public void UpdateSnapshot(Vector3Int planetSector, Vector3 planetLocalPos, Vector3 serverVel, Vector3Int cameraSector)
    {
        // 카메라가 위치한 섹터를 기준으로 상대 좌표 계산 (부동소수점 오차 방지)
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

        // 초기화 전이거나 오차가 임계값을 넘으면 보간 없이 즉시 이동
        if (!isInitialized || Vector3.Distance(transform.position, targetPosition) > snapDistance)
        {
            transform.position = targetPosition;
            networkPosition = targetPosition;
            networkVelocity = serverVel;
            visualOffset = Vector3.zero;
            isInitialized = true;

            // 텔레포트 시 트레일이 비정상적으로 길게 그려지는 현상 방지
            if (trailRenderer != null)
            {
                trailRenderer.Clear();
            }
            return;
        }

        // 부드러운 이동을 위한 시각적 오프셋 계산
        Vector3 currentVisualPos = transform.position;
        networkPosition = targetPosition;
        networkVelocity = serverVel;
        visualOffset = currentVisualPos - networkPosition;
    }

    private void Update()
    {
        if (!isInitialized) return;

        float dt = Time.deltaTime;

        // 서버 속도를 기반으로 한 클라이언트 측 위치 예측(Dead Reckoning) 및 보간
        networkPosition += networkVelocity * dt;
        visualOffset = Vector3.Lerp(visualOffset, Vector3.zero, dt * smoothSpeed);
        transform.position = networkPosition + visualOffset;

        UpdateSatellites(Time.time);
    }

    // 위성의 공전 궤도 업데이트
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
}