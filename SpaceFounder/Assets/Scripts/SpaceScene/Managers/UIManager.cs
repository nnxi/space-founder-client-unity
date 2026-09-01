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
        // UserManager의 로그아웃 로직 호출 (PlayerPrefs 캐시 삭제 포함)
        if (UserManager.Instance != null)
        {
            UserManager.Instance.ClearUserData();
        }
        
        // 로그인 씬으로 이동
        SceneManager.LoadScene(loginSceneName);
    }
}