using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro; 
using UnityEngine.SceneManagement;

public class LoginManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject loginPanel;
    public GameObject signUpPanel;

    [Header("Navigation Buttons")]
    public Button goToSignUpButton;
    public Button backToLoginButton;

    [Header("UI References")]
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public TextMeshProUGUI errorText;
    public Button loginButton;
    public Button signupButton;
    
    private const string NextSceneName = "SpaceScene"; 

    private void Start()
    {
        // 버튼 클릭 이벤트 등록
        if (loginButton != null)
        {
            loginButton.onClick.AddListener(OnLoginButtonClicked);
        }

        // 텍스트 입력 변경 이벤트 등록
        if (emailInput != null)
        {
            emailInput.onValueChanged.AddListener(OnInputValueChanged);
        }
        if (passwordInput != null)
        {
            passwordInput.asteriskChar = '•';
            passwordInput.onValueChanged.AddListener(OnInputValueChanged);
        }

        // 패널 변경 이벤트 등록
        if (goToSignUpButton != null)
        {
            goToSignUpButton.onClick.AddListener(ShowSignUpPanel);
        }
        if (backToLoginButton != null)
        {
            backToLoginButton.onClick.AddListener(ShowLoginPanel);
        }

        // 시작 시 버튼 비활성화 처리
        UpdateButtonState();

        // 저장된 토큰 확인
        string savedToken = PlayerPrefs.GetString("AuthToken", "");
        if (!string.IsNullOrEmpty(savedToken))
        {
            StartCoroutine(VerifyToken(savedToken));
        }
    }

    private void OnDestroy()
    {
        // 이벤트 해제
        if (loginButton != null)
        {
            loginButton.onClick.RemoveListener(OnLoginButtonClicked);
        }
        if (emailInput != null)
        {
            emailInput.onValueChanged.RemoveListener(OnInputValueChanged);
        }
        if (passwordInput != null)
        {
            passwordInput.onValueChanged.RemoveListener(OnInputValueChanged);
        }
        if (goToSignUpButton != null)
        {
            goToSignUpButton.onClick.RemoveListener(ShowSignUpPanel);
        }
        if (backToLoginButton != null)
        {
            backToLoginButton.onClick.RemoveListener(ShowLoginPanel);
        }
    }

    private void Update()
    {
        if (emailInput.isFocused || passwordInput.isFocused)
        {
            Input.imeCompositionMode = IMECompositionMode.Off;
        }
        else
        {
            Input.imeCompositionMode = IMECompositionMode.On;
        }
    }

    // 텍스트가 변경될 때마다 호출
    private void OnInputValueChanged(string text)
    {
        UpdateButtonState();
    }

    // 두 입력 필드 모두 값이 있을 때만 버튼 활성화
    private void UpdateButtonState()
    {
        bool hasEmail = !string.IsNullOrEmpty(emailInput.text);
        bool hasPassword = !string.IsNullOrEmpty(passwordInput.text);

        if (loginButton != null)
        {
            loginButton.interactable = (hasEmail && hasPassword);
        }
    }

    public void OnLoginButtonClicked()
    {
        string email = emailInput.text;
        string password = passwordInput.text;

        StartCoroutine(RequestLogin(email, password));
    }

    public void ShowSignUpPanel()
    {
        loginPanel.SetActive(false);
        signUpPanel.SetActive(true);
    }

    // 로그인 패널로 전환
    public void ShowLoginPanel()
    {
        signUpPanel.SetActive(false);
        loginPanel.SetActive(true);
    }

    private IEnumerator VerifyToken(string token)
    {
        using (UnityWebRequest request = UnityWebRequest.Get($"{UserManager.Instance.ApiBaseUrl}/users/me"))
        {
            request.SetRequestHeader("Authorization", $"Bearer {token}");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                UserProfile profile = JsonUtility.FromJson<UserProfile>(request.downloadHandler.text);
                UserManager.Instance.SetUserData(token, profile);
                
                // 자동 로그인 시에도 행성 보유 여부 검사
                if (profile.hasPlanet)
                {
                    SceneManager.LoadScene("SpaceScene");
                }
                else
                {
                    SceneManager.LoadScene("PlanetCreationScene");
                }
            }
            else
            {
                Debug.LogWarning("[Auth] 만료되거나 유효하지 않은 토큰입니다.");
                UserManager.Instance.ClearUserData();
            }
        }
    }

    private IEnumerator RequestLogin(string email, string password)
    {
        errorText.text = "";

        string jsonBody = $"{{\"email\":\"{email}\", \"password\":\"{password}\"}}";
        
        using (UnityWebRequest request = new UnityWebRequest($"{UserManager.Instance.ApiBaseUrl}/users/login", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 1. 로그인 성공 후 토큰 획득
                LoginResponse response = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);
                string token = response.token;

                // 2. 획득한 토큰으로 유저 정보(me) 요청
                using (UnityWebRequest meRequest = UnityWebRequest.Get($"{UserManager.Instance.ApiBaseUrl}/users/me"))
                {
                    meRequest.SetRequestHeader("Authorization", $"Bearer {token}");
                    yield return meRequest.SendWebRequest();

                    if (meRequest.result == UnityWebRequest.Result.Success)
                    {
                        // 3. 유저 정보 저장
                        UserProfile profile = JsonUtility.FromJson<UserProfile>(meRequest.downloadHandler.text);
                        UserManager.Instance.SetUserData(token, profile);

                        // 4. 행성 보유 여부에 따른 씬 이동 분기
                        if (profile.hasPlanet)
                        {
                            SceneManager.LoadScene("SpaceScene");
                        }
                        else
                        {
                            // 행성 생성 씬의 실제 이름으로 변경 필요
                            SceneManager.LoadScene("PlanetCreationScene"); 
                        }
                    }
                    else
                    {
                        errorText.text = "Failed to retrieve user profile.";
                    }
                }
            }
            else
            {
                errorText.text = "Sign in failed. Check your Email or Password.";
            }
        }
    }
}

[System.Serializable]
public class LoginResponse
{
    public string token;
    public UserProfile user;
}