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
    [SerializeField] private UnityEngine.UI.Image uiSprite = null;

    alien_level Result = new alien_level();

    void Start()
    {
        string levelPath = @"G:\SteamLibrary\steamapps\common\Alien Isolation\DATA\ENV\PRODUCTION\BSP_TORRENS";

        Result.GlobalTextures = TestProject.File_Handlers.Textures.TexturePAK.Load(levelPath + "/../../GLOBAL/WORLD/GLOBAL_TEXTURES.ALL.PAK", levelPath + "/../../GLOBAL/WORLD/GLOBAL_TEXTURES_HEADERS.ALL.BIN");
        Result.LevelTextures = TestProject.File_Handlers.Textures.TexturePAK.Load(levelPath + "/RENDERABLE/LEVEL_TEXTURES.ALL.PAK", levelPath + "/RENDERABLE/LEVEL_TEXTURE_HEADERS.ALL.BIN");

        Result.ModelsCST = File.ReadAllBytes(levelPath + "/RENDERABLE/LEVEL_MODELS.CST");
        Result.ModelsMTL = TestProject.File_Handlers.Models.ModelsMTL.Load(levelPath + "/RENDERABLE/LEVEL_MODELS.MTL", Result.ModelsCST);
        Result.ModelsBIN = TestProject.File_Handlers.Models.ModelBIN.Load(levelPath + "/RENDERABLE/MODELS_LEVEL.BIN");
        Result.ModelsPAK = TestProject.File_Handlers.Models.ModelPAK.Load(levelPath + "/RENDERABLE/LEVEL_MODELS.PAK");

        Result.ShadersPAK = TestProject.File_Handlers.Shaders.ShadersPAK.Load(levelPath + "/RENDERABLE/LEVEL_SHADERS_DX11.PAK");
        //Result.ShadersBIN = TestProject.File_Handlers.Shaders.ShadersBIN.Load(levelPath + "/RENDERABLE/LEVEL_SHADERS_DX11_BIN.PAK");
        Result.ShadersIDXRemap = TestProject.File_Handlers.Shaders.IDXRemap.Load(levelPath + "/RENDERABLE/LEVEL_SHADERS_DX11_IDX_REMAP.PAK");
    }

    private Texture2D LoadTexture(int EntryIndex, int paktype = 0)
    {
        alien_textures AlienTextures = Result.LevelTextures; //todo; pass as param
        if (paktype == 2) AlienTextures = Result.GlobalTextures;
        if (EntryIndex < 0 || EntryIndex >= AlienTextures.PAK.Header.EntryCount)
        {
            Debug.LogWarning("Asked to load texture at index " + EntryIndex + ", which is out of bounds!");
            return null;
        }

        alien_pak_entry Entry = AlienTextures.PAK.Entries[EntryIndex];
        alien_texture_bin_texture InTexture = AlienTextures.BIN.Textures[Entry.BINIndex];

        //ASSUMES V1!!

        if (InTexture.Length_V1 == 0)
        {
            Debug.LogWarning("LENGTH ZERO - NOT LOADING");
            return null;
        }
        if (InTexture.Type == 7) 
        {
            Debug.LogWarning("CUBEMAP! NOT CURRENTLY SUPPORTED");
            return null;
        }

        UnityEngine.TextureFormat format = UnityEngine.TextureFormat.BC7;
        switch (InTexture.Format)
        {
            case alien_texture_format.Alien_R32G32B32A32_SFLOAT:
                format = UnityEngine.TextureFormat.RGBA32;
                break;
            case alien_texture_format.Alien_FORMAT_R8G8B8A8_UNORM:
                format = UnityEngine.TextureFormat.ETC2_RGBA8; //?
                break;
            case alien_texture_format.Alien_FORMAT_R8G8B8A8_UNORM_0:
                format = UnityEngine.TextureFormat.ETC2_RGBA8; //?
                break;
            case alien_texture_format.Alien_FORMAT_SIGNED_DISTANCE_FIELD:
                Debug.LogWarning("SDF! NOT LOADED");
                return null;
            case alien_texture_format.Alien_FORMAT_R8:
                format = UnityEngine.TextureFormat.R8;
                break;
            case alien_texture_format.Alien_FORMAT_BC1:
                Debug.LogWarning("BC1! NOT LOADED");
                return null;
            case alien_texture_format.Alien_FORMAT_BC2:
                Debug.LogWarning("BC2! NOT LOADED");
                return null;
            case alien_texture_format.Alien_FORMAT_BC5:
                format = UnityEngine.TextureFormat.BC5;
                break;
            case alien_texture_format.Alien_FORMAT_BC3:
                Debug.LogWarning("BC3! NOT LOADED");
                return null;
            case alien_texture_format.Alien_FORMAT_BC7:
                format = UnityEngine.TextureFormat.BC7;
                break;
            case alien_texture_format.Alien_FORMAT_R8G8:
                Debug.LogWarning("R8G8! NOT LOADED");
                return null;
        }

        Texture2D texture = new Texture2D(InTexture.Size_V1[0], InTexture.Size_V1[1], format, InTexture.MipLevelsV1, true);
        texture.name = AlienTextures.BIN.TextureFilePaths[Entry.BINIndex];
        BinaryReader tempReader = new BinaryReader(new MemoryStream(AlienTextures.PAK.DataStart));
        tempReader.BaseStream.Position = Entry.Offset;
        texture.LoadRawTextureData(tempReader.ReadBytes(InTexture.Length_V1));
        tempReader.Close();
        texture.Apply();
        return texture;
    }

    private GameObject LoadModel(int EntryIndex)
    {
        if (EntryIndex < 0 || EntryIndex >= Result.ModelsPAK.Models.Count)
        {
            Debug.LogWarning("Asked to load model at index " + EntryIndex + ", which is out of bounds!");
            return new GameObject();
        }

        alien_pak_model_entry ChunkArray = Result.ModelsPAK.Models[EntryIndex];

        GameObject ThisModel = new GameObject();

        for (int ChunkIndex = 0; ChunkIndex < ChunkArray.Header.ChunkCount; ++ChunkIndex)
        {
            int BINIndex = ChunkArray.ChunkInfos[ChunkIndex].BINIndex;
            alien_model_bin_model_info Model = Result.ModelsBIN.Models[BINIndex];
            if (Model.BlockSize == 0) continue;
            
            //Debug.Log(InMaterial.UberShaderIndex);
            //Debug.Log(Result.ShadersIDXRemap.Datas.Count);
            //int RemappedIndex = Result.ShadersIDXRemap.Datas[InMaterial.UberShaderIndex].Index;
            //alien_shader_pak_shader Shader = Result.ShadersPAK.Shaders[RemappedIndex];
            //
            //int TextureReferenceCount = Result.ModelsMTL.TextureReferenceCounts[Model.MaterialLibraryIndex];
            //int TableIndex = 11;
            //byte[] PairFromSlotTable = Shader.Tables[TableIndex];
            //int PairFromSlotTableSize = Shader.Header.TableEntryCounts[TableIndex];
            //for (int SlotIndex = 0; SlotIndex < PairFromSlotTableSize; ++SlotIndex)
            //{
            //    int PairIndex = PairFromSlotTable[SlotIndex];
            //    // NOTE: PairIndex == 255 means no index.
            //    if (PairIndex < TextureReferenceCount)
            //    {
            //        alien_mtl_texture_reference Pair = InMaterial.TextureReferences[PairIndex];
            //        alien_textures Textures = GetTexturesTable(Pair.TextureTableIndex);
            //        int TextureIndex = Pair.TextureIndex;
            //        alien_pak_entry Entry = Textures.PAK.Entries[TextureIndex];
            //        alien_texture_bin_texture InTexture = Textures.BIN.Textures[Entry.BINIndex];
            //        string TextureFilePath = Textures.BIN.TextureFilePaths[Entry.BINIndex];
            //        Debug.Log(TextureFilePath);
            //        alien_texture_format AlienFormat = (alien_texture_format)InTexture.Format;
            //    }
            //}

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
            List<Vector4> InTangents = new List<Vector4>();
            List<Vector2> InUVs0 = new List<Vector2>();
            List<Vector2> InUVs1 = new List<Vector2>();
            List<Vector2> InUVs2 = new List<Vector2>();
            List<Vector2> InUVs3 = new List<Vector2>();
            List<Vector2> InUVs7 = new List<Vector2>();

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
                                                InTangents.Add(new Vector4(Value.x, Value.y, Value.z, 0));
                                                break;
                                            case alien_vertex_input_slot.AlienVertexInputSlot_UV:
                                                //TODO: 3D UVW
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
                                                //??
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
                                                //skinned mesh joints
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
                                                //skinned mesh binding weights
                                                break;
                                            case alien_vertex_input_slot.AlienVertexInputSlot_UV:
                                                InUVs2.Add(new Vector2(Value.x, Value.y));
                                                InUVs3.Add(new Vector2(Value.z, Value.w));
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
                                                if (Input.VariantIndex == 0) InUVs0.Add(Value);
                                                else if (Input.VariantIndex == 1)
                                                {
                                                    // TODO: We can figure this out based on alien_vertex_buffer_format_element.
                                                    //Material->Material.Flags |= Material_HasTexCoord1;
                                                    InUVs1.Add(Value);
                                                }
                                                else if (Input.VariantIndex == 2) InUVs2.Add(Value);
                                                else if (Input.VariantIndex == 3) InUVs3.Add(Value);
                                                else if (Input.VariantIndex == 7) InUVs7.Add(Value);
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


            //TEST
            List<alien_mtl_texture_reference> refs = Result.ModelsMTL.Materials[Model.MaterialLibraryIndex].TextureReferences.ToList();
            Texture testTex = null;
            for (int i = 0; i < refs.Count; i++)
            {
                if (refs[i].TextureTableIndex != 0 && refs[i].TextureTableIndex != 2) continue;
                testTex = LoadTexture(refs[i].TextureIndex, refs[i].TextureTableIndex);
                if (testTex) break;
            }


            if (InVertices.Count == 0) continue;

            Mesh thisMesh = new Mesh();
            thisMesh.SetVertices(InVertices);
            thisMesh.SetNormals(InNormals);
            thisMesh.SetIndices(InIndices, MeshTopology.Triangles, 0); //0??
            thisMesh.SetTangents(InTangents);
            thisMesh.SetUVs(0, InUVs0);
            thisMesh.SetUVs(1, InUVs1);
            thisMesh.SetUVs(2, InUVs2);
            thisMesh.SetUVs(3, InUVs3);
            thisMesh.SetUVs(7, InUVs7);
            thisMesh.RecalculateBounds();
            thisMesh.RecalculateNormals();
            thisMesh.RecalculateTangents();
            ThisModelPart.AddComponent<MeshFilter>().mesh = thisMesh;
            ThisModelPart.AddComponent<MeshRenderer>().material = new Material(UnityEngine.Shader.Find("Diffuse"));
            ThisModelPart.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", testTex);

        }

        return ThisModel;
    }

    private alien_textures GetTexturesTable(int TableIndex)
    {
        switch (TableIndex)
        {
            case 0:
                return Result.LevelTextures;
            case 2:
                return Result.GlobalTextures;
        }
        return new alien_textures();
    }

    GameObject currentMesh = null;
    int currentMeshIndex = 50;
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (currentMesh != null) Destroy(currentMesh);
            currentMesh = LoadModel(currentMeshIndex);
            //LoadTexture(currentMeshIndex);
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
