using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using SocketIOClient;
using Newtonsoft.Json.Linq; // JSON 파싱용 (패키지 매니저 확인 필요)

public class AuthManager : MonoBehaviour
{
    [Header("임시 테스트용 UI")]
    public TMP_InputField emailInput; // 아까 userId 대신 email로 변경했습니다.
    public TMP_InputField pwInput;
    public Button loginButton;

    [Header("서버 통신 주소")]
    // 백엔드 API 라우트 표에 맞춰 주소를 /api/auth/... 로 업데이트했습니다.
    public string registerApiUrl = "http://localhost:4000/api/auth/register"; 
    public string loginApiUrl = "http://localhost:4000/api/auth/login"; 
    public string socketUrl = "http://localhost:4000";

    private SocketIOUnity socket;

    void Start()
    {
        // 로그인 버튼 클릭 시 함수 실행
        loginButton.onClick.AddListener(() => 
        {
            StartCoroutine(RequestLoginToken(emailInput.text, pwInput.text));
        });
    }

    // ==============================================================
    // [선택 기능] 버튼 등에 연결해서 테스트 계정을 강제로 하나 만들 때 씁니다.
    // ==============================================================
    public void TestRegister()
    {
        StartCoroutine(RequestRegister("test@test.com", "1234", "테스트어르신"));
    }

    private IEnumerator RequestRegister(string email, string password, string nickname)
    {
        Debug.Log("🔄 백엔드에 테스트 계정 생성(회원가입) 요청 중...");
        
        string jsonBody = $"{{\"email\":\"{email}\", \"password\":\"{password}\", \"nickname\":\"{nickname}\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        UnityWebRequest request = new UnityWebRequest(registerApiUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("🎉 계정 생성 완료! 이제 이 계정으로 로그인을 시도해 보세요.");
        }
        else
        {
            Debug.Log("❌ 계정 생성 실패: " + request.downloadHandler.text);
        }
    }

    // ==============================================================
    // 1. 백엔드에 이메일/비번 보내서 JWT 토큰 받아오기
    // ==============================================================
    private IEnumerator RequestLoginToken(string email, string password)
    {
        Debug.Log("🔄 1. 로그인 요청 중...");
        
        // 백엔드가 요구하는 'email' 키값으로 데이터 포장
        string jsonBody = $"{{\"email\":\"{email}\", \"password\":\"{password}\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        UnityWebRequest request = new UnityWebRequest(loginApiUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            // 발급된 JSON 데이터에서 토큰 문자열만 쏙 빼오기
            string responseText = request.downloadHandler.text;
            JObject jsonResponse = JObject.Parse(responseText);
            string token = jsonResponse["token"]?.ToString();

            Debug.Log("✅ 2. 토큰 발급 성공: " + token);
            ConnectSocket(token); // 토큰 쥐고 소켓 입장!
        }
        else
        {
            // 400, 401, 404 에러 시 정확한 백엔드 응답을 로그에 찍어줍니다.
            Debug.Log("❌ 로그인 실패: " + request.downloadHandler.text);
        }
    }

    // ==============================================================
    // 2. 발급받은 토큰으로 소켓 서버 문지기 통과하기
    // ==============================================================
    private void ConnectSocket(string token)
    {
        Debug.Log("🔄 3. 소켓 서버 문지기 통과 시도...");

        var uri = new Uri(socketUrl);
        socket = new SocketIOUnity(uri, new SocketIOOptions
        {
            EIO = SocketIOClient.EngineIO.V4,
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
            Auth = new System.Collections.Generic.Dictionary<string, string>
            {
                { "token", token } // 여기서 백엔드의 JWT 검사 미들웨어로 토큰을 넘겨줍니다.
            }
        });

        socket.OnConnected += (sender, e) => Debug.Log("🎉 4. JWT 인증 뚫고 소켓 연결 최종 성공!");
        socket.OnDisconnected += (sender, e) => Debug.Log("❌ 소켓 연결 끊김: " + e);

        socket.Connect();
    }

    void OnDestroy()
    {
        if (socket != null) socket.Disconnect();
    }
}