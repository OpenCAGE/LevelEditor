using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using System;
using TestProject.File_Handlers.Commands;
using CathodeLib;

public class CommandsLoader
{
    private CommandsPAK commandsPAK = null;
    private List<alien_reds_entry> redsBIN;

    //Test code to load in everything that has a position: note, the hierarchy of objects needs to be considered here
    public void LoadCommandsPAK(string LEVEL_NAME, List<alien_reds_entry> redsbin, System.Action<int, GameObject> loadModelCallback)
    {
        string basePath = LEVEL_NAME + "\\";
        commandsPAK = new CommandsPAK(basePath + @"WORLD\COMMANDS.PAK");
        redsBIN = redsbin;

        for (int i = 0; i < commandsPAK.EntryPoints.Count; i++)
        {
            GameObject thisFlowgraphGO = new GameObject(commandsPAK.EntryPoints[i].name);
            RecursiveLoad(commandsPAK.EntryPoints[i], thisFlowgraphGO, loadModelCallback);
        }
    }

    private void RecursiveLoad(CathodeFlowgraph flowgraph, GameObject parentTransform, System.Action<int, GameObject> loadModelCallback)
    {
        List<CathodeNodeEntity> models = GetAllOfType(flowgraph, new string[] { "ModelReference", "EnvironmentModelReference" });
        foreach (CathodeNodeEntity node in models)
        {
            PosAndRot trans = GetTransform(node);
            GameObject thisNodeGO = new GameObject(NodeDB.GetNodeTypeName(node.nodeType, commandsPAK) + ": " + NodeDB.GetFriendlyName(node.nodeID));
            thisNodeGO.transform.parent = parentTransform.transform;
            thisNodeGO.transform.localPosition = trans.position;
            thisNodeGO.transform.localRotation = trans.rotation;

            LoadModelNode(node, flowgraph, thisNodeGO, loadModelCallback);
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

        List<CathodeNodeEntity> lights = GetAllOfType(flowgraph, new string[] { "LightReference" });
        foreach (CathodeNodeEntity node in lights)
        {
            PosAndRot thisNodePos = GetTransform(node) + stackedTransform;
            GameObject newCube = new GameObject(NodeDB.GetName(node.nodeType));
            newCube.transform.position = new Vector3(thisNodePos.position.x, thisNodePos.position.y, thisNodePos.position.z);
            newCube.transform.eulerAngles = new Vector3(thisNodePos.rotation.x, thisNodePos.rotation.y, thisNodePos.rotation.z);
            newCube.AddComponent<Light>();
            continue;
        }
        */

        foreach (CathodeNodeEntity node in flowgraph.nodes)
        {
            CathodeFlowgraph nextCall = commandsPAK.GetFlowgraph(node.nodeType);
            if (nextCall == null) continue;
            PosAndRot trans = GetTransform(node);
            GameObject nextFlowgraphGO = new GameObject(nextCall.name);
            nextFlowgraphGO.transform.parent = parentTransform.transform;
            nextFlowgraphGO.transform.localPosition = trans.position;
            nextFlowgraphGO.transform.localRotation = trans.rotation;
            RecursiveLoad(nextCall, nextFlowgraphGO, loadModelCallback);
        }
    }

    private List<CathodeNodeEntity> GetAllOfType(CathodeFlowgraph flowgraph, string[] typeMatch)
    {
        List<CathodeNodeEntity> matchingNodes = new List<CathodeNodeEntity>();
        foreach (CathodeNodeEntity node in flowgraph.nodes)
        {
            if (!node.HasNodeType) continue;
            if (!typeMatch.ToList<string>().Contains(NodeDB.GetName(node.nodeType))) continue;
            matchingNodes.Add(node);
        }
        return matchingNodes;
    }

    private PosAndRot GetTransform(CathodeNodeEntity node)
    {
        PosAndRot toReturn = new PosAndRot();
        foreach (CathodeParameterReference paramRef in node.nodeParameterReferences)
        {
            CathodeParameter param = commandsPAK.GetParameter(paramRef.offset);
            if (param == null) continue;
            if (param.dataType != CathodeDataType.POSITION) continue;
            Vec3 position = ((CathodeTransform)param).position;
            Vec3 rotation = ((CathodeTransform)param).rotation;
            toReturn.position = new Vector3(position.x, position.y, position.z);
            toReturn.rotation = Quaternion.Euler(rotation.x, rotation.y, rotation.z);
        }
        return toReturn;
    }

    private List<byte[]> GetResource(CathodeNodeEntity node)
    {
        List<byte[]> resources = new List<byte[]>();
        foreach (CathodeParameterReference paramRef in node.nodeParameterReferences)
        {
            CathodeParameter param = commandsPAK.GetParameter(paramRef.offset);
            if (param == null) continue;
            if (param.dataType != CathodeDataType.SHORT_GUID) continue;
            resources.Add(((CathodeResource)param).resourceID);
        }
        return resources;
    }

    private void LoadModelNode(CathodeNodeEntity node, CathodeFlowgraph flowgraph, GameObject thisNodeGO, System.Action<int, GameObject> loadModelCallback)
    {
        List<byte[]> resourceID = GetResource(node);
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