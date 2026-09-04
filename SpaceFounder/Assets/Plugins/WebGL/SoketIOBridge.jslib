mergeInto(LibraryManager.library, {

    // 소켓 연결 및 이벤트 리스너 등록
    JS_ConnectSocketIO: function (urlPtr, tokenPtr) {
        var url = UTF8ToString(urlPtr);
        var token = UTF8ToString(tokenPtr);

        // index.html에 socket.io 스크립트가 로드되어 있는지 확인
        if (typeof io === 'undefined') {
            console.error("[WebGL Socket] Socket.IO library is not loaded in index.html.");
            return;
        }

        // 소켓 초기화 (WSS 자동 업그레이드 포함)
        window.unitySocket = io(url, {
            auth: { token: "Bearer " + token },
            transports: ['websocket'],
            upgrade: true
        });

        // ---------------------------------------------
        // 서버 -> 클라이언트 이벤트 수신
        // ---------------------------------------------
        window.unitySocket.on('connect', function() {
            window.unityInstance.SendMessage('NetworkManager', 'OnSocketConnected');
        });

        window.unitySocket.on('disconnect', function() {
            window.unityInstance.SendMessage('NetworkManager', 'OnSocketDisconnected');
        });

        window.unitySocket.on('connect_error', function(err) {
            window.unityInstance.SendMessage('NetworkManager', 'OnSocketConnectError', err.message);
        });

        // 1. player:init
        window.unitySocket.on('player:init', function(data) {
            window.unityInstance.SendMessage('NetworkManager', 'OnPlayerInitReceived', JSON.stringify(data));
        });

        // 2. sector:joined
        window.unitySocket.on('sector:joined', function(data) {
            window.unityInstance.SendMessage('NetworkManager', 'OnSectorJoinedReceived', JSON.stringify(data));
        });

        // 3. world:update (바이너리 데이터 Base64 인코딩 처리)
        window.unitySocket.on('world:update', function(data) {
            var bytes = new Uint8Array(data);
            var binary = '';
            for (var i = 0; i < bytes.byteLength; i++) {
                binary += String.fromCharCode(bytes[i]);
            }
            // 유니티 SendMessage는 문자열만 지원하므로 Base64로 변환하여 전달
            var base64String = window.btoa(binary);
            window.unityInstance.SendMessage('NetworkManager', 'OnWorldUpdateReceived', base64String);
        });
    },

    // 소켓 연결 해제
    JS_DisconnectSocketIO: function () {
        if (window.unitySocket) {
            window.unitySocket.disconnect();
            window.unitySocket = null;
        }
    },

    // ---------------------------------------------
    // 클라이언트 -> 서버 이벤트 발신 (Emit)
    // ---------------------------------------------
    
    // 다중 섹터 구독
    JS_EmitSubscribeGrid: function (jsonStrPtr) {
        if (window.unitySocket) {
            var jsonStr = UTF8ToString(jsonStrPtr);
            window.unitySocket.emit('sector:subscribe_grid', JSON.parse(jsonStr));
        }
    },

    // 다중 섹터 구독 해제
    JS_EmitUnsubscribeGrid: function (jsonStrPtr) {
        if (window.unitySocket) {
            var jsonStr = UTF8ToString(jsonStrPtr);
            window.unitySocket.emit('sector:unsubscribe_grid', JSON.parse(jsonStr));
        }
    },

    // 내 행성 추적 요청 (콜백 포함)
    JS_RequestTrackMyPlanet: function () {
        if (window.unitySocket) {
            window.unitySocket.emit('camera:track_me', function(response) {
                window.unityInstance.SendMessage('NetworkManager', 'OnCameraTrackMeResponse', JSON.stringify(response));
            });
        }
    }
});