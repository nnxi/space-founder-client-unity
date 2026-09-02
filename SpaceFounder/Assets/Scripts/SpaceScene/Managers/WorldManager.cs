using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance { get; private set; }

    [Header("Prefabs & Scaling")]
    // 프리팹 2종류로 분리
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

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.EmitSubscribeGrid(gridSectors);
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

                // 정적 행성이므로 staticPlanetPrefab 사용
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
                
                // 타입에 따라 생성할 프리팹 결정
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

                // 정적 여부를 isDefault 값으로 판단하여 컨트롤러에 전달
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
            case "icy":
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

            // 트레일 렌더러가 있는 유저 행성은 컨트롤러 내부에서 렌더러 배열 수정 후 이동을 처리
            if (controller != null)
            {
                controller.ApplyWorldShift(shiftAmount);
            }
            else
            {
                // 컨트롤러가 없는 단순 배경/정적 오브젝트는 여기서 직접 이동
                planetObj.transform.position -= shiftAmount;
            }
        }
    }
}