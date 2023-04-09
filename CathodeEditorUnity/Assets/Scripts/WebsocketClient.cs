using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
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

    private string labelToShow = "";

    void Start()
    {
        loader = GetComponent<AlienLevelLoader>();
        StartCoroutine(ReconnectLoop());
    }

    private void Update()
    {
        if (shouldLoad)
        {
            if (loader.CurrentLevelName != levelToLoad)
                loader.LoadLevel(levelToLoad);
            SendMessage(MessageType.REPORT_LOADED_LEVEL, "");
            shouldLoad = false;
        }
        if (shouldRepositionCam)
        {
            Camera.main.transform.position = camPosition - new Vector3(0, 1, 0);
            Camera.main.transform.LookAt(camPosition);
            this.transform.position = camPosition;
            Selection.activeGameObject = this.gameObject;
            StartCoroutine(FocusDelayed());
            shouldRepositionCam = false;
        }
        if (shouldFocusOnReds)
        {
            List<UnityEngine.Object> objs = new List<UnityEngine.Object>(redsIndex.Length);
            for (int i = 0; i < redsIndex.Length; i++) objs.Add(GameObject.Find(levelToLoad).transform.GetChild(redsIndex[i]));
            Selection.objects = objs.ToArray();
            StartCoroutine(FocusDelayed());
            shouldFocusOnReds = false;
        }
    }
    private IEnumerator FocusDelayed()
    {
        yield return new WaitForEndOfFrame();
        //SceneView.FrameLastActiveSceneView();
        //yield return new WaitForEndOfFrame();
        //SceneView.FrameLastActiveSceneView();
    }

    private void OnDrawGizmos()
    {
        Handles.Label(this.transform.position, labelToShow);
    }

    private void OnMessage(object sender, MessageEventArgs e)
    {
        MessageType type = (MessageType)Convert.ToInt32(e.Data.Substring(0, 1));
        Debug.Log(type + ": " + e.Data.Substring(1));
        switch (type)
        {
            case MessageType.SYNC_VERSION:
                {
                    if (e.Data.Substring(1) != VERSION.ToString())
                    {
                        Debug.LogError("Your Commands Editor is utilising a newer API version than this Unity client!!\nPlease update this Unity client to avoid experiencing errors.");
                    }
                    else
                    {
                        Debug.Log("Commands Editor correctly utilises API version " + VERSION + ".");
                    }
                    break;
                }
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
            case MessageType.SHOW_ENTITY_NAME:
                {
                    mutex.WaitOne();
                    labelToShow = e.Data.Substring(1);
                    mutex.ReleaseMutex();
                    break;
                }
        }
    }

    private void OnClose(object sender, CloseEventArgs e)
    {
        Debug.Log("Websocket CLOSED");
    }
    private IEnumerator ReconnectLoop()
    {
        yield return new WaitForEndOfFrame();

        while (true)
        {
            if (client != null)
            {
                client.OnMessage -= OnMessage;
                client.OnOpen -= Client_OnOpen;
                client.OnClose -= OnClose;
            }

            client = new WebSocket("ws://localhost:1702/commands_editor");
            client.OnMessage += OnMessage;
            client.OnOpen += Client_OnOpen;
            client.OnClose += OnClose;

            Debug.Log("Trying to connect to Commands Editor...");

            while (!client.IsAlive)
            {
                try { client.Connect(); } catch { }
                yield return new WaitForSeconds(1.5f);
            }

            Debug.Log("Connected to Commands Editor!");

            while (client != null && client.IsAlive)
                yield return new WaitForEndOfFrame();

            client.Close();
        }

    }

    private void Client_OnOpen(object sender, EventArgs e)
    {
        Debug.Log("Websocket OPEN");
    }

    public void SendMessage(MessageType type, string content)
    {
        client.Send(((int)type).ToString() + content);
    }

    //TODO: Keep this in sync with server
    public const int VERSION = 1;
    public enum MessageType
    {
        SYNC_VERSION,

        LOAD_LEVEL,
        LOAD_LEVEL_AT_POSITION,

        GO_TO_POSITION,
        GO_TO_REDS,

        SHOW_ENTITY_NAME,

        REPORT_LOADED_LEVEL,
        REPORTING_LOADED_LEVEL,
    }
}