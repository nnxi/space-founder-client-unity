using UnityEngine;
using System;

[System.Serializable]
public class UserProfile
{
    public string id;
    public string email;
    public string username;
    public bool hasPlanet;
    public int satelliteCount;
}

public class UserManager : MonoBehaviour
{
    public static UserManager Instance { get; private set; }

    public UserProfile CurrentUser { get; private set; }
    public string AuthToken { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(AuthToken);

    [Header("Network Configuration")]
    [SerializeField] private ServerConfig serverConfig;

    // 다른 스크립트에서 주소를 꺼내갈 수 있도록 public으로 제공
    //public string ApiBaseUrl => serverConfig.ApiBaseUrl;
    public string ApiBaseUrl = "";

    // 다른 스크립트에서 로그인/로그아웃 이벤트를 구독할 수 있도록 추가
    public event Action<string> OnLoginSuccess;
    public event Action OnLogout;

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

    public void SetUserData(string token, UserProfile profile)
    {
        AuthToken = token;
        CurrentUser = profile;

        Debug.Log(CurrentUser.id);
        Debug.Log(CurrentUser.email);
        Debug.Log(CurrentUser.username);
        Debug.Log(CurrentUser.hasPlanet);
        Debug.Log(CurrentUser.satelliteCount);
        
        PlayerPrefs.SetString("AuthToken", token);
        PlayerPrefs.Save();

        // 로그인 성공 이벤트 발생
        OnLoginSuccess?.Invoke(AuthToken);
    }

    public void ClearUserData()
    {
        AuthToken = null;
        CurrentUser = null;
        
        PlayerPrefs.DeleteKey("AuthToken");
        PlayerPrefs.Save();

        // 로그아웃 이벤트 발생
        OnLogout?.Invoke();
    }
}