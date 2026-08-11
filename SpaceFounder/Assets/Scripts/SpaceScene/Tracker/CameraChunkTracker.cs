using UnityEngine;

public class CameraChunkTracker : MonoBehaviour
{
    [SerializeField] private float chunkSize = 1000f;
    [SerializeField] private CameraController mainCameraController;
    
    // 이전에 구독했던 논리적 기준점(서버의 0,0,0)을 저장할 변수 (초기화 필요)
    private bool isInitialized = false;

    private void Update()
    {
        if (WorldManager.Instance == null || WorldManager.Instance.MyPlanetId == -1) return;
        if (mainCameraController == null || !mainCameraController.HasFocusedOnMyPlanet) return;

        // 초기화 1회: 현재 월드매니저의 섹터를 기준으로 시작
        if (!isInitialized)
        {
            if (WorldManager.Instance.CurrentCameraSector.x != int.MinValue)
            {
                isInitialized = true;
            }
            return;
        }

        Vector3 camPos = mainCameraController.transform.position;
        Vector3Int offsetSector = new Vector3Int(
            Mathf.FloorToInt(camPos.x / chunkSize),
            Mathf.FloorToInt(camPos.y / chunkSize),
            Mathf.FloorToInt(camPos.z / chunkSize)
        );

        // 카메라는 항상 0번 섹터(-1000 ~ 1000) 안에서만 놀아야 함
        // 오프셋이 발생했다는 것은 섹터 경계를 넘었다는 의미
        if (offsetSector != Vector3Int.zero)
        {
            PerformWorldShift(offsetSector);
        }
    }

    private void PerformWorldShift(Vector3Int offsetSector)
    {
        // 1. 서버에 요청할 CurrentCameraSector를 새로운 절대 좌표로 갱신 (예: 5,4,3 -> 5,5,3)
        Vector3Int newServerSector = WorldManager.Instance.CurrentCameraSector + offsetSector;
        WorldManager.Instance.UpdateCameraSector(newServerSector, false);
        
        Debug.Log($"[World Shift] 섹터 갱신: {newServerSector}, 발생한 오프셋: {offsetSector}");

        // 2. 물리적 이동량 계산
        Vector3 shiftAmount = new Vector3(
            offsetSector.x * chunkSize,
            offsetSector.y * chunkSize,
            offsetSector.z * chunkSize
        );
        
        // 3. 카메라를 원점(0,0,0) 부근으로 강제로 되돌림
        mainCameraController.transform.position -= shiftAmount;

        // 4. 행성들도 똑같이 되돌려서 화면상에 전혀 흔들림이 없도록 처리
        WorldManager.Instance.ShiftAllPlanets(shiftAmount);
    }
}