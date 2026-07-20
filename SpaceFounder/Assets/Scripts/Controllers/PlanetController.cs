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

    public void UpdateSnapshot(Vector3 serverPos, Vector3 serverVel)
    {
        if (!isInitialized || Vector3.Distance(transform.position, serverPos) > 100f)
        {
            transform.position = serverPos;
            networkPosition = serverPos;
            networkVelocity = serverVel;
            visualOffset = Vector3.zero;
            isInitialized = true;
            return;
        }

        Vector3 currentVisualPos = transform.position;

        networkPosition = serverPos;
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