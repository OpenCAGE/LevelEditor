using CATHODE.Models;
using System;
using System.Collections;
using System.Collections.Generic;
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
    }
    public void TAB_ShowLoadLevel()
    {
        mvrViewerPage.SetActive(false);
        levelLoaderPage.SetActive(true);
        mvrEditorPage.SetActive(false);
    }
    public void TAB_ShowEditMVR()
    {
        mvrViewerPage.SetActive(false);
        levelLoaderPage.SetActive(false);
        mvrEditorPage.SetActive(true);
    }

    public void LoadMVR(int index = -1)
    {
        if (index == -1) index = Convert.ToInt32(mvrIndex.text);
        alien_mvr_entry entry = levelLoader.CurrentLevel.ModelsMVR.Entries[index];
        mvrInfoDump.text = JsonUtility.ToJson(entry, true);
    }

    public void LoadMVRToEdit(int index = -1)
    {
        if (index == -1) index = Convert.ToInt32(mvrIndexEditor.text);
        alien_mvr_entry entry = levelLoader.CurrentLevel.ModelsMVR.Entries[index];
        mvrContentEditor.text = JsonUtility.ToJson(entry, true);
    }
    public void SaveMVRFromEdit(int index = -1)
    {
        if (index == -1) index = Convert.ToInt32(mvrIndexEditor.text);
        levelLoader.CurrentLevel.ModelsMVR.SetEntry(index, JsonUtility.FromJson<alien_mvr_entry>(mvrContentEditor.text));
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
