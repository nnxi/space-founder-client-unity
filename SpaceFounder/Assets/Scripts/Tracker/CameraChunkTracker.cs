using UnityEngine;

public class CameraChunkTracker : MonoBehaviour
{
    [SerializeField] private float chunkSize = 1000f;
    [SerializeField] private CameraController mainCameraController;
    private Vector3Int currentCenterSector = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

    private bool isFirst = false;


    private void Update()
    {
        if (WorldManager.Instance == null) return;

        if (mainCameraController == null || !mainCameraController.HasFocusedOnMyPlanet) return;
        
        // 내 행성 ID가 할당되기 전(서버 연결 및 초기화 전)에는 감지 중지
        if (WorldManager.Instance.MyPlanetId == -1) return;

        Vector3Int newCenterSector = CalculateSector(transform.position);

        // 방어 로직: 카메라는 아직 (0,0,0)에 있는데 내 실제 섹터가 (0,0,0)이 아닐 경우
        // 이는 행성이 생성되기 전이나 카메라 워프가 완료되지 않은 찰나의 상태이므로 무시
        if (WorldManager.Instance.MyPlanet == null && 
            newCenterSector == Vector3Int.zero && 
            WorldManager.Instance.CurrentCameraSector != Vector3Int.zero)
        {
            return; 
        }

        // 최초 1회 동기화
        // WorldManager가 통신을 통해 먼저 세팅해둔 내 섹터 값을 그대로 수용하여 중복 구독 방지
        if (currentCenterSector.x == int.MinValue)
        {
            currentCenterSector = WorldManager.Instance.CurrentCameraSector;
            return;
        }

        // 카메라 이동으로 실제 섹터가 바뀌었을 때만 WorldManager에 알림
        if (newCenterSector != currentCenterSector)
        {
            currentCenterSector = newCenterSector;
            WorldManager.Instance.UpdateCameraSector(currentCenterSector, false);
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
}