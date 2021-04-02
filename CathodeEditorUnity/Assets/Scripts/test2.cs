using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TestProject;
using System.IO;
using System;
using System.Linq;

public class test2 : MonoBehaviour
{
    alien_level Result = new alien_level();
    Texture2D[] LoadedTexturesGlobal;
    Texture2D[] LoadedTexturesLevel;
    GameObjectHolder[] LoadedModels;

    [SerializeField] private bool LOAD_COMMANDS_PAK = false;
    [SerializeField] private string LEVEL_NAME = "BSP_TORRENS";

    void Start()
    {
        string levelPath = @"G:\SteamLibrary\steamapps\common\Alien Isolation\DATA\ENV\PRODUCTION\" + LEVEL_NAME;

        //Parse content
        Result.GlobalTextures = TestProject.File_Handlers.Textures.TexturePAK.Load(levelPath + "/../../GLOBAL/WORLD/GLOBAL_TEXTURES.ALL.PAK", levelPath + "/../../GLOBAL/WORLD/GLOBAL_TEXTURES_HEADERS.ALL.BIN");
        Result.LevelTextures = TestProject.File_Handlers.Textures.TexturePAK.Load(levelPath + "/RENDERABLE/LEVEL_TEXTURES.ALL.PAK", levelPath + "/RENDERABLE/LEVEL_TEXTURE_HEADERS.ALL.BIN");
        Result.ModelsCST = File.ReadAllBytes(levelPath + "/RENDERABLE/LEVEL_MODELS.CST");
        Result.ModelsMTL = TestProject.File_Handlers.Models.ModelsMTL.Load(levelPath + "/RENDERABLE/LEVEL_MODELS.MTL", Result.ModelsCST);
        Result.ModelsBIN = TestProject.File_Handlers.Models.ModelBIN.Load(levelPath + "/RENDERABLE/MODELS_LEVEL.BIN");
        Result.ModelsPAK = TestProject.File_Handlers.Models.ModelPAK.Load(levelPath + "/RENDERABLE/LEVEL_MODELS.PAK");
        Result.RenderableREDS = TestProject.File_Handlers.Misc.RenderableElementsBIN.Load(levelPath + "/WORLD/REDS.BIN");
        Result.ShadersPAK = TestProject.File_Handlers.Shaders.ShadersPAK.Load(levelPath + "/RENDERABLE/LEVEL_SHADERS_DX11.PAK");
        //Result.ShadersBIN = TestProject.File_Handlers.Shaders.ShadersBIN.Load(levelPath + "/RENDERABLE/LEVEL_SHADERS_DX11_BIN.PAK");
        Result.ShadersIDXRemap = TestProject.File_Handlers.Shaders.IDXRemap.Load(levelPath + "/RENDERABLE/LEVEL_SHADERS_DX11_IDX_REMAP.PAK");

        //Load all textures - TODO: flip array and load V2 first? - I suspect V1 is first as A:I loads V1s passively throughout, and then V2s by zone
        LoadedTexturesGlobal = new Texture2D[Result.GlobalTextures.BIN.Header.EntryCount];
        LoadedTexturesLevel = new Texture2D[Result.LevelTextures.BIN.Header.EntryCount];
        bool[] TextureLoadTrackerGlobal = new bool[Result.GlobalTextures.BIN.Header.EntryCount];
        bool[] TextureLoadTrackerLevel = new bool[Result.LevelTextures.BIN.Header.EntryCount];
        for (int i = 0; i < Result.GlobalTextures.PAK.Header.EntryCount; i++)
        {
            int binIndex = Result.GlobalTextures.PAK.Entries[i].BINIndex;
            Texture2D newTex = LoadTexture(i, 2, !TextureLoadTrackerGlobal[binIndex]);
            if (newTex != null) LoadedTexturesGlobal[binIndex] = newTex;
            TextureLoadTrackerGlobal[binIndex] = true;
        }
        for (int i = 0; i < Result.LevelTextures.PAK.Header.EntryCount; i++)
        {
            int binIndex = Result.LevelTextures.PAK.Entries[i].BINIndex;
            Texture2D newTex = LoadTexture(i, 0, !TextureLoadTrackerLevel[binIndex]);
            if (newTex != null) LoadedTexturesLevel[binIndex] = newTex;
            TextureLoadTrackerLevel[binIndex] = true;
        }

        //Load all models
        LoadedModels = new GameObjectHolder[Result.ModelsBIN.Header.ModelCount];
        for (int i = 0; i < Result.ModelsPAK.Models.Count; i++) LoadModel(i);

        //for (int i = 0; i < Result.ModelsBIN.Models.Count; i++)
        //{
        //    GameObject thisBin = new GameObject(Result.ModelsBIN.ModelFilePaths[i]);
        //    SpawnModel(i, thisBin);
        //}
        //return;

        if (!LOAD_COMMANDS_PAK) return;
        //Populate scene with positioned models 
        CommandsLoader newTest = new CommandsLoader();
        newTest.LoadCommandsPAK(levelPath, Result.RenderableREDS.Entries, SpawnModel);
    }

    private void SpawnModel(int binIndex, GameObject parent)
    {
        if (binIndex >= Result.ModelsBIN.Header.ModelCount)
        {
            Debug.LogWarning("binIndex out of range!");
            return;
        }
        if (LoadedModels[binIndex] == null)
        {
            Debug.Log("Attempted to load non-parsed model. Skipping!");
            return;
        }
        GameObject newModelSpawn = new GameObject();
        if (parent != null) newModelSpawn.transform.parent = parent.transform;
        newModelSpawn.transform.localPosition = Vector3.zero;
        newModelSpawn.transform.localRotation = Quaternion.identity;
        newModelSpawn.transform.localScale = LoadedModels[binIndex].LocalScale;
        newModelSpawn.name = LoadedModels[binIndex].Name;
        newModelSpawn.AddComponent<MeshFilter>().sharedMesh = LoadedModels[binIndex].MainMesh;
        newModelSpawn.AddComponent<MeshRenderer>().sharedMaterial = LoadedModels[binIndex].MainMaterial;

        if (!LOAD_COMMANDS_PAK) currentMesh = newModelSpawn;
    }

    private Texture2D LoadTexture(int EntryIndex, int paktype = 0, bool loadV1 = true)
    {
        alien_textures AlienTextures = GetTexturesTable(paktype); 
        if (EntryIndex < 0 || EntryIndex >= AlienTextures.PAK.Header.EntryCount)
        {
            Debug.LogWarning("Asked to load texture at index " + EntryIndex + ", which is out of bounds!");
            return null;
        }

        alien_pak_entry Entry = AlienTextures.PAK.Entries[EntryIndex];
        alien_texture_bin_texture InTexture = AlienTextures.BIN.Textures[Entry.BINIndex];

        Vector2 textureDims;
        int textureLength = 0;
        int mipLevels = 0;

        if (loadV1)
        {
            textureDims = new Vector2(InTexture.Size_V1[0], InTexture.Size_V1[1]);
            textureLength = InTexture.Length_V1;
            mipLevels = InTexture.MipLevelsV1;
        } 
        else
        {
            textureDims = new Vector2(InTexture.Size_V2[0], InTexture.Size_V2[1]);
            textureLength = InTexture.Length_V2;
            mipLevels = InTexture.MipLevelsV2;
        }

        if (textureLength == 0)
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
                format = UnityEngine.TextureFormat.DXT1;
                break;
            case alien_texture_format.Alien_FORMAT_BC2:
                Debug.LogWarning("BC2! NOT LOADED");
                return null;
            case alien_texture_format.Alien_FORMAT_BC5:
                format = UnityEngine.TextureFormat.BC5; //Is this correct?
                break;
            case alien_texture_format.Alien_FORMAT_BC3:
                format = UnityEngine.TextureFormat.DXT5;
                break;
            case alien_texture_format.Alien_FORMAT_BC7:
                format = UnityEngine.TextureFormat.BC7;
                break;
            case alien_texture_format.Alien_FORMAT_R8G8:
                format = UnityEngine.TextureFormat.BC5; // is this correct?
                break;
        }

        Texture2D texture = new Texture2D((int)textureDims[0], (int)textureDims[1], format, mipLevels, true);
        texture.name = AlienTextures.BIN.TextureFilePaths[Entry.BINIndex];
        BinaryReader tempReader = new BinaryReader(new MemoryStream(AlienTextures.PAK.DataStart));
        tempReader.BaseStream.Position = Entry.Offset;
        texture.LoadRawTextureData(tempReader.ReadBytes(textureLength));
        tempReader.Close();
        texture.Apply();
        return texture;
    }

    private void LoadModel(int EntryIndex)
    {
        if (EntryIndex < 0 || EntryIndex >= Result.ModelsPAK.Models.Count)
        {
            Debug.LogWarning("Asked to load model at index " + EntryIndex + ", which is out of bounds!");
            //return new GameObject();
            return;
        }

        alien_pak_model_entry ChunkArray = Result.ModelsPAK.Models[EntryIndex];
        for (int ChunkIndex = 0; ChunkIndex < ChunkArray.Header.ChunkCount; ++ChunkIndex)
        {
            int BINIndex = ChunkArray.ChunkInfos[ChunkIndex].BINIndex;
            alien_model_bin_model_info Model = Result.ModelsBIN.Models[BINIndex];
            //if (Model.BlockSize == 0) continue;

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

            //TODO: implement skeleton lookup for the indexes
            List<Vector4> InBoneIndexes = new List<Vector4>(); //The indexes of 4 bones that affect each vertex
            List<Vector4> InBoneWeights = new List<Vector4>(); //The weights for each bone

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
                                                InBoneIndexes.Add(Value);
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
                                                float Sum = Value.x + Value.y + Value.z + Value.w;
                                                InBoneWeights.Add(Value / Sum);
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

                                case alien_vertex_input_type.AlienVertexInputType_v4u8_NTB:
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
            //thisMesh.SetBoneWeights(InBoneWeights.ToArray());
            thisMesh.RecalculateBounds();
            thisMesh.RecalculateNormals();
            thisMesh.RecalculateTangents();

            GameObjectHolder ThisModelPart = new GameObjectHolder();
            ThisModelPart.LocalScale = new Vector3(Model.ScaleFactor, Model.ScaleFactor, Model.ScaleFactor);
            ThisModelPart.Name = Result.ModelsBIN.ModelFilePaths[BINIndex] + ": " + Result.ModelsBIN.ModelLODPartNames[BINIndex] + " (" + Result.ModelsMTL.MaterialNames[Model.MaterialLibraryIndex] + ")";
            ThisModelPart.MainMesh = thisMesh;
            ThisModelPart.MainMaterial = MakeMaterial(Model.MaterialLibraryIndex);
            LoadedModels[BINIndex] = ThisModelPart;
        }
    }

    public Material MakeMaterial(int MTLIndex)
    {
        alien_mtl_material InMaterial = Result.ModelsMTL.Materials[MTLIndex];
        int RemappedIndex = Result.ShadersIDXRemap.Datas[InMaterial.UberShaderIndex].Index;
        alien_shader_pak_shader Shader = Result.ShadersPAK.Shaders[RemappedIndex];

        List<alien_slot_ids> SlotOffsets = new List<alien_slot_ids>();
        alien_shader_category ShaderCategory = (alien_shader_category)Shader.Header2.ShaderCategory;

        switch (ShaderCategory)
        {
            case alien_shader_category.AlienShaderCategory_Particle:
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.COLOR_RAMP_MAP);
                break;

            case alien_shader_category.AlienShaderCategory_Ribbon:
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.COLOR_RAMP_MAP);
                break;

            case alien_shader_category.AlienShaderCategory_Environment:
                SlotOffsets.Add(alien_slot_ids.OPACITY);
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.SPECULAR_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_SPECULAR_MAP);
                SlotOffsets.Add(alien_slot_ids.ENVIRONMENT_MAP);
                SlotOffsets.Add(alien_slot_ids.OCCLUSION);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.FRESNEL_LUT);
                SlotOffsets.Add(alien_slot_ids.PARALLAX_MAP);
                SlotOffsets.Add(alien_slot_ids.OPACITY_NOISE_MAP);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.DIRT_MAP);
                SlotOffsets.Add(alien_slot_ids.WETNESS_NOISE);
                break;

            case alien_shader_category.AlienShaderCategory_ShadowCaster:
                return new Material(UnityEngine.Shader.Find("Diffuse")); //todo-mattf: flag this in a colour

            case alien_shader_category.AlienShaderCategory_DecalEnvironment:
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.PARALLAX_MAP);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.ALPHA_THRESHOLD);
                break;

            case alien_shader_category.AlienShaderCategory_Character:
                SlotOffsets.Add(alien_slot_ids.DIRT_MAP);
                SlotOffsets.Add(alien_slot_ids.OPACITY_NOISE_MAP);
                SlotOffsets.Add(alien_slot_ids.OPACITY);
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.SPECULAR_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_SPECULAR_MAP);
                SlotOffsets.Add(alien_slot_ids.ENVIRONMENT_MAP);
                SlotOffsets.Add(alien_slot_ids.OCCLUSION);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.IRRADIANCE_MAP);
                break;

            // TODO: The folowing should use their own specific structs and shaders.

            case alien_shader_category.AlienShaderCategory_Skin:
                SlotOffsets.Add(alien_slot_ids.CONVOLVED_DIFFUSE);
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.WRINKLE_MASK);
                SlotOffsets.Add(alien_slot_ids.WRINKLE_NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.SPECULAR_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_SPECULAR_MAP);
                SlotOffsets.Add(alien_slot_ids.ENVIRONMENT_MAP);
                SlotOffsets.Add(alien_slot_ids.IRRADIANCE_MAP);
                SlotOffsets.Add(alien_slot_ids.DIRT_MAP);
                SlotOffsets.Add(alien_slot_ids.OPACITY_NOISE_MAP);
                break;

            case alien_shader_category.AlienShaderCategory_Hair:
                SlotOffsets.Add(alien_slot_ids.FLOW_MAP);
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.IRRADIANCE_MAP);
                SlotOffsets.Add(alien_slot_ids.SPECULAR_MAP);
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                break;

            case alien_shader_category.AlienShaderCategory_Eye:
                SlotOffsets.Add(alien_slot_ids.CONVOLVED_DIFFUSE);
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);//IrisMap
                SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);//VeinsMap
                SlotOffsets.Add(alien_slot_ids.SCATTER_MAP);
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.ENVIRONMENT_MAP);
                SlotOffsets.Add(alien_slot_ids.IRRADIANCE_MAP);
                break;

            case alien_shader_category.AlienShaderCategory_SkinOcclusion:
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                break;

            case alien_shader_category.AlienShaderCategory_Deferred:
                // NOTE: This material doesn't have textures.
                // TODO: Not sure what to do here. Why would objects have a "deferred" material?
                //  From looking at the asset files, it seems like this is a deferred point light.
                return new Material(UnityEngine.Shader.Find("Diffuse")); //todo-mattf: flag this in a colour

            case alien_shader_category.AlienShaderCategory_Decal:
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.EMISSIVE);
                SlotOffsets.Add(alien_slot_ids.SPECULAR_MAP);
                SlotOffsets.Add(alien_slot_ids.PARALLAX_MAP);
                SlotOffsets.Add(alien_slot_ids.BURN_THROUGH);
                SlotOffsets.Add(alien_slot_ids.LIQUIFY);
                SlotOffsets.Add(alien_slot_ids.ALPHA_THRESHOLD);
                SlotOffsets.Add(alien_slot_ids.LIQUIFY2);
                SlotOffsets.Add(alien_slot_ids.ENVIRONMENT_MAP);
                SlotOffsets.Add(alien_slot_ids.COLOR_RAMP);
                break;

            case alien_shader_category.AlienShaderCategory_FogPlane:
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);
                // TODO: Should be 'DiffuseMapStatic' - but I am not using that yet.  In order to keep the light cones
                //  visually appealing and not slabs of solid white, I am using normal diffuse for now.
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP_STATIC);
                break;

            case alien_shader_category.AlienShaderCategory_Debug:
                // NOTE: Debug materials have no textures.
                // TODO: Maybe we shouldn't be rendering those, or maybe separate them.
                return new Material(UnityEngine.Shader.Find("Diffuse")); //todo-mattf: flag this in a colour

            case alien_shader_category.AlienShaderCategory_OcclusionCulling:
                // NOTE: No textures here.
                return new Material(UnityEngine.Shader.Find("Diffuse")); //todo-mattf: flag this in a colour

            case alien_shader_category.AlienShaderCategory_Refraction:
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.ALPHA_MASK);
                SlotOffsets.Add(alien_slot_ids.FLOW_MAP);
                SlotOffsets.Add(alien_slot_ids.ALPHA_THRESHOLD);
                //Material->Material.BaseColor = { };
                break;

            case alien_shader_category.AlienShaderCategory_NonInteractiveWater:
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.ALPHA_MASK);
                SlotOffsets.Add(alien_slot_ids.FLOW_MAP);
                break;

            case alien_shader_category.AlienShaderCategory_LowLODCharacter:
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.SPECULAR_MAP);
                SlotOffsets.Add(alien_slot_ids.LOW_LOD_CHARACTER_MASK);
                SlotOffsets.Add(alien_slot_ids.IRRADIANCE_MAP);
                SlotOffsets.Add(alien_slot_ids.ENVIRONMENT_MAP);
                break;

            case alien_shader_category.AlienShaderCategory_LightDecal:
                SlotOffsets.Add(alien_slot_ids.EMISSIVE);
                break;

            case alien_shader_category.AlienShaderCategory_SpaceSuitVisor:
                SlotOffsets.Add(alien_slot_ids.ENVIRONMENT_MAP);
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.MASKING_MAP);
                SlotOffsets.Add(alien_slot_ids.FACE_MAP);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.UNSCALED_DIRT_MAP);
                SlotOffsets.Add(alien_slot_ids.DIRT_MAP);
                break;

            case alien_shader_category.AlienShaderCategory_Planet:
                SlotOffsets.Add(alien_slot_ids.ATMOSPHERE_MAP);
                SlotOffsets.Add(alien_slot_ids.DETAIL_MAP);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.FLOW_MAP);
                break;

            case alien_shader_category.AlienShaderCategory_LightMapEnvironment:
                SlotOffsets.Add(alien_slot_ids.LIGHT_MAP);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.DIRT_MAP);
                SlotOffsets.Add(alien_slot_ids.OPACITY_NOISE_MAP);
                SlotOffsets.Add(alien_slot_ids.OPACITY);
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.SPECULAR_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_SPECULAR_MAP);
                SlotOffsets.Add(alien_slot_ids.ENVIRONMENT_MAP);
                SlotOffsets.Add(alien_slot_ids.NONE); //Occlusion?
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NONE);
                break;

            case alien_shader_category.AlienShaderCategory_Terrain:
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.SPECULAR_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_SPECULAR_MAP);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.OPACITY_NOISE_MAP);
                SlotOffsets.Add(alien_slot_ids.ENVIRONMENT_MAP);
                SlotOffsets.Add(alien_slot_ids.NONE);
                break;

            // TODO: Remove this after we handle all possible shader categories.
            default:
                return new Material(UnityEngine.Shader.Find("Diffuse")); //todo-mattf: flag this in a colour
        }

        List<Texture> availableTextures = new List<Texture>();
        for (int SlotIndex = 0; SlotIndex < Shader.Header.TextureLinkCount; ++SlotIndex)
        {
            int PairIndex = Shader.TextureLinks[SlotIndex];
            // NOTE: PairIndex == 255 means no index.
            if (PairIndex < Result.ModelsMTL.TextureReferenceCounts[MTLIndex])
            {
                alien_mtl_texture_reference Pair = InMaterial.TextureReferences[PairIndex];
                if (Pair.TextureTableIndex == 0) availableTextures.Add(LoadedTexturesLevel[Pair.TextureIndex]);
                else if (Pair.TextureTableIndex == 2) availableTextures.Add(LoadedTexturesGlobal[Pair.TextureIndex]);
                else availableTextures.Add(null);
            }
            else
            {
                availableTextures.Add(null);
            }
        }

        Material ToReturn = new Material(UnityEngine.Shader.Find("Standard")); // (Specular setup)
        for (int i = 0; i < SlotOffsets.Count; i++)
        {
            if (i >= availableTextures.Count) continue;
            switch (SlotOffsets[i])
            {
                case alien_slot_ids.DIFFUSE_MAP:
                    ToReturn.SetTexture("_MainTex", availableTextures[i]);
                    break;
                case alien_slot_ids.DETAIL_MAP:
                    ToReturn.EnableKeyword("_DETAIL_MULX2");
                    ToReturn.SetTexture("_DetailMask", availableTextures[i]);
                    break;
                case alien_slot_ids.EMISSIVE:
                    ToReturn.EnableKeyword("_EMISSION");
                    ToReturn.SetTexture("_EmissionMap", availableTextures[i]);
                    break;
                case alien_slot_ids.PARALLAX_MAP:
                    ToReturn.EnableKeyword("_PARALLAXMAP");
                    ToReturn.SetTexture("_ParallaxMap", availableTextures[i]);
                    break;
                case alien_slot_ids.OCCLUSION:
                    ToReturn.SetTexture("_OcclusionMap", availableTextures[i]);
                    break;
                case alien_slot_ids.SPECULAR_MAP:
                    ToReturn.EnableKeyword("_METALLICGLOSSMAP");
                    ToReturn.SetTexture("_MetallicGlossMap", availableTextures[i]); //TODO _SPECGLOSSMAP?
                    ToReturn.SetFloat("_Glossiness", 0.1f); //TODO: get this from game
                    ToReturn.SetFloat("_GlossMapScale", 0.1f); //TODO: get this from game
                    break;
                case alien_slot_ids.NORMAL_MAP:
                    ToReturn.EnableKeyword("_NORMALMAP");
                    ToReturn.SetTexture("_BumpMap", availableTextures[i]);
                    break;
            }
            //_ALPHATEST_ON if transparent?
        }
        //ToReturn.color = new Color(InMaterial.Co)

        return ToReturn;
    }

    public alien_textures GetTexturesTable(int TableIndex)
    {
        if (TableIndex == 0) return Result.LevelTextures;
        if (TableIndex == 2) return Result.GlobalTextures;
        throw new Exception("Texture bank can only be 0 or 2");
    }

    GameObject currentMesh = null;
    int currentMeshIndex = 1255;
    void Update()
    {
        return;
        if (LOAD_COMMANDS_PAK) return;
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (currentMesh != null) Destroy(currentMesh);
            SpawnModel(currentMeshIndex, null);
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

    enum alien_slot_ids
    {
        NONE = -1,
        DIFFUSE_MAP,
        COLOR_RAMP_MAP,
        SECONDARY_DIFFUSE_MAP,
        DIFFUSE_MAP_STATIC,
        OPACITY,
        NORMAL_MAP,
        SECONDARY_NORMAL_MAP,
        SPECULAR_MAP,
        SECONDARY_SPECULAR_MAP,
        ENVIRONMENT_MAP,
        OCCLUSION,
        FRESNEL_LUT,
        PARALLAX_MAP,
        OPACITY_NOISE_MAP,
        DIRT_MAP,
        WETNESS_NOISE,
        ALPHA_THRESHOLD,
        IRRADIANCE_MAP,
        CONVOLVED_DIFFUSE,
        WRINKLE_MASK,
        WRINKLE_NORMAL_MAP,
        SCATTER_MAP,
        EMISSIVE,
        BURN_THROUGH,
        LIQUIFY,
        LIQUIFY2,
        COLOR_RAMP,
        FLOW_MAP,
        ALPHA_MASK,
        LOW_LOD_CHARACTER_MASK,
        UNSCALED_DIRT_MAP,
        FACE_MAP,
        MASKING_MAP,
        ATMOSPHERE_MAP,
        DETAIL_MAP,
        LIGHT_MAP
    }
}

//Temp wrapper for GameObject while we just want it in memory
public class GameObjectHolder
{
    public Vector3 LocalScale;
    public string Name;
    public Mesh MainMesh; //TODO: should this be contained in a globally referenced array?
    public Material MainMaterial; //TODO: should this be global?
}