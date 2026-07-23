using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance { get; private set; }

    [Header("Prefabs & Scaling")]
    [SerializeField] private GameObject planetPrefab;
    [SerializeField] private float scaleFactor = 0.01f;
    [SerializeField] private float sectorSize = 1000f;

    public int MyPlanetId { get; private set; } = -1;
    public Vector3Int CurrentCameraSector { get; private set; } = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    public GameObject MyPlanet { get; private set; }

    private Dictionary<string, StaticPlanetData> staticDataMap = new Dictionary<string, StaticPlanetData>();
    private Dictionary<string, GameObject> activePlanets = new Dictionary<string, GameObject>();
    
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

    private void Update()
    {
        while (updateQueue.TryDequeue(out DecodedPlanetSnapshot[] planets))
        {
            ProcessWorldUpdate(planets);
        }
    }

    // NetworkManager에서 최초 접속 시 1회 호출
    public void InitializePlayer(int planetId, Vector3Int initialSector)
    {
        MyPlanetId = planetId;
        UpdateCameraSector(initialSector, true);
        Debug.Log($"[WorldManager] 플레이어 초기화 완료 - ID: {MyPlanetId}, 초기 섹터: {initialSector}");
    }

    // CameraChunkTracker에서 섹터 변경을 감지했을 때 호출
    public void UpdateCameraSector(Vector3Int newSector, bool forceUpdate = false)
    {
        if (!forceUpdate && CurrentCameraSector == newSector) return;

        CurrentCameraSector = newSector;

        // 중심 섹터를 기준으로 3x3x3 (27개) 구독망 생성
        List<Vector3Int> gridSectors = new List<Vector3Int>();
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    gridSectors.Add(new Vector3Int(newSector.x + x, newSector.y + y, newSector.z + z));
                }
            }
        }

        // NetworkManager를 통해 구독 발송
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.EmitSubscribeGrid(gridSectors);
        }
    }

    // CameraController 등 외부 스크립트에서 내 행성 위치를 네트워크로 요청할 때 사용하는 중간다리
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
        if (staticPlanets == null) return;

        foreach (var sp in staticPlanets)
        {
            string type = string.IsNullOrEmpty(sp.userType) ? "user" : sp.userType;
            string key = $"{type}_{sp.planetId}";
            staticDataMap[key] = sp;
        }
    }

    public void OnWorldUpdateReceived(DecodedPlanetSnapshot[] planets)
    {
        updateQueue.Enqueue(planets);
    }

    private void ProcessWorldUpdate(DecodedPlanetSnapshot[] planets)
    {
        if (planetPrefab == null) return;

        HashSet<string> currentFrameKeys = new HashSet<string>();

        foreach (var pData in planets)
        {
            int rawId = pData.id;
            
            // 바이너리 프로토콜 약속: 음수면 default, 양수면 user
            bool isDefault = rawId < 0;
            int actualId = Mathf.Abs(rawId);
            
            string uniqueKey = isDefault ? $"default_{actualId}" : $"user_{actualId}";
            currentFrameKeys.Add(uniqueKey);

            staticDataMap.TryGetValue(uniqueKey, out StaticPlanetData staticData);
            bool hasStaticData = !string.IsNullOrEmpty(staticData.planetName);

            Vector3 scaledLocalPos = pData.localPosition * scaleFactor;
            Vector3 scaledVelocity = pData.velocity * scaleFactor;

            if (activePlanets.TryGetValue(uniqueKey, out GameObject planetObj))
            {
                PlanetController controller = planetObj.GetComponent<PlanetController>();
                if (controller != null)
                {
                    controller.UpdateSnapshot(pData.sectorIndex, scaledLocalPos, scaledVelocity, CurrentCameraSector);
                }

                if (!isDefault && actualId == MyPlanetId && MyPlanet == null)
                {
                    MyPlanet = planetObj;
                }
            }
            else
            {
                Vector3 absolutePosition = CalculateAbsolutePosition(pData.sectorIndex, scaledLocalPos);
                GameObject newPlanet = Instantiate(planetPrefab, absolutePosition, Quaternion.identity);

                PlanetController controller = newPlanet.GetComponent<PlanetController>();
                if (controller != null)
                {
                    controller.UpdateSnapshot(pData.sectorIndex, scaledLocalPos, scaledVelocity, CurrentCameraSector);
                }

                if (hasStaticData)
                {
                    PlanetShader shaderComp = newPlanet.GetComponent<PlanetShader>();
                    if (shaderComp != null) shaderComp.ApplyShader(actualId, staticData.planetType, staticData.colorHex);
                    newPlanet.name = $"{uniqueKey}_{staticData.planetName}";
                }
                else
                {
                    newPlanet.name = uniqueKey;
                }

                activePlanets.Add(uniqueKey, newPlanet);

                if (!isDefault && actualId == MyPlanetId)
                {
                    MyPlanet = newPlanet;
                }
            }
        }

        RemoveStalePlanets(currentFrameKeys);
    }

    private void RemoveStalePlanets(HashSet<string> currentFrameKeys)
    {
        List<string> toRemove = new List<string>();
        string myPlanetKey = $"user_{MyPlanetId}";

        foreach (var key in activePlanets.Keys)
        {
            if (!currentFrameKeys.Contains(key))
            {
                // 내 행성은 시야 밖으로 벗어나더라도 씬에서 강제 삭제되는 것을 방지
                if (key == myPlanetKey) continue;

                Destroy(activePlanets[key]);
                toRemove.Add(key);
            }
        }

        foreach (var key in toRemove)
        {
            activePlanets.Remove(key);
        }
    }

    private Vector3 CalculateAbsolutePosition(Vector3Int sector, Vector3 localPos)
    {
        return new Vector3(
            sector.x * sectorSize + localPos.x,
            sector.y * sectorSize + localPos.y,
            sector.z * sectorSize + localPos.z
        );
    }

    public PlanetController GetPlanetController(int planetId, string userType = "user")
    {
        string key = $"{userType}_{planetId}";
        if (activePlanets.TryGetValue(key, out GameObject planetObj))
        {
            return planetObj.GetComponent<PlanetController>();
        }
        return null;
    }

    public GameObject GetPlanet(int planetId, string userType = "user")
    {
        string key = $"{userType}_{planetId}";
        if (activePlanets.TryGetValue(key, out GameObject planetObj))
        {
            return planetObj;
        }
        return null;
    }
}