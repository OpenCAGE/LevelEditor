using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CATHODE;
using System.IO;
using System;
using System.Linq;
using UnityEditor;

public class AlienLevelLoader : MonoBehaviour
{
    alien_level Result = null;
    alien_textures GlobalTextures;

    AlienTexture[] LoadedTexturesGlobal;
    AlienTexture[] LoadedTexturesLevel;
    GameObjectHolder[] LoadedModels;
    Material[] LoadedMaterials;

    [SerializeField] private bool LOAD_COMMANDS_PAK = false;
    [SerializeField] private string LEVEL_NAME = "BSP_TORRENS";

    [SerializeField] UnityEngine.UI.Text mvrindex1;
    public void LoadMvrIndex()
    {
        List<CATHODE.Models.alien_mvr_entry> debugentries = new List<CATHODE.Models.alien_mvr_entry>();
        foreach (string num in mvrindex1.text.Split(','))
        {
            debugentries.Add(Result.ModelsMVR.GetEntry(Convert.ToInt32(num)));
        }
        string breakhere = "";
    }

    [SerializeField] UnityEngine.UI.Text mvrindex2;
    public void ResetMvrNodeId()
    {
        CATHODE.Models.alien_mvr_entry mvr = Result.ModelsMVR.GetEntry(Convert.ToInt32(mvrindex2.text));
        mvr.NodeID = 0;
        Result.ModelsMVR.SetEntry(Convert.ToInt32(mvrindex2.text), mvr);
        Result.ModelsMVR.Save();
    }

    void Start()
    {
        if (SharedVals.instance.LevelName != "") LEVEL_NAME = SharedVals.instance.LevelName;
#if UNITY_EDITOR
        string pathToEnv = @"G:\SteamLibrary\steamapps\common\Alien Isolation\DATA\ENV";
#else
        string pathToEnv = "DATA/ENV";
#endif

        //Load global assets
        GlobalTextures = CATHODE.Textures.TexturePAK.Load(pathToEnv + "/GLOBAL/WORLD/GLOBAL_TEXTURES.ALL.PAK", pathToEnv + "/GLOBAL/WORLD/GLOBAL_TEXTURES_HEADERS.ALL.BIN");
        //alien_pak2 GlobalAnimations;
        //alien_anim_string_db GlobalAnimationsStrings;

        //Load level assets
        Result = CATHODE.AlienLevel.Load(LEVEL_NAME, pathToEnv);

        //Load all textures - TODO: flip array and load V2 first? - I suspect V1 is first as A:I loads V1s passively throughout, and then V2s by zone
        LoadedTexturesGlobal = new AlienTexture[GlobalTextures.BIN.Header.EntryCount];
        LoadedTexturesLevel = new AlienTexture[Result.LevelTextures.BIN.Header.EntryCount];
        bool[] TextureLoadTrackerGlobal = new bool[GlobalTextures.BIN.Header.EntryCount];
        bool[] TextureLoadTrackerLevel = new bool[Result.LevelTextures.BIN.Header.EntryCount];
        for (int i = 0; i < GlobalTextures.PAK.Header.EntryCount; i++)
        {
            int binIndex = GlobalTextures.PAK.Entries[i].BINIndex;
            LoadedTexturesGlobal[binIndex] = LoadTexture(i, 2, !TextureLoadTrackerGlobal[binIndex]);
            TextureLoadTrackerGlobal[binIndex] = true;
        }
        for (int i = 0; i < Result.LevelTextures.PAK.Header.EntryCount; i++)
        {
            int binIndex = Result.LevelTextures.PAK.Entries[i].BINIndex;
            LoadedTexturesLevel[binIndex] = LoadTexture(i, 0, !TextureLoadTrackerLevel[binIndex]);
            TextureLoadTrackerLevel[binIndex] = true;
        }

        //Load all materials
        LoadedMaterials = new Material[Result.ModelsMTL.Header.MaterialCount];
        for (int i = 0; i < Result.ModelsMTL.Header.MaterialCount; i++) LoadMaterial(i);

        //Load all models
        LoadedModels = new GameObjectHolder[Result.ModelsBIN.Header.ModelCount];
        for (int i = 0; i < Result.ModelsPAK.Models.Count; i++) LoadModel(i);

        //Populate the level with "movers"
        levelParent = new GameObject(LEVEL_NAME);
        for (int i = 0; i < Result.ModelsMVR.Entries.Count; i++)
        {
            GameObject thisParent = new GameObject(i + "/" + Result.ModelsMVR.Entries[i].REDSIndex + "/" + Result.ModelsMVR.Entries[i].ModelCount);
            Matrix4x4 m = Result.ModelsMVR.Entries[i].Transform;
            thisParent.transform.position = m.GetColumn(3);
            thisParent.transform.rotation = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
            thisParent.transform.localScale = new Vector3(m.GetColumn(0).magnitude, m.GetColumn(1).magnitude, m.GetColumn(2).magnitude);
            thisParent.transform.parent = levelParent.transform;
            for (int x = 0; x < Result.ModelsMVR.Entries[i].ModelCount; x++)
            {
                CATHODE.Misc.alien_reds_entry RenderableElement = Result.RenderableREDS.Entries[(int)Result.ModelsMVR.Entries[i].REDSIndex + x];
                SpawnModel(RenderableElement.ModelIndex, RenderableElement.MaterialLibraryIndex, thisParent);
            }
        }

        //Pull content from COMMANDS
        //CommandsLoader cmdLoader = gameObject.AddComponent<CommandsLoader>();
        //StartCoroutine(cmdLoader.LoadCommandsPAK(levelPath));
    }
    
    //first saving attempt
    GameObject levelParent;
    public void SaveLevel()
    {
        for (int i = 0; i < levelParent.transform.childCount; i++)
        {
            GameObject mvrEntry = levelParent.transform.GetChild(i).gameObject;
            if (mvrEntry.name.Split('/')[0] != i.ToString()) Debug.LogWarning("Something wrong!");

            CATHODE.Models.alien_mvr_entry thisEntry = Result.ModelsMVR.GetEntry(i);
            thisEntry.Transform = mvrEntry.transform.localToWorldMatrix;
            Result.ModelsMVR.SetEntry(i, thisEntry);
        }

        Result.ModelsMVR.Save();
    }

    private void SpawnModel(int binIndex, int mtlIndex, GameObject parent)
    {
        if (binIndex >= Result.ModelsBIN.Header.ModelCount)
        {
            Debug.LogWarning("binIndex out of range!");
            return;
        }
        if (LoadedModels[binIndex] == null)
        {
            Debug.Log("Attempted to load non-parsed model (" + binIndex + ", " + Result.ModelsBIN.ModelFilePaths[binIndex] + "). Skipping!");
            return;
        }
        GameObject newModelSpawn = new GameObject();
        if (parent != null) newModelSpawn.transform.parent = parent.transform;
        newModelSpawn.transform.localPosition = Vector3.zero;
        newModelSpawn.transform.localRotation = Quaternion.identity;
        newModelSpawn.transform.localScale = LoadedModels[binIndex].LocalScale;
        newModelSpawn.name = LoadedModels[binIndex].Name;
        newModelSpawn.AddComponent<MeshFilter>().sharedMesh = LoadedModels[binIndex].MainMesh;
        newModelSpawn.AddComponent<MeshRenderer>().sharedMaterial = LoadedMaterials[(mtlIndex == -1) ? LoadedModels[binIndex].DefaultMaterial : mtlIndex];

        //todo apply mvr colour scale here
    }

    private AlienTexture LoadTexture(int EntryIndex, int paktype = 0, bool loadV1 = true)
    {
        AlienTexture toReturn = new AlienTexture();

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
            return toReturn;
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
                return toReturn;
            case alien_texture_format.Alien_FORMAT_R8:
                format = UnityEngine.TextureFormat.R8;
                break;
            case alien_texture_format.Alien_FORMAT_BC1:
                format = UnityEngine.TextureFormat.DXT1;
                break;
            case alien_texture_format.Alien_FORMAT_BC2:
                Debug.LogWarning("BC2! NOT LOADED");
                return toReturn;
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

        BinaryReader tempReader = new BinaryReader(new MemoryStream(AlienTextures.PAK.DataStart));
        tempReader.BaseStream.Position = Entry.Offset;

        if (InTexture.Type == 7)
        {
            Cubemap cubemapTex = new Cubemap((int)textureDims.x, format, true);
            cubemapTex.name = AlienTextures.BIN.TextureFilePaths[Entry.BINIndex];
            cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.PositiveX);
            cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.NegativeX);
            cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.PositiveY);
            cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.NegativeY);
            cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.PositiveZ);
            cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.NegativeZ);
            cubemapTex.Apply();
            toReturn.cubemap = cubemapTex;
            //AssetDatabase.CreateAsset(cubemapTex, "Assets/Cubemaps/" + Path.GetFileNameWithoutExtension(cubemapTex.name) + ".cubemap");
        }
        else
        {
            Texture2D texture = new Texture2D((int)textureDims[0], (int)textureDims[1], format, mipLevels, true);
            texture.name = AlienTextures.BIN.TextureFilePaths[Entry.BINIndex];
            texture.LoadRawTextureData(tempReader.ReadBytes(textureLength));
            texture.Apply();
            toReturn.texture = texture;
        }

        tempReader.Close();
        return toReturn;
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
                CATHODE.Utilities.Align(ref Stream, 16);
            }

            if (InVertices.Count == 0) continue;

            Mesh thisMesh = new Mesh();
            thisMesh.name = Result.ModelsBIN.ModelFilePaths[BINIndex] + ": " + Result.ModelsBIN.ModelLODPartNames[BINIndex];
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
            ThisModelPart.DefaultMaterial = Model.MaterialLibraryIndex;
            LoadedModels[BINIndex] = ThisModelPart;
        }
    }

    private MaterialPropertyIndex GetMaterialPropertyIndex(int MaterialIndex)
    {
        alien_mtl_material InMaterial = Result.ModelsMTL.Materials[MaterialIndex];
        alien_shader_pak_shader Shader = Result.ShadersPAK.Shaders[InMaterial.UberShaderIndex];

        MaterialPropertyIndex toReturn = new MaterialPropertyIndex();

        switch ((alien_shader_category)Shader.Header2.ShaderCategory)
        {
            case alien_shader_category.AlienShaderCategory_Environment:
                toReturn.OpacityUVMultiplierIndex = 5;
                toReturn.DiffuseUVMultiplierIndex = 6;
                toReturn.DiffuseIndex = 7;
                toReturn.SecondaryDiffuseUVMultiplierIndex = 8;
                toReturn.NormalUVMultiplierIndex = 10;
                toReturn.SecondaryNormalUVMultiplierIndex = 12;
                toReturn.SpecularFactorIndex = 14;
                toReturn.SpecularUVMultiplierIndex = 15;
                toReturn.SecondarySpecularUVMultiplierIndex = 18;
                toReturn.OcclusionUVMultiplierIndex = 27;
                toReturn.OpacityNoiseUVMultiplierIndex = 39;
                toReturn.DirtMapUVMultiplierIndex = 48;
                break;

            case alien_shader_category.AlienShaderCategory_Character:
                toReturn.OpacityNoiseUVMultiplierIndex = 12;
                toReturn.DiffuseUVMultiplierIndex = 15;
                toReturn.DiffuseIndex = 16;
                toReturn.SecondaryDiffuseUVMultiplierIndex = 17;
                //toReturn.SecondaryDiffuseIndex = 18;
                toReturn.NormalUVMultiplierIndex = 19;
                toReturn.SecondaryNormalUVMultiplierIndex = 21;
                toReturn.SpecularUVMultiplierIndex = 24;
                toReturn.SpecularFactorIndex = 25;
                break;

            case alien_shader_category.AlienShaderCategory_Skin:
                toReturn.DiffuseUVMultiplierIndex = 4;
                toReturn.DiffuseIndex = 5;
                toReturn.NormalUVMultiplierIndex = 8;
                toReturn.NormalUVMultiplierOfMultiplierIndex = 10;
                toReturn.SecondaryNormalUVMultiplierIndex = 11;
                break;

            case alien_shader_category.AlienShaderCategory_Hair:
                toReturn.DiffuseIndex = 2;
                break;

            case alien_shader_category.AlienShaderCategory_Eye:
                toReturn.DiffuseUVAdderIndex = 3;
                // TODO: These three determine the iris color. They map to rgb channels of the iris map.
                //  I am using the middle color for now for everything but we should not do that.
                //toReturn.ColorIndex = 7;
                toReturn.DiffuseIndex = 8;
                //toReturn.ColorIndex = 9;
                toReturn.DiffuseUVMultiplierIndex = 10;

                // TODO: This info is available in 'Shader->TextureEntries[CorrectIndex].TextureAddressMode'.
                toReturn.DiffuseSamplerIndex = 0;
                break;

            case alien_shader_category.AlienShaderCategory_Decal:
                //toReturn.ColorIndex = 3;
                //Material->BaseColor = {};
                break;

            case alien_shader_category.AlienShaderCategory_FogPlane:
                //toReturn.DiffuseIndex = 8;
                //Material.BaseColor = { };
                break;

            case alien_shader_category.AlienShaderCategory_Terrain:
                toReturn.DiffuseIndex = 4;
                break;

            case alien_shader_category.AlienShaderCategory_LightMapEnvironment:
                toReturn.DiffuseIndex = 12;
                break;

            case alien_shader_category.AlienShaderCategory_Refraction:
                toReturn.DiffuseUVMultiplierIndex = 3;
                break;
        }

        return toReturn;
    }

    public void LoadMaterial(int MTLIndex)
    {
        alien_mtl_material InMaterial = Result.ModelsMTL.Materials[MTLIndex];
        int RemappedIndex = Result.ShadersIDXRemap.Datas[InMaterial.UberShaderIndex].Index;
        alien_shader_pak_shader Shader = Result.ShadersPAK.Shaders[RemappedIndex];

        Material toReturn = new Material(UnityEngine.Shader.Find("Standard"));
        toReturn.name = Result.ModelsMTL.MaterialNames[MTLIndex];

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

            //Unsupported shader slot types - draw transparent for now
            default:
                toReturn.name = "UNRESOLVED: " + ShaderCategory.ToString();
                toReturn.color = new Color(0,0,0,0);
                toReturn.SetFloat("_Mode", 1.0f);
                toReturn.EnableKeyword("_ALPHATEST_ON");
                LoadedMaterials[MTLIndex] = toReturn;
                return;
        }

        List<Texture> availableTextures = new List<Texture>();
        for (int SlotIndex = 0; SlotIndex < Shader.Header.TextureLinkCount; ++SlotIndex)
        {
            int PairIndex = Shader.TextureLinks[SlotIndex];
            // NOTE: PairIndex == 255 means no index.
            if (PairIndex < Result.ModelsMTL.TextureReferenceCounts[MTLIndex])
            {
                alien_mtl_texture_reference Pair = InMaterial.TextureReferences[PairIndex];
                if (Pair.TextureTableIndex == 0) availableTextures.Add(LoadedTexturesLevel[Pair.TextureIndex].texture);
                else if (Pair.TextureTableIndex == 2) availableTextures.Add(LoadedTexturesGlobal[Pair.TextureIndex].texture);
                else availableTextures.Add(null);
            }
            else
            {
                availableTextures.Add(null);
            }
        }

        //Apply materials
        for (int i = 0; i < SlotOffsets.Count; i++)
        {
            if (i >= availableTextures.Count) continue;
            switch (SlotOffsets[i])
            {
                case alien_slot_ids.DIFFUSE_MAP:
                    toReturn.SetTexture("_MainTex", availableTextures[i]);
                    break;
                case alien_slot_ids.DETAIL_MAP:
                    toReturn.EnableKeyword("_DETAIL_MULX2");
                    toReturn.SetTexture("_DetailMask", availableTextures[i]);
                    break;
                case alien_slot_ids.EMISSIVE:
                    toReturn.EnableKeyword("_EMISSION");
                    toReturn.SetTexture("_EmissionMap", availableTextures[i]);
                    break;
                case alien_slot_ids.PARALLAX_MAP:
                    toReturn.EnableKeyword("_PARALLAXMAP");
                    toReturn.SetTexture("_ParallaxMap", availableTextures[i]);
                    break;
                case alien_slot_ids.OCCLUSION:
                    toReturn.SetTexture("_OcclusionMap", availableTextures[i]);
                    break;
                case alien_slot_ids.SPECULAR_MAP:
                    toReturn.EnableKeyword("_METALLICGLOSSMAP");
                    toReturn.SetTexture("_MetallicGlossMap", availableTextures[i]); //TODO _SPECGLOSSMAP?
                    toReturn.SetFloat("_Glossiness", 0.0f);
                    toReturn.SetFloat("_GlossMapScale", 0.0f);
                    break;
                case alien_slot_ids.NORMAL_MAP:
                    toReturn.EnableKeyword("_NORMALMAP");
                    toReturn.SetTexture("_BumpMap", availableTextures[i]);
                    break;
            }
        }

        //Apply properties
        MaterialPropertyIndex cstIndex = GetMaterialPropertyIndex(MTLIndex);
        BinaryReader cstReader = new BinaryReader(new MemoryStream(Result.ModelsCST));
        int baseOffset = Result.ModelsMTL.Header.CSTOffsets[2] + (InMaterial.CSTOffsets[2] * 4);
        if (CSTIndexValid(cstIndex.DiffuseIndex, ref Shader))
        {
            Vector4 colour = LoadFromCST<Vector4>(ref cstReader, baseOffset + (Shader.CSTLinks[2][cstIndex.DiffuseIndex] * 4));
            toReturn.SetColor("_Color", colour);
            if (colour.w != 1)
            {
                toReturn.SetFloat("_Mode", 1.0f);
                toReturn.EnableKeyword("_ALPHATEST_ON");
            }
        }
        if (CSTIndexValid(cstIndex.DiffuseUVMultiplierIndex, ref Shader))
        {
            float offset = LoadFromCST<float>(ref cstReader, baseOffset + (Shader.CSTLinks[2][cstIndex.DiffuseUVMultiplierIndex] * 4));
            toReturn.SetTextureScale("_MainTex", new Vector2(offset, offset));
        }
        if (CSTIndexValid(cstIndex.DiffuseUVAdderIndex, ref Shader))
        {
            float offset = LoadFromCST<float>(ref cstReader, baseOffset + (Shader.CSTLinks[2][cstIndex.DiffuseUVAdderIndex] * 4));
            toReturn.SetTextureOffset("_MainTex", new Vector2(offset, offset));
        }
        if (CSTIndexValid(cstIndex.NormalUVMultiplierIndex, ref Shader))
        {
            float offset = LoadFromCST<float>(ref cstReader, baseOffset + (Shader.CSTLinks[2][cstIndex.NormalUVMultiplierIndex] * 4));
            toReturn.SetTextureScale("_BumpMap", new Vector2(offset, offset));
            toReturn.SetFloat("_BumpScale", offset);
        }
        if (CSTIndexValid(cstIndex.OcclusionUVMultiplierIndex, ref Shader))
        {
            float offset = LoadFromCST<float>(ref cstReader, baseOffset + (Shader.CSTLinks[2][cstIndex.OcclusionUVMultiplierIndex] * 4));
            toReturn.SetTextureScale("_OcclusionMap", new Vector2(offset, offset));
        }
        if (CSTIndexValid(cstIndex.SpecularUVMultiplierIndex, ref Shader))
        {
            float offset = LoadFromCST<float>(ref cstReader, baseOffset + (Shader.CSTLinks[2][cstIndex.SpecularUVMultiplierIndex] * 4));
            toReturn.SetTextureScale("_MetallicGlossMap", new Vector2(offset, offset));
            toReturn.SetFloat("_GlossMapScale", offset);
        }
        if (CSTIndexValid(cstIndex.SpecularFactorIndex, ref Shader))
        {
            float spec = LoadFromCST<float>(ref cstReader, baseOffset + (Shader.CSTLinks[2][cstIndex.SpecularFactorIndex] * 4));
            toReturn.SetFloat("_Glossiness", spec);
            toReturn.SetFloat("_GlossMapScale", spec);
        }
        cstReader.Close();

        LoadedMaterials[MTLIndex] = toReturn;
    }
    private T LoadFromCST<T>(ref BinaryReader cstReader, int offset)
    {
        cstReader.BaseStream.Position = offset;
        return Utilities.Consume<T>(ref cstReader);
    }
    private bool CSTIndexValid(int i, ref alien_shader_pak_shader Shader)
    {
        return i >= 0 && i < Shader.Header.CSTCounts[2] && (int)Shader.CSTLinks[2][i] != -1;
    }

    public alien_textures GetTexturesTable(int TableIndex)
    {
        if (TableIndex == 0) return Result.LevelTextures;
        if (TableIndex == 2) return GlobalTextures;
        throw new Exception("Texture bank can only be 0 or 2");
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
    public int DefaultMaterial; 
}

class MaterialPropertyIndex
{
    public int OpacityUVMultiplierIndex = -1;
    public int DiffuseUVMultiplierIndex = -1;
    public int DiffuseUVAdderIndex = -1;
    public UInt16 DiffuseSamplerIndex = 1;
    public int SpecularFactorIndex = -1;
    public int SecondaryDiffuseUVMultiplierIndex = -1;
    public int NormalUVMultiplierIndex = -1;
    public int NormalUVMultiplierOfMultiplierIndex = -1;
    public int SecondaryNormalUVMultiplierIndex = -1;
    public int SpecularUVMultiplierIndex = -1;
    public int SecondarySpecularUVMultiplierIndex = -1;
    public int DirtMapUVMultiplierIndex = -1;
    public int OpacityNoiseUVMultiplierIndex = -1;
    public int DiffuseIndex = -1;
    public int OcclusionUVMultiplierIndex = -1;
}

public class AlienTexture
{
    public Cubemap cubemap = null;
    public Texture2D texture = null;

    public bool HasLoaded { get { return cubemap != null || texture != null; } }
    public bool IsCubemap { get { return cubemap != null; } }
    public bool IsTexture { get { return texture != null; } }
}