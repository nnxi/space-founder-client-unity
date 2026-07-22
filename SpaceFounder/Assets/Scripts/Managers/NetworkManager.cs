using System;
using System.Collections.Generic;
using UnityEngine;
using SocketIOClient;
using SocketIOClient.Transport;

// 백엔드 SectorIndices 대응
[Serializable]
public struct SectorData
{
    public int x { get; set; }
    public int y { get; set; }
    public int z { get; set; }
}

// 백엔드 Vec3 대응
[Serializable]
public struct PositionData
{
    public float x { get; set; }
    public float y { get; set; }
    public float z { get; set; }
}

// 백엔드 PlayerInitPayload 대응
[Serializable]
public struct PlayerInitData
{
    public int myPlanetId { get; set; }
    public SectorData currentSector { get; set; }
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
    public SectorData sector { get; set; }
    public StaticPlanetData[] staticPlanets { get; set; }
}

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
        string token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1c2VySWQiOiI0OWYwZjVhNi02MGM2LTRkMTctOWI0ZC1iZTE0OGJiNmY2MTYiLCJlbWFpbCI6InNrd29ndXIwM0BnbWFpbC5jb20iLCJpYXQiOjE3ODQ2NDU2MzksImV4cCI6MTc4NDczMjAzOX0.URZD2JoecM70X_5J5NFN0aLgO1IpzaxPEVkoTjnsXE4";

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

        // 1. 초기화 데이터 수신
        socket.On("player:init", response =>
        {
            try
            {
                PlayerInitData initData = response.GetValue<PlayerInitData>();
                MyPlanetId = initData.myPlanetId;
                
                Vector3Int mySector = new Vector3Int(
                    initData.currentSector.x, 
                    initData.currentSector.y, 
                    initData.currentSector.z
                );

                Debug.Log($"[NetworkManager] player:init - myPlanetId: {initData.myPlanetId}, mySector: {mySector}");

                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    if (WorldManager.Instance != null)
                    {
                        // 값 전달만 수행. 카메라 제어는 CameraController가 담당.
                        WorldManager.Instance.CurrentCameraSector = mySector;
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
                
                Debug.Log($"[NetworkManager] sector:joined - room: {joinedData.room}, staticPlanets: {planetCount}ea");
                
                if (joinedData.staticPlanets != null) 
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        WorldManager.Instance.SetStaticData(joinedData.staticPlanets);
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
                DecodedWorldUpdatePacket packet = WorldPacketDecoder.Decode(rawPayload);
                WorldManager.Instance.OnWorldUpdateReceived(packet.planets);
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
        if (socket == null || !socket.Connected || gridSectors == null) return;

        List<SectorData> sectorsToSubscribe = new List<SectorData>(gridSectors.Count);
        foreach (var pos in gridSectors)
        {
            sectorsToSubscribe.Add(new SectorData { x = pos.x, y = pos.y, z = pos.z });
        }

        socket.EmitAsync("sector:subscribe_grid", sectorsToSubscribe);
    }

    // 내 행성 추적 요청 (Client -> Server)
    public void RequestTrackMyPlanet(Action<bool, Vector3Int, Vector3> onComplete)
    {
        if (socket == null || !socket.Connected)
        {
            onComplete?.Invoke(false, default, Vector3.zero);
            return;
        }

        socket.EmitAsync("camera:track_me", response =>
        {
            try
            {
                var res = response.GetValue<TrackMeResponseData>();
                if (res.ok)
                {
                    Vector3Int chunk = new Vector3Int(res.chunkIndex.x, res.chunkIndex.y, res.chunkIndex.z);
                    Vector3 localPos = new Vector3(res.localPosition.x, res.localPosition.y, res.localPosition.z);

                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        onComplete?.Invoke(true, chunk, localPos);
                    });
                }
                else
                {
                    Debug.LogWarning($"[NetworkManager] track_me failed: {res.error}");
                    UnityMainThreadDispatcher.Instance().Enqueue(() => onComplete?.Invoke(false, default, Vector3.zero));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] track_me parsing error: {ex.Message}");
                UnityMainThreadDispatcher.Instance().Enqueue(() => onComplete?.Invoke(false, default, Vector3.zero));
            }
        });
    }

    [Serializable]
    private struct TrackMeResponseData
    {
        public bool ok { get; set; }
        public string error { get; set; }
        public SectorData chunkIndex { get; set; }
        public PositionData localPosition { get; set; }
    }

    private async void OnApplicationQuit()
    {
        if (socket != null && socket.Connected)
        {
            await socket.DisconnectAsync();
        }
    }
}