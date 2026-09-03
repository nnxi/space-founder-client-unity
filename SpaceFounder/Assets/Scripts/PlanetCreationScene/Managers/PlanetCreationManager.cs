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

    [Header("UI References")]
    public TextMeshProUGUI typeText;
    public TextMeshProUGUI seedText;
    public TextMeshProUGUI coreColorText;
    public TMP_InputField planetNameInput;
    public Button generateRandomButton;
    public Button launchButton;
    public TextMeshProUGUI errorText;
    public Button logoutButton;

    
    private RandomPlanetGenerator planetGenerator;
    
    private string currentPlanetType;
    private string currentColorHex;
    private int currentNumericId;

    private void Start()
    {
        if (planetTransform != null)
        {
            planetGenerator = planetTransform.GetComponent<RandomPlanetGenerator>();
        }

        if (generateRandomButton != null)
            generateRandomButton.onClick.AddListener(GenerateNewPlanet);

        if (launchButton != null)
            launchButton.onClick.AddListener(OnLaunchClicked);

        if (planetNameInput != null)
            planetNameInput.onValueChanged.AddListener(ValidateInput);

        if (logoutButton != null)
            logoutButton.onClick.AddListener(OnLogoutButtonClicked);

        GenerateNewPlanet();
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
        if (generateRandomButton != null) generateRandomButton.onClick.RemoveListener(GenerateNewPlanet);
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

    private void GenerateNewPlanet()
    {
        if (planetGenerator == null) return;

        planetGenerator.GenerateRandomPlanet();

        currentPlanetType = planetGenerator.CurrentType;
        currentColorHex = planetGenerator.CurrentColorHex;
        currentNumericId = planetGenerator.CurrentId;

        UpdateUI();
        UpdatePlanetScale();
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

    private void UpdatePlanetScale()
    {
        if (planetTransform == null) return;

        float randomFactor = Mathf.Repeat(currentNumericId * 137.54f, 1.0f);
        float scaleMultiplier = 1.0f;

        if (currentPlanetType == "gaseous") scaleMultiplier = 1.5f + (randomFactor * 1.5f);
        else if (currentPlanetType == "ice") scaleMultiplier = 0.9f + (randomFactor * 0.6f);
        else scaleMultiplier = 0.7f + (randomFactor * 0.6f); 

        planetTransform.localScale = new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier);
    }

    private void OnLaunchClicked()
    {
        string planetName = planetNameInput.text.Trim();
        if (string.IsNullOrEmpty(planetName)) return;

        StartCoroutine(SubmitPlanetRoutine(planetName));
    }

    private void OnLogoutButtonClicked()
    {
        // NetworkManager가 존재하는 경우에만 연결 해제 및 파괴 수행
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnApplicationQuit();
            Destroy(NetworkManager.Instance.gameObject);
        }

        // 유저 데이터 및 로컬 캐시 삭제
        if (UserManager.Instance != null)
        {
            UserManager.Instance.ClearUserData();
        }
        
        // 로그인 씬으로 이동
        SceneManager.LoadScene("LoginScene");
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

        using (UnityWebRequest request = new UnityWebRequest($"{UserManager.Instance.ApiBaseUrl}/planets", "POST"))
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
                    string responseText = request.downloadHandler.text;
                    string displayError = "Creation Failed: " + request.error;

                    if (!string.IsNullOrEmpty(responseText))
                    {
                        try
                        {
                            // 백엔드 JSON 응답 파싱
                            ErrorResponse backendError = JsonUtility.FromJson<ErrorResponse>(responseText);
                            if (backendError != null && !string.IsNullOrEmpty(backendError.error))
                            {
                                displayError = backendError.error;
                            }
                        }
                        catch
                        {
                            // 파싱 실패 시 기본 에러 유지
                        }
                    }
                    
                    errorText.text = displayError;
                }
                launchButton.interactable = true;
            }
        }
    }
}

// 이전에 중복 선언 문제가 있었다면 이 부분을 삭제하고 사용하세요.
[System.Serializable]
public class PlanetCreatePayload
{
    public string name;
    public int constellationId;
    public string planetType;
    public string colorHex;
}