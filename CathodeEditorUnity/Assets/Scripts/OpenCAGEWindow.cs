using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditor.SceneManagement;

public class OpenCAGEWindow : EditorWindow
{
    private static AlienLevelLoader _loader = null;

    private static Vector2 scrollPos;

    [MenuItem("Window/OpenCAGE Utils")]
    public static void ShowWindow()
    {
        EditorSceneManager.OpenScene("Assets/Scene.unity");
        FindObjects();

        EditorWindow ew = GetWindow(typeof(OpenCAGEWindow), false, "OpenCAGE Utils", true);
        GUIContent title = EditorGUIUtility.IconContent("CustomTool");
        title.text = "OpenCAGE Utils";
        ew.titleContent = title;
    }

    private static bool FindObjects()
    {
        if (_loader == null)
        {
            _loader = FindObjectOfType<AlienLevelLoader>();
            //_loader.OnLoaded += ReloadUI;
        }

        return _loader != null;
    }

    private void OnGUI()
    {
        if (!FindObjects()) return;

        scrollPos = GUILayout.BeginScrollView(scrollPos, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);

        EditorGUILayout.Space();

        if (Application.isPlaying)
        {
            if (GUILayout.Button("Disconnect From Commands Editor"))
            {
                EditorApplication.ExitPlaymode();
            }

            GUILayout.BeginVertical("", GUI.skin.box);
            EditorGUILayout.LabelField("Level: " + _loader.LevelName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Composite: " + _loader.CompositeName, EditorStyles.boldLabel);
            GUILayout.EndVertical();
        }
        else
        {
            if (GUILayout.Button("Connect To Commands Editor"))
            {
                EditorApplication.EnterPlaymode();
            }
        }

        //EditorGUILayout.Space();

        //EditorGUILayout.BeginHorizontal();
        //EditorGUILayout.LabelField("Managers");
        //EditorGUILayout.EndHorizontal();

        /*
        string entityName = Selection.activeGameObject?.name;
        if (entityName != null && entityName.Length > ("[FUNCTION ENTTIY]").Length && entityName.Substring(0, ("[FUNCTION ENTITY]").Length) == "[FUNCTION ENTITY]")
        {
            if (GUILayout.Button("Apply Current Position"))
            {
                //TODO: send position back to script editor, make alias if child entity & one doesn't exist
            }
        }
        */

        GUILayout.EndScrollView();
    }
}