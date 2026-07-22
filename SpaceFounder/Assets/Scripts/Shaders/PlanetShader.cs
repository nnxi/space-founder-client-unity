using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class PlanetShader : MonoBehaviour
{
    private const string ShaderName = "Custom/ProceduralPlanet";
    private Material planetMaterial;

    /// <summary>
    /// 전달받은 정적 데이터를 기반으로 절차적 셰이더 매터리얼을 세팅합니다.
    /// </summary>
    /// <param name="planetId">행성 고유 ID (난수 시드용)</param>
    /// <param name="planetType">행성의 타입 ("rocky", "gaseous", "icy")</param>
    /// <param name="colorHex">행성의 기본 색상 헥스 코드</param>
    public void ApplyShader(int planetId, string planetType, string colorHex)
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogError("[PlanetShader] Renderer 컴포넌트를 찾을 수 없습니다.");
            return;
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"[PlanetShader] {ShaderName} 셰이더를 찾을 수 없습니다. 셰이더 파일이 존재하는지 확인하세요.");
            return;
        }

        // 기존에 생성된 매터리얼이 있다면 파기하여 메모리 관리
        if (planetMaterial != null)
        {
            Destroy(planetMaterial);
        }

        planetMaterial = new Material(shader);

        // 1. 색상 파싱 및 적용
        Color baseColor = Color.white;
        if (!string.IsNullOrEmpty(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out Color parsedColor))
        {
            baseColor = parsedColor;
        }
        planetMaterial.SetColor("_BaseColor", baseColor);

        // 2. 시드값 계산 및 적용 (기존 Three.js 로직과 동일)
        float seed = planetId * 137.54f;
        planetMaterial.SetFloat("_Seed", seed);

        // 3. 행성 타입 매핑 (0: Rocky, 1: Gaseous, 2: Icy)
        int typeInt = 0;
        if (!string.IsNullOrEmpty(planetType))
        {
            string lowerType = planetType.ToLower();
            if (lowerType == "gaseous") typeInt = 1;
            else if (lowerType == "icy") typeInt = 2;
        }
        planetMaterial.SetInt("_PlanetType", typeInt);

        // 4. Renderer에 최종 매터리얼 할당
        renderer.material = planetMaterial;
    }

    private void OnDestroy()
    {
        // 오브젝트 파괴 시 동적 생성된 매터리얼 메모리 해제
        if (planetMaterial != null)
        {
            Destroy(planetMaterial);
        }
    }
}