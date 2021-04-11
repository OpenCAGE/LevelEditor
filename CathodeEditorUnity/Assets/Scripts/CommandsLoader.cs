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
    private List<int> redsBIN;
    private List<PreloadedFlowgraphContent> preloadedModelReferenceNodes = new List<PreloadedFlowgraphContent>();
    private List<PreloadedFlowgraphContent> preloadedPlayerTriggerBoxNodes = new List<PreloadedFlowgraphContent>();

    UInt32 ModelReferenceID = 0;
    UInt32 PlayerTriggerBoxID = 0;
    void Start()
    {
        byte[] ModelReferenceIDBytes = new byte[4] { 0x94, 0xA8, 0xB4, 0xB9 };
        byte[] PlayerTriggerBoxIDBytes = new byte[4] { 0xD3, 0xFA, 0x3E, 0x2F };
        ModelReferenceID = BitConverter.ToUInt32(ModelReferenceIDBytes, 0);
        PlayerTriggerBoxID = BitConverter.ToUInt32(PlayerTriggerBoxIDBytes, 0);
    }

    public IEnumerator LoadCommandsPAK(string LEVEL_NAME, List<int> redsbin, System.Action<int, GameObject> loadModelCallback)
    {
        string basePath = LEVEL_NAME + "\\";
        commandsPAK = new CommandsPAK(basePath + @"WORLD\COMMANDS.PAK");
        redsBIN = redsbin;

        for (int i = 0; i < commandsPAK.AllFlowgraphs.Count; i++)
        {
            preloadedModelReferenceNodes.Add(PreloadFlowgraphContent(commandsPAK.AllFlowgraphs[i], ModelReferenceID));
            preloadedPlayerTriggerBoxNodes.Add(PreloadFlowgraphContent(commandsPAK.AllFlowgraphs[i], PlayerTriggerBoxID));
        }

        for (int i = 0; i < commandsPAK.EntryPoints.Count; i++)
        {
            GameObject thisFlowgraphGO = new GameObject(commandsPAK.EntryPoints[i].name);
            StartCoroutine(RecursiveLoad(commandsPAK.EntryPoints[i], thisFlowgraphGO, loadModelCallback));
        }

        yield break;
    }

    private PreloadedFlowgraphContent PreloadFlowgraphContent(CathodeFlowgraph flowgraph, uint typeID)
    {
        PreloadedFlowgraphContent content = new PreloadedFlowgraphContent();
        content.flowraphID = flowgraph.nodeID;
        List<CathodeNodeEntity> models = GetAllOfType(ref flowgraph, typeID);
        for (int i = 0; i < models.Count; i++)
        {
            CathodeNodeEntity thisNode = models[i];
            List<int> modelIndexes = new List<int>();
            List<UInt32> resourceID = new List<UInt32>();
            foreach (CathodeParameterReference paramRef in thisNode.nodeParameterReferences)
            {
                CathodeParameter param = commandsPAK.GetParameter(paramRef.offset);
                if (param == null) continue;
                if (param.dataType != CathodeDataType.SHORT_GUID) continue;
                resourceID.Add(((CathodeResource)param).resourceID);
            }
            for (int x = 0; x < resourceID.Count; x++)
            {
                List<CathodeResourceReference> resRef = flowgraph.GetResourceReferencesByID(resourceID[x]);
                for (int y = 0; y < resRef.Count; y++)
                {
                    if (resRef[y].entryType != CathodeResourceReferenceType.RENDERABLE_INSTANCE) continue; //Ignoring collision maps, etc, for now
                    for (int p = 0; p < resRef[y].entryCountREDS; p++) modelIndexes.Add(redsBIN[resRef[y].entryIndexREDS] + p);
                }
            }
            content.nodeModelIDs.Add(modelIndexes);
            content.nodeTransforms.Add(GetTransform(ref thisNode));
            content.half_dimensions = GetHalfDimensions(ref thisNode);
            content.nodeNames.Add(NodeDB.GetNodeTypeName(thisNode.nodeType, ref commandsPAK) + ": " + NodeDB.GetFriendlyName(thisNode.nodeID));
        }
        return content;
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

        StartCoroutine(LoadModelReferenceNodes(flowgraph, parentTransform, loadModelCallback));
        StartCoroutine(LoadPlayerTriggerBoxNodes(flowgraph, parentTransform, loadModelCallback));

        yield break;
    }

    private IEnumerator LoadModelReferenceNodes(CathodeFlowgraph flowgraph, GameObject parentTransform, System.Action<int, GameObject> loadModelCallback)
    {
        PreloadedFlowgraphContent content = preloadedModelReferenceNodes.FirstOrDefault(o => o.flowraphID == flowgraph.nodeID);
        for (int i = 0; i < content.nodeNames.Count; i++)
        {
            GameObject thisNodeGO = new GameObject(content.nodeNames[i]);
            thisNodeGO.transform.parent = parentTransform.transform;
            thisNodeGO.transform.localPosition = content.nodeTransforms[i].position;
            thisNodeGO.transform.localRotation = content.nodeTransforms[i].rotation;
            for (int x = 0; x < content.nodeModelIDs[i].Count; x++) loadModelCallback(content.nodeModelIDs[i][x], thisNodeGO);
        }
        yield break;
    }
    private IEnumerator LoadPlayerTriggerBoxNodes(CathodeFlowgraph flowgraph, GameObject parentTransform, System.Action<int, GameObject> loadModelCallback)
    {
        PreloadedFlowgraphContent content = preloadedPlayerTriggerBoxNodes.FirstOrDefault(o => o.flowraphID == flowgraph.nodeID);
        for (int i = 0; i < content.nodeNames.Count; i++)
        {
            GameObject thisNodeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            thisNodeGO.name = content.nodeNames[i];
            thisNodeGO.transform.parent = parentTransform.transform;
            thisNodeGO.transform.localPosition = content.nodeTransforms[i].position;
            thisNodeGO.transform.localRotation = content.nodeTransforms[i].rotation;
            thisNodeGO.transform.localScale = new Vector3(content.half_dimensions.y, content.half_dimensions.z, content.half_dimensions.x) * 2; //i dont think this is right
        }
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
    private Vector3 GetHalfDimensions(ref CathodeNodeEntity node)
    {
        Vector3 toReturn = new Vector3(0,0,0);
        foreach (CathodeParameterReference paramRef in node.nodeParameterReferences)
        {
            CathodeParameter param = commandsPAK.GetParameter(paramRef.offset);
            if (param == null) continue;
            if (param.dataType != CathodeDataType.DIRECTION) continue;
            CathodeVector3 transform = (CathodeVector3)param;
            toReturn = transform.value;
            break;
        }
        return toReturn;
    }
}

class PreloadedFlowgraphContent
{
    public UInt32 flowraphID;
    public List<string> nodeNames = new List<string>();
    public List<List<int>> nodeModelIDs = new List<List<int>>();
    public List<PosAndRot> nodeTransforms = new List<PosAndRot>();
    public Vector3 half_dimensions = new Vector3(0, 0, 0);
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