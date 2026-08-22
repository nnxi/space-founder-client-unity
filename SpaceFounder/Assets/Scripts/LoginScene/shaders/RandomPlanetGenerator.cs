using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class RandomPlanetGenerator : MonoBehaviour
{
    private const string ShaderName = "Custom/ProceduralPlanet";
    private static Shader cachedShader;
    
    private Renderer planetRenderer;
    private MaterialPropertyBlock propBlock;

    // 셰이더 속성에 맞춘 행성 타입 배열 (0:Rocky, 1:Gas, 2:Ice)
    private readonly string[] planetTypes = { "rocky", "gaseous", "icy" };

    private void Awake()
    {
        planetRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        // 씬 시작 시 자동으로 랜덤 행성 생성
        GenerateRandomPlanet();
    }

    public void GenerateRandomPlanet()
    {
        // 무작위 시드값 생성
        int randomId = Random.Range(1, 100000);

        // 무작위 타입 선택
        string randomType = planetTypes[Random.Range(0, planetTypes.Length)];

        // 무작위 색상 생성 (너무 어둡거나 밝은 색을 피하기 위해 HSV 범위 제한)
        Color randomColor = Random.ColorHSV(0f, 1f, 0.4f, 1f, 0.5f, 1f);
        string randomColorHex = "#" + ColorUtility.ToHtmlStringRGB(randomColor);

        // 셰이더 적용 메서드 호출
        ApplyShader(randomId, randomType, randomColorHex);
    }

    public void ApplyShader(int planetId, string planetType, string colorHex)
    {
        if (planetRenderer == null)
        {
            Debug.LogError("[PlanetShader] Renderer component not found.");
            return;
        }

        if (cachedShader == null)
        {
            cachedShader = Shader.Find(ShaderName);
            if (cachedShader == null)
            {
                Debug.LogError($"[PlanetShader] Shader not found: {ShaderName}");
                return;
            }
        }

        // 공유 매터리얼 초기화
        if (planetRenderer.sharedMaterial == null || planetRenderer.sharedMaterial.shader != cachedShader)
        {
            planetRenderer.sharedMaterial = new Material(cachedShader);
        }

        planetRenderer.GetPropertyBlock(propBlock);

        // 색상 적용
        Color baseColor = Color.white;
        if (!string.IsNullOrEmpty(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out Color parsedColor))
        {
            baseColor = parsedColor;
        }
        propBlock.SetColor("_BaseColor", baseColor);

        // 시드 적용
        float seed = planetId * 137.54f;
        propBlock.SetFloat("_Seed", seed);

        // 타입 적용
        int typeInt = 0;
        if (!string.IsNullOrEmpty(planetType))
        {
            string lowerType = planetType.ToLower();
            if (lowerType == "gaseous") typeInt = 1;
            else if (lowerType == "icy") typeInt = 2;
        }
        propBlock.SetInt("_PlanetType", typeInt);

        // 갱신된 속성 적용
        planetRenderer.SetPropertyBlock(propBlock);
    }
}