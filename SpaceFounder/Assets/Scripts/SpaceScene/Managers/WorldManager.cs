using System;
using System.Collections.Concurrent;
using UnityEngine;

// 월드 상태의 외부 진입점(Facade).
// 섹터 구독 영역 계산은 SectorGrid, 행성 생성/관리는 PlanetSpawner가 담당하며
// 이 클래스는 네트워크 연동(NetworkManager 호출)과 메인 스레드 큐 처리만 조율한다.
// 외부 public API와 [SerializeField] 필드는 분리 전과 동일하게 유지.
public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance { get; private set; }

    [Header("Prefabs & Scaling")]
    [SerializeField] private GameObject userPlanetPrefab;
    [SerializeField] private GameObject staticPlanetPrefab;
    [SerializeField] private float scaleFactor = 0.01f;
    [SerializeField] private float sectorSize = 1000f;

    public int MyPlanetId => planetSpawner.MyPlanetId;
    public Vector3Int CurrentCameraSector { get; private set; } = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    public GameObject MyPlanet => planetSpawner.MyPlanet;

    private readonly SectorGrid sectorGrid = new SectorGrid();
    private PlanetSpawner planetSpawner;

    private ConcurrentQueue<DecodedPlanetSnapshot[]> updateQueue = new ConcurrentQueue<DecodedPlanetSnapshot[]>();

    private void Awake()
    {
        // 직렬화 값이 로드된 이후(Awake)에 생성. Instance 등록 전에 생성하여 외부 접근 시 null 방지
        planetSpawner = new PlanetSpawner(userPlanetPrefab, staticPlanetPrefab, scaleFactor, sectorSize);

        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        while (updateQueue.TryDequeue(out DecodedPlanetSnapshot[] snapshots))
        {
            planetSpawner.ProcessWorldUpdate(snapshots, CurrentCameraSector);
        }
    }

    public void InitializePlayer(int planetId, Vector3Int initialSector)
    {
        planetSpawner.MyPlanetId = planetId;
        UpdateCameraSector(initialSector, true);
        Debug.Log($"[WorldManager] Player init - ID: {MyPlanetId}, Sector: {initialSector}");
    }

    public void UpdateCameraSector(Vector3Int newSector, bool forceUpdate = false)
    {
        if (!forceUpdate && CurrentCameraSector == newSector) return;
        CurrentCameraSector = newSector;

        var (sectorsToSubscribe, sectorsToUnsubscribe) = sectorGrid.Recenter(newSector);

        // 완전히 멀어진 섹터의 정적 행성들을 씬에서 파괴 (내 행성 제외 기준은 기존과 동일하게 NetworkManager.MyPlanetId)
        int? protectedPlanetId = NetworkManager.Instance != null ? NetworkManager.Instance.MyPlanetId : (int?)null;
        planetSpawner.UnloadSectors(sectorsToUnsubscribe, protectedPlanetId);

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.EmitSubscribeGrid(sectorsToSubscribe);
            NetworkManager.Instance.EmitUnsubscribeGrid(sectorsToUnsubscribe);
        }
    }

    public void RequestMyPlanetLocation(Action<Vector3Int, Vector3> onLocationReceived)
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.RequestTrackMyPlanet((success, sector, localPos) =>
            {
                if (success)
                {
                    onLocationReceived?.Invoke(sector, localPos);
                }
            });
        }
    }

    public void SetStaticData(StaticPlanetData[] staticPlanets)
    {
        planetSpawner.SetStaticData(staticPlanets, CurrentCameraSector);
    }

    public void OnWorldUpdateReceived(DecodedPlanetSnapshot[] planets)
    {
        updateQueue.Enqueue(planets);
    }

    public void ShiftAllPlanets(Vector3 shiftAmount)
    {
        planetSpawner.ShiftAll(shiftAmount);
    }
}
