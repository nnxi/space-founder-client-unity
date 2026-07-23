using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class PlanetShader : MonoBehaviour
{
    private const string ShaderName = "Custom/ProceduralPlanet";
    private static Shader cachedShader;
    
    private Renderer planetRenderer;
    private MaterialPropertyBlock propBlock;

    private void Awake()
    {
        planetRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
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

        // 매터리얼 인스턴스가 없다면 공유 매터리얼로 초기화
        if (planetRenderer.sharedMaterial == null || planetRenderer.sharedMaterial.shader != cachedShader)
        {
            planetRenderer.sharedMaterial = new Material(cachedShader);
        }

        // PropertyBlock을 가져와 파라미터 갱신 후 다시 설정 (메모리 최적화)
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

        // 갱신된 PropertyBlock 적용
        planetRenderer.SetPropertyBlock(propBlock);
    }
}