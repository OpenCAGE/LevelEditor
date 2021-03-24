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

    private void LoadTexture(int EntryIndex)
    {
        alien_textures AlienTextures = Result.LevelTextures; //todo; pass as param
        if (EntryIndex < 0 || EntryIndex >= AlienTextures.PAK.Header.EntryCount)
        {
            Debug.LogWarning("Asked to load texture at index " + EntryIndex + ", which is out of bounds!");
            return;
        }

        alien_pak_entry Entry = AlienTextures.PAK.Entries[EntryIndex];
        alien_texture_bin_texture InTexture = AlienTextures.BIN.Textures[Entry.BINIndex];

        //ASSUMES V2!!

        if (InTexture.Length_V2 == 0) return;

        Debug.Log(InTexture.Size_V2[0]);
        Debug.Log(InTexture.Size_V2[1]);
        Debug.Log(InTexture.Length_V2);

        Texture2D texture = new Texture2D(InTexture.Size_V2[0], InTexture.Size_V2[1], UnityEngine.TextureFormat.BC7, false);
        texture.name = AlienTextures.BIN.TextureFilePaths[Entry.BINIndex];
        //texture.mipmapCount = (int)InTexture.MipLevelsV2;
        BinaryReader tempReader = new BinaryReader(new MemoryStream(AlienTextures.PAK.DataStart));
        tempReader.BaseStream.Position = Entry.Offset;
        texture.LoadRawTextureData(tempReader.ReadBytes(InTexture.Length_V2));
        texture.Apply();
        tempReader.Close();

        Debug.Log(texture.format);
        Debug.Log((alien_texture_format)InTexture.Format);

        if (uiSprite != null) uiSprite.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0, 0));
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

            /*
            alien_mtl_material InMaterial = Result.ModelsMTL.Materials[Model.MaterialLibraryIndex];
            Debug.Log(InMaterial.UberShaderIndex);
            Debug.Log(Result.ShadersIDXRemap.Datas.Count);
            int RemappedIndex = Result.ShadersIDXRemap.Datas[InMaterial.UberShaderIndex].Index;
            alien_shader_pak_shader Shader = Result.ShadersPAK.Shaders[RemappedIndex];

            int TextureReferenceCount = Result.ModelsMTL.TextureReferenceCounts[Model.MaterialLibraryIndex];
            int TableIndex = 11;
            byte[] PairFromSlotTable = Shader.Tables[TableIndex];
            int PairFromSlotTableSize = Shader.Header.TableEntryCounts[TableIndex];
            for (int SlotIndex = 0; SlotIndex < PairFromSlotTableSize; ++SlotIndex)
            {
                int PairIndex = PairFromSlotTable[SlotIndex];
                // NOTE: PairIndex == 255 means no index.
                if (PairIndex < TextureReferenceCount)
                {
                    alien_mtl_texture_reference Pair = InMaterial.TextureReferences[PairIndex];
                    alien_textures Textures = GetTexturesTable(Pair.TextureTableIndex);
                    int TextureIndex = Pair.TextureIndex;
                    alien_pak_entry Entry = Textures.PAK.Entries[TextureIndex];
                    alien_texture_bin_texture InTexture = Textures.BIN.Textures[Entry.BINIndex];
                    string TextureFilePath = Textures.BIN.TextureFilePaths[Entry.BINIndex];
                    Debug.Log(TextureFilePath);
                    alien_texture_format AlienFormat = (alien_texture_format)InTexture.Format;
                }
            }
            */

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
            ThisModelPart.AddComponent<MeshRenderer>().material = new Material(UnityEngine.Shader.Find("Diffuse"));

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
    int currentMeshIndex = 850;
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (currentMesh != null) Destroy(currentMesh);
            //currentMesh = LoadModel(currentMeshIndex);
            LoadTexture(currentMeshIndex);
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
