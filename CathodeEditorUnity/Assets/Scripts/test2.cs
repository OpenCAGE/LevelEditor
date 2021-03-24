using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TestProject;
using System.IO;
using System;

public class test2 : MonoBehaviour
{
    alien_level Result = new alien_level();

    void Start()
    {
        string levelPath = @"G:\SteamLibrary\steamapps\common\Alien Isolation\DATA\ENV\PRODUCTION\BSP_TORRENS";

        Result.ModelsCST = File.ReadAllBytes(levelPath + "/RENDERABLE/LEVEL_MODELS.CST");
        Result.ModelsMTL = TestProject.File_Handlers.Models.ModelsMTL.Load(levelPath + "/RENDERABLE/LEVEL_MODELS.MTL", Result.ModelsCST);
        Result.ModelsBIN = TestProject.File_Handlers.Models.ModelBIN.Load(levelPath + "/RENDERABLE/MODELS_LEVEL.BIN");
        Result.ModelsPAK = TestProject.File_Handlers.Models.ModelPAK.Load(levelPath + "/RENDERABLE/LEVEL_MODELS.PAK");
    }

    private GameObject LoadModel(int EntryIndex)
    {
        Debug.Log("---");
        Debug.Log("LOADING MODEL AT INDEX " + EntryIndex);
        alien_pak_model_entry ChunkArray = Result.ModelsPAK.Models[EntryIndex];
        Debug.Log("CHUNK COUNT: " + ChunkArray.Header.ChunkCount);

        GameObject ThisModel = new GameObject();

        for (int ChunkIndex = 0; ChunkIndex < ChunkArray.Header.ChunkCount; ++ChunkIndex)
        {
            int BINIndex = ChunkArray.ChunkInfos[ChunkIndex].BINIndex;
            alien_model_bin_model_info Model = Result.ModelsBIN.Models[BINIndex];
            if (Model.BlockSize == 0) continue;

            alien_vertex_buffer_format VertexInput = Result.ModelsBIN.VertexBufferFormats[Model.VertexFormatIndex];
            alien_vertex_buffer_format VertexInputLowDetail = Result.ModelsBIN.VertexBufferFormats[Model.VertexFormatIndexLowDetail];

            BinaryReader Stream = new BinaryReader(new MemoryStream(ChunkArray.Chunks[ChunkIndex]));

            int VertexArrayCount = 1;
            alien_vertex_buffer_format_element[] Elements = new alien_vertex_buffer_format_element[256];
            Elements[0] = VertexInput.Elements[0];
            int[] ElementCounts = new int[256];
            for (int ElementIndex = 0; ElementIndex < VertexInput.ElementCount; ++ElementIndex)
            {
                alien_vertex_buffer_format_element Element = VertexInput.Elements[ElementIndex];
                if (VertexArrayCount - 1 != Element.ArrayIndex)
                {
                    Elements[VertexArrayCount++] = Element;
                }
                ElementCounts[VertexArrayCount - 1]++;
            }

            //Debug.Log(VertexArrayCount);

            List<UInt16> InIndices = new List<UInt16>();
            List<Vector3> InVertices = new List<Vector3>();
            List<Vector3> InNormals = new List<Vector3>();

            for (int VertexArrayIndex = 0; VertexArrayIndex < VertexArrayCount; ++VertexArrayIndex)
            {
                Debug.Log(Stream.BaseStream.Position + " - " + Stream.BaseStream.Length);

                int ElementCount = ElementCounts[VertexArrayIndex];
                alien_vertex_buffer_format_element Inputs = Elements[VertexArrayIndex];
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
                        for (int ElementIndex = 0; ElementIndex < ElementCount; ++ElementIndex)
                        {
                            alien_vertex_buffer_format_element Input = Elements[VertexArrayIndex + ElementIndex];
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
            ThisModel.name = Result.ModelsBIN.ModelFilePaths[BINIndex];
            ThisModelPart.name = Result.ModelsBIN.ModelLODPartNames[BINIndex] + "(" + Result.ModelsMTL.MaterialNames[Model.MaterialLibraryIndex] + ")"; 

            if (InVertices.Count == 0) continue;

            int maxind = -int.MaxValue;
            for (int i = 0; i < InIndices.Count; i++)
            {
                //Debug.Log(indiciesConv[i]);
                if (InIndices[i] > maxind) maxind = InIndices[i];
            }

            Debug.Log("Indices: " + InIndices.Count + ", Vertices: " + InVertices.Count + ", MaxInd: " + maxind);
            Debug.Log("---");

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

    // Update is called once per frame
    GameObject currentMesh = null;
    int currentMeshIndex = 50;
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (currentMesh != null) Destroy(currentMesh);
            currentMesh = LoadModel(currentMeshIndex);
            currentMeshIndex++;
        }
    }

    public void Align(BinaryReader reader, int val)
    {
        while (reader.BaseStream.Position % val != 0)
        {
            reader.ReadByte();
        }
    }
}
