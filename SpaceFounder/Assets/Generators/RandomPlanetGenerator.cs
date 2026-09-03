using UnityEngine;

[RequireComponent(typeof(PlanetShader))]
public class RandomPlanetGenerator : MonoBehaviour
{
    public int CurrentId { get; private set; }
    public string CurrentType { get; private set; }
    public string CurrentColorHex { get; private set; }

    private PlanetShader planetShader;
    
    // 유저가 생성할 수 있는 3가지 타입
    private readonly string[] planetTypes = { "rocky", "gaseous", "ice" };

    private void Awake()
    {
        planetShader = GetComponent<PlanetShader>();
    }

    private void Start()
    {
        GenerateRandomPlanet();
    }

    public void GenerateRandomPlanet()
    {
        CurrentId = Random.Range(1, 100000);
        CurrentType = planetTypes[Random.Range(0, planetTypes.Length)];
        
        Color randomColor = Random.ColorHSV(0f, 1f, 0.4f, 1f, 0.5f, 1f);
        CurrentColorHex = "#" + ColorUtility.ToHtmlStringRGB(randomColor);

        if (planetShader != null)
        {
            planetShader.ApplyShader(CurrentId, CurrentType, CurrentColorHex);
        }
    }
}