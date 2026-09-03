using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance { get; private set; }

    [Header("Prefabs & Scaling")]
    [SerializeField] private GameObject userPlanetPrefab;
    [SerializeField] private GameObject staticPlanetPrefab;
    [SerializeField] private float scaleFactor = 0.01f;
    [SerializeField] private float sectorSize = 1000f;

    public int MyPlanetId { get; private set; } = -1;
    public Vector3Int CurrentCameraSector { get; private set; } = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    public GameObject MyPlanet { get; private set; }

    public Vector3Int BaseSector { get; private set; }

    private Dictionary<string, StaticPlanetData> staticDataMap = new Dictionary<string, StaticPlanetData>();
    private Dictionary<string, GameObject> activePlanets = new Dictionary<string, GameObject>();
    
    private ConcurrentQueue<DecodedPlanetSnapshot[]> updateQueue = new ConcurrentQueue<DecodedPlanetSnapshot[]>();

    private HashSet<Vector3Int> activeSectors = new HashSet<Vector3Int>();

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

    public void InitializePlayer(int planetId, Vector3Int initialSector)
    {
        MyPlanetId = planetId;
        BaseSector = initialSector;
        UpdateCameraSector(initialSector, true);
        Debug.Log($"[WorldManager] Player init - ID: {MyPlanetId}, Sector: {initialSector}");
    }

    public void UpdateCameraSector(Vector3Int newSector, bool forceUpdate = false)
    {
        if (!forceUpdate && CurrentCameraSector == newSector) return;
        CurrentCameraSector = newSector;

        HashSet<Vector3Int> newCoreSectors = new HashSet<Vector3Int>();   // 3x3x3 구독(요청) 구역
        HashSet<Vector3Int> newBufferSectors = new HashSet<Vector3Int>(); // 5x5x5 유지(버퍼) 구역
        
        for (int x = -2; x <= 2; x++)
        {
            for (int y = -2; y <= 2; y++)
            {
                for (int z = -2; z <= 2; z++)
                {
                    Vector3Int sec = new Vector3Int(newSector.x + x, newSector.y + y, newSector.z + z);
                    
                    // 5x5x5 버퍼 영역
                    newBufferSectors.Add(sec);
                    
                    // 3x3x3 코어 영역
                    if (Mathf.Abs(x) <= 1 && Mathf.Abs(y) <= 1 && Mathf.Abs(z) <= 1)
                    {
                        newCoreSectors.Add(sec);
                    }
                }
            }
        }

        List<Vector3Int> sectorsToSubscribe = new List<Vector3Int>();
        List<Vector3Int> sectorsToUnsubscribe = new List<Vector3Int>();

        // 1. 구독 해제 (버리는 로직): 기존에 활성화된 섹터 중, 5x5x5 버퍼를 '완전히 벗어난' 섹터
        foreach (var sector in activeSectors)
        {
            if (!newBufferSectors.Contains(sector))
            {
                sectorsToUnsubscribe.Add(sector);
            }
        }

        // 2. 새로 구독: 3x3x3 코어 영역에 새로 들어온 섹터
        foreach (var sector in newCoreSectors)
        {
            if (!activeSectors.Contains(sector))
            {
                sectorsToSubscribe.Add(sector);
            }
        }

        // activeSectors 리스트 갱신
        foreach (var sector in sectorsToUnsubscribe) activeSectors.Remove(sector);
        foreach (var sector in sectorsToSubscribe) activeSectors.Add(sector);

        // 3. 완전히 멀어진 섹터의 정적 행성들을 씬(메모리)에서 파괴
        UnloadSectors(sectorsToUnsubscribe);

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.EmitSubscribeGrid(sectorsToSubscribe);
            NetworkManager.Instance.EmitUnsubscribeGrid(sectorsToUnsubscribe);
        }
    }

    // 버퍼 존을 벗어난 섹터의 정적 천체 메모리 해제
    private void UnloadSectors(List<Vector3Int> sectorsToClear)
    {
        if (sectorsToClear == null || sectorsToClear.Count == 0) return;

        HashSet<Vector3Int> clearSet = new HashSet<Vector3Int>(sectorsToClear);
        List<string> keysToRemove = new List<string>();

        // staticDataMap을 순회하며 삭제할 섹터에 속한 정적 행성을 찾음
        foreach (var kvp in staticDataMap)
        {
            if (clearSet.Contains(kvp.Value.chunkIndex.ToVector3Int()))
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        // 찾은 행성들을 씬에서 파괴하고 딕셔너리에서 제거
        foreach (var key in keysToRemove)
        {
            if (activePlanets.TryGetValue(key, out GameObject obj))
            {
                Destroy(obj); 
                activePlanets.Remove(key);
            }
            staticDataMap.Remove(key);
        }
        
        Debug.Log($"[WorldManager] Unloaded {sectorsToClear.Count} sectors and destroyed {keysToRemove.Count} static planets.");
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
        if (staticPlanets == null) return;

        foreach (var sp in staticPlanets)
        {
            string type = string.IsNullOrEmpty(sp.userType) ? "user" : sp.userType;
            string key = $"{type}_{sp.planetId}";
            staticDataMap[key] = sp;

            if (type == "default" && !activePlanets.ContainsKey(key))
            {
                Vector3 scaledLocalPos = sp.localPosition.ToVector3() * scaleFactor;
                Vector3 absolutePosition = CalculateAbsolutePosition(sp.chunkIndex.ToVector3Int(), scaledLocalPos);

                GameObject newPlanet = Instantiate(staticPlanetPrefab, absolutePosition, Quaternion.identity);
                
                newPlanet.transform.localScale = GetPlanetScale(sp.planetType, sp.planetId);
                newPlanet.name = $"{key}_{sp.planetName}";

                PlanetController controller = newPlanet.GetComponent<PlanetController>();
                if (controller != null)
                {
                    controller.UpdateSnapshot(sp.chunkIndex.ToVector3Int(), scaledLocalPos, Vector3.zero, CurrentCameraSector);
                    controller.SetPlanetData(sp.planetName, sp.username, sp.planetType, true);
                }

                PlanetShader shaderComp = newPlanet.GetComponent<PlanetShader>();
                if (shaderComp != null) shaderComp.ApplyShader(sp.planetId, sp.planetType, sp.colorHex);

                activePlanets.Add(key, newPlanet);
            }
        }
    }

    public void OnWorldUpdateReceived(DecodedPlanetSnapshot[] planets)
    {
        updateQueue.Enqueue(planets);
    }

    private void ProcessWorldUpdate(DecodedPlanetSnapshot[] planets)
    {
        if (userPlanetPrefab == null || staticPlanetPrefab == null) return;

        HashSet<string> currentFrameKeys = new HashSet<string>();

        foreach (var pData in planets)
        {
            int rawId = pData.id;
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
                
                GameObject prefabToInstantiate = isDefault ? staticPlanetPrefab : userPlanetPrefab;
                GameObject newPlanet = Instantiate(prefabToInstantiate, absolutePosition, Quaternion.identity);

                PlanetController controller = newPlanet.GetComponent<PlanetController>();
                if (controller != null)
                {
                    controller.UpdateSnapshot(pData.sectorIndex, scaledLocalPos, scaledVelocity, CurrentCameraSector);
                }

                if (hasStaticData)
                {
                    newPlanet.transform.localScale = GetPlanetScale(staticData.planetType, actualId);

                    PlanetShader shaderComp = newPlanet.GetComponent<PlanetShader>();
                    if (shaderComp != null) shaderComp.ApplyShader(actualId, staticData.planetType, staticData.colorHex);
                    newPlanet.name = $"{uniqueKey}_{staticData.planetName}";
                }
                else
                {
                    newPlanet.name = uniqueKey;
                }

                if (controller != null)
                {
                    controller.SetPlanetData(staticData.planetName, staticData.username, staticData.planetType, isDefault);
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
            if (key.StartsWith("user_"))
            {
                if (!currentFrameKeys.Contains(key))
                {
                    if (key == myPlanetKey) continue;

                    Destroy(activePlanets[key]);
                    toRemove.Add(key);
                }
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

    private Vector3 GetPlanetScale(string planetType, int planetId)
    {
        if (string.IsNullOrEmpty(planetType)) return Vector3.one;

        string lowerType = planetType.ToLower();
        
        float randomVariance = 0.8f + ((Mathf.Abs(planetId) % 100) / 100f) * 0.4f;

        switch (lowerType)
        {
            case "star":
                return Vector3.one * 70f * randomVariance; 
            case "lava":
                return Vector3.one * 25f * randomVariance; 
            case "gaseous":
            case "gas":
                return Vector3.one * 8f * randomVariance; 
            case "ice":
                return Vector3.one * 1.5f * randomVariance; 
            case "rocky":
            default:
                return Vector3.one * 1f * randomVariance; 
        }
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

    public void ShiftAllPlanets(Vector3 shiftAmount)
    {
        foreach (var planetObj in activePlanets.Values)
        {
            if (planetObj == null) continue;

            PlanetController controller = planetObj.GetComponent<PlanetController>();

            if (controller != null)
            {
                controller.ApplyWorldShift(shiftAmount);
            }
            else
            {
                planetObj.transform.position -= shiftAmount;
            }
        }
    }
}