using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;

public class UserManager : MonoBehaviour
{
    public static UserManager Instance { get; private set; }

    public string AuthToken { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(AuthToken);

    public event Action<string> OnLoginSuccess;
    public event Action OnLogout;

    private const string PREF_TOKEN_KEY = "AuthToken";

    [Header("Network Settings")]
    [SerializeField] private string backendLoginUrl = "http://localhost:3000/login";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        LoadToken();
    }

    // 로컬 저장소에서 토큰 불러오기 (자동 로그인 지원)
    private void LoadToken()
    {
        string savedToken = PlayerPrefs.GetString(PREF_TOKEN_KEY, "");
        if (!string.IsNullOrEmpty(savedToken))
        {
            AuthToken = savedToken;
            Debug.Log("[UserManager] Saved token loaded.");
        }
    }

    // 로그인 UI 버튼 클릭 시 호출
    public void Login(string userId, string password)
    {
        StartCoroutine(LoginRoutine(userId, password));
    }

    private IEnumerator LoginRoutine(string userId, string password)
    {
        string jsonPayload = $"{{\"userId\":\"{userId}\", \"password\":\"{password}\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest request = new UnityWebRequest(backendLoginUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                LoginResponse responseData = JsonUtility.FromJson<LoginResponse>(responseText);
                
                if (!string.IsNullOrEmpty(responseData.token))
                {
                    AuthToken = responseData.token;
                    
                    // WebGL 환경에서 IndexedDB에 토큰 영구 저장
                    PlayerPrefs.SetString(PREF_TOKEN_KEY, AuthToken);
                    PlayerPrefs.Save(); 

                    Debug.Log("[UserManager] Login success.");
                    OnLoginSuccess?.Invoke(AuthToken);
                }
            }
            else
            {
                Debug.LogError("[UserManager] Login failed: " + request.error);
            }
        }
    }

    public void Logout()
    {
        AuthToken = null;
        PlayerPrefs.DeleteKey(PREF_TOKEN_KEY);
        PlayerPrefs.Save();
        
        Debug.Log("[UserManager] Logout complete.");
        OnLogout?.Invoke();
    }

    // 백엔드 JSON 응답 파싱용 클래스
    [Serializable]
    private class LoginResponse
    {
        public string token;
    }
}