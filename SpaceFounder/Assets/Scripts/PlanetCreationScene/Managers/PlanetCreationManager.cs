using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class PlanetCreationManager : MonoBehaviour
{
    [Header("3D Planet Target")]
    public Transform planetTransform;
    public Renderer planetRenderer;

    [Header("UI References")]
    public TextMeshProUGUI typeText;
    public TextMeshProUGUI seedText;
    public TextMeshProUGUI coreColorText;
    public TMP_InputField planetNameInput;
    public Button generateRandomButton;
    public Button launchButton;
    public TextMeshProUGUI errorText;

    private const string ApiBaseUrl = "http://localhost:3000/api";
    private const string ShaderName = "Custom/ProceduralPlanet";
    
    private static Shader cachedShader;
    private MaterialPropertyBlock propBlock;
    
    private string currentPlanetType;
    private string currentColorHex;
    private int currentNumericId;

    private void Awake()
    {
        // 런타임 메모리 할당 최소화를 위해 Awake에서 한 번만 초기화
        propBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        if (generateRandomButton != null)
            generateRandomButton.onClick.AddListener(GenerateRandomPlanet);

        if (launchButton != null)
            launchButton.onClick.AddListener(OnLaunchClicked);

        if (planetNameInput != null)
            planetNameInput.onValueChanged.AddListener(ValidateInput);

        GenerateRandomPlanet();
        ValidateInput(planetNameInput.text);
    }

    private void Update()
    {
        if (planetNameInput.isFocused)
        {
            Input.imeCompositionMode = IMECompositionMode.Off;
        }
        else
        {
            Input.imeCompositionMode = IMECompositionMode.On;
        }
    }

    private void OnDestroy()
    {
        if (generateRandomButton != null) generateRandomButton.onClick.RemoveListener(GenerateRandomPlanet);
        if (launchButton != null) launchButton.onClick.RemoveListener(OnLaunchClicked);
        if (planetNameInput != null) planetNameInput.onValueChanged.RemoveListener(ValidateInput);
    }

    private void ValidateInput(string text)
    {
        if (launchButton != null)
        {
            launchButton.interactable = !string.IsNullOrEmpty(text.Trim());
        }
    }

    private void GenerateRandomPlanet()
    {
        string[] types = { "rocky", "gaseous", "icy" };
        currentPlanetType = types[Random.Range(0, types.Length)];
        
        Color randomColor = Random.ColorHSV(0f, 1f, 0.4f, 1f, 0.5f, 1f);
        currentColorHex = "#" + ColorUtility.ToHtmlStringRGB(randomColor);
        
        currentNumericId = Random.Range(1, 100000);

        UpdateUI();
        UpdatePlanetVisuals();
    }

    private void UpdateUI()
    {
        if (typeText != null) typeText.text = currentPlanetType;
        if (seedText != null) seedText.text = "#" + currentNumericId.ToString();
        if (coreColorText != null)
        {
            coreColorText.text = currentColorHex;
            if (ColorUtility.TryParseHtmlString(currentColorHex, out Color textColor))
            {
                coreColorText.color = textColor;
            }
        }
    }

    private void UpdatePlanetVisuals()
    {
        if (planetTransform == null || planetRenderer == null) return;

        // 크기 계산
        float randomFactor = Mathf.Repeat(currentNumericId * 137.54f, 1.0f);
        float scaleMultiplier = 1.0f;

        if (currentPlanetType == "gaseous") scaleMultiplier = 1.5f + (randomFactor * 1.5f);
        else if (currentPlanetType == "icy") scaleMultiplier = 0.9f + (randomFactor * 0.6f);
        else scaleMultiplier = 0.7f + (randomFactor * 0.6f);

        planetTransform.localScale = new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier);

        // 셰이더 캐싱 및 공유 매터리얼 할당
        if (cachedShader == null)
        {
            cachedShader = Shader.Find(ShaderName);
        }

        if (planetRenderer.sharedMaterial == null || planetRenderer.sharedMaterial.shader != cachedShader)
        {
            planetRenderer.sharedMaterial = new Material(cachedShader);
        }

        // 셰이더 프로퍼티 블록 적용
        planetRenderer.GetPropertyBlock(propBlock);

        if (ColorUtility.TryParseHtmlString(currentColorHex, out Color baseColor))
        {
            propBlock.SetColor("_BaseColor", baseColor);
        }

        float seedValue = currentNumericId * 137.54f;
        propBlock.SetFloat("_Seed", seedValue);

        int typeInt = currentPlanetType == "rocky" ? 0 : (currentPlanetType == "gaseous" ? 1 : 2);
        propBlock.SetInt("_PlanetType", typeInt);

        planetRenderer.SetPropertyBlock(propBlock);
    }

    private void OnLaunchClicked()
    {
        string planetName = planetNameInput.text.Trim();
        if (string.IsNullOrEmpty(planetName)) return;

        StartCoroutine(SubmitPlanetRoutine(planetName));
    }

    private IEnumerator SubmitPlanetRoutine(string planetName)
    {
        launchButton.interactable = false;
        if (errorText != null) errorText.text = "";

        string token = UserManager.Instance.AuthToken;
        if (string.IsNullOrEmpty(token))
        {
            Debug.LogError("No Auth Token found. Please login first.");
            yield break;
        }

        PlanetCreatePayload payload = new PlanetCreatePayload
        {
            name = planetName,
            constellationId = currentNumericId,
            planetType = currentPlanetType,
            colorHex = currentColorHex
        };

        string jsonBody = JsonUtility.ToJson(payload);

        using (UnityWebRequest request = new UnityWebRequest($"{ApiBaseUrl}/planets", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                SceneManager.LoadScene("SpaceScene");
            }
            else
            {
                if (errorText != null)
                {
                    errorText.text = "Creation Failed: " + request.error;
                }
                launchButton.interactable = true;
            }
        }
    }
}

[System.Serializable]
public class PlanetCreatePayload
{
    public string name;
    public int constellationId;
    public string planetType;
    public string colorHex;
}