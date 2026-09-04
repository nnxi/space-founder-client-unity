using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Runtime.InteropServices;
using System.Text;

// 에디터 환경에서만 기존 소켓 라이브러리 사용
#if !UNITY_WEBGL || UNITY_EDITOR
using SocketIOClient;
using SocketIOClient.Transport;
#endif

#region Network Data Structs (DTOs)
[Serializable]
public struct Vector3IntData
{
    public int x;
    public int y;
    public int z;
    public Vector3Int ToVector3Int() => new Vector3Int(x, y, z);
}

[Serializable]
public struct Vector3Data
{
    public float x;
    public float y;
    public float z;
    public Vector3 ToVector3() => new Vector3(x, y, z);
}

[Serializable]
public struct PlayerInitData
{
    public int myPlanetId;
    public Vector3IntData currentSector;
}

[Serializable]
public struct StaticPlanetData
{
    public int planetId;
    public string planetName;
    public string userType;
    public string username;
    public string colorHex;
    public string planetType;
    public int constellationId;
    public Vector3IntData chunkIndex;
    public Vector3Data localPosition;
}

[Serializable]
public struct SectorJoinedData
{
    public string room;
    public Vector3IntData sector;
    public StaticPlanetData[] staticPlanets;
}

[Serializable]
public struct CameraTrackMeResponse
{
    public bool ok;
    public string error;
    public Vector3IntData chunkIndex;
    public Vector3Data localPosition;
}
#endregion

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }
    
    public int MyPlanetId { get; private set; } = -1;
    public bool IsConnected { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    // 자바스크립트 브릿지 함수 임포트
    [DllImport("__Internal")] private static extern void JS_ConnectSocketIO(string url, string token);
    [DllImport("__Internal")] private static extern void JS_DisconnectSocketIO();
    [DllImport("__Internal")] private static extern void JS_EmitSubscribeGrid(string jsonStr);
    [DllImport("__Internal")] private static extern void JS_EmitUnsubscribeGrid(string jsonStr);
    [DllImport("__Internal")] private static extern void JS_RequestTrackMyPlanet();
#else
    private SocketIO socket;
#endif

    // 콜백 저장을 위한 델리게이트
    private Action<bool, Vector3Int, Vector3> pendingTrackMeCallback;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // jslib의 SendMessage 타겟이 되기 위해 게임오브젝트 이름을 강제 고정
            gameObject.name = "NetworkManager";
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async void Start()
    {
        string token = UserManager.Instance.AuthToken;

        if (string.IsNullOrEmpty(token))
        {
            Debug.LogError("[NetworkManager] 유효한 인증 토큰이 없습니다.");
            UserManager.Instance.ClearUserData();
            SceneManager.LoadScene("LoginScene");
            return;
        }

        string socketUrl = UserManager.Instance.BaseUrl;
        if (string.IsNullOrEmpty(socketUrl))
        {
            socketUrl = Application.absoluteURL;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL 통신 환경 (Render 배포)
        if (socketUrl.StartsWith("https://")) socketUrl = socketUrl.Replace("https://", "wss://");
        else if (socketUrl.StartsWith("http://")) socketUrl = socketUrl.Replace("http://", "ws://");

        JS_ConnectSocketIO(socketUrl, token);
#else
        // 유니티 에디터 환경 (로컬 테스트용 폴백)
        if (string.IsNullOrEmpty(socketUrl) || socketUrl.Contains("onrender.com"))
        {
            socketUrl = "http://localhost:3000";
        }

        var options = new SocketIOOptions
        {
            Auth = new Dictionary<string, string> { { "token", $"Bearer {token}" } },
            Transport = TransportProtocol.WebSocket,
            AutoUpgrade = true
        };

        socket = new SocketIO(socketUrl, options);
        RegisterEditorSocketEvents();
        await socket.ConnectAsync();
#endif
    }

    public void Disconnect()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        JS_DisconnectSocketIO();
        IsConnected = false;
#else
        if (socket != null && socket.Connected)
        {
            socket.DisconnectAsync();
        }
        socket = null;
        IsConnected = false;
#endif
    }

    // ---------------------------------------------------------
    // 서버 발신 요청 (Emit) 로직
    // ---------------------------------------------------------
    public void EmitSubscribeGrid(List<Vector3Int> gridSectors)
    {
        if (!IsConnected || gridSectors == null || gridSectors.Count == 0) return;
        
        Debug.Log($"[NetworkManager] EmitSubscribeGrid 발송 - 섹터 개수: {gridSectors.Count}");
        
        List<Vector3IntData> sectorsToSubscribe = ConvertToDataList(gridSectors);

#if UNITY_WEBGL && !UNITY_EDITOR
        JS_EmitSubscribeGrid(ListToJsonArray(sectorsToSubscribe));
#else
        // 단일 인자로 묶어서 전송하기 위해 List<object> 사용
        var emitList = new List<object>();
        for (int i = 0; i < sectorsToSubscribe.Count; i++)
        {
            emitList.Add(new { x = sectorsToSubscribe[i].x, y = sectorsToSubscribe[i].y, z = sectorsToSubscribe[i].z });
        }
        
        socket.EmitAsync("sector:subscribe_grid", emitList);
#endif
    }

    public void EmitUnsubscribeGrid(List<Vector3Int> gridSectors)
    {
        if (!IsConnected || gridSectors == null || gridSectors.Count == 0) return;
        List<Vector3IntData> sectorsToUnsubscribe = ConvertToDataList(gridSectors);

#if UNITY_WEBGL && !UNITY_EDITOR
        JS_EmitUnsubscribeGrid(ListToJsonArray(sectorsToUnsubscribe));
#else
        var emitList = new List<object>();
        for (int i = 0; i < sectorsToUnsubscribe.Count; i++)
        {
            emitList.Add(new { x = sectorsToUnsubscribe[i].x, y = sectorsToUnsubscribe[i].y, z = sectorsToUnsubscribe[i].z });
        }
        
        socket.EmitAsync("sector:unsubscribe_grid", emitList);
#endif
    }

    public void RequestTrackMyPlanet(Action<bool, Vector3Int, Vector3> onComplete)
    {
        if (!IsConnected)
        {
            onComplete?.Invoke(false, default, Vector3.zero);
            return;
        }

        pendingTrackMeCallback = onComplete;

#if UNITY_WEBGL && !UNITY_EDITOR
        JS_RequestTrackMyPlanet();
#else
        socket.EmitAsync("camera:track_me", response =>
        {
            try
            {
                // 구조체 변환 건너뛰고 바로 원본 텍스트 넘기기
                string rawJson = response.GetValue<System.Text.Json.JsonElement>().GetRawText();
                UnityMainThreadDispatcher.Instance().Enqueue(() => OnCameraTrackMeResponse(rawJson));
            }
            catch
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => onComplete?.Invoke(false, default, Vector3.zero));
            }
        });
#endif
    }

    // ---------------------------------------------------------
    // WebGL 환경: jslib SendMessage 수신부
    // ---------------------------------------------------------
    public void OnSocketConnected()
    {
        IsConnected = true;
        Debug.Log("[NetworkManager] WebGL Socket Connected");
    }

    public void OnSocketDisconnected()
    {
        IsConnected = false;
        Debug.Log("[NetworkManager] WebGL Socket Disconnected");
    }

    public void OnSocketConnectError(string error)
    {
        Debug.LogError($"[NetworkManager] WebGL Connect Error: {error}");
    }

    public void OnPlayerInitReceived(string jsonStr)
    {
        try
        {
            Debug.Log(jsonStr);
            var initData = JsonUtility.FromJson<PlayerInitData>(jsonStr);
            MyPlanetId = initData.myPlanetId;
            Vector3Int mySector = initData.currentSector.ToVector3Int();

            if (WorldManager.Instance != null)
                WorldManager.Instance.InitializePlayer(MyPlanetId, mySector);
        }
        catch (Exception ex) { Debug.LogError($"[NetworkManager] player:init parse error: {ex.Message}"); }
    }

    public void OnSectorJoinedReceived(string jsonStr)
    {
        try
        {
            Debug.Log($"[NetworkManager] sector:joined 수신 데이터: {jsonStr}");

            var joinedData = JsonUtility.FromJson<SectorJoinedData>(jsonStr);
            if (joinedData.staticPlanets != null && WorldManager.Instance != null)
                WorldManager.Instance.SetStaticData(joinedData.staticPlanets);
        }
        catch (Exception ex) { Debug.LogError($"[NetworkManager] sector:joined parse error: {ex.Message}"); }
    }

    public void OnWorldUpdateReceived(string base64String)
    {
        try
        {
            byte[] rawPayload = Convert.FromBase64String(base64String);
            DecodedWorldUpdatePacket packet = WorldPacketDecoder.Decode(rawPayload);

            if (WorldManager.Instance != null)
                WorldManager.Instance.OnWorldUpdateReceived(packet.planets);
        }
        catch (Exception ex) { Debug.LogError($"[NetworkManager] world:update parse error: {ex.Message}"); }
    }

    public void OnCameraTrackMeResponse(string jsonStr)
    {
        if (pendingTrackMeCallback == null) return;
        try
        {
            var res = JsonUtility.FromJson<CameraTrackMeResponse>(jsonStr);
            if (res.ok) pendingTrackMeCallback.Invoke(true, res.chunkIndex.ToVector3Int(), res.localPosition.ToVector3());
            else pendingTrackMeCallback.Invoke(false, default, Vector3.zero);
        }
        catch
        {
            pendingTrackMeCallback.Invoke(false, default, Vector3.zero);
        }
        pendingTrackMeCallback = null;
    }

    // ---------------------------------------------------------
    // 유니티 에디터 환경: 기존 라이브러리 이벤트 등록
    // ---------------------------------------------------------
#if !UNITY_WEBGL || UNITY_EDITOR
    private void RegisterEditorSocketEvents()
    {
        socket.OnConnected += (sender, e) => { IsConnected = true; Debug.Log("[NetworkManager] Editor Socket Connected"); };
        socket.OnDisconnected += (sender, e) => { IsConnected = false; Debug.Log("[NetworkManager] Editor Socket Disconnected"); };
        socket.On("connect_error", res => Debug.LogError($"[NetworkManager] Editor Connect Error: {res}"));

        socket.On("player:init", res =>
        {
            // 구조체 변환을 생략하고 서버가 보낸 순수 JSON 문자열을 바로 추출합니다.
            string rawJson = res.GetValue<System.Text.Json.JsonElement>().GetRawText();
            UnityMainThreadDispatcher.Instance().Enqueue(() => OnPlayerInitReceived(rawJson));
        });

        socket.On("sector:joined", res =>
        {
            string rawJson = res.GetValue<System.Text.Json.JsonElement>().GetRawText();
            UnityMainThreadDispatcher.Instance().Enqueue(() => OnSectorJoinedReceived(rawJson));
        });

        socket.On("world:update", res =>
        {
            byte[] rawPayload = res.GetValue<byte[]>();
            UnityMainThreadDispatcher.Instance().Enqueue(() => OnWorldUpdateReceived(Convert.ToBase64String(rawPayload)));
        });
    }
#endif

    // ---------------------------------------------------------
    // 유틸리티 함수
    // ---------------------------------------------------------
    private List<Vector3IntData> ConvertToDataList(List<Vector3Int> gridSectors)
    {
        var list = new List<Vector3IntData>(gridSectors.Count);
        foreach (var pos in gridSectors) list.Add(new Vector3IntData { x = pos.x, y = pos.y, z = pos.z });
        return list;
    }

    // JsonUtility는 최상위 배열을 파싱하지 못하므로 수동으로 문자열 변환
    private string ListToJsonArray(List<Vector3IntData> list)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < list.Count; i++)
        {
            sb.Append($"{{\"x\":{list[i].x},\"y\":{list[i].y},\"z\":{list[i].z}}}");
            if (i < list.Count - 1) sb.Append(",");
        }
        sb.Append("]");
        return sb.ToString();
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }
}