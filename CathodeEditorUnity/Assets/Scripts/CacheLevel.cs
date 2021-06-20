using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CATHODE.Commands;
using UnityEditor;
using System.IO;
using System.Linq;

public class CacheLevel : MonoBehaviour
{
    void Start()
    {
        CommandsPAK commandsPAK = new CommandsPAK(SharedVals.instance.PathToEnv + "/PRODUCTION/" + SharedVals.instance.LevelName + "/WORLD/COMMANDS.PAK");
        GameObject rootGO = new GameObject();

        //First, make dummy prefabs of all flowgraphs
        AssetDatabase.StartAssetEditing();
        for (int i = 0; i < commandsPAK.AllFlowgraphs.Count; i++)
        {
            string fullFilePath = GetFlowgraphAssetPath(commandsPAK.AllFlowgraphs[i]);
            string fileDirectory = fullFilePath.Substring(0, fullFilePath.Length - Path.GetFileName(fullFilePath).Length);
            if (!Directory.Exists(fileDirectory)) Directory.CreateDirectory(fileDirectory);
            if (!File.Exists(fullFilePath)) PrefabUtility.SaveAsPrefabAsset(rootGO, fullFilePath);
        }
        AssetDatabase.StopAssetEditing();

        //Then, populate the prefabs for all flowgraphs
        AssetDatabase.StartAssetEditing();
        for (int i = 0; i < commandsPAK.AllFlowgraphs.Count; i++)
        {
            GameObject flowgraphGO = new GameObject(commandsPAK.AllFlowgraphs[i].name);
            for (int x = 0; x < commandsPAK.AllFlowgraphs[i].nodes.Count; x++) 
            {
                CathodeFlowgraph flowgraphRef = commandsPAK.GetFlowgraph(commandsPAK.AllFlowgraphs[i].nodes[x].nodeType);
                GameObject nodeGO = null;
                if (flowgraphRef != null)
                {
                    //This is a reference to another flowgraph
                    GameObject flowgraphAsset = Resources.Load<GameObject>(GetFlowgraphAssetPath(flowgraphRef, true));
                    nodeGO = PrefabUtility.InstantiatePrefab(flowgraphAsset) as GameObject;
                    nodeGO.name = flowgraphRef.name;
                }
                else
                {
                    //This is a node
                    nodeGO = new GameObject(CathodeLib.NodeDB.GetFriendlyName(commandsPAK.AllFlowgraphs[i].nodes[x].nodeID));
                }
                nodeGO.transform.parent = flowgraphGO.transform;
                foreach (CathodeParameterReference paramRef in commandsPAK.AllFlowgraphs[i].nodes[x].nodeParameterReferences)
                {
                    CathodeParameter param = commandsPAK.GetParameter(paramRef.offset);
                    if (param == null) continue;
                    if (param.dataType != CathodeDataType.POSITION) continue;
                    CathodeTransform transform = (CathodeTransform)param;
                    nodeGO.transform.position = transform.position;
                    nodeGO.transform.rotation = Quaternion.Euler(transform.rotation);
                    break;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(flowgraphGO, GetFlowgraphAssetPath(commandsPAK.AllFlowgraphs[i]));
            Destroy(flowgraphGO);
        }
        AssetDatabase.StopAssetEditing();

        //Now, populate scene with the base flowgraph prefabs
        rootGO.name = SharedVals.instance.LevelName;
        for (int i = 0; i < commandsPAK.EntryPoints.Count; i++)
        {
            GameObject flowgraphGO = PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>(GetFlowgraphAssetPath(commandsPAK.EntryPoints[i], true))) as GameObject;
            flowgraphGO.name = commandsPAK.EntryPoints[i].name;
            flowgraphGO.transform.parent = rootGO.transform;
        }
    }

    private string GetFlowgraphAssetPath(CathodeFlowgraph flowgraph, bool resourcePath = false)
    {
        string basePath = SharedVals.instance.LevelName + "/Flowgraphs/" + flowgraph.name.Replace("\\", "/").Replace(":", "_");
        if (resourcePath) return basePath;
        else return "Assets/Resources/" + basePath + ".prefab";
    }
}