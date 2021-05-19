using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using System;
using CATHODE.Commands;
using CathodeLib;

public class CommandsLoader : MonoBehaviour
{
    private CommandsPAK commandsPAK = null;

    /* Setup node IDs we'll be looking for */
    UInt32 LightReferenceID = 0;
    void Start()
    {
        LightReferenceID = BitConverter.ToUInt32(new byte[4] { 0xEE, 0x2D, 0xA6, 0xF5 }, 0);
    }

    /* Load all flowgraph entry points */
    public IEnumerator LoadCommandsPAK(string basePath)
    {
        commandsPAK = new CommandsPAK(basePath + @"\WORLD\COMMANDS.PAK");

        for (int i = 0; i < commandsPAK.EntryPoints.Count; i++)
        {
            GameObject thisFlowgraphGO = new GameObject(commandsPAK.EntryPoints[i].name);
            StartCoroutine(RecursiveLoad(commandsPAK.EntryPoints[i], thisFlowgraphGO));
        }

        yield break;
    }

    /* Recursively load a flowgraph */
    private IEnumerator RecursiveLoad(CathodeFlowgraph flowgraph, GameObject parentTransform)
    {
        //Find and load all light nodes
        List<CathodeNodeEntity> lightNodes = GetAllOfType(ref flowgraph, LightReferenceID);
        for (int i = 0; i < lightNodes.Count; i++)
        {
            CathodeNodeEntity thisNode = lightNodes[i];
            PosAndRot thisNodePosAndRot = GetTransform(ref thisNode);
            GameObject thisLightGO = new GameObject();
            thisLightGO.name = NodeDB.GetFriendlyName(thisNode.nodeID);
            thisLightGO.transform.parent = parentTransform.transform;
            thisLightGO.transform.localPosition = thisNodePosAndRot.position;
            thisLightGO.transform.localRotation = thisNodePosAndRot.rotation;
            Light thisLight = thisLightGO.AddComponent<Light>();

            //Default vals
            thisLight.enabled = false; //"on"
            //Overridden vals
            foreach (CathodeParameterReference lightParamRef in lightNodes[i].nodeParameterReferences)
            {
                CathodeParameter lightParam = commandsPAK.GetParameter(lightParamRef.offset);
                if (lightParam == null) continue;
                if (lightParam.dataType == CathodeDataType.NONE) continue;
                string paramName = NodeDB.GetFriendlyName(lightParamRef.paramID);

                if (lightParam.dataType == CathodeDataType.DIRECTION)
                {
                    Vector3 v = ((CathodeVector3)lightParam).value;
                    if (paramName == "colour") thisLight.color = new Color(v.x, v.y, v.z);
                }
                else if(lightParam.dataType == CathodeDataType.FLOAT || lightParam.dataType == CathodeDataType.INTEGER)
                {
                    float f = 0.0f;
                    if (lightParam.dataType == CathodeDataType.FLOAT) f = ((CathodeFloat)lightParam).value;
                    if (lightParam.dataType == CathodeDataType.INTEGER) f = ((CathodeInteger)lightParam).value;
                    if (paramName == "intensity_multiplier") thisLight.intensity = f;
                }
                else if (lightParam.dataType == CathodeDataType.BOOL)
                {
                    bool b = ((CathodeBool)lightParam).value;
                    if (paramName == "on") thisLight.enabled = b;
                }
                else if (lightParam.dataType == CathodeDataType.ENUM)
                {
                    CathodeEnum e = (CathodeEnum)lightParam;
                    if (NodeDB.GetFriendlyName(e.enumID) == "LIGHT_TYPE") thisLight.type = (LightType)e.enumIndex; //TODO: work out mapping of this enum to Unity enum
                }
            }
        }

        //Find references to other flowgraphs, and continue down the hierarchy to load them
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
            StartCoroutine(RecursiveLoad(nextCall, nextFlowgraphGO));
        }

        yield break;
    }

    /* Get all nodes of a specified type from flowgraph */
    private List<CathodeNodeEntity> GetAllOfType(ref CathodeFlowgraph flowgraph, UInt32 nodeType)
    {
        return flowgraph.nodes.FindAll(o => o.nodeType == nodeType);
    }

    /* Get transform of node */
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