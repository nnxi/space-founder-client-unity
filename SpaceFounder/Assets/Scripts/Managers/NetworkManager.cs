using System;
using System.Collections.Generic;
using UnityEngine;
using SocketIOClient;
using SocketIOClient.Transport;

#region Network Data Structs (DTOs)
[Serializable]
public struct Vector3IntData
{
    public int x { get; set; }
    public int y { get; set; }
    public int z { get; set; }
    
    public Vector3Int ToVector3Int() => new Vector3Int(x, y, z);
}

[Serializable]
public struct Vector3Data
{
    public float x { get; set; }
    public float y { get; set; }
    public float z { get; set; }

    public Vector3 ToVector3() => new Vector3(x, y, z);
}

[Serializable]
public struct PlayerInitData
{
    public int myPlanetId { get; set; }
    public Vector3IntData currentSector { get; set; }
}

[Serializable]
public struct StaticPlanetData
{
    public int planetId { get; set; }
    public string planetName { get; set; }
    public string userType { get; set; }
    public string username { get; set; }
    public string colorHex { get; set; }
    public string planetType { get; set; }
    public int constellationId { get; set; }
}

[Serializable]
public struct SectorJoinedData
{
    public string room { get; set; }
    public Vector3IntData sector { get; set; }
    public StaticPlanetData[] staticPlanets { get; set; }
}

[Serializable]
public struct CameraTrackMeResponse
{
    public bool ok { get; set; }
    public string error { get; set; }
    public Vector3IntData chunkIndex { get; set; }
    public Vector3Data localPosition { get; set; }
}
#endregion

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }
    
    public int MyPlanetId { get; private set; } = -1;
    public bool IsConnected => socket != null && socket.Connected;

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
        // TODO: 실제 환경에서는 인증 토큰을 동적으로 받아오도록 처리
        string token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1c2VySWQiOiI0OWYwZjVhNi02MGM2LTRkMTctOWI0ZC1iZTE0OGJiNmY2MTYiLCJlbWFpbCI6InNrd29ndXIwM0BnbWFpbC5jb20iLCJpYXQiOjE3ODQ4MjExOTIsImV4cCI6MTc4NDkwNzU5Mn0.0w7AxoClbs50U8I6YU8V7nCmqJ1Ew7uV5hJXlys33gM";

        var options = new SocketIOOptions
        {
            Auth = new Dictionary<string, string>
            {
                { "token", $"Bearer {token}" }
            },
            Transport = TransportProtocol.WebSocket,
            AutoUpgrade = true
        };

        socket = new SocketIO(serverUrl, options);

        RegisterSocketEvents();

        await socket.ConnectAsync();
    }

    // 소켓 수신 이벤트 등록 (Server -> Client)
    private void RegisterSocketEvents()
    {
        socket.OnConnected += (sender, e) => Debug.Log("[NetworkManager] Connected to server");
        socket.OnDisconnected += (sender, e) => Debug.Log("[NetworkManager] Disconnected from server");
        
        socket.On("connect_error", response => Debug.LogError($"[NetworkManager] Connect Error: {response}"));

        // 1. 초기화 데이터 수신
        socket.On("player:init", response =>
        {
            try
            {
                PlayerInitData initData = response.GetValue<PlayerInitData>();
                MyPlanetId = initData.myPlanetId;
                Vector3Int mySector = initData.currentSector.ToVector3Int();

                Debug.Log($"<color=cyan>[NetworkManager] 1. Received player:init -> MyPlanetId: {MyPlanetId}, MySector: {mySector}</color>");

                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    if (WorldManager.Instance != null)
                    {
                        // NetworkManager는 직접 트래커나 카메라를 건드리지 않고, 중앙 통제실인 WorldManager에게 데이터만 넘김
                        WorldManager.Instance.InitializePlayer(MyPlanetId, mySector);
                    }
                });
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
                int planetCount = joinedData.staticPlanets != null ? joinedData.staticPlanets.Length : 0;
                
                Debug.Log($"<color=yellow>[NetworkManager] 2. Received sector:joined -> Room: {joinedData.room}, StaticPlanets: {planetCount}ea</color>");
                
                if (joinedData.staticPlanets != null) 
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        if (WorldManager.Instance != null)
                        {
                            WorldManager.Instance.SetStaticData(joinedData.staticPlanets);
                        }
                    });
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
                byte[] rawPayload = response.GetValue<byte[]>();
                
                // 디코딩(파싱) 연산은 백그라운드 스레드에서 처리하여 성능 최적화
                DecodedWorldUpdatePacket packet = WorldPacketDecoder.Decode(rawPayload);
                
                // 유니티 오브젝트 제어는 메인 스레드로 디스패치
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    if (WorldManager.Instance != null)
                    {
                        WorldManager.Instance.OnWorldUpdateReceived(packet.planets);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] world:update parsing error: {ex.Message}");
            }
        });
    }

    // 다중 섹터 구독 요청 (Client -> Server)
    public void EmitSubscribeGrid(List<Vector3Int> gridSectors)
    {
        if (!IsConnected || gridSectors == null) return;

        List<Vector3IntData> sectorsToSubscribe = new List<Vector3IntData>(gridSectors.Count);
        foreach (var pos in gridSectors)
        {
            sectorsToSubscribe.Add(new Vector3IntData { x = pos.x, y = pos.y, z = pos.z });
        }

        socket.EmitAsync("sector:subscribe_grid", sectorsToSubscribe);
    }

    // 내 행성 추적 요청 (Client -> Server)
    public void RequestTrackMyPlanet(Action<bool, Vector3Int, Vector3> onComplete)
    {
        if (!IsConnected)
        {
            onComplete?.Invoke(false, default, Vector3.zero);
            return;
        }

        socket.EmitAsync("camera:track_me", response =>
        {
            try
            {
                var res = response.GetValue<CameraTrackMeResponse>();
                
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    if (res.ok)
                    {
                        onComplete?.Invoke(true, res.chunkIndex.ToVector3Int(), res.localPosition.ToVector3());
                    }
                    else
                    {
                        Debug.LogWarning($"[NetworkManager] track_me failed: {res.error}");
                        onComplete?.Invoke(false, default, Vector3.zero);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] track_me parsing error: {ex.Message}");
                UnityMainThreadDispatcher.Instance().Enqueue(() => onComplete?.Invoke(false, default, Vector3.zero));
            }
        });
    }

    private async void OnApplicationQuit()
    {
        if (IsConnected)
        {
            await socket.DisconnectAsync();
        }
    }
}