using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class SignUpManager : MonoBehaviour
{
    [Header("UI References - Inputs")]
    public TMP_InputField usernameInput;
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;

    [Header("UI References - Error Texts")]
    public TextMeshProUGUI usernameErrorText;
    public TextMeshProUGUI emailErrorText;
    public TextMeshProUGUI passwordErrorText;
    public TextMeshProUGUI backendErrorText; // 서버 통신 관련 에러

    [Header("UI References - Buttons & Panels")]
    public Button signUpButton;
    public GameObject loginPanel;
    public GameObject signUpPanel;


    private void Start()
    {
        if (signUpButton != null)
        {
            signUpButton.onClick.AddListener(OnSignUpButtonClicked);
        }

        passwordInput.asteriskChar = '•';

        // 각 입력 필드별 독립적인 이벤트 리스너 등록
        if (usernameInput != null) usernameInput.onValueChanged.AddListener(ValidateUsername);
        if (emailInput != null) emailInput.onValueChanged.AddListener(ValidateEmail);
        if (passwordInput != null) passwordInput.onValueChanged.AddListener(ValidatePassword);

        ClearAllErrorTexts();
        UpdateButtonState();
    }

    private void Update()
    {
        if (usernameInput.isFocused || emailInput.isFocused || passwordInput.isFocused)
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
        if (signUpButton != null) signUpButton.onClick.RemoveListener(OnSignUpButtonClicked);
        if (usernameInput != null) usernameInput.onValueChanged.RemoveListener(ValidateUsername);
        if (emailInput != null) emailInput.onValueChanged.RemoveListener(ValidateEmail);
        if (passwordInput != null) passwordInput.onValueChanged.RemoveListener(ValidatePassword);
    }

    private void ValidateUsername(string text)
    {
        backendErrorText.text = ""; // 새로운 타이핑 시 서버 에러 문구 초기화

        if (string.IsNullOrEmpty(text))
        {
            usernameErrorText.text = "";
        }
        else if (!Regex.IsMatch(text, @"^[a-zA-Z0-9_]+$"))
        {
            usernameErrorText.text = "Only letters, numbers, and underscores are allowed.";
        }
        else
        {
            usernameErrorText.text = "";
        }

        UpdateButtonState();
    }

    private void ValidateEmail(string text)
    {
        backendErrorText.text = "";

        if (string.IsNullOrEmpty(text))
        {
            emailErrorText.text = "";
        }
        else if (!Regex.IsMatch(text, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
        {
            emailErrorText.text = "Please enter a valid email address.";
        }
        else
        {
            emailErrorText.text = "";
        }

        UpdateButtonState();
    }

    private void ValidatePassword(string text)
    {
        backendErrorText.text = "";

        if (string.IsNullOrEmpty(text))
        {
            passwordErrorText.text = "";
        }
        else if (!Regex.IsMatch(text, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{6,}$"))
        {
            passwordErrorText.text = "Must include lowercase, uppercase letters, digits, and symbols.";
        }
        else
        {
            passwordErrorText.text = "";
        }

        UpdateButtonState();
    }

    private void UpdateButtonState()
    {
        // 필드에 값이 있고, 할당된 에러 텍스트가 모두 비어있을 때만 true
        bool hasValidUsername = !string.IsNullOrEmpty(usernameInput.text) && string.IsNullOrEmpty(usernameErrorText.text);
        bool hasValidEmail = !string.IsNullOrEmpty(emailInput.text) && string.IsNullOrEmpty(emailErrorText.text);
        bool hasValidPassword = !string.IsNullOrEmpty(passwordInput.text) && string.IsNullOrEmpty(passwordErrorText.text);

        if (signUpButton != null)
        {
            signUpButton.interactable = (hasValidUsername && hasValidEmail && hasValidPassword);
        }
    }

    private void ClearAllErrorTexts()
    {
        if (usernameErrorText != null) usernameErrorText.text = "";
        if (emailErrorText != null) emailErrorText.text = "";
        if (passwordErrorText != null) passwordErrorText.text = "";
        if (backendErrorText != null) backendErrorText.text = "";
    }

    public void OnSignUpButtonClicked()
    {
        string username = usernameInput.text;
        string email = emailInput.text;
        string password = passwordInput.text;

        StartCoroutine(RequestSignUp(username, email, password));
    }

    private IEnumerator RequestSignUp(string username, string email, string password)
    {
        signUpButton.interactable = false;
        backendErrorText.text = "";

        // JSON 페이로드 생성
        string jsonBody = $"{{\"email\":\"{email}\", \"password\":\"{password}\", \"username\":\"{username}\"}}";
        
        using (UnityWebRequest request = new UnityWebRequest($"{UserManager.Instance.ApiBaseUrl}/users/signup", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 성공 시 입력 필드 초기화 및 패널 전환
                usernameInput.text = "";
                emailInput.text = "";
                passwordInput.text = "";
                ClearAllErrorTexts();

                signUpPanel.SetActive(false);
                loginPanel.SetActive(true);
            }
            else
            {
                // 백엔드 에러 응답 파싱 및 UI 출력
                string responseText = request.downloadHandler.text;
                
                if (!string.IsNullOrEmpty(responseText))
                {
                    try
                    {
                        ErrorResponse errorResponse = JsonUtility.FromJson<ErrorResponse>(responseText);
                        if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.error))
                        {
                            backendErrorText.text = errorResponse.error;
                        }
                        else
                        {
                            backendErrorText.text = "Registration failed: " + request.error;
                        }
                    }
                    catch
                    {
                        backendErrorText.text = "Registration failed. Server error.";
                    }
                }
                else
                {
                    backendErrorText.text = "Registration failed: " + request.error;
                }
                
                signUpButton.interactable = true;
            }
        }
    }
}

// 에러 메시지 파싱을 위한 클래스
[System.Serializable]
public class ErrorResponse
{
    public string error;
}