using UnityEngine;
using System.Collections.Generic;

public class PlanetController : MonoBehaviour
{
    private Vector3 networkPosition;
    private Vector3 networkVelocity;
    private Vector3 visualOffset;
    private bool isInitialized = false;

    [Header("Smoothing Settings")]
    [SerializeField] private float smoothSpeed = 10f; 

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

    private const float SECTOR_SIZE = 1000f; 

    // 카메라의 현재 섹터 정보를 함께 받아 상대 좌표 연산 수행
    public void UpdateSnapshot(Vector3Int planetSector, Vector3 planetLocalPos, Vector3 serverVel, Vector3Int cameraSector)
    {
        // 카메라 상대 좌표가 아닌, 월드 절대 좌표로 계산
        Vector3 absolutePos = new Vector3(
            (planetSector.x * SECTOR_SIZE) + planetLocalPos.x,
            (planetSector.y * SECTOR_SIZE) + planetLocalPos.y,
            (planetSector.z * SECTOR_SIZE) + planetLocalPos.z
        );

        if (float.IsNaN(absolutePos.x) || float.IsNaN(absolutePos.y) || float.IsNaN(absolutePos.z))
        {
            return;
        }

        if (!isInitialized || Vector3.Distance(transform.position, absolutePos) > 100f)
        {
            transform.position = absolutePos;
            networkPosition = absolutePos;
            networkVelocity = serverVel;
            visualOffset = Vector3.zero;
            isInitialized = true;
            return;
        }

        Vector3 currentVisualPos = transform.position;
        networkPosition = absolutePos;
        networkVelocity = serverVel;
        visualOffset = currentVisualPos - networkPosition;
    }

    private void Update()
    {
        if (!isInitialized) return;

        float dt = Time.deltaTime;

        networkPosition += networkVelocity * dt;
        visualOffset = Vector3.Lerp(visualOffset, Vector3.zero, dt * smoothSpeed);
        transform.position = networkPosition + visualOffset;

        if (satellites.Count > 0)
        {
            float timeSec = Time.time;

            foreach (var sat in satellites)
            {
                float theta = timeSec * sat.speed;
                float satX = transform.position.x + sat.radius * Mathf.Cos(theta);
                float satY = transform.position.y + sat.radius * Mathf.Sin(theta) * Mathf.Sin(sat.inclination);
                float satZ = transform.position.z + sat.radius * Mathf.Sin(theta) * Mathf.Cos(sat.inclination);

                sat.satelliteTransform.position = new Vector3(satX, satY, satZ);
            }
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