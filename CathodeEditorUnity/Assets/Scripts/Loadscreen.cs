using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Loadscreen : MonoBehaviour
{
    [SerializeField] GameObject loadingSpinner;

    private void Start()
    {
        StartCoroutine(LoadYourAsyncScene());
    }

    void Update()
    {
        loadingSpinner.transform.Rotate(0, 0, -360 * Time.deltaTime);
    }

    IEnumerator LoadYourAsyncScene()
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("LevelLoader");
        while (!asyncLoad.isDone) yield return null;
    }
}
