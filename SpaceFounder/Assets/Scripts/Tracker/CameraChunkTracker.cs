using System.Collections.Generic;
using UnityEngine;

public class CameraChunkTracker : MonoBehaviour
{
    [SerializeField] private float chunkSize = 1000f;
    private Vector3Int currentCenterSector = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    
    private bool isInitialSubscribed = false;

    // 🔥 NetworkManager가 player:init 수신 후 명확한 mySector를 전달하며 호출
    public void InitializeTracker(Vector3Int initialSector)
    {
        currentCenterSector = initialSector;
        SendSectorGridSubscribe(currentCenterSector);
        isInitialSubscribed = true;
        Debug.Log($"<color=cyan>[TRACKER INITIALIZED] 내 실제 섹터 기준 구독 요청 완료: {currentCenterSector}</color>");
    }

    private void Update()
    {
        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected) return;
        
        // player:init으로 초기 위치를 전달받기 전에는 절대 자율 구독하지 않음
        if (!isInitialSubscribed) return;

        Vector3Int newCenterSector = CalculateSector(transform.position);

        // 카메라 이동에 따라 섹터 변경 시에만 신규 구독
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
        Debug.Log($"[TRACKER] 섹터 구독 발송: {center} | 카메라 위치: {transform.position}");

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