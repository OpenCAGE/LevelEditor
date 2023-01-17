using UnityEngine;
using CATHODE;
using static CATHODE.Models;

public class ModelTester : MonoBehaviour
{
    int i = 30;
    GameObject modelGO = null;
    Models mdls = null;
    private void Start()
    {
        mdls = new Models("G:\\SteamLibrary\\steamapps\\common\\Alien Isolation\\DATA\\ENV\\PRODUCTION\\SOLACE\\RENDERABLE\\LEVEL_MODELS.PAK");
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (modelGO != null)
                Destroy(modelGO);

            CS2 model = mdls.Entries[i];
            Debug.Log("[" + model.Submeshes.Count + "] " + model.Name);
            modelGO = new GameObject(model.Name);
            for (int x = 0; x < model.Submeshes.Count; ++x)
            {
                GameObject submeshGO = new GameObject();
                submeshGO.transform.parent = modelGO.transform;
                submeshGO.name = model.Submeshes[x].Name;
                submeshGO.AddComponent<MeshFilter>().mesh = mdls.GetMesh(model.Submeshes[x]);
                submeshGO.AddComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
            }
            i++;
        }
    }
}