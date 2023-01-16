using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;
using WebSocketSharp;

public class WebsocketClient : MonoBehaviour
{
    private WebSocket client;
    private AlienLevelLoader loader;
    private Mutex mutex = new Mutex();

    private string levelToLoad = "";
    private bool shouldLoad = false;

    private Vector3 camPosition;
    private bool shouldRepositionCam = false;

    private int[] redsIndex;
    private bool shouldFocusOnReds = false;

    void Start()
    {
        loader = GetComponent<AlienLevelLoader>();

        client = new WebSocket("ws://localhost:1702/commands_editor");
        client.OnMessage += OnMessage;
        client.OnOpen += Client_OnOpen;
        client.OnClose += OnClose;
        client.Connect();

        SendMessage(MessageType.TEST, "Test");
    }

    private void Update()
    {
        if (shouldLoad)
        {
            loader.LoadLevel(levelToLoad);
            shouldLoad = false;
        }
        if (shouldRepositionCam)
        {
            Camera.main.transform.position = camPosition - new Vector3(0, 1, 0);
            Camera.main.transform.LookAt(camPosition);
            shouldRepositionCam = false;
        }
        if (shouldFocusOnReds)
        {
            List<UnityEngine.Object> objs = new List<UnityEngine.Object>(redsIndex.Length);
            for (int i = 0; i < redsIndex.Length; i++) objs.Add(GameObject.Find(levelToLoad).transform.GetChild(redsIndex[i]));
            Selection.objects = objs.ToArray();
            shouldFocusOnReds = false;
        }
    }

    private void OnMessage(object sender, MessageEventArgs e)
    {
        MessageType type = (MessageType)Convert.ToInt32(e.Data.Substring(0, 1));
        switch (type)
        {
            case MessageType.LOAD_LEVEL:
                {
                    mutex.WaitOne();
                    levelToLoad = e.Data.Substring(1);
                    shouldLoad = true;
                    mutex.ReleaseMutex();
                    break;
                }
            case MessageType.LOAD_LEVEL_AT_POSITION:
                {
                    mutex.WaitOne();
                    string[] content = e.Data.Substring(1).Split('>');
                    levelToLoad = content[0];
                    camPosition = new Vector3(Convert.ToSingle(content[1]), Convert.ToSingle(content[2]), Convert.ToSingle(content[3]));
                    shouldLoad = true;
                    shouldRepositionCam = true;
                    mutex.ReleaseMutex();
                    break;
                }
            case MessageType.REPORT_LOADED_LEVEL:
                {
                    SendMessage(MessageType.REPORTING_LOADED_LEVEL, levelToLoad);
                    break;
                }
            case MessageType.GO_TO_POSITION:
                {
                    mutex.WaitOne();
                    string[] content = e.Data.Substring(1).Split('>');
                    camPosition = new Vector3(Convert.ToSingle(content[0]), Convert.ToSingle(content[1]), Convert.ToSingle(content[2]));
                    shouldRepositionCam = true;
                    mutex.ReleaseMutex();
                    break;
                }
            case MessageType.GO_TO_REDS:
                {
                    mutex.WaitOne();
                    string[] content = e.Data.Substring(1).Split('>');
                    List<int> cont = new List<int>();
                    for (int i = 0; i < content.Length; i++) cont.Add(Convert.ToInt32(content[i]));
                    redsIndex = cont.ToArray();
                    shouldFocusOnReds = true;
                    mutex.ReleaseMutex();
                    break;
                }
        }
        Debug.Log(e.Data);
    }

    private void OnClose(object sender, CloseEventArgs e)
    {

    }

    private void Client_OnOpen(object sender, EventArgs e)
    {
        Debug.Log("open");
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
    LOAD_LEVEL_AT_POSITION,

    GO_TO_POSITION,
    GO_TO_REDS,

    REPORT_LOADED_LEVEL,
    REPORTING_LOADED_LEVEL,
}