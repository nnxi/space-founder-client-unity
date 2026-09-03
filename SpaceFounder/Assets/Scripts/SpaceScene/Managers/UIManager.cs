using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class UserProfileUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Button profileButton;
    [SerializeField] private GameObject userPanel;
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text emailText;
    [SerializeField] private Button logoutButton;

    [Header("Scene Settings")]
    [SerializeField] private string loginSceneName = "LoginScene";

    private RectTransform panelRect;
    private RectTransform buttonRect;

    private void Start()
    {
        panelRect = userPanel.GetComponent<RectTransform>();
        buttonRect = profileButton.GetComponent<RectTransform>();

        userPanel.SetActive(false);

        profileButton.onClick.AddListener(TogglePanel);
        logoutButton.onClick.AddListener(OnLogoutClicked);
    }

    private void Update()
    {
        // 패널이 켜져 있을 때 좌클릭이 발생하면 위치 검사
        if (userPanel.activeSelf && Input.GetMouseButtonDown(0))
        {
            // 클릭한 위치가 패널 내부도 아니고, 프로필 버튼 내부도 아니라면 패널 닫기
            if (!RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition) &&
                !RectTransformUtility.RectangleContainsScreenPoint(buttonRect, Input.mousePosition))
            {
                userPanel.SetActive(false);
            }
        }
    }

    private void TogglePanel()
    {
        bool isOpening = !userPanel.activeSelf;
        userPanel.SetActive(isOpening);

        // 패널이 열릴 때 최신 유저 정보를 매니저에서 가져와 UI에 반영
        if (isOpening)
        {
            UpdateUserInfo();
        }
    }

    private void UpdateUserInfo()
    {
        if (UserManager.Instance != null && UserManager.Instance.CurrentUser != null)
        {
            nicknameText.text = UserManager.Instance.CurrentUser.username;
            emailText.text = UserManager.Instance.CurrentUser.email;
        }
    }

    private void OnLogoutClicked()
    {
        // 1. 네트워크 소켓 연결 해제 및 초기화
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnApplicationQuit();
            // 다음 로그인 시 깔끔하게 재생성되도록 파괴
            Destroy(NetworkManager.Instance.gameObject); 
        }

        // 2. 유저 데이터 및 로컬 캐시 삭제
        if (UserManager.Instance != null)
        {
            UserManager.Instance.ClearUserData();
        }
        
        // 3. 로그인 씬으로 이동
        SceneManager.LoadScene(loginSceneName);
    }
}