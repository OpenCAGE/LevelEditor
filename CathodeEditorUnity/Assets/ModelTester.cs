using UnityEngine;
using CATHODE;
using static CATHODE.Models;

public class ModelTester : MonoBehaviour
{
    int i = 50;
    GameObject modelGO = null;
    Models mdls = null;
    private void Start()
    {
        mdls = new Models("G:\\SteamLibrary\\steamapps\\common\\Alien Isolation\\DATA\\ENV\\PRODUCTION\\SOLACE\\RENDERABLE\\LEVEL_MODELS.PAK");
        LoadModel();
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            i++;
            LoadModel();
        }
        if (Input.GetKeyDown(KeyCode.V))
        {
            i--;
            LoadModel();
        }
    }

    private void LoadModel()
    {
        if (modelGO != null)
            Destroy(modelGO);

        CS2 model = mdls.Entries[i];
        Debug.Log("[" + model.Submeshes.Count + "] " + model.Name);
        modelGO = new GameObject(model.Name);
        for (int x = 0; x < model.Submeshes.Count; ++x)
        {
            try
            {
                GameObject submeshGO = new GameObject();
                submeshGO.transform.parent = modelGO.transform;
                submeshGO.name = model.Submeshes[x].Name;
                submeshGO.AddComponent<MeshFilter>().mesh = mdls.GetMesh(model.Submeshes[x]);
                submeshGO.AddComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
            }
            catch (System.Exception e)
            {
                Debug.LogError(e.ToString());
            }
        }
    }
}