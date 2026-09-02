using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlanetInfoUIManager : MonoBehaviour
{
    public static PlanetInfoUIManager Instance { get; private set; }

    // 크기 갱신을 위해 GameObject 대신 RectTransform 사용
    [SerializeField] private RectTransform infoPanelRect; 
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI ownerText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (infoPanelRect != null) infoPanelRect.gameObject.SetActive(false);
    }

    private void Update()
    {
        // 툴팁 활성화 시 마우스 커서 위치 추적
        if (infoPanelRect != null && infoPanelRect.gameObject.activeSelf)
        {
            infoPanelRect.position = Input.mousePosition + new Vector3(15f, -15f, 0f);
        }
    }

    public void ShowPlanetInfo(string pName, string pOwner, string pType, bool isDefault)
    {
        if (infoPanelRect == null) return;

        if (pType != "star")
        {
            nameText.text = $"Planet name: {pName}";
        }
        else
        {
            nameText.text = $"Star name: {pName}";
        }

        // Owner 텍스트 표시 분기 처리
        if (isDefault || string.IsNullOrEmpty(pOwner))
        {
            ownerText.gameObject.SetActive(false);
        }
        else
        {
            ownerText.text = $"Owner: {pOwner}";
            ownerText.gameObject.SetActive(true);
        }

        infoPanelRect.gameObject.SetActive(true);

        // 텍스트 변경사항에 맞춰 패널 크기 즉시 재계산
        LayoutRebuilder.ForceRebuildLayoutImmediate(infoPanelRect);
    }

    public void HidePlanetInfo()
    {
        if (infoPanelRect != null)
        {
            infoPanelRect.gameObject.SetActive(false);
        }
    }
}