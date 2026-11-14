using System.Collections.Generic;
using UnityEngine;

// WorldManager에서 분리: 행성 생성/등록/제거/월드 시프트 담당 (MonoBehaviour 아님, 네트워크 호출 없음)
// 프리팹/스케일 값은 WorldManager의 [SerializeField]에서 생성자로 전달받으므로 인스펙터 바인딩은 WorldManager에 그대로 유지됨
public class PlanetSpawner
{
    private readonly GameObject userPlanetPrefab;
    private readonly GameObject staticPlanetPrefab;
    private readonly float scaleFactor;
    private readonly float sectorSize;

    private readonly Dictionary<string, StaticPlanetData> staticDataMap = new Dictionary<string, StaticPlanetData>();
    private readonly Dictionary<string, GameObject> activePlanets = new Dictionary<string, GameObject>();
    // activePlanets와 키를 맞춰 유지하는 PlanetController 캐시 (GetComponent 반복 호출 방지)
    private readonly Dictionary<string, PlanetController> planetControllers = new Dictionary<string, PlanetController>();

    public int MyPlanetId { get; set; } = -1;
    public GameObject MyPlanet { get; private set; }

    public PlanetSpawner(GameObject userPlanetPrefab, GameObject staticPlanetPrefab, float scaleFactor, float sectorSize)
    {
        this.userPlanetPrefab = userPlanetPrefab;
        this.staticPlanetPrefab = staticPlanetPrefab;
        this.scaleFactor = scaleFactor;
        this.sectorSize = sectorSize;
    }

    // 버퍼 존을 벗어난 섹터의 정적 천체 메모리 해제
    // protectedPlanetId: 삭제에서 제외할 내 행성 ID (기존과 동일하게 NetworkManager.MyPlanetId 기준, 없으면 null)
    public void UnloadSectors(List<Vector3Int> sectorsToClear, int? protectedPlanetId)
    {
        if (sectorsToClear == null || sectorsToClear.Count == 0) return;

        HashSet<Vector3Int> clearSet = new HashSet<Vector3Int>(sectorsToClear);
        List<string> keysToRemove = new List<string>();

        // 삭제할 섹터에 속한 정적 행성 탐색
        foreach (var kvp in staticDataMap)
        {
            if (clearSet.Contains(kvp.Value.chunkIndex.ToVector3Int()))
            {
                // 내 행성은 삭제 대상에서 제외
                if (protectedPlanetId.HasValue && kvp.Value.planetId == protectedPlanetId.Value)
                {
                    continue;
                }

                keysToRemove.Add(kvp.Key);
            }
        }

        // 씬에서 파괴 및 딕셔너리에서 제거
        foreach (var key in keysToRemove)
        {
            if (activePlanets.TryGetValue(key, out GameObject obj))
            {
                Object.Destroy(obj);
                activePlanets.Remove(key);
            }
            planetControllers.Remove(key);
            staticDataMap.Remove(key);
        }

        Debug.Log($"[WorldManager] Unloaded {sectorsToClear.Count} sectors and destroyed {keysToRemove.Count} static planets.");
    }

    public void SetStaticData(StaticPlanetData[] staticPlanets, Vector3Int cameraSector)
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

                GameObject newPlanet = Object.Instantiate(staticPlanetPrefab, absolutePosition, Quaternion.identity);

                newPlanet.transform.localScale = GetPlanetScale(sp.planetType, sp.planetId);
                newPlanet.name = $"{key}_{sp.planetName}";

                PlanetController controller = newPlanet.GetComponent<PlanetController>();
                if (controller != null)
                {
                    controller.UpdateSnapshot(sp.chunkIndex.ToVector3Int(), scaledLocalPos, Vector3.zero, cameraSector);
                    controller.SetPlanetData(sp.planetName, sp.username, sp.planetType, true);
                    planetControllers[key] = controller;
                }

                PlanetShader shaderComp = newPlanet.GetComponent<PlanetShader>();
                if (shaderComp != null) shaderComp.ApplyShader(sp.planetId, sp.planetType, sp.colorHex);

                activePlanets.Add(key, newPlanet);
            }
        }
    }

    public void ProcessWorldUpdate(DecodedPlanetSnapshot[] planets, Vector3Int cameraSector)
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

            // [수정 핵심 1] 딕셔너리에 키가 있는지 뿐만 아니라, 씬에 오브젝트가 살아있는지(planetObj != null) 확실히 체크
            if (activePlanets.TryGetValue(uniqueKey, out GameObject planetObj) && planetObj != null)
            {
                planetControllers.TryGetValue(uniqueKey, out PlanetController controller);
                if (controller != null)
                {
                    controller.UpdateSnapshot(pData.sectorIndex, scaledLocalPos, scaledVelocity, cameraSector);
                }

                if (!isDefault && actualId == MyPlanetId && MyPlanet == null)
                {
                    MyPlanet = planetObj;
                }
            }
            else
            {
                // [수정 핵심 2] 딕셔너리에는 없지만 MyPlanet 레퍼런스는 씬에 살아있는 엣지 케이스 방어 (복수 생성 원천 차단)
                if (!isDefault && actualId == MyPlanetId && MyPlanet != null)
                {
                    activePlanets[uniqueKey] = MyPlanet;

                    PlanetController myController = MyPlanet.GetComponent<PlanetController>();
                    if (myController != null)
                    {
                        planetControllers[uniqueKey] = myController;
                        myController.UpdateSnapshot(pData.sectorIndex, scaledLocalPos, scaledVelocity, cameraSector);
                    }
                    continue;
                }

                // 정상적인 신규 생성 로직
                Vector3 absolutePosition = CalculateAbsolutePosition(pData.sectorIndex, scaledLocalPos);

                GameObject prefabToInstantiate = isDefault ? staticPlanetPrefab : userPlanetPrefab;
                GameObject newPlanet = Object.Instantiate(prefabToInstantiate, absolutePosition, Quaternion.identity);

                PlanetController controller = newPlanet.GetComponent<PlanetController>();
                if (controller != null)
                {
                    planetControllers[uniqueKey] = controller;
                    controller.UpdateSnapshot(pData.sectorIndex, scaledLocalPos, scaledVelocity, cameraSector);
                }
                else
                {
                    planetControllers.Remove(uniqueKey);
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

                // [수정 핵심 3] Add() 대신 인덱서(=) 사용. 파괴된 참조 찌꺼기가 남아있을 때 ArgumentException 발생을 방지
                activePlanets[uniqueKey] = newPlanet;

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
                    if (key == myPlanetKey)
                    {
                        // [수정 핵심 4] 무조건 건너뛰는 게 아니라, 내 행성이 진짜로 파괴(Null)되었다면 딕셔너리에서도 완전히 지워주어 다음 프레임에 정상 생성되도록 유도
                        if (MyPlanet == null) toRemove.Add(key);
                        continue;
                    }

                    // 이미 파괴된 오브젝트에 Destroy를 호출하지 않도록 안전 장치 추가
                    if (activePlanets[key] != null)
                    {
                        Object.Destroy(activePlanets[key]);
                    }
                    toRemove.Add(key);
                }
            }
        }

        foreach (var key in toRemove)
        {
            activePlanets.Remove(key);
            planetControllers.Remove(key);
        }
    }

    public void ShiftAll(Vector3 shiftAmount)
    {
        foreach (var kvp in activePlanets)
        {
            GameObject planetObj = kvp.Value;
            if (planetObj == null) continue;

            planetControllers.TryGetValue(kvp.Key, out PlanetController controller);

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
}
