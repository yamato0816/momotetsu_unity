using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RailTrip3D
{
    [Serializable] public class NetworkReply
    {
        public bool ok;
        public string error, code, token;
        public int seat, version;
        public MatchData snapshot;
    }
    [Serializable] public class CreateRoomBody { public string name; public RailMap board; }
    [Serializable] public class JoinRoomBody { public string name; }
    [Serializable] public class ActionBody { public string token, action; public int node = -1, card = -1; }

    // HTTP polling deliberately works inside Unity WebGL (no incoming sockets needed).
    public class OnlineClient : MonoBehaviour
    {
        public string serverUrl = "";
        public string roomCode = "";
        public string seatToken = "";
        public int seat;
        public bool connected, busy;
        public string lastError = "";
        public MatchData latest;
        public Action<MatchData> OnSnapshot;
        private float nextPoll;
        private int version = -1;

        public void CreateRoom(string name, RailMap map)
        {
            if (busy) return;
            StartCoroutine(Send("POST", "rooms", JsonUtility.ToJson(new CreateRoomBody { name = name, board = map.Copy() }), OpenRoom));
        }
        public void JoinRoom(string code, string name)
        {
            if (busy) return;
            code = code.Trim().ToUpperInvariant();
            StartCoroutine(Send("POST", "rooms/" + code + "/join", JsonUtility.ToJson(new JoinRoomBody { name = name }), OpenRoom));
        }
        private void OpenRoom(NetworkReply r)
        {
            if (!r.ok) return;
            connected = true; roomCode = r.code; seatToken = r.token; seat = r.seat; version = -1;
            if (r.snapshot != null) { latest = r.snapshot; OnSnapshot?.Invoke(latest); }
            nextPoll = 0;
        }
        public void Act(string action, int node = -1, int card = -1)
        {
            if (!connected || busy) return;
            StartCoroutine(Send("POST", "rooms/" + roomCode + "/action",
                JsonUtility.ToJson(new ActionBody { token = seatToken, action = action, node = node, card = card }),
                r => { if (r.ok && r.snapshot != null) { version = r.version; latest = r.snapshot; OnSnapshot?.Invoke(latest); } }));
        }
        public void Disconnect()
        {
            StopAllCoroutines();
            connected = false; busy = false; roomCode = ""; seatToken = ""; latest = null; version = -1;
        }
        private void Update()
        {
            if (!connected || busy || Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 1.4f;
            StartCoroutine(Send("GET", "rooms/" + roomCode + "/state", null,
                r => { if (r.ok && r.snapshot != null && r.version != version) { version = r.version; latest = r.snapshot; OnSnapshot?.Invoke(latest); } }));
        }
        private IEnumerator Send(string method, string path, string body, Action<NetworkReply> done)
        {
            busy = true; lastError = "";
            string baseUrl = serverUrl.Trim().TrimEnd('/');
            if (!baseUrl.StartsWith("https://") && !baseUrl.StartsWith("http://"))
            { lastError = "Server URL must start with https://"; busy = false; yield break; }
            using (var req = new UnityWebRequest(baseUrl + "/" + path, method))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = 15;
                if (body != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                yield return req.SendWebRequest();
                NetworkReply reply = null;
                if (req.result == UnityWebRequest.Result.Success || req.responseCode == 400 || req.responseCode == 403 || req.responseCode == 404)
                {
                    try { reply = JsonUtility.FromJson<NetworkReply>(req.downloadHandler.text); }
                    catch (Exception e) { lastError = "Invalid server response: " + e.Message; }
                }
                else lastError = "Network error: " + req.error;
                if (reply != null)
                {
                    if (!reply.ok) lastError = string.IsNullOrEmpty(reply.error) ? "Request failed" : reply.error;
                    done?.Invoke(reply);
                }
            }
            busy = false;
        }
    }
}
