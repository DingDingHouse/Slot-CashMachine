using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Newtonsoft.Json;
using Best.SocketIO;
using Best.SocketIO.Events;

public class SocketIOManager : MonoBehaviour
{
    internal PlayerData playerdata = null;
    internal GameData TempResultData;
    internal List<int> Bets;
    internal bool isResultdone = false;

    private SocketManager manager;

    [SerializeField]
    private SlotController SlotManager;

    protected string SocketURI = null;
    protected string TestSocketURI = "https://game-crm-rtp-backend.onrender.com/";
    //protected string TestSocketURI = "https://7p68wzhv-5000.inc1.devtunnels.ms/";

    [SerializeField]
    private string testToken;

    protected string gameID = "SL-CM";

    internal bool SetInit = false;

    private const int maxReconnectionAttempts = 6;
    private readonly TimeSpan reconnectionDelay = TimeSpan.FromSeconds(10);

    private void Awake()
    {
        SetInit = false;
    }

    private void Start()
    {
        OpenSocket();
    }

    void ReceiveAuthToken(string jsonData)
    {
        Debug.Log("Received data: " + jsonData);

        // Parse the JSON data
        var data = JsonUtility.FromJson<AuthTokenData>(jsonData);
        SocketURI = data.socketURL;
        myAuth = data.cookie;

        // Proceed with connecting to the server using myAuth and socketURL
    }

    string myAuth = null;

    private void OpenSocket()
    {
        // Create and setup SocketOptions
        SocketOptions options = new SocketOptions();
        options.ReconnectionAttempts = maxReconnectionAttempts;
        options.ReconnectionDelay = reconnectionDelay;
        options.Reconnection = true;

        Application.ExternalCall("window.parent.postMessage", "authToken", "*");

#if UNITY_WEBGL && !UNITY_EDITOR
        Application.ExternalEval(@"
            window.addEventListener('message', function(event) {
                if (event.data.type === 'authToken') {
                    var combinedData = JSON.stringify({
                        cookie: event.data.cookie,
                        socketURL: event.data.socketURL
                    });
                    // Send the combined data to Unity
                    SendMessage('SocketManager', 'ReceiveAuthToken', combinedData);
                }});");
        StartCoroutine(WaitForAuthToken(options));
#else
        Func<SocketManager, Socket, object> authFunction = (manager, socket) =>
        {
            return new
            {
                token = testToken,
                gameId = gameID
            };
        };
        options.Auth = authFunction;
        // Proceed with connecting to the server
        SetupSocketManager(options);
#endif
    }


    private IEnumerator WaitForAuthToken(SocketOptions options)
    {
        // Wait until myAuth is not null
        while (myAuth == null)
        {
            Debug.Log("My Auth is null");
            yield return null;
        }
        while (SocketURI == null)
        {
            Debug.Log("My Socket is null");
            yield return null;
        }
        Debug.Log("My Auth is not null");
        // Once myAuth is set, configure the authFunction
        Func<SocketManager, Socket, object> authFunction = (manager, socket) =>
        {
            return new
            {
                token = myAuth,
                gameId = gameID
            };
        };
        options.Auth = authFunction;

        Debug.Log("Auth function configured with token: " + myAuth);

        // Proceed with connecting to the server
        SetupSocketManager(options);
    }

    private void SetupSocketManager(SocketOptions options)
    {
        // Create and setup SocketManager
#if UNITY_EDITOR
        this.manager = new SocketManager(new Uri(TestSocketURI), options);
#else
        this.manager = new SocketManager(new Uri(SocketURI), options);
#endif
        // Set subscriptions
        this.manager.Socket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnConnected);
        this.manager.Socket.On<string>(SocketIOEventTypes.Disconnect, OnDisconnected);
        this.manager.Socket.On<string>(SocketIOEventTypes.Error, OnError);
        this.manager.Socket.On<string>("message", OnListenEvent);
        this.manager.Socket.On<bool>("socketState", OnSocketState);
        this.manager.Socket.On<string>("internalError", OnSocketError);
        this.manager.Socket.On<string>("alert", OnSocketAlert);
        // Start connecting to the server
    }

    // Connected event handler implementation
    void OnConnected(ConnectResponse resp)
    {
        Debug.Log("Connected!");
        SendPing();
    }

    private void OnDisconnected(string response)
    {
        Debug.Log("Disconnected from the server");
        StopAllCoroutines();
        SlotManager.DisconnectionPopup();
    }

    private void OnError(string response)
    {
        Debug.LogError("Error: " + response);
    }

    private void OnListenEvent(string data)
    {
        Debug.Log("Received some_event with data: " + data);
        ParseResponse(data);
    }

    private void OnSocketState(bool state)
    {
        if (state)
        {
            Debug.Log("my state is " + state);
        }
        else
        {

        }
    }
    private void OnSocketError(string data)
    {
        Debug.Log("Received error with data: " + data);
    }
    private void OnSocketAlert(string data)
    {
        Debug.Log("Received alert with data: " + data);
    }

    private void SendPing()
    {
        InvokeRepeating("AliveRequest", 0f, 3f);
    }

    private void AliveRequest()
    {
        SendDataWithNamespace("YES I AM ALIVE");
    }

    private void SendDataWithNamespace(string eventName, string json = null)
    {
        // Send the message
        if (this.manager.Socket != null && this.manager.Socket.IsOpen)
        {
            if (json != null)
            {
                this.manager.Socket.Emit(eventName, json);
                Debug.Log("JSON data sent: " + json);
            }
            else
            {
                this.manager.Socket.Emit(eventName);
            }
        }
        else
        {
            Debug.LogWarning("Socket is not connected.");
        }
    }



    internal void CloseSocket()
    {
        SendDataWithNamespace("EXIT");
    }

    private void ParseResponse(string jsonObject)
    {
        Root myData = JsonConvert.DeserializeObject<Root>(jsonObject);

        string id = myData.id;

        switch (id)
        {
            case "InitData":
                {
                    if (SlotManager) SlotManager.UpdateUI(myData.message.PlayerData.Balance);
                    if (!SetInit)
                    {
                        Application.ExternalCall("window.parent.postMessage", "OnEnter", "*");
                        Bets = myData.message.GameData.Bets;
                        SlotManager.PopulateSymbols(myData.message.UIData.paylines);
                        SetInit = true;
                    }
                    break;
                }
            case "ResultData":
                {
                    Debug.Log(jsonObject);
                    playerdata = myData.message.PlayerData;
                    TempResultData = myData.message.GameData;
                    isResultdone = true;
                    break;
                }
            case "ExitUser":
                {
                    if (this.manager != null)
                    {
                        Debug.Log("Dispose my Socket");
                        this.manager.Close();
                    }
                    Application.ExternalCall("window.parent.postMessage", "onExit", "*");
                    break;
                }
        }
    }

    internal void AccumulateResult(double currBet)
    {
        isResultdone = false;
        MessageData message = new MessageData();
        message.data = new BetData();
        message.data.currentBet = currBet;
        message.data.matrixX = currBet + 1;
        message.id = "SPIN";
        // Serialize message data to JSON
        string json = JsonUtility.ToJson(message);
        SendDataWithNamespace("message", json);
    }

    private List<string> RemoveQuotes(List<string> stringList)
    {
        for (int i = 0; i < stringList.Count; i++)
        {
            stringList[i] = stringList[i].Replace("\"", ""); // Remove inverted commas
        }
        return stringList;
    }
}
