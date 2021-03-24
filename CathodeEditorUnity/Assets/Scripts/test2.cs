using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TestProject;
using System.IO;
using System;
using CATHODE;
using System.Linq;

public class test2 : MonoBehaviour
{
    alien_level Result = new alien_level();

    CATHODE.CommandsPAK commandsPAK;
    CATHODE.RenderableElementsBIN redsBIN;

    void Start()
    {
        string levelPath = @"C:\Users\MattFiler\Downloads\BSP_TORRENS\BSP_TORRENS";

        Result.ModelsCST = File.ReadAllBytes(levelPath + "/RENDERABLE/LEVEL_MODELS.CST");
        Result.ModelsMTL = TestProject.File_Handlers.Models.ModelsMTL.Load(levelPath + "/RENDERABLE/LEVEL_MODELS.MTL", Result.ModelsCST);
        Result.ModelsBIN = TestProject.File_Handlers.Models.ModelBIN.Load(levelPath + "/RENDERABLE/MODELS_LEVEL.BIN");
        Result.ModelsPAK = TestProject.File_Handlers.Models.ModelPAK.Load(levelPath + "/RENDERABLE/LEVEL_MODELS.PAK");


        commandsPAK = new CATHODE.CommandsPAK(levelPath + @"\WORLD\COMMANDS.PAK");
        redsBIN = new CATHODE.RenderableElementsBIN(levelPath + @"\WORLD\REDS.BIN");
        RecursiveLoad(commandsPAK.EntryPoints[0], new PosAndRot());
    }

    private void RecursiveLoad(CathodeFlowgraph flowgraph, PosAndRot stackedTransform)
    {
        List<CathodeNodeEntity> models = flowgraph.nodes; //TODO: reimplement model filtering from test.cs (nodedb broken)
        foreach (CathodeNodeEntity node in models)
        {
            PosAndRot thisNodePos = GetTransform(node) + stackedTransform;
            LoadREDS(node, flowgraph, thisNodePos);
        }

        foreach (CathodeNodeEntity node in flowgraph.nodes)
        {
            CathodeFlowgraph nextCall = commandsPAK.GetFlowgraph(node.nodeType);
            if (nextCall == null) continue;
            Debug.Log(GetTransform(node).position);
            RecursiveLoad(nextCall, stackedTransform + GetTransform(node));
        }
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
    private void LoadREDS(CathodeNodeEntity node, CathodeFlowgraph flowgraph, PosAndRot thisNodePos)
    {
        //If has a renderable element, try create it
        byte[] resourceID = GetResource(node);
        if (resourceID == null) return;
        CathodeResourceReference resRef = flowgraph.GetResourceReferenceByID(resourceID);
        if (resRef == null || resRef.entryType != CathodeResourceReferenceType.RENDERABLE_INSTANCE) return;
        List<RenderableElement> redsList = new List<RenderableElement>();
        for (int p = 0; p < resRef.entryCountREDS; p++) redsList.Add(redsBIN.GetRenderableElement(resRef.entryIndexREDS + p));
        if (resRef.entryCountREDS != redsList.Count || redsList.Count == 0) return; //TODO: handle this nicer
        foreach (RenderableElement renderable in redsList)
        {
            LoadModel(renderable.model_index, thisNodePos);
        }
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

    private GameObject LoadModel(int EntryIndex, PosAndRot thisNodePos)
    {
        if (EntryIndex < 0 || EntryIndex >= Result.ModelsPAK.Models.Count)
        {
            Debug.LogWarning("Asked to load model at index " + EntryIndex + ", which is out of bounds!");
            return new GameObject();
        }

        alien_pak_model_entry ChunkArray = Result.ModelsPAK.Models[EntryIndex];

        GameObject ThisModel = new GameObject();
        ThisModel.transform.position = thisNodePos.position;
        ThisModel.transform.rotation = Quaternion.Euler(thisNodePos.rotation);

        for (int ChunkIndex = 0; ChunkIndex < ChunkArray.Header.ChunkCount; ++ChunkIndex)
        {
            int BINIndex = ChunkArray.ChunkInfos[ChunkIndex].BINIndex;
            alien_model_bin_model_info Model = Result.ModelsBIN.Models[BINIndex];
            if (Model.BlockSize == 0) continue;

            alien_vertex_buffer_format VertexInput = Result.ModelsBIN.VertexBufferFormats[Model.VertexFormatIndex];
            alien_vertex_buffer_format VertexInputLowDetail = Result.ModelsBIN.VertexBufferFormats[Model.VertexFormatIndexLowDetail];

            BinaryReader Stream = new BinaryReader(new MemoryStream(ChunkArray.Chunks[ChunkIndex]));

            List<List<alien_vertex_buffer_format_element>> Elements = new List<List<alien_vertex_buffer_format_element>>();
            alien_vertex_buffer_format_element ElementHeader = new alien_vertex_buffer_format_element();
            foreach (alien_vertex_buffer_format_element Element in VertexInput.Elements)
            {
                if (Element.ArrayIndex == 0xFF)
                {
                    ElementHeader = Element;
                    continue;
                }

                while (Elements.Count - 1 < Element.ArrayIndex) Elements.Add(new List<alien_vertex_buffer_format_element>());
                Elements[Element.ArrayIndex].Add(Element);
            }
            Elements.Add(new List<alien_vertex_buffer_format_element>() { ElementHeader });

            List<UInt16> InIndices = new List<UInt16>();
            List<Vector3> InVertices = new List<Vector3>();
            List<Vector3> InNormals = new List<Vector3>();

            for (int VertexArrayIndex = 0; VertexArrayIndex < Elements.Count; ++VertexArrayIndex)
            {
                alien_vertex_buffer_format_element Inputs = Elements[VertexArrayIndex][0];
                if (Inputs.ArrayIndex == 0xFF)
                {
                    for (int i = 0; i < Model.IndexCount; i++)
                    {
                        InIndices.Add(Stream.ReadUInt16());
                    }
                }
                else
                {
                    for (int VertexIndex = 0; VertexIndex < Model.VertexCount; ++VertexIndex)
                    {
                        for (int ElementIndex = 0; ElementIndex < Elements[VertexArrayIndex].Count; ++ElementIndex)
                        {
                            alien_vertex_buffer_format_element Input = Elements[VertexArrayIndex][ElementIndex];
                            switch (Input.VariableType)
                            {
                                case alien_vertex_input_type.AlienVertexInputType_v3:
                                    {
                                        Vector3 Value = new Vector3(Stream.ReadSingle(), Stream.ReadSingle(), Stream.ReadSingle());
                                        switch (Input.ShaderSlot)
                                        {
                                            case alien_vertex_input_slot.AlienVertexInputSlot_N:
                                                InNormals.Add(Value);
                                                break;
                                            case alien_vertex_input_slot.AlienVertexInputSlot_T:
                                                break;
                                            case alien_vertex_input_slot.AlienVertexInputSlot_UV:
                                                break;
                                        };
                                        break;
                                    }

                                case alien_vertex_input_type.AlienVertexInputType_u32_C:
                                    {
                                        int Value = Stream.ReadInt32();
                                        switch (Input.ShaderSlot)
                                        {
                                            case alien_vertex_input_slot.AlienVertexInputSlot_C:
                                                break;
                                        }
                                        break;
                                    }

                                case alien_vertex_input_type.AlienVertexInputType_v4u8_i:
                                    {
                                        Vector4 Value = new Vector4(Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte());
                                        switch (Input.ShaderSlot)
                                        {
                                            case alien_vertex_input_slot.AlienVertexInputSlot_BI:
                                                break;
                                        }
                                        break;
                                    }

                                case alien_vertex_input_type.AlienVertexInputType_v4u8_f:
                                    {
                                        Vector4 Value = new Vector4(Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte());
                                        Value /= 255.0f;
                                        switch (Input.ShaderSlot)
                                        {
                                            case alien_vertex_input_slot.AlienVertexInputSlot_BW:
                                                break;
                                            case alien_vertex_input_slot.AlienVertexInputSlot_UV:
                                                break;
                                        }
                                        break;
                                    }

                                case alien_vertex_input_type.AlienVertexInputType_v2s16_UV:
                                    {
                                        Vector2 Value = new Vector2(Stream.ReadInt16(), Stream.ReadInt16());
                                        Value /= 2048.0f;
                                        switch (Input.ShaderSlot)
                                        {
                                            case alien_vertex_input_slot.AlienVertexInputSlot_UV:
                                                break;
                                        }
                                        break;
                                    }

                                case alien_vertex_input_type.AlienVertexInputType_v4s16_f:
                                    {
                                        Vector4 Value = new Vector4(Stream.ReadInt16(), Stream.ReadInt16(), Stream.ReadInt16(), Stream.ReadInt16());
                                        Value /= (float)Int16.MaxValue;
                                        switch (Input.ShaderSlot)
                                        {
                                            case alien_vertex_input_slot.AlienVertexInputSlot_P:
                                                InVertices.Add(Value);
                                                break;
                                        }
                                        break;
                                    }

                                case alien_vertex_input_type.AlienVertexInputType_v2s16_f:
                                    {
                                        Vector4 Value = new Vector4(Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte());
                                        Value /= (float)byte.MaxValue - 0.5f;
                                        Value.Normalize();
                                        switch (Input.ShaderSlot)
                                        {
                                            case alien_vertex_input_slot.AlienVertexInputSlot_N:
                                                InNormals.Add(Value);
                                                break;
                                            case alien_vertex_input_slot.AlienVertexInputSlot_T:
                                                break;
                                            case alien_vertex_input_slot.AlienVertexInputSlot_B:
                                                break;
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                }
                Align(Stream, 16);
            }

            GameObject ThisModelPart = new GameObject();
            ThisModelPart.transform.parent = ThisModel.transform;
            ThisModelPart.transform.localScale = new Vector3(Model.ScaleFactor, Model.ScaleFactor, Model.ScaleFactor);
            ThisModel.name = Result.ModelsBIN.ModelFilePaths[BINIndex];
            ThisModelPart.name = Result.ModelsBIN.ModelLODPartNames[BINIndex] + "(" + Result.ModelsMTL.MaterialNames[Model.MaterialLibraryIndex] + ")"; 

            if (InVertices.Count == 0) continue;

            Mesh thisMesh = new Mesh();
            thisMesh.SetVertices(InVertices);
            thisMesh.SetNormals(InNormals);
            thisMesh.SetIndices(InIndices, MeshTopology.Triangles, 0); //0??
            thisMesh.RecalculateBounds();
            thisMesh.RecalculateNormals();
            thisMesh.RecalculateTangents();
            ThisModelPart.AddComponent<MeshFilter>().mesh = thisMesh;
            ThisModelPart.AddComponent<MeshRenderer>().material = new Material(Shader.Find("Diffuse"));

        }

        return ThisModel;
    }

    /*
    GameObject currentMesh = null;
    int currentMeshIndex = 550;
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (currentMesh != null) Destroy(currentMesh);
            currentMesh = LoadModel(currentMeshIndex);
            currentMeshIndex++;
        }
    }
    */

    public void Align(BinaryReader reader, int val)
    {
        while (reader.BaseStream.Position % val != 0)
        {
            reader.ReadByte();
        }
    }
}
