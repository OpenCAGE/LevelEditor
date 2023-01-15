using System;
using System.Threading;
using UnityEngine;
using WebSocketSharp;

public class WebsocketClient : MonoBehaviour
{
    private WebSocket client;
    private AlienLevelLoader loader;
    private Mutex mutex = new Mutex();

    private string levelToLoad = "";
    private bool shouldLoad = false;

    void Start()
    {
        loader = GetComponent<AlienLevelLoader>();

        client = new WebSocket("ws://localhost:1702/commands_editor");
        client.OnMessage += OnMessage;
        client.OnClose += OnClose;
        client.Connect();

        SendMessage(MessageType.TEST, "Test");
    }

    private void Update()
    {
        if (shouldLoad)
        {
            Debug.Log("Loading");
            loader.LoadLevel(levelToLoad);
            Debug.Log("loaded");
            shouldLoad = false;
        }
    }

    private void OnMessage(object sender, MessageEventArgs e)
    {
        MessageType type = (MessageType)Convert.ToInt32(e.Data.Substring(0, 1));
        switch (type)
        {
            case MessageType.LOAD_LEVEL:
                mutex.WaitOne();
                levelToLoad = e.Data.Substring(1);
                Debug.Log(levelToLoad);
                shouldLoad = true;
                mutex.ReleaseMutex();
                break;
            default:
                Debug.Log(e.Data.Substring(1));
                break;
        }
    }

    private void OnClose(object sender, CloseEventArgs e)
    {

    }

    public void SendMessage(MessageType type, string content)
    {
        client.Send(((int)type).ToString() + content);
    }
}

//TODO: Keep this in sync with server
public enum MessageType
{
    TEST,
    LOAD_LEVEL,
}