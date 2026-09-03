using UnityEngine;

[RequireComponent(typeof(PlanetShader))]
public class SceneStarSetup : MonoBehaviour
{
    [Header("Star Settings")]
    public string starColorHex = "#FFFFFF"; // 기본 백색광
    public float lightIntensityScale = 1f;

    private void Start()
    {
        PlanetShader shader = GetComponent<PlanetShader>();
        
        // 아이디는 임의의 값(999), 타입은 "star", 색상은 설정한 헥스 코드로 쉐이더 적용
        shader.ApplyShader(-1, "star", starColorHex);
        
        // 스케일을 키우면 빛의 영향력과 시각적 크기가 커집니다
        transform.localScale = Vector3.one * 50f * lightIntensityScale;
    }
}