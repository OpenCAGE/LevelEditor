using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using System;
using CATHODE;
using CathodeLib;

public class test
{
    private CommandsPAK commandsPAK = null;
    private List<alien_reds_entry> redsBIN;

    //Test code to load in everything that has a position: note, the hierarchy of objects needs to be considered here
    public void LoadCommandsPAK(List<alien_reds_entry> redsbin, System.Action<int, PosAndRot> loadModelCallback)
    {
        string basePath = @"G:\SteamLibrary\steamapps\common\Alien Isolation\DATA\ENV\PRODUCTION\BSP_TORRENS\";
        commandsPAK = new CommandsPAK(basePath + @"WORLD\COMMANDS.PAK");
        redsBIN = redsbin;

        RecursiveLoad(commandsPAK.EntryPoints[0], new PosAndRot(), loadModelCallback);
    }

    private void RecursiveLoad(CathodeFlowgraph flowgraph, PosAndRot stackedTransform, System.Action<int, PosAndRot> loadModelCallback)
    {
        List<CathodeNodeEntity> models = GetAllOfType(flowgraph, new string[] { "ModelReference", "EnvironmentModelReference" });
        foreach (CathodeNodeEntity node in models)
        {
            PosAndRot thisNodePos = GetTransform(node) + stackedTransform;
            //GameObject newCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            //newCube.name = NodeDB.GetFriendlyName(node.nodeID);
            //newCube.transform.position = new Vector3(thisNodePos.position.x, thisNodePos.position.y, thisNodePos.position.z);
            //newCube.transform.eulerAngles = new Vector3(thisNodePos.rotation.x, thisNodePos.rotation.y, thisNodePos.rotation.z);
            LoadREDS(node, flowgraph, thisNodePos, loadModelCallback);
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
            Debug.Log(GetTransform(node).position);
            RecursiveLoad(nextCall, stackedTransform + GetTransform(node), loadModelCallback);
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
            toReturn.rotation = new Vector3(rotation.x, rotation.y, rotation.z);
        }
        return toReturn;
    }

    private byte[] GetResource(CathodeNodeEntity node)
    {
        foreach (CathodeParameterReference paramRef in node.nodeParameterReferences)
        {
            CathodeParameter param = commandsPAK.GetParameter(paramRef.offset);
            if (param == null) continue;
            if (param.dataType != CathodeDataType.SHORT_GUID) continue;
            return ((CathodeResource)param).resourceID;
        }
        return null;
    }

    private void LoadREDS(CathodeNodeEntity node, CathodeFlowgraph flowgraph, PosAndRot thisNodePos, System.Action<int, PosAndRot> loadModelCallback)
    {
        //If has a renderable element, try create it
        byte[] resourceID = GetResource(node);
        if (resourceID == null) return;
        CathodeResourceReference resRef = flowgraph.GetResourceReferenceByID(resourceID);
        if (resRef == null || resRef.entryType != CathodeResourceReferenceType.RENDERABLE_INSTANCE) return;
        for (int p = 0; p < resRef.entryCountREDS; p++)
        {
            loadModelCallback(redsBIN[resRef.entryIndexREDS].ModelIndex + p, thisNodePos);
        }
    }
}

public class PosAndRot
{
    public static PosAndRot operator+ (PosAndRot a, PosAndRot b)
    {
        PosAndRot newTrans = new PosAndRot();
        newTrans.position = a.position + b.position;
        newTrans.rotation = a.rotation + b.rotation;
        return newTrans;
    }

    public Vector3 position = new Vector3();
    public Vector3 rotation = new Vector3();
}