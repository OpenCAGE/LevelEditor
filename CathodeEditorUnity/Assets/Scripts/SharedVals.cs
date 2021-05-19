using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SharedVals : MonoSingleton<SharedVals>
{
    [HideInInspector] public string LevelName = "";

    [SerializeField] TMPro.TMP_InputField input;
    public void OnClick()
    {
        LevelName = input.text;
        SceneManager.LoadScene("Loadscreen");
    }
}
