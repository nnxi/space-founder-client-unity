using System.Collections.Generic;
using UnityEngine;

public class CameraChunkTracker : MonoBehaviour
{
    [SerializeField] private float chunkSize = 1000f;
    private Vector3Int currentCenterSector = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    
    private bool isInitialized = false;

    // CameraController가 내 행성을 찾아 카메라 배치를 완전히 마친 뒤 1회 호출
    public void InitializeTracker()
    {
        Vector3 camPos = transform.position;
        currentCenterSector = CalculateSector(camPos);

        if (WorldManager.Instance != null)
        {
            WorldManager.Instance.CurrentCameraSector = currentCenterSector;
        }

        isInitialized = true;
        SendSectorGridSubscribe(currentCenterSector);
    }

    private void Update()
    {
        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected) return;
        if (NetworkManager.Instance.MyPlanetId == -1) return;

        // 카메라가 내 행성에 포커싱되기 전까지는 절대로 섹터 추적 및 구독을 하지 않음
        if (!isInitialized) return;

        Vector3Int newCenterSector = CalculateSector(transform.position);

        if (newCenterSector != currentCenterSector)
        {
            currentCenterSector = newCenterSector;

            if (WorldManager.Instance != null)
            {
                WorldManager.Instance.CurrentCameraSector = currentCenterSector;
            }

            SendSectorGridSubscribe(currentCenterSector);
        }
    }

    private Vector3Int CalculateSector(Vector3 pos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(pos.x / chunkSize),
            Mathf.FloorToInt(pos.y / chunkSize),
            Mathf.FloorToInt(pos.z / chunkSize)
        );
    }

    private void SendSectorGridSubscribe(Vector3Int center)
    {
        if (center == Vector3Int.zero)
        {
            Debug.LogError($"[TRACKER ERROR] (0,0,0) 섹터 구독이 시도됨! | isInitialized: {isInitialized} | transform.position: {transform.position}");
        }
        else
        {
            Debug.Log($"[TRACKER NORMAL] 정상 섹터 구독: {center} | transform.position: {transform.position}");
        }

        List<Vector3Int> gridSectors = new List<Vector3Int>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    gridSectors.Add(new Vector3Int(center.x + x, center.y + y, center.z + z));
                }
            }
        }

        NetworkManager.Instance.EmitSubscribeGrid(gridSectors);
    }
}