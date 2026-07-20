using System;
using System.Collections.Generic;
using UnityEngine;
using SocketIOClient;
using SocketIOClient.Transport;

[Serializable]
public struct SectorData
{
    public int x { get; set; }
    public int y { get; set; }
    public int z { get; set; }
}

[Serializable]
public struct PlayerInitData
{
    public int myPlanetId { get; set; }
    public SectorData currentSector { get; set; }
}

[Serializable]
public struct StaticPlanetData
{
    public int id { get; set; }
    public string name { get; set; }
    public string colorHex { get; set; }
    public string planetType { get; set; }
    public int constellationId { get; set; }
}

[Serializable]
public struct SectorJoinedData
{
    public string room { get; set; }
    public SectorData sector { get; set; }
    public StaticPlanetData[] staticPlanets { get; set; }
}

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }
    public int MyPlanetId { get; private set; } = -1;

    [SerializeField] private string serverUrl = "http://localhost:3000";
    private SocketIO socket;

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

    private async void Start()
    {
        string token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1c2VySWQiOiI0OWYwZjVhNi02MGM2LTRkMTctOWI0ZC1iZTE0OGJiNmY2MTYiLCJlbWFpbCI6InNrd29ndXIwM0BnbWFpbC5jb20iLCJpYXQiOjE3ODQ1NTY4NTEsImV4cCI6MTc4NDY0MzI1MX0.gsvEcehZ4Xf3yxDErYjwFzQppBSjb6L1hY8pSOxwHU0";

        var options = new SocketIOOptions
        {
            Auth = new Dictionary<string, string>
            {
                { "token", $"Bearer {token}" }
            },
            // 프론트엔드의 transports: ["websocket", "polling"] 설정 반영
            Transport = TransportProtocol.WebSocket,
            AutoUpgrade = true
        };

        socket = new SocketIO(serverUrl, options);

        socket.OnConnected += (sender, e) =>
        {
            Debug.Log("[NetworkManager] Connected to server");
        };

        socket.OnDisconnected += (sender, e) =>
        {
            Debug.Log("[NetworkManager] Disconnected from server");
        };

        socket.On("connect_error", response =>
        {
            Debug.LogError($"[NetworkManager] Connect Error: {response}");
        });

        // 1. 초기화 데이터 수신 및 섹터 입장 요청
        socket.On("player:init", response =>
        {
            try
            {
                PlayerInitData initData = response.GetValue<PlayerInitData>();
                MyPlanetId = initData.myPlanetId;
                Debug.Log($"[NetworkManager] player:init - myPlanetId: {initData.myPlanetId}");
                
                socket.EmitAsync("sector:update", initData.currentSector);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] player:init parsing error: {ex.Message}");
            }
        });

        // 2. 섹터 입장 성공 및 정적 행성 데이터 수신
        socket.On("sector:joined", response =>
        {
            try
            {
                SectorJoinedData joinedData = response.GetValue<SectorJoinedData>();
                
                // 💡 배열이 null인지 먼저 체크하여 에러 방지
                int planetCount = joinedData.staticPlanets != null ? joinedData.staticPlanets.Length : 0;
                
                Debug.Log($"[NetworkManager] sector:joined - room: {joinedData.room}, staticPlanets: {planetCount}ea");
                
                if (joinedData.staticPlanets != null) {
                    WorldManager.Instance.SetStaticData(joinedData.staticPlanets);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] sector:joined parsing error: {ex.Message}");
            }
        });

        // 3. 바이너리 틱 데이터 수신
        socket.On("world:update", response =>
        {
            try
            {
                // JSON 파싱이 아닌 byte 배열로 원시 데이터 추출
                byte[] rawPayload = response.GetValue<byte[]>();

                DecodedWorldUpdatePacket packet = WorldPacketDecoder.Decode(rawPayload);

                WorldManager.Instance.OnWorldUpdateReceived(packet.planets);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] world:update parsing error: {ex.Message}");
            }
        });

        await socket.ConnectAsync();
    }

    private async void OnApplicationQuit()
    {
        if (socket != null && socket.Connected)
        {
            await socket.DisconnectAsync();
        }
    }
}