using UnityEngine;

[CreateAssetMenu(fileName = "ServerConfig", menuName = "Config/ServerConfig", order = 0)]
public class ServerConfig : ScriptableObject
{
    [Header("Server Connection Settings")]
    [SerializeField] private string baseUrl = "http://localhost:3000";

    public string BaseUrl => baseUrl;
    public string ApiBaseUrl => $"{baseUrl}/api";
}