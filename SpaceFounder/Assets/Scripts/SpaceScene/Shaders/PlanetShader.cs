using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Renderer))]
public class PlanetShader : MonoBehaviour
{
    private const string ShaderName = "Custom/ProceduralPlanet";
    private static Shader cachedShader;

    private Vector4[] lightDirs = new Vector4[3];
    private Vector4[] lightColors = new Vector4[3];
    
    // 메모리 재할당(GC) 방지를 위한 리스트 캐싱
    private List<KeyValuePair<float, PlanetShader>> sortedStars = new List<KeyValuePair<float, PlanetShader>>();
    
    private Renderer planetRenderer;
    private MaterialPropertyBlock propBlock;

    // 활성화된 모든 항성을 추적
    public static HashSet<PlanetShader> ActiveStars = new HashSet<PlanetShader>();
    private bool isStar = false;
    private Vector3 currentLightDir = new Vector3(1, 1, 0.5f);

    private void Awake()
    {
        planetRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
    }

    public void ApplyShader(int planetId, string planetType, string colorHex)
    {
        if (planetRenderer == null) return;
        if (cachedShader == null) cachedShader = Shader.Find(ShaderName);

        if (planetRenderer.sharedMaterial == null || planetRenderer.sharedMaterial.shader != cachedShader)
        {
            planetRenderer.sharedMaterial = new Material(cachedShader);
        }

        planetRenderer.GetPropertyBlock(propBlock);

        Color baseColor = Color.white;
        if (!string.IsNullOrEmpty(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out Color parsedColor))
        {
            baseColor = parsedColor;
        }
        propBlock.SetColor("_BaseColor", baseColor);

        float seed = planetId * 137.54f;
        propBlock.SetFloat("_Seed", seed);

        int typeInt = 0;
        if (!string.IsNullOrEmpty(planetType))
        {
            string lowerType = planetType.ToLower();
            if (lowerType == "gaseous" || lowerType == "gas") typeInt = 1;
            else if (lowerType == "icy" || lowerType == "ice") typeInt = 2;
            else if (lowerType == "lava") typeInt = 3;
            else if (lowerType == "star") typeInt = 4;
        }
        propBlock.SetInt("_PlanetType", typeInt);

        planetRenderer.SetPropertyBlock(propBlock);

        HandleStarLight(typeInt, baseColor);
    }

    private void HandleStarLight(int typeInt, Color lightColor)
    {
        Light pointLight = GetComponent<Light>();

        if (typeInt == 4)
        {
            isStar = true;
            ActiveStars.Add(this); 

            if (pointLight == null) pointLight = gameObject.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.range = 4000f; 
            pointLight.intensity = 5f; 
            pointLight.color = lightColor;
        }
        else if (pointLight != null)
        {
            Destroy(pointLight);
        }
    }

    private void Update()
    {
        if (isStar || ActiveStars.Count == 0) return;

        // 매 프레임 할당하던 리스트를 비우고 재사용
        sortedStars.Clear();

        foreach (var star in ActiveStars)
        {
            if (star == null) continue;
            float distSq = (star.transform.position - transform.position).sqrMagnitude;
            sortedStars.Add(new KeyValuePair<float, PlanetShader>(distSq, star));
        }

        // 거리 기준 오름차순 정렬
        sortedStars.Sort((x, y) => x.Key.CompareTo(y.Key));

        // 배열 초기화
        for (int i = 0; i < 3; i++)
        {
            lightDirs[i] = Vector4.zero;
            lightColors[i] = Vector4.zero;
        }

        // 상위 최대 3개의 항성 데이터 추출
        int limit = Mathf.Min(3, sortedStars.Count);
        for (int i = 0; i < limit; i++)
        {
            PlanetShader star = sortedStars[i].Value;
            float distSq = sortedStars[i].Key;

            if (distSq < 1f) continue;

            Vector3 dir = (star.transform.position - transform.position).normalized;
            // 거리 역제곱에 비례하는 광원 가중치 계산 (임의의 감쇠 상수 적용)
            float intensity = Mathf.Clamp01(10000000f / distSq); 

            // xyz는 방향, w는 빛의 세기
            lightDirs[i] = new Vector4(dir.x, dir.y, dir.z, intensity);
            
            // 항성의 색상을 가져옴
            Color sColor = star.propBlock.GetColor("_BaseColor");
            lightColors[i] = new Vector4(sColor.r, sColor.g, sColor.b, 1f);
        }

        // 쉐이더로 다중 배열 데이터 전송
        planetRenderer.GetPropertyBlock(propBlock);
        propBlock.SetVectorArray("_LightDirs", lightDirs);
        propBlock.SetVectorArray("_LightColors", lightColors);
        planetRenderer.SetPropertyBlock(propBlock);
    }

    private void OnDestroy()
    {
        if (isStar)
        {
            ActiveStars.Remove(this);
        }
    }
}