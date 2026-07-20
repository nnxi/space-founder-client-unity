using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance { get; private set; }

    [SerializeField] private GameObject planetPrefab;
    [SerializeField] private float scaleFactor = 0.01f;

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

        HashSet<int> currentFrameIds = new HashSet<int>();

        foreach (var pData in planets)
        {
            int planetId = pData.id;
            currentFrameIds.Add(planetId);
            
            // 좌표 및 속도 스케일 변환
            Vector3 targetPos = pData.position * scaleFactor;
            Vector3 targetVel = pData.velocity * scaleFactor;

            if (activePlanets.TryGetValue(planetId, out GameObject planetObj))
            {
                PlanetController controller = planetObj.GetComponent<PlanetController>();
                if (controller != null)
                {
                    controller.UpdateSnapshot(targetPos, targetVel);
                }
            }
            else
            {
                GameObject newPlanet = Instantiate(planetPrefab, targetPos, Quaternion.identity);
                newPlanet.name = $"Planet_{planetId}";

                PlanetController controller = newPlanet.GetComponent<PlanetController>();
                if (controller != null)
                {
                    controller.UpdateSnapshot(targetPos, targetVel);
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

                activePlanets.Add(planetId, newPlanet);
            }
        }

        // 현재 프레임에 없는 행성 삭제
        List<int> toRemove = new List<int>();
        foreach (var id in activePlanets.Keys)
        {
            if (!currentFrameIds.Contains(id))
            {
                Destroy(activePlanets[id]);
                toRemove.Add(id);
            }
        }

        foreach (var id in toRemove)
        {
            activePlanets.Remove(id);
        }
    }
}