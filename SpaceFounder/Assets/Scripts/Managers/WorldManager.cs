using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance { get; private set; }

    [SerializeField] private GameObject planetPrefab;
    [SerializeField] private float scaleFactor = 0.01f;
    [SerializeField] private float sectorSize = 1000f;

    public Vector3Int CurrentCameraSector { get; set; } = Vector3Int.zero;

    // 내 행성이 담길 곳
    public GameObject MyPlanet { get; private set; }

    // [수정] 고유 키("user_9", "default_9")를 사용하는 딕셔너리로 전환
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

    public void SetStaticData(StaticPlanetData[] staticPlanets)
    {
        // staticDataMap.Clear();
        if (staticPlanets == null) return;

        foreach (var sp in staticPlanets)
        {
            // userType(role)과 planetId를 조합해 고유 키 생성 (예: "user_9", "default_9")
            string type = string.IsNullOrEmpty(sp.userType) ? "user" : sp.userType;
            string key = $"{type}_{sp.planetId}";
            staticDataMap[key] = sp;
            Debug.Log($"<color=green>[WorldManager] SetStaticData map added -> Key: {key}, Name: {sp.planetName}</color>");
        }
    }

    // [수정] 숫자 ID 및 type으로 PlanetController 조회
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
        if (planetPrefab == null) return;

        int myPlanetId = NetworkManager.Instance != null ? NetworkManager.Instance.MyPlanetId : -1;
        HashSet<string> currentFrameKeys = new HashSet<string>();

        foreach (var pData in planets)
        {
            int rawId = pData.id;
            
            // 🔥 핵심: 음수면 default, 양수면 user. 부호 떼고 실제 ID 추출.
            bool isDefault = rawId < 0;
            int actualId = Mathf.Abs(rawId);
            
            // 고유 키를 완벽하고 확실하게 생성
            string uniqueKey = isDefault ? $"default_{actualId}" : $"user_{actualId}";
            currentFrameKeys.Add(uniqueKey);

            // 해당 키의 정적 데이터가 있는지 확인
            staticDataMap.TryGetValue(uniqueKey, out StaticPlanetData staticData);
            bool hasStaticData = !string.IsNullOrEmpty(staticData.planetName);

            Vector3 scaledLocalPos = pData.localPosition * scaleFactor;
            Vector3 scaledVelocity = pData.velocity * scaleFactor;

            if (activePlanets.TryGetValue(uniqueKey, out GameObject planetObj))
            {
                // 기존 객체 갱신
                PlanetController controller = planetObj.GetComponent<PlanetController>();
                if (controller != null)
                {
                    controller.UpdateSnapshot(pData.sectorIndex, scaledLocalPos, scaledVelocity, CurrentCameraSector);
                }

                if (uniqueKey == $"user_{myPlanetId}" && MyPlanet == null)
                {
                    MyPlanet = planetObj;
                }
            }
            else
            {
                // 새 객체 생성
                Vector3 actualPosition = CalculateAbsolutePosition(pData.sectorIndex, scaledLocalPos);
                GameObject newPlanet = Instantiate(planetPrefab, actualPosition, Quaternion.identity);

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

                if (uniqueKey == $"user_{myPlanetId}")
                {
                    MyPlanet = newPlanet;
                }
            }
        }

        // 현재 프레임에 없는 행성 제거
        List<string> toRemove = new List<string>();
        string myPlanetKey = $"user_{myPlanetId}";

        foreach (var key in activePlanets.Keys)
        {
            if (!currentFrameKeys.Contains(key))
            {
                // 내 행성은 누락 시 삭제 보호
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
}