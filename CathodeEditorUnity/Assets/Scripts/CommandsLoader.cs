using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using System;
using TestProject.File_Handlers.Commands;
using CathodeLib;

public class CommandsLoader : MonoBehaviour
{
    private CommandsPAK commandsPAK = null;
    private List<alien_reds_entry> redsBIN;

    UInt32 ModelReferenceID = 0;
    void Start()
    {
        byte[] ModelReferenceIDBytes = new byte[4] { 0x94, 0xA8, 0xB4, 0xB9 };
        ModelReferenceID = BitConverter.ToUInt32(ModelReferenceIDBytes, 0);
    }

    //Test code to load in everything that has a position: note, the hierarchy of objects needs to be considered here
    public void LoadCommandsPAK(string LEVEL_NAME, List<alien_reds_entry> redsbin, System.Action<int, GameObject> loadModelCallback)
    {
        string basePath = LEVEL_NAME + "\\";
        commandsPAK = new CommandsPAK(basePath + @"WORLD\COMMANDS.PAK");
        redsBIN = redsbin;

        for (int i = 0; i < commandsPAK.EntryPoints.Count; i++)
        {
            GameObject thisFlowgraphGO = new GameObject(commandsPAK.EntryPoints[i].name);
            StartCoroutine(RecursiveLoad(commandsPAK.EntryPoints[i], thisFlowgraphGO, loadModelCallback));
        }
    }

    private IEnumerator RecursiveLoad(CathodeFlowgraph flowgraph, GameObject parentTransform, System.Action<int, GameObject> loadModelCallback)
    {
        for (int i = 0; i < flowgraph.nodes.Count; i++)
        {
            CathodeNodeEntity node = flowgraph.nodes[i];
            CathodeFlowgraph nextCall = commandsPAK.GetFlowgraph(node.nodeType);
            if (nextCall == null) continue;
            PosAndRot trans = GetTransform(ref node);
            GameObject nextFlowgraphGO = new GameObject(nextCall.name);
            nextFlowgraphGO.transform.parent = parentTransform.transform;
            nextFlowgraphGO.transform.localPosition = trans.position;
            nextFlowgraphGO.transform.localRotation = trans.rotation;
            StartCoroutine(RecursiveLoad(nextCall, nextFlowgraphGO, loadModelCallback));
        }

        List<CathodeNodeEntity> models = GetAllOfType(ref flowgraph, ModelReferenceID);
        for (int i = 0; i < models.Count; i++)
        {
            CathodeNodeEntity thisModel = models[i];

            PosAndRot trans = GetTransform(ref thisModel);
            GameObject thisNodeGO = new GameObject(NodeDB.GetNodeTypeName(thisModel.nodeType, ref commandsPAK) + ": " + NodeDB.GetFriendlyName(thisModel.nodeID));
            thisNodeGO.transform.parent = parentTransform.transform;
            thisNodeGO.transform.localPosition = trans.position;
            thisNodeGO.transform.localRotation = trans.rotation;

            LoadModelNode(ref thisModel, ref flowgraph, thisNodeGO, loadModelCallback);
        }

        /*
        List<CathodeNodeEntity> sounds = GetAllOfType(flowgraph, new string[] { "SoundPlaybackBaseClass", "SoundObject", "Sound" });
        foreach (CathodeNodeEntity node in sounds)
        {
            PosAndRot thisNodePos = GetTransform(node) + stackedTransform;
            GameObject newCube = new GameObject(NodeDB.GetName(node.nodeType));
            newCube.transform.position = new Vector3(thisNodePos.position.x, thisNodePos.position.y, thisNodePos.position.z);
            newCube.transform.eulerAngles = new Vector3(thisNodePos.rotation.x, thisNodePos.rotation.y, thisNodePos.rotation.z);
            newCube.AddComponent<AudioSource>();
            continue;
        }

        List<CathodeNodeEntity> particles = GetAllOfType(flowgraph, new string[] { "ParticleEmitterReference", "RibbonEmitterReference", "GPU_PFXEmitterReference" });
        foreach (CathodeNodeEntity node in particles)
        {
            PosAndRot thisNodePos = GetTransform(node) + stackedTransform;
            GameObject newCube = new GameObject(NodeDB.GetName(node.nodeType));
            newCube.transform.position = new Vector3(thisNodePos.position.x, thisNodePos.position.y, thisNodePos.position.z);
            newCube.transform.eulerAngles = new Vector3(thisNodePos.rotation.x, thisNodePos.rotation.y, thisNodePos.rotation.z);
            newCube.AddComponent<ParticleSystem>().emissionRate = 0;
            continue;
        }
        */

        /*
        List<CathodeNodeEntity> lights = GetAllOfType(flowgraph, new string[] { "LightReference" });
        foreach (CathodeNodeEntity node in lights)
        {
            PosAndRot trans = GetTransform(node);
            GameObject thisNodeGO = new GameObject(NodeDB.GetNodeTypeName(node.nodeType, commandsPAK) + ": " + NodeDB.GetFriendlyName(node.nodeID));
            thisNodeGO.transform.parent = parentTransform.transform;
            thisNodeGO.transform.localPosition = trans.position;
            thisNodeGO.transform.localRotation = trans.rotation;
            thisNodeGO.AddComponent<Light>(); //todo: pull properties from game
        }
        */

        yield break;
    }

    private List<CathodeNodeEntity> GetAllOfType(ref CathodeFlowgraph flowgraph, UInt32 nodeType)
    {
        return flowgraph.nodes.FindAll(o => o.nodeType == nodeType);
    }

    private PosAndRot GetTransform(ref CathodeNodeEntity node)
    {
        PosAndRot toReturn = new PosAndRot();
        foreach (CathodeParameterReference paramRef in node.nodeParameterReferences)
        {
            CathodeParameter param = commandsPAK.GetParameter(paramRef.offset);
            if (param == null) continue;
            if (param.dataType != CathodeDataType.POSITION) continue;
            CathodeTransform transform = (CathodeTransform)param;
            toReturn.position = transform.position;
            toReturn.rotation = Quaternion.Euler(transform.rotation);
            break;
        }
        return toReturn;
    }

    private List<UInt32> GetResource(ref CathodeNodeEntity node)
    {
        List<UInt32> resources = new List<UInt32>();
        foreach (CathodeParameterReference paramRef in node.nodeParameterReferences)
        {
            CathodeParameter param = commandsPAK.GetParameter(paramRef.offset);
            if (param == null) continue;
            if (param.dataType != CathodeDataType.SHORT_GUID) continue;
            resources.Add(((CathodeResource)param).resourceID);
        }
        return resources;
    }

    private void LoadModelNode(ref CathodeNodeEntity node, ref CathodeFlowgraph flowgraph, GameObject thisNodeGO, System.Action<int, GameObject> loadModelCallback)
    {
        List<UInt32> resourceID = GetResource(ref node);
        for (int i = 0; i < resourceID.Count; i++)
        {
            List<CathodeResourceReference> resRef = flowgraph.GetResourceReferencesByID(resourceID[i]);
            for (int x = 0; x < resRef.Count; x++)
            {
                if (resRef[x].entryType != CathodeResourceReferenceType.RENDERABLE_INSTANCE) continue; //Ignoring collision maps, etc, for now
                for (int p = 0; p < resRef[x].entryCountREDS; p++) loadModelCallback(redsBIN[resRef[x].entryIndexREDS].ModelIndex + p, thisNodeGO);
            }
        }
    }
}

public class PosAndRot
{
    public static PosAndRot operator+ (PosAndRot a, PosAndRot b)
    {
        PosAndRot newTrans = new PosAndRot();
        newTrans.position = a.position + b.position;
        newTrans.rotation = a.rotation * b.rotation;
        return newTrans;
    }

    public Vector3 position = new Vector3();
    public Quaternion rotation = new Quaternion();
}