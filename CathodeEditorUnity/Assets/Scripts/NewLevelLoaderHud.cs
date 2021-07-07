using CATHODE.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class NewLevelLoaderHud : MonoBehaviour
{
    [SerializeField] private AlienLevelLoader levelLoader;

    [SerializeField] private TMPro.TMP_Text levelName;

    [Header("MVR Info Viewer Page")]
    [SerializeField] private GameObject mvrViewerPage;
    [SerializeField] private TMPro.TMP_InputField mvrIndex;
    [SerializeField] private TMPro.TMP_Text mvrInfoDump;

    [Header("MVR Info Editor Page")]
    [SerializeField] private GameObject mvrEditorPage;
    [SerializeField] private TMPro.TMP_InputField mvrIndexEditor;
    [SerializeField] private TMPro.TMP_InputField mvrContentEditor;

    [Header("Level Loader Page")]
    [SerializeField] private GameObject levelLoaderPage;
    [SerializeField] private TMPro.TMP_InputField levelNameToLoad;

    [Header("MVR Bulk Info Editor Page")]
    [SerializeField] private GameObject mvrBulkEditorPage;
    [SerializeField] private TMPro.TMP_InputField mvrTypeToSetFromBulk;
    [SerializeField] private TMPro.TMP_InputField mvrTypeToSetBulk;

#if UNITY_EDITOR
    string selectedObject = "";
    private void Update()
    {
        if (Selection.activeTransform == null || Selection.activeTransform.gameObject == null) return;
        if (selectedObject == Selection.activeTransform.gameObject.name) return;
        selectedObject = Selection.activeTransform.gameObject.name;
        string thisSelectedObject = selectedObject;
        if (!(thisSelectedObject.Length >= 5 && thisSelectedObject.Substring(0, 5) == "MVR: "))
        {
            if (Selection.activeTransform.gameObject.transform.parent != null)
            {
                string selectedObjectParent = Selection.activeTransform.gameObject.transform.parent.gameObject.name;
                if (!(selectedObjectParent.Length >= 5 && selectedObjectParent.Substring(0, 5) == "MVR: ")) return;
                thisSelectedObject = selectedObjectParent;
            }
            else return;
        }
        string[] parts = thisSelectedObject.Substring(5).Split('/');
        if (parts == null || parts.Length != 3) return;
        int selectedObjectI = 0;
        try { selectedObjectI = Convert.ToInt32(parts[0]); } catch { }
        if (loadedMVR != selectedObjectI) LoadMVR(selectedObjectI);
        if (loadedEditMVR != selectedObjectI) LoadMVRToEdit(selectedObjectI);
    }
#endif

    void Start()
    {
        levelLoader.LevelLoadCompleted += OnLevelLoaded;
        TAB_ShowMvrInfo();
    }

    private void OnLevelLoaded(alien_level data)
    {
        levelName.text = levelLoader.CurrentLevelName;
        levelNameToLoad.text = levelLoader.CurrentLevelName;

        mvrIndex.text = "0";
        LoadMVR(0);
    }

    //todo: enumify this
    public void TAB_ShowMvrInfo()
    {
        mvrViewerPage.SetActive(true);
        levelLoaderPage.SetActive(false);
        mvrEditorPage.SetActive(false);
        mvrBulkEditorPage.SetActive(false);
    }
    public void TAB_ShowLoadLevel()
    {
        mvrViewerPage.SetActive(false);
        levelLoaderPage.SetActive(true);
        mvrEditorPage.SetActive(false);
        mvrBulkEditorPage.SetActive(false);
    }
    public void TAB_ShowEditMVR()
    {
        mvrViewerPage.SetActive(false);
        levelLoaderPage.SetActive(false);
        mvrEditorPage.SetActive(true);
        mvrBulkEditorPage.SetActive(false);
    }
    public void TAB_ShowBulkEditMVR()
    {
        mvrViewerPage.SetActive(false);
        levelLoaderPage.SetActive(false);
        mvrEditorPage.SetActive(false);
        mvrBulkEditorPage.SetActive(true);
    }

    private int loadedMVR = -1;
    public void LoadMVR(int index = -1)
    {
        if (index == -1) index = Convert.ToInt32(mvrIndex.text);
        alien_mvr_entry entry = levelLoader.CurrentLevel.ModelsMVR.Entries[index];
        mvrInfoDump.text = JsonUtility.ToJson(entry, true);
        loadedMVR = index;
        mvrIndex.text = index.ToString();
    }

    private int loadedEditMVR = -1;
    public void LoadMVRToEdit(int index = -1)
    {
        if (index == -1) index = Convert.ToInt32(mvrIndexEditor.text);
        alien_mvr_entry entry = levelLoader.CurrentLevel.ModelsMVR.Entries[index];
        mvrContentEditor.text = JsonUtility.ToJson(entry, true);
        loadedEditMVR = index;
        mvrIndexEditor.text = index.ToString();
    }
    public void SaveMVRFromEdit(int index = -1)
    {
        if (index == -1) index = Convert.ToInt32(mvrIndexEditor.text);
        levelLoader.CurrentLevel.ModelsMVR.SetEntry(index, JsonUtility.FromJson<alien_mvr_entry>(mvrContentEditor.text));
        levelLoader.CurrentLevel.ModelsMVR.Save();
    }
    public void SaveMVRTransformFromEdit(int index = -1)
    {
        if (index == -1) index = Convert.ToInt32(mvrIndexEditor.text);
        for (int i = 0; i < levelLoader.CurrentLevelGameObject.transform.childCount; i++)
        {
            if (index != i) continue;

            GameObject mvrEntry = levelLoader.CurrentLevelGameObject.transform.GetChild(i).gameObject;
            if (mvrEntry.name.Substring(5).Split('/')[0] != i.ToString()) Debug.LogWarning("Something wrong!");

            alien_mvr_entry thisEntry = levelLoader.CurrentLevel.ModelsMVR.GetEntry(i);
            thisEntry.Transform = mvrEntry.transform.localToWorldMatrix;
            levelLoader.CurrentLevel.ModelsMVR.SetEntry(i, thisEntry);

            break;
        }
        levelLoader.CurrentLevel.ModelsMVR.Save();
    }

    public void BulkEditMVRTypes()
    {
        for (int i = 0; i < levelLoader.CurrentLevel.ModelsMVR.Entries.Count; i++)
        {
            alien_mvr_entry thisEntry = levelLoader.CurrentLevel.ModelsMVR.GetEntry(i);
            if (thisEntry.IsThisTypeID == (ushort)Convert.ToInt32(mvrTypeToSetFromBulk.text)) continue;
            thisEntry.IsThisTypeID = (ushort)Convert.ToInt32(mvrTypeToSetBulk.text);
            levelLoader.CurrentLevel.ModelsMVR.SetEntry(i, thisEntry);
        }
        levelLoader.CurrentLevel.ModelsMVR.Save();
    }

    public void LoadLevel(string levelname = "")
    {
        if (levelname == "") levelname = levelNameToLoad.text;
        levelLoader.LoadLevel(levelname);
    }

    /*
    [SerializeField] UnityEngine.UI.Text mvrindex2;
    [SerializeField] InputField mvrtypeid;
    public void ShowMvrIdInUi()
    {
        CATHODE.Models.alien_mvr_entry mvr = Result.ModelsMVR.GetEntry(Convert.ToInt32(mvrindex2.text));
        mvrtypeid.text = mvr.IsThisTypeID.ToString();
    }
    public void UpdateMvrIdFromUi()
    {
        CATHODE.Models.alien_mvr_entry mvr = Result.ModelsMVR.GetEntry(Convert.ToInt32(mvrindex2.text));
        mvr.IsThisTypeID = (ushort)Convert.ToInt32(mvrtypeid.text);
        Result.ModelsMVR.SetEntry(Convert.ToInt32(mvrindex2.text), mvr);
        Result.ModelsMVR.Save();
    }
    */
}
