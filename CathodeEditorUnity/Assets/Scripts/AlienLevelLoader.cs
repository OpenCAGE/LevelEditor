using System.Collections.Generic;
using UnityEngine;
using CATHODE;
using System.IO;
using System;
using CathodeLib;
using CATHODE.LEGACY;
using static CATHODE.LEGACY.ShadersPAK;

public class AlienLevelLoader : MonoBehaviour
{
    [SerializeField] private bool LOAD_COMMANDS_PAK = false;
    [SerializeField] private string LEVEL_NAME = "BSP_TORRENS";

    public delegate void LoadedEvent(alien_level data);
    public LoadedEvent LevelLoadCompleted;

    public alien_level CurrentLevel { get { return Result; } }
    public string CurrentLevelName { get { return LEVEL_NAME; } }
    public GameObject CurrentLevelGameObject { get { return levelParent; } }

    private alien_level Result = null;
    private CathodeTextures GlobalTextures;

    private GameObject levelParent = null;

    private AlienTexture[] LoadedTexturesGlobal;
    private AlienTexture[] LoadedTexturesLevel;
    private GameObjectHolder[] LoadedModels;
    private Material[] LoadedMaterials;

    void Start()
    {
        /*
        CATHODE.Models.ModelsMVR mvr = new CATHODE.Models.ModelsMVR(@"G:\SteamLibrary\steamapps\common\Alien Isolation\DATA\ENV\PRODUCTION\TECH_HUB\WORLD\MODELS.MVR");
        for (int i =0; i < mvr.EntryCount; i++)
        {
            CATHODE.Models.alien_mvr_entry entry = mvr.GetEntry(i);
            entry.IsThisTypeID = 6;
            mvr.SetEntry(i, entry);
        }
        mvr.Save();
        return;
        */

        //Load global assets
        GlobalTextures = new CathodeTextures(SharedVals.instance.PathToEnv + "/GLOBAL/WORLD/GLOBAL_TEXTURES.ALL.PAK", SharedVals.instance.PathToEnv + "/GLOBAL/WORLD/GLOBAL_TEXTURES_HEADERS.ALL.BIN");
        //alien_pak2 GlobalAnimations;
        //alien_anim_string_db GlobalAnimationsStrings;

        //Load level stuff
        //LoadLevel(LEVEL_NAME);



        //LoadLevel(SharedVals.instance.LevelName);
    }

    public void LoadLevel(string level)
    {
        LEVEL_NAME = level;
        if (levelParent != null) Destroy(levelParent);

        //Load level assets
        Result = AlienLevel.Load(LEVEL_NAME, SharedVals.instance.PathToEnv);

        //Load all textures - TODO: flip array and load V2 first? - I suspect V1 is first as A:I loads V1s passively throughout, and then V2s by zone
        LoadedTexturesGlobal = new AlienTexture[GlobalTextures.entryContents.Count];
        LoadedTexturesLevel = new AlienTexture[Result.LevelTextures.entryContents.Count];
        bool[] TextureLoadTrackerGlobal = new bool[GlobalTextures.entryContents.Count];
        bool[] TextureLoadTrackerLevel = new bool[Result.LevelTextures.entryContents.Count];
        for (int i = 0; i < GlobalTextures.entryContents.Count; i++)
        {
            int binIndex = GlobalTextures.entryHeaders[i].BINIndex;
            LoadedTexturesGlobal[binIndex] = LoadTexture(i, 2, !TextureLoadTrackerGlobal[binIndex]);
            TextureLoadTrackerGlobal[binIndex] = true;
        }
        for (int i = 0; i < Result.LevelTextures.entryContents.Count; i++)
        {
            int binIndex = Result.LevelTextures.entryHeaders[i].BINIndex;
            LoadedTexturesLevel[binIndex] = LoadTexture(i, 0, !TextureLoadTrackerLevel[binIndex]);
            TextureLoadTrackerLevel[binIndex] = true;
        }

        //Load all materials
        LoadedMaterials = new Material[Result.ModelsMTL._materials.Length];
        for (int i = 0; i < Result.ModelsMTL._materials.Length; i++) LoadMaterial(i);

        //Load all models
        LoadedModels = new GameObjectHolder[Result.ModelsPAK.modelBIN.Header.ModelCount];
        for (int i = 0; i < Result.ModelsPAK.Models.Count; i++) LoadModel(i);

        //Populate the level with "movers"
        levelParent = new GameObject(LEVEL_NAME);
        for (int i = 0; i < Result.ModelsMVR.Movers.Count; i++)
        {
            GameObject thisParent = new GameObject("MVR: " + i + "/" + Result.ModelsMVR.Movers[i].renderableElementIndex + "/" + Result.ModelsMVR.Movers[i].renderableElementCount);
            Matrix4x4 m = Result.ModelsMVR.Movers[i].transform;
            thisParent.transform.position = m.GetColumn(3);
            thisParent.transform.rotation = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
            thisParent.transform.localScale = new Vector3(m.GetColumn(0).magnitude, m.GetColumn(1).magnitude, m.GetColumn(2).magnitude);
            thisParent.transform.parent = levelParent.transform;
            for (int x = 0; x < Result.ModelsMVR.Movers[i].renderableElementCount; x++)
            {
                RenderableElementsDatabase.RenderableElement RenderableElement = Result.RenderableREDS.RenderableElements[(int)Result.ModelsMVR.Movers[i].renderableElementIndex + x];
                SpawnModel(RenderableElement.ModelIndex, RenderableElement.MaterialLibraryIndex, thisParent);
            }
        }

        //Pull content from COMMANDS
        //CommandsLoader cmdLoader = gameObject.AddComponent<CommandsLoader>();
        //StartCoroutine(cmdLoader.LoadCommandsPAK(levelPath));

        LevelLoadCompleted?.Invoke(Result);
    }

    //first saving attempt
    public void SaveLevel()
    {
        for (int i = 0; i < levelParent.transform.childCount; i++)
        {
            GameObject mvrEntry = levelParent.transform.GetChild(i).gameObject;
            if (mvrEntry.name.Substring(5).Split('/')[0] != i.ToString()) Debug.LogWarning("Something wrong!");

            MoverDatabase.MOVER_DESCRIPTOR thisEntry = Result.ModelsMVR.Movers[i];
            thisEntry.transform = mvrEntry.transform.localToWorldMatrix;
            Result.ModelsMVR.Movers[i] = thisEntry;
        }

        Result.ModelsMVR.Save();
    }

    private void SpawnModel(int binIndex, int mtlIndex, GameObject parent)
    {
        if (binIndex >= Result.ModelsPAK.modelBIN.Header.ModelCount)
        {
            Debug.LogWarning("binIndex out of range!");
            return;
        }
        if (LoadedModels[binIndex] == null)
        {
            Debug.Log("Attempted to load non-parsed model (" + binIndex + ", " + Result.ModelsPAK.modelBIN.ModelFilePaths[binIndex] + "). Skipping!");
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

        CathodeTextures AlienTextures = GetTexturesTable(paktype); 
        if (EntryIndex < 0 || EntryIndex >= AlienTextures.entryContents.Count)
        {
            Debug.LogWarning("Asked to load texture at index " + EntryIndex + ", which is out of bounds!");
            return null;
        }

        GenericPAKEntry Entry = AlienTextures.entryHeaders[EntryIndex];
        TextureEntry InTexture = AlienTextures.Textures[Entry.BINIndex];

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
            case CATHODE.LEGACY.TextureFormat.R32G32B32A32_SFLOAT:
                format = UnityEngine.TextureFormat.RGBA32;
                break;
            case CATHODE.LEGACY.TextureFormat.R8G8B8A8_UNORM:
                format = UnityEngine.TextureFormat.ETC2_RGBA8; //?
                break;
            case CATHODE.LEGACY.TextureFormat.R8G8B8A8_UNORM_0:
                format = UnityEngine.TextureFormat.ETC2_RGBA8; //?
                break;
            case CATHODE.LEGACY.TextureFormat.SIGNED_DISTANCE_FIELD:
                Debug.LogWarning("SDF! NOT LOADED");
                return toReturn;
            case CATHODE.LEGACY.TextureFormat.R8:
                format = UnityEngine.TextureFormat.R8;
                break;
            case CATHODE.LEGACY.TextureFormat.DDS_BC1:
                format = UnityEngine.TextureFormat.DXT1;
                break;
            case CATHODE.LEGACY.TextureFormat.DDS_BC2:
                Debug.LogWarning("BC2! NOT LOADED");
                return toReturn;
            case CATHODE.LEGACY.TextureFormat.DDS_BC5:
                format = UnityEngine.TextureFormat.BC5; //Is this correct?
                break;
            case CATHODE.LEGACY.TextureFormat.DDS_BC3:
                format = UnityEngine.TextureFormat.DXT5;
                break;
            case CATHODE.LEGACY.TextureFormat.DDS_BC7:
                format = UnityEngine.TextureFormat.BC7;
                break;
            case CATHODE.LEGACY.TextureFormat.R8G8:
                format = UnityEngine.TextureFormat.BC5; // is this correct?
                break;
        }

        BinaryReader tempReader = new BinaryReader(new MemoryStream(AlienTextures.dataStart));
        tempReader.BaseStream.Position = Entry.Offset;

        if (InTexture.Type == 7)
        {
            Cubemap cubemapTex = new Cubemap((int)textureDims.x, format, true);
            cubemapTex.name = AlienTextures.TextureFilePaths[Entry.BINIndex];
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
            texture.name = AlienTextures.TextureFilePaths[Entry.BINIndex];
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

        CathodeModels.ModelData ChunkArray = Result.ModelsPAK.Models[EntryIndex];
        for (int ChunkIndex = 0; ChunkIndex < ChunkArray.Header.SubmeshCount; ++ChunkIndex)
        {
            int BINIndex = ChunkArray.Submeshes[ChunkIndex].binIndex;
            CathodeModels.alien_model_bin_model_info Model = Result.ModelsPAK.modelBIN.Models[BINIndex];
            //if (Model.BlockSize == 0) continue;

            CathodeModels.alien_vertex_buffer_format VertexInput = Result.ModelsPAK.modelBIN.VertexBufferFormats[Model.VertexFormatIndex];
            CathodeModels.alien_vertex_buffer_format VertexInputLowDetail = Result.ModelsPAK.modelBIN.VertexBufferFormats[Model.VertexFormatIndexLowDetail];

            BinaryReader Stream = new BinaryReader(new MemoryStream(ChunkArray.Submeshes[ChunkIndex].content));

            List<List<CathodeModels.alien_vertex_buffer_format_element>> Elements = new List<List<CathodeModels.alien_vertex_buffer_format_element>>();
            CathodeModels.alien_vertex_buffer_format_element ElementHeader = new CathodeModels.alien_vertex_buffer_format_element();
            foreach (CathodeModels.alien_vertex_buffer_format_element Element in VertexInput.Elements)
            {
                if (Element.ArrayIndex == 0xFF)
                {
                    ElementHeader = Element;
                    continue;
                }

                while (Elements.Count - 1 < Element.ArrayIndex) Elements.Add(new List<CathodeModels.alien_vertex_buffer_format_element>());
                Elements[Element.ArrayIndex].Add(Element);
            }
            Elements.Add(new List<CathodeModels.alien_vertex_buffer_format_element>() { ElementHeader });

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
                CathodeModels.alien_vertex_buffer_format_element Inputs = Elements[VertexArrayIndex][0];
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
                            CathodeModels.alien_vertex_buffer_format_element Input = Elements[VertexArrayIndex][ElementIndex];
                            switch (Input.VariableType)
                            {
                                case CathodeModels.alien_vertex_input_type.AlienVertexInputType_v3:
                                    {
                                        Vector3 Value = new Vector3(Stream.ReadSingle(), Stream.ReadSingle(), Stream.ReadSingle());
                                        switch (Input.ShaderSlot)
                                        {
                                            case CathodeModels.alien_vertex_input_slot.AlienVertexInputSlot_N:
                                                InNormals.Add(Value);
                                                break;
                                            case CathodeModels.alien_vertex_input_slot.AlienVertexInputSlot_T:
                                                InTangents.Add(new Vector4(Value.x, Value.y, Value.z, 0));
                                                break;
                                            case CathodeModels.alien_vertex_input_slot.AlienVertexInputSlot_UV:
                                                //TODO: 3D UVW
                                                break;
                                        };
                                        break;
                                    }

                                case CathodeModels.alien_vertex_input_type.AlienVertexInputType_u32_C:
                                    {
                                        int Value = Stream.ReadInt32();
                                        switch (Input.ShaderSlot)
                                        {
                                            case CathodeModels.alien_vertex_input_slot.AlienVertexInputSlot_C:
                                                //??
                                                break;
                                        }
                                        break;
                                    }

                                case CathodeModels.alien_vertex_input_type.AlienVertexInputType_v4u8_i:
                                    {
                                        Vector4 Value = new Vector4(Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte());
                                        switch (Input.ShaderSlot)
                                        {
                                            case CathodeModels.alien_vertex_input_slot.AlienVertexInputSlot_BI:
                                                InBoneIndexes.Add(Value);
                                                break;
                                        }
                                        break;
                                    }

                                case CathodeModels.alien_vertex_input_type.AlienVertexInputType_v4u8_f:
                                    {
                                        Vector4 Value = new Vector4(Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte());
                                        Value /= 255.0f;
                                        switch (Input.ShaderSlot)
                                        {
                                            case CathodeModels.alien_vertex_input_slot.AlienVertexInputSlot_BW:
                                                float Sum = Value.x + Value.y + Value.z + Value.w;
                                                InBoneWeights.Add(Value / Sum);
                                                break;
                                            case CathodeModels.alien_vertex_input_slot.AlienVertexInputSlot_UV:
                                                InUVs2.Add(new Vector2(Value.x, Value.y));
                                                InUVs3.Add(new Vector2(Value.z, Value.w));
                                                break;
                                        }
                                        break;
                                    }

                                case CathodeModels.alien_vertex_input_type.AlienVertexInputType_v2s16_UV:
                                    {
                                        Vector2 Value = new Vector2(Stream.ReadInt16(), Stream.ReadInt16());
                                        Value /= 2048.0f;
                                        switch (Input.ShaderSlot)
                                        {
                                            case CathodeModels.alien_vertex_input_slot.AlienVertexInputSlot_UV:
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

                                case CathodeModels.alien_vertex_input_type.AlienVertexInputType_v4s16_f:
                                    {
                                        Vector4 Value = new Vector4(Stream.ReadInt16(), Stream.ReadInt16(), Stream.ReadInt16(), Stream.ReadInt16());
                                        Value /= (float)Int16.MaxValue;
                                        switch (Input.ShaderSlot)
                                        {
                                            case CathodeModels.alien_vertex_input_slot.AlienVertexInputSlot_P:
                                                InVertices.Add(Value);
                                                break;
                                        }
                                        break;
                                    }

                                case CathodeModels.alien_vertex_input_type.AlienVertexInputType_v4u8_NTB:
                                    {
                                        Vector4 Value = new Vector4(Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte());
                                        Value /= (float)byte.MaxValue - 0.5f;
                                        Value.Normalize();
                                        switch (Input.ShaderSlot)
                                        {
                                            case CathodeModels.alien_vertex_input_slot.AlienVertexInputSlot_N:
                                                InNormals.Add(Value);
                                                break;
                                            case CathodeModels.alien_vertex_input_slot.AlienVertexInputSlot_T:
                                                break;
                                            case CathodeModels.alien_vertex_input_slot.AlienVertexInputSlot_B:
                                                break;
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                }
                CathodeLib.Utilities.Align(Stream, 16);
            }

            if (InVertices.Count == 0) continue;

            Mesh thisMesh = new Mesh();
            thisMesh.name = Result.ModelsPAK.modelBIN.ModelFilePaths[BINIndex] + ": " + Result.ModelsPAK.modelBIN.ModelLODPartNames[BINIndex];
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
            ThisModelPart.Name = Result.ModelsPAK.modelBIN.ModelFilePaths[BINIndex] + ": " + Result.ModelsPAK.modelBIN.ModelLODPartNames[BINIndex] + " (" + Result.ModelsMTL._materialNames[Model.MaterialLibraryIndex] + ")";
            ThisModelPart.MainMesh = thisMesh;
            ThisModelPart.DefaultMaterial = Model.MaterialLibraryIndex;
            LoadedModels[BINIndex] = ThisModelPart;
        }
    }

    private MaterialPropertyIndex GetMaterialPropertyIndex(int MaterialIndex)
    {
        MaterialDatabase.Entry InMaterial = Result.ModelsMTL._materials[MaterialIndex];
        ShadersPAK.ShaderEntry Shader = Result.ShadersPAK.Shaders[InMaterial.UberShaderIndex];

        MaterialPropertyIndex toReturn = new MaterialPropertyIndex();

        switch ((ShaderCategory)Shader.Header2.ShaderCategory)
        {
            case ShaderCategory.AlienShaderCategory_Environment:
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

            case ShaderCategory.AlienShaderCategory_Character:
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

            case ShaderCategory.AlienShaderCategory_Skin:
                toReturn.DiffuseUVMultiplierIndex = 4;
                toReturn.DiffuseIndex = 5;
                toReturn.NormalUVMultiplierIndex = 8;
                toReturn.NormalUVMultiplierOfMultiplierIndex = 10;
                toReturn.SecondaryNormalUVMultiplierIndex = 11;
                break;

            case ShaderCategory.AlienShaderCategory_Hair:
                toReturn.DiffuseIndex = 2;
                break;

            case ShaderCategory.AlienShaderCategory_Eye:
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

            case ShaderCategory.AlienShaderCategory_Decal:
                //toReturn.ColorIndex = 3;
                //Material->BaseColor = {};
                break;

            case ShaderCategory.AlienShaderCategory_FogPlane:
                //toReturn.DiffuseIndex = 8;
                //Material.BaseColor = { };
                break;

            case ShaderCategory.AlienShaderCategory_Terrain:
                toReturn.DiffuseIndex = 4;
                break;

            case ShaderCategory.AlienShaderCategory_LightMapEnvironment:
                toReturn.DiffuseIndex = 12;
                break;

            case ShaderCategory.AlienShaderCategory_Refraction:
                toReturn.DiffuseUVMultiplierIndex = 3;
                break;
        }

        return toReturn;
    }

    public void LoadMaterial(int MTLIndex)
    {
        MaterialDatabase.Entry InMaterial = Result.ModelsMTL._materials[MTLIndex];
        int RemappedIndex = Result.ShadersIDXRemap.Datas[InMaterial.UberShaderIndex].Index;
        ShadersPAK.ShaderEntry Shader = Result.ShadersPAK.Shaders[RemappedIndex];

        Material toReturn = new Material(UnityEngine.Shader.Find("Standard"));
        toReturn.name = Result.ModelsMTL._materialNames[MTLIndex];

        List<alien_slot_ids> SlotOffsets = new List<alien_slot_ids>();
        ShaderCategory ShaderCategory = (ShaderCategory)Shader.Header2.ShaderCategory;

        switch (ShaderCategory)
        {
            case ShaderCategory.AlienShaderCategory_Particle:
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.COLOR_RAMP_MAP);
                break;

            case ShaderCategory.AlienShaderCategory_Ribbon:
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.COLOR_RAMP_MAP);
                break;

            case ShaderCategory.AlienShaderCategory_Environment:
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

            case ShaderCategory.AlienShaderCategory_DecalEnvironment:
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

            case ShaderCategory.AlienShaderCategory_Character:
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

            case ShaderCategory.AlienShaderCategory_Skin:
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

            case ShaderCategory.AlienShaderCategory_Hair:
                SlotOffsets.Add(alien_slot_ids.FLOW_MAP);
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.IRRADIANCE_MAP);
                SlotOffsets.Add(alien_slot_ids.SPECULAR_MAP);
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                break;

            case ShaderCategory.AlienShaderCategory_Eye:
                SlotOffsets.Add(alien_slot_ids.CONVOLVED_DIFFUSE);
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);//IrisMap
                SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);//VeinsMap
                SlotOffsets.Add(alien_slot_ids.SCATTER_MAP);
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.ENVIRONMENT_MAP);
                SlotOffsets.Add(alien_slot_ids.IRRADIANCE_MAP);
                break;

            case ShaderCategory.AlienShaderCategory_SkinOcclusion:
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                break;

            case ShaderCategory.AlienShaderCategory_Decal:
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

            case ShaderCategory.AlienShaderCategory_FogPlane:
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);
                // TODO: Should be 'DiffuseMapStatic' - but I am not using that yet.  In order to keep the light cones
                //  visually appealing and not slabs of solid white, I am using normal diffuse for now.
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP_STATIC);
                break;

            case ShaderCategory.AlienShaderCategory_Refraction:
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.ALPHA_MASK);
                SlotOffsets.Add(alien_slot_ids.FLOW_MAP);
                SlotOffsets.Add(alien_slot_ids.ALPHA_THRESHOLD);
                //Material->Material.BaseColor = { };
                break;

            case ShaderCategory.AlienShaderCategory_NonInteractiveWater:
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.ALPHA_MASK);
                SlotOffsets.Add(alien_slot_ids.FLOW_MAP);
                break;

            case ShaderCategory.AlienShaderCategory_LowLODCharacter:
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.SPECULAR_MAP);
                SlotOffsets.Add(alien_slot_ids.LOW_LOD_CHARACTER_MASK);
                SlotOffsets.Add(alien_slot_ids.IRRADIANCE_MAP);
                SlotOffsets.Add(alien_slot_ids.ENVIRONMENT_MAP);
                break;

            case ShaderCategory.AlienShaderCategory_LightDecal:
                SlotOffsets.Add(alien_slot_ids.EMISSIVE);
                break;

            case ShaderCategory.AlienShaderCategory_SpaceSuitVisor:
                SlotOffsets.Add(alien_slot_ids.ENVIRONMENT_MAP);
                SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                SlotOffsets.Add(alien_slot_ids.MASKING_MAP);
                SlotOffsets.Add(alien_slot_ids.FACE_MAP);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.UNSCALED_DIRT_MAP);
                SlotOffsets.Add(alien_slot_ids.DIRT_MAP);
                break;

            case ShaderCategory.AlienShaderCategory_Planet:
                SlotOffsets.Add(alien_slot_ids.ATMOSPHERE_MAP);
                SlotOffsets.Add(alien_slot_ids.DETAIL_MAP);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.NONE);
                SlotOffsets.Add(alien_slot_ids.FLOW_MAP);
                break;

            case ShaderCategory.AlienShaderCategory_LightMapEnvironment:
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

            case ShaderCategory.AlienShaderCategory_Terrain:
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
            if (PairIndex < Result.ModelsMTL._textureReferenceCounts[MTLIndex])
            {
                MaterialDatabase.TextureReference Pair = InMaterial.TextureReferences[PairIndex];
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
        int baseOffset = Result.ModelsMTL._header.CSTOffsets[2] + (InMaterial.CSTOffsets[2] * 4);
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
        return Utilities.Consume<T>(cstReader);
    }
    private bool CSTIndexValid(int i, ref ShadersPAK.ShaderEntry Shader)
    {
        return i >= 0 && i < Shader.Header.CSTCounts[2] && (int)Shader.CSTLinks[2][i] != -1;
    }

    public CathodeTextures GetTexturesTable(int TableIndex)
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