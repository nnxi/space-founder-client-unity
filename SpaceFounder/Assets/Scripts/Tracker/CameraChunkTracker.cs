using System.Collections.Generic;
using UnityEngine;

public class CameraChunkTracker : MonoBehaviour
{
    [SerializeField] private float chunkSize = 1000f; // 유니티 스케일이 적용된 섹터 크기
    private Vector3Int currentCenterSector = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    
    private bool isInitialSubscribed = false;
    private bool isCameraTrackingEnabled = false;

    private void Update()
    {
        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected) return;
        if (NetworkManager.Instance.MyPlanetId == -1) return;
        if (WorldManager.Instance == null) return;

        // 1. 최초 초기화: 내 행성의 섹터 정보가 들어오면 카메라 이동 전에 1차 구독부터 전송
        if (!isInitialSubscribed)
        {
            currentCenterSector = WorldManager.Instance.CurrentCameraSector;
            SendSectorGridSubscribe(currentCenterSector);
            isInitialSubscribed = true;
            return;
        }

        Vector3Int newCenterSector = CalculateSector(transform.position);

        // 2. 카메라 추적 시작 대기 (핵심 방어벽)
        // 행성이 생성되기 전 카메라는 (0,0,0)에 머물러 있습니다.
        // 이때 (0,0,0)으로 엉뚱한 구독이 나가는 것을 막기 위해, 카메라가 1차 목적지 섹터로 이동을 완료할 때까지 추적을 보류합니다.
        if (!isCameraTrackingEnabled)
        {
            if (newCenterSector == currentCenterSector)
            {
                isCameraTrackingEnabled = true; // 카메라 목적지 안착 확인, 추적 개시
            }
            return;
        }

        // 3. 실제 카메라 이동에 따른 섹터 변경 감지 및 구독
        if (newCenterSector != currentCenterSector)
        {
            currentCenterSector = newCenterSector;
            WorldManager.Instance.CurrentCameraSector = currentCenterSector;
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
            Debug.LogWarning($"[TRACKER] (0,0,0) 섹터 구독 발송 | 초기화 여부: {isInitialSubscribed} | 카메라 위치: {transform.position}");
        }
        else
        {
            Debug.Log($"[TRACKER NORMAL] 정상 섹터 구독: {center} | 카메라 위치: {transform.position}");
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