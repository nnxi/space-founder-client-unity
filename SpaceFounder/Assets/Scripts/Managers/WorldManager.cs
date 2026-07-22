using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance { get; private set; }

    [SerializeField] private GameObject planetPrefab;
    [SerializeField] private float scaleFactor = 0.01f;
    [SerializeField] private float sectorSize = 1000f; // 섹터 한 변의 크기 (유니티에선 1000으로 줄임)

    // NetworkManager 및 CameraChunkTracker에서 갱신하는 현재 카메라 섹터 데이터
    public Vector3Int CurrentCameraSector { get; set; } = Vector3Int.zero;

    // 내 행성이 담길 곳
    public GameObject MyPlanet { get; private set; }

    // 정적 데이터 보관용
    private Dictionary<int, StaticPlanetData> staticDataMap = new Dictionary<int, StaticPlanetData>();
    
    // 활성화된 행성 보관용
    private Dictionary<int, GameObject> activePlanets = new Dictionary<int, GameObject>();
    
    // 수신된 동적 데이터 처리 큐
    private ConcurrentQueue<DecodedPlanetSnapshot[]> updateQueue = new ConcurrentQueue<DecodedPlanetSnapshot[]>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetStaticData(StaticPlanetData[] staticPlanets)
    {
        staticDataMap.Clear();
        if (staticPlanets == null) return;

        foreach (var sp in staticPlanets)
        {
            staticDataMap[sp.id] = sp;
        }
    }

    public PlanetController GetPlanetController(int planetId)
    {
        if (activePlanets.TryGetValue(planetId, out GameObject planetObj))
        {
            return planetObj.GetComponent<PlanetController>();
        }
        return null;
    }

    public GameObject GetPlanet(int planetId)
    {
        if (activePlanets.TryGetValue(planetId, out GameObject planetObj))
        {
            return planetObj;
        }
        return null;
    }

    public void OnWorldUpdateReceived(DecodedPlanetSnapshot[] planets)
    {
        updateQueue.Enqueue(planets);
    }

    private void Update()
    {
        while (updateQueue.TryDequeue(out DecodedPlanetSnapshot[] planets))
        {
            ProcessWorldUpdate(planets);
        }
    }

    private void ProcessWorldUpdate(DecodedPlanetSnapshot[] planets)
    {
        if (planetPrefab == null)
        {
            Debug.LogError("[WorldManager] PlanetPrefab is not assigned.");
            return;
        }

        // 네트워크 매니저로부터 내 행성 ID 조회
        int myPlanetId = NetworkManager.Instance != null ? NetworkManager.Instance.MyPlanetId : -1;
        HashSet<int> currentFrameIds = new HashSet<int>();

        foreach (var pData in planets)
        {
            int planetId = pData.id;
            currentFrameIds.Add(planetId);
            
            // 로컬 좌표 및 속도 스케일 변환
            Vector3 scaledLocalPos = pData.localPosition * scaleFactor;
            Vector3 scaledVelocity = pData.velocity * scaleFactor;

            if (activePlanets.TryGetValue(planetId, out GameObject planetObj))
            {
                PlanetController controller = planetObj.GetComponent<PlanetController>();
                if (controller != null)
                {
                    controller.UpdateSnapshot(pData.sectorIndex, scaledLocalPos, scaledVelocity, CurrentCameraSector);
                }

                // 생성되어 있던 객체가 내 행성일 경우 프로퍼티에 할당
                if (planetId == myPlanetId && MyPlanet == null)
                {
                    MyPlanet = planetObj;
                    Debug.Log("WorldManager.cs 115 내 행성 저장됨.");
                }
            }
            else
            {
                // 생성 시점의 절대 좌표 계산
                Vector3 actualPosition = CalculateAbsolutePosition(pData.sectorIndex, scaledLocalPos);

                // 원점이 아닌 계산된 실제 위치에서 즉시 생성
                GameObject newPlanet = Instantiate(planetPrefab, actualPosition, Quaternion.identity);

                PlanetController controller = newPlanet.GetComponent<PlanetController>();
                if (controller != null)
                {
                    controller.UpdateSnapshot(pData.sectorIndex, scaledLocalPos, scaledVelocity, CurrentCameraSector);
                }

                if (staticDataMap.TryGetValue(planetId, out StaticPlanetData staticData))
                {
                    if (!string.IsNullOrEmpty(staticData.colorHex) && ColorUtility.TryParseHtmlString(staticData.colorHex, out Color parsedColor))
                    {
                        Renderer rend = newPlanet.GetComponent<Renderer>();
                        if (rend != null) rend.material.color = parsedColor;
                    }
                    newPlanet.name = $"Planet_{planetId}_{staticData.name}";
                }
                else
                {
                    newPlanet.name = $"Planet_{planetId}";
                }

                activePlanets.Add(planetId, newPlanet);

                // 새로 생성된 객체가 내 행성일 경우 프로퍼티에 할당
                if (planetId == myPlanetId)
                {
                    MyPlanet = newPlanet;
                }
            }
        }

        // 현재 프레임에 없는 행성 삭제
        List<int> toRemove = new List<int>();
        foreach (var id in activePlanets.Keys)
        {
            if (!currentFrameIds.Contains(id))
            {
                // 내 행성은 네트워크 스냅샷 누락 시 파괴되지 않도록 보호
                if (id == myPlanetId) continue;

                Destroy(activePlanets[id]);
                toRemove.Add(id);
            }
        }

        foreach (var id in toRemove)
        {
            activePlanets.Remove(id);
        }
    }

    // 섹터 인덱스와 로컬 좌표를 조합하여 유니티 월드상의 절대 좌표를 반환
    private Vector3 CalculateAbsolutePosition(Vector3Int sector, Vector3 localPos)
    {
        return new Vector3(
            sector.x * sectorSize + localPos.x,
            sector.y * sectorSize + localPos.y,
            sector.z * sectorSize + localPos.z
        );
    }
}