using System.Collections.Generic;
using UnityEngine;
using CATHODE;
using System.IO;
using System;
using CathodeLib;
using CATHODE.LEGACY;
using static CATHODE.LEGACY.ShadersPAK;
using System.Linq;
using Unity.Profiling;

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
    private Textures GlobalTextures;

    private GameObject levelParent = null;

    private AlienTexture[] LoadedTexturesGlobal;
    private AlienTexture[] LoadedTexturesLevel;
    private GameObjectHolder[] LoadedModels;
    private Material[] LoadedMaterials;

    static readonly ProfilerMarker marker_LoadAssets = new ProfilerMarker("LOADER.LoadingAssets");
    static readonly ProfilerMarker marker_Populating = new ProfilerMarker("LOADER.PopulatingLevel");

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
        GlobalTextures = new Textures(SharedVals.instance.PathToEnv + "/GLOBAL/WORLD/GLOBAL_TEXTURES.ALL.PAK");
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
        marker_LoadAssets.Begin();

        //Load level assets
        Result = AlienLevel.Load(LEVEL_NAME, SharedVals.instance.PathToEnv);

        //Load all textures - TODO: flip array and load V2 first? - I suspect V1 is first as A:I loads V1s passively throughout, and then V2s by zone
        LoadedTexturesGlobal = new AlienTexture[GlobalTextures.Entries.Count];
        LoadedTexturesLevel = new AlienTexture[Result.LevelTextures.Entries.Count];
        bool[] TextureLoadTrackerGlobal = new bool[GlobalTextures.Entries.Count];
        bool[] TextureLoadTrackerLevel = new bool[Result.LevelTextures.Entries.Count];
        for (int i = 0; i < GlobalTextures.Entries.Count; i++)
        {
            LoadedTexturesGlobal[i] = LoadTexture(i, 2, !TextureLoadTrackerGlobal[i]);
            TextureLoadTrackerGlobal[i] = true;
        }
        for (int i = 0; i < Result.LevelTextures.Entries.Count; i++)
        {
            LoadedTexturesLevel[i] = LoadTexture(i, 0, !TextureLoadTrackerLevel[i]);
            TextureLoadTrackerLevel[i] = true;
        }

        //Load all materials
        LoadedMaterials = new Material[99999];
        for (int i = 0; i < Result.ModelsMTL.Entries.Count; i++) LoadMaterial(i);

        //Load all models
        LoadedModels = new GameObjectHolder[99999];
        for (int i = 0; i < Result.ModelsPAK.Entries.Count; i++) LoadModel(i);

        //Populate the level with "movers"
        marker_LoadAssets.End();
        marker_Populating.Begin();
        levelParent = new GameObject(LEVEL_NAME);
        for (int i = 0; i < Result.ModelsMVR.Entries.Count; i++)
        {
            GameObject thisParent = new GameObject("MVR: " + i + "/" + Result.ModelsMVR.Entries[i].renderableElementIndex + "/" + Result.ModelsMVR.Entries[i].renderableElementCount);
            Matrix4x4 m = Result.ModelsMVR.Entries[i].transform;
            thisParent.transform.position = m.GetColumn(3);
            thisParent.transform.rotation = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
            thisParent.transform.localScale = new Vector3(m.GetColumn(0).magnitude, m.GetColumn(1).magnitude, m.GetColumn(2).magnitude);
            thisParent.transform.parent = levelParent.transform;
            for (int x = 0; x < Result.ModelsMVR.Entries[i].renderableElementCount; x++)
            {
                RenderableElements.Element RenderableElement = Result.RenderableREDS.Entries[(int)Result.ModelsMVR.Entries[i].renderableElementIndex + x];
                SpawnModel(RenderableElement.ModelIndex, RenderableElement.MaterialIndex, thisParent);
            }
        }
        marker_Populating.End();

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

            Result.ModelsMVR.Entries[i].transform = mvrEntry.transform.localToWorldMatrix;
        }

        Result.ModelsMVR.Save();
    }

    private void SpawnModel(int binIndex, int mtlIndex, GameObject parent)
    {
        GameObjectHolder holder = LoadModel(binIndex);
        if (holder == null)
        {
            Debug.Log("Attempted to load non-parsed model (" + binIndex + "). Skipping!");
            return;
        }

        GameObject newModelSpawn = new GameObject();
        if (parent != null) newModelSpawn.transform.parent = parent.transform;
        newModelSpawn.transform.localPosition = Vector3.zero;
        newModelSpawn.transform.localRotation = Quaternion.identity;
        newModelSpawn.transform.localScale = holder.LocalScale;
        newModelSpawn.name = holder.Name;
        newModelSpawn.AddComponent<MeshFilter>().sharedMesh = holder.MainMesh;
        newModelSpawn.AddComponent<MeshRenderer>().sharedMaterial = LoadMaterial((mtlIndex == -1) ? holder.DefaultMaterial : mtlIndex);

        //todo apply mvr colour scale here
    }

    private AlienTexture LoadTexture(int EntryIndex, int paktype = 0, bool loadV1 = true)
    {
        AlienTexture toReturn = new AlienTexture();

        Textures AlienTextures = GetTexturesTable(paktype); 
        if (EntryIndex < 0 || EntryIndex >= AlienTextures.Entries.Count)
        {
            Debug.LogWarning("Asked to load texture at index " + EntryIndex + ", which is out of bounds!");
            return null;
        }

        Textures.TEX4 InTexture = AlienTextures.Entries[EntryIndex];
        Textures.TEX4_Part TexPart = loadV1 ? InTexture.tex_HighRes : InTexture.tex_LowRes;

        Vector2 textureDims;
        int textureLength = 0;
        int mipLevels = 0;

        textureDims = new Vector2(TexPart.Width, TexPart.Height);
        if (TexPart.Content == null || TexPart.Content.Length == 0)
        {
            //Debug.LogWarning("LENGTH ZERO - NOT LOADING");
            return toReturn;
        }
        textureLength = TexPart.Content.Length;
        mipLevels = TexPart.MipLevels;

        UnityEngine.TextureFormat format = UnityEngine.TextureFormat.BC7;
        switch (InTexture.Format)
        {
            case Textures.TextureFormat.DXGI_FORMAT_BC1_UNORM:
                format = UnityEngine.TextureFormat.DXT1;
                break;
            case Textures.TextureFormat.DXGI_FORMAT_BC3_UNORM:
                format = UnityEngine.TextureFormat.DXT5;
                break;
            case Textures.TextureFormat.DXGI_FORMAT_BC5_UNORM:
                format = UnityEngine.TextureFormat.BC5;
                break;
            case Textures.TextureFormat.DXGI_FORMAT_BC7_UNORM:
                format = UnityEngine.TextureFormat.BC7;
                break;
            case Textures.TextureFormat.DXGI_FORMAT_B8G8R8_UNORM:
                Debug.LogWarning("BGR24 UNSUPPORTED!");
                return toReturn;
            case Textures.TextureFormat.DXGI_FORMAT_B8G8R8A8_UNORM:
                format = UnityEngine.TextureFormat.BGRA32;
                break;
        }

        BinaryReader tempReader = new BinaryReader(new MemoryStream(TexPart.Content));

        switch (InTexture.Type)
        {
            case Textures.AlienTextureType.ENVIRONMENT_MAP:
                Cubemap cubemapTex = new Cubemap((int)textureDims.x, format, true);
                cubemapTex.name = InTexture.Name;
                cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.PositiveX);
                cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.NegativeX);
                cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.PositiveY);
                cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.NegativeY);
                cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.PositiveZ);
                cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.NegativeZ);
                cubemapTex.Apply();
                toReturn.cubemap = cubemapTex;
                //AssetDatabase.CreateAsset(cubemapTex, "Assets/Cubemaps/" + Path.GetFileNameWithoutExtension(cubemapTex.name) + ".cubemap");
                break;
            default:
                Texture2D texture = new Texture2D((int)textureDims[0], (int)textureDims[1], format, mipLevels, true);
                texture.name = InTexture.Name;
                texture.LoadRawTextureData(tempReader.ReadBytes(textureLength));
                texture.Apply();
                toReturn.texture = texture;
                break;
        }

        tempReader.Close();
        return toReturn;
    }

    private GameObjectHolder LoadModel(int EntryIndex)
    {
        if (LoadedModels[EntryIndex] == null)
        {
            Models.CS2.LOD.Submesh submesh = Result.ModelsPAK.GetAtWriteIndex(EntryIndex);
            if (submesh == null) return null;
            Models.CS2.LOD lod = Result.ModelsPAK.FindModelLODForSubmesh(submesh);
            Models.CS2 mesh = Result.ModelsPAK.FindModelForSubmesh(submesh);
            Mesh thisMesh = Result.ModelsPAK.GetMesh(submesh);

            GameObjectHolder ThisModelPart = new GameObjectHolder();
            ThisModelPart.LocalScale = new Vector3(submesh.ScaleFactor, submesh.ScaleFactor, submesh.ScaleFactor);
            ThisModelPart.Name = ((mesh == null) ? "" : mesh.Name) + ": " + ((lod == null) ? "" : lod.Name);
            ThisModelPart.MainMesh = thisMesh;
            ThisModelPart.DefaultMaterial = submesh.MaterialLibraryIndex;
            LoadedModels[EntryIndex] = ThisModelPart;
        }

        return LoadedModels[EntryIndex];
    }

    private MaterialPropertyIndex GetMaterialPropertyIndex(int MaterialIndex)
    {
        Materials.Material InMaterial = Result.ModelsMTL.GetAtWriteIndex(MaterialIndex);
        ShadersPAK.ShaderEntry Shader = Result.ShadersPAK.Shaders[InMaterial.UberShaderIndex];

        MaterialPropertyIndex toReturn = new MaterialPropertyIndex();

        switch ((ShaderCategory)Shader.Header2.ShaderCategory)
        {
            case ShaderCategory.CA_ENVIRONMENT:
                toReturn.Unknown3_Index = 3;
                toReturn.OpacityUVMultiplierIndex = 5;
                toReturn.DiffuseUVMultiplierIndex = 6;
                toReturn.DiffuseIndex = 7;
                toReturn.SecondaryDiffuseUVMultiplierIndex = 8;
                toReturn.SecondaryDiffuseIndex = 9;
                toReturn.NormalUVMultiplierIndex = 10;
                toReturn.NormalMapStrength0Index = 11;
                toReturn.SecondaryNormalUVMultiplierIndex = 12;
                toReturn.NormalMapStrength1Index = 13;
                toReturn.SpecularFactorIndex = 14;
                toReturn.SpecularUVMultiplierIndex = 15;
                toReturn.MetallicFactorIndex = 16;
                toReturn.SecondarySpecularFactorIndex = 17;
                toReturn.SecondarySpecularUVMultiplierIndex = 18;
                toReturn.SecondaryMetallicFactorIndex = 19;
                toReturn.EnvironmentMapStrength2Index = 24;
                toReturn.EnvironmentMapStrengthIndex = 25;
                toReturn.DirtDiffuseIndex = -1; // TODO: ...
                toReturn.OcclusionUVMultiplierIndex = 27;
                toReturn.OcclusionTintIndex = 28;
                toReturn.EmissiveFactorIndex = 29;
                toReturn.EmissiveIndex = 30;
                toReturn.ParallaxUVMultiplierIndex = 35;
                toReturn.ParallaxFactorIndex = 36;
                toReturn.ParallaxOffsetIndex = 37;
                toReturn.IsTransparentIndex = 38;
                toReturn.OpacityNoiseUVMultiplierIndex1 = 39;
                toReturn.OpacityNoiseAmplitudeIndex = 40;
                toReturn.DirtMapUVMultiplier0Index = 47;
                toReturn.DirtMapUVMultiplier1Index = 48;
                toReturn.DirtStrengthIndex = 49;
                break;

            case ShaderCategory.CA_CHARACTER:
                toReturn.OpacityNoiseUVMultiplierIndex1 = 12;
                toReturn.DiffuseUVMultiplierIndex = 15;
                toReturn.DiffuseIndex = 16;
                toReturn.SecondaryDiffuseUVMultiplierIndex = 17;
                toReturn.SecondaryDiffuseIndex = 18;
                toReturn.NormalUVMultiplierIndex = 19;
                toReturn.SecondaryNormalUVMultiplierIndex = 21;
                toReturn.SpecularUVMultiplierIndex = 24;
                toReturn.SpecularFactorIndex = 25;
                break;

            case ShaderCategory.CA_SKIN:
                toReturn.DiffuseUVMultiplierIndex = 4;
                toReturn.DiffuseIndex = 5;
                toReturn.NormalUVMultiplierIndex = 8;
                toReturn.NormalUVMultiplierOfMultiplierIndex = 10;
                toReturn.SecondaryNormalUVMultiplierIndex = 11;
                break;

            case ShaderCategory.CA_HAIR:
                toReturn.DiffuseIndex = 2;
                break;

            case ShaderCategory.CA_EYE:
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

            case ShaderCategory.CA_DECAL:
                //toReturn.ColorIndex = 3;
                //Material->BaseColor = {};
                break;

            case ShaderCategory.CA_FOGPLANE:
                //toReturn.DiffuseIndex = 8;
                //Material.BaseColor = { };
                break;

            case ShaderCategory.CA_REFRACTION:
                toReturn.DiffuseUVMultiplierIndex = 3;
                break;

            case ShaderCategory.CA_TERRAIN:
                toReturn.DiffuseIndex = 4;
                break;

            case ShaderCategory.CA_LIGHTMAP_ENVIRONMENT:
                toReturn.DiffuseIndex = 12;
                break;

            case ShaderCategory.CA_CAMERA_MAP:
                //DiffuseFallback = V4(1);
                break;

            case ShaderCategory.CA_PLANET:
                //DiffuseFallback = V4(1);
                break;
        }

        return toReturn;
    }

    public Material LoadMaterial(int MTLIndex)
    {
        if (LoadedMaterials[MTLIndex] == null)
        {
            Materials.Material InMaterial = Result.ModelsMTL.GetAtWriteIndex(MTLIndex);
            int RemappedIndex = Result.ShadersIDXRemap.Datas[InMaterial.UberShaderIndex].Index;
            ShadersPAK.ShaderEntry Shader = Result.ShadersPAK.Shaders[RemappedIndex];

            Material toReturn = new Material(UnityEngine.Shader.Find("Standard"));
            toReturn.name = InMaterial.Name;

            List<alien_slot_ids> SlotOffsets = new List<alien_slot_ids>();
            ShaderCategory ShaderCategory = (ShaderCategory)Shader.Header2.ShaderCategory;

            switch (ShaderCategory)
            {
                case ShaderCategory.CA_PARTICLE:
                    SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);      //TODO: is it really?
                    SlotOffsets.Add(alien_slot_ids.COLOR_RAMP_MAP);
                    SlotOffsets.Add(alien_slot_ids.FLOW_MAP);         //TODO: unsure
                    SlotOffsets.Add(alien_slot_ids.FLOW_TEXTURE_MAP); //TODO: unsure
                    SlotOffsets.Add(alien_slot_ids.NONE);
                    break;

                case ShaderCategory.CA_RIBBON:
                    SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                    SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);
                    SlotOffsets.Add(alien_slot_ids.COLOR_RAMP_MAP);
                    break;

                case ShaderCategory.CA_ENVIRONMENT:
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

                case ShaderCategory.CA_DECAL_ENVIRONMENT:
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

                case ShaderCategory.CA_CHARACTER:
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

                case ShaderCategory.CA_SKIN:
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

                case ShaderCategory.CA_HAIR:
                    SlotOffsets.Add(alien_slot_ids.FLOW_MAP);
                    SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                    SlotOffsets.Add(alien_slot_ids.NONE);
                    SlotOffsets.Add(alien_slot_ids.IRRADIANCE_MAP);
                    SlotOffsets.Add(alien_slot_ids.SPECULAR_MAP);
                    SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                    break;

                case ShaderCategory.CA_EYE:
                    SlotOffsets.Add(alien_slot_ids.CONVOLVED_DIFFUSE);
                    SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);//IrisMap
                    SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);//VeinsMap
                    SlotOffsets.Add(alien_slot_ids.SCATTER_MAP);
                    SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                    SlotOffsets.Add(alien_slot_ids.ENVIRONMENT_MAP);
                    SlotOffsets.Add(alien_slot_ids.IRRADIANCE_MAP);
                    break;

                case ShaderCategory.CA_SKIN_OCCLUSION:
                    SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                    break;

                    /*
                case ShaderCategory.CA_DECAL:
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
                    */

                /*
            case ShaderCategory.CA_FOGPLANE:
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);
                // TODO: Should be 'DiffuseMapStatic' - but I am not using that yet.  In order to keep the light cones
                //  visually appealing and not slabs of solid white, I am using normal diffuse for now.
                SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP_STATIC);
                break;
                */

                case ShaderCategory.CA_REFRACTION:
                    SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                    SlotOffsets.Add(alien_slot_ids.SECONDARY_NORMAL_MAP);
                    SlotOffsets.Add(alien_slot_ids.ALPHA_MASK);
                    SlotOffsets.Add(alien_slot_ids.FLOW_MAP);
                    SlotOffsets.Add(alien_slot_ids.ALPHA_THRESHOLD);
                    //Material->Material.BaseColor = { };
                    break;

                case ShaderCategory.CA_NONINTERACTIVE_WATER:
                    SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                    SlotOffsets.Add(alien_slot_ids.SECONDARY_NORMAL_MAP);
                    SlotOffsets.Add(alien_slot_ids.ALPHA_MASK);
                    SlotOffsets.Add(alien_slot_ids.FLOW_MAP);
                    break;

                case ShaderCategory.CA_LOW_LOD_CHARACTER:
                    SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                    SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                    SlotOffsets.Add(alien_slot_ids.SPECULAR_MAP);
                    SlotOffsets.Add(alien_slot_ids.LOW_LOD_CHARACTER_MASK);
                    SlotOffsets.Add(alien_slot_ids.IRRADIANCE_MAP);
                    SlotOffsets.Add(alien_slot_ids.ENVIRONMENT_MAP);
                    break;

                case ShaderCategory.CA_LIGHT_DECAL:
                    SlotOffsets.Add(alien_slot_ids.EMISSIVE);
                    break;

                case ShaderCategory.CA_SPACESUIT_VISOR:
                    SlotOffsets.Add(alien_slot_ids.ENVIRONMENT_MAP);
                    SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                    SlotOffsets.Add(alien_slot_ids.MASKING_MAP);
                    SlotOffsets.Add(alien_slot_ids.FACE_MAP);
                    SlotOffsets.Add(alien_slot_ids.NONE);
                    SlotOffsets.Add(alien_slot_ids.UNSCALED_DIRT_MAP);
                    SlotOffsets.Add(alien_slot_ids.DIRT_MAP);
                    break;

                case ShaderCategory.CA_PLANET:
                    SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);          // TODO: This is the AtmosphereMap.
                    SlotOffsets.Add(alien_slot_ids.DETAIL_MAP);
                    SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);           // TODO: This is the AtmosphereNormalMap.
                    SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);// TODO: This is the TerrainMap.
                    SlotOffsets.Add(alien_slot_ids.SECONDARY_NORMAL_MAP); // TODO: This is the TerrainNormalMap.
                    SlotOffsets.Add(alien_slot_ids.FLOW_MAP);
                    break;

                case ShaderCategory.CA_LIGHTMAP_ENVIRONMENT:
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
                    SlotOffsets.Add(alien_slot_ids.OCCLUSION);
                    SlotOffsets.Add(alien_slot_ids.NONE);
                    SlotOffsets.Add(alien_slot_ids.NONE);
                    SlotOffsets.Add(alien_slot_ids.PARALLAX_MAP);
                    SlotOffsets.Add(alien_slot_ids.NONE);
                    break;

                case ShaderCategory.CA_TERRAIN:
                    SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                    SlotOffsets.Add(alien_slot_ids.SECONDARY_DIFFUSE_MAP);
                    SlotOffsets.Add(alien_slot_ids.NORMAL_MAP);
                    SlotOffsets.Add(alien_slot_ids.SECONDARY_NORMAL_MAP);
                    SlotOffsets.Add(alien_slot_ids.SPECULAR_MAP);
                    SlotOffsets.Add(alien_slot_ids.SECONDARY_SPECULAR_MAP);
                    SlotOffsets.Add(alien_slot_ids.NONE);
                    SlotOffsets.Add(alien_slot_ids.OPACITY_NOISE_MAP);
                    SlotOffsets.Add(alien_slot_ids.ENVIRONMENT_MAP);
                    SlotOffsets.Add(alien_slot_ids.LIGHT_MAP);
                    break;

                case ShaderCategory.CA_CAMERA_MAP:
                    SlotOffsets.Add(alien_slot_ids.DIFFUSE_MAP);
                    break;

                //Unsupported shader slot types - draw transparent for now
                case ShaderCategory.CA_SHADOWCASTER:
                case ShaderCategory.CA_DEFERRED:
                case ShaderCategory.CA_DEBUG:
                case ShaderCategory.CA_OCCLUSION_CULLING:
                default:
                    toReturn.name += " (NOT RENDERED: " + ShaderCategory.ToString() + ")";
                    toReturn.color = new Color(0, 0, 0, 0);
                    toReturn.SetFloat("_Mode", 1.0f);
                    toReturn.EnableKeyword("_ALPHATEST_ON");
                    LoadedMaterials[MTLIndex] = toReturn;
                    return LoadedMaterials[MTLIndex];
            }
            toReturn.name += " " + ShaderCategory.ToString();

            List<Texture> availableTextures = new List<Texture>();
            for (int SlotIndex = 0; SlotIndex < Shader.Header.TextureLinkCount; ++SlotIndex)
            {
                int PairIndex = Shader.TextureLinks[SlotIndex];
                // NOTE: PairIndex == 255 means no index.
                if (PairIndex < InMaterial.TextureReferences.Count)
                {
                    Materials.Material.Texture Pair = InMaterial.TextureReferences[PairIndex];
                    switch (Pair.Source)
                    {
                        case Materials.Material.Texture.TextureSource.LEVEL:
                            availableTextures.Add(LoadedTexturesLevel[Pair.BinIndex].texture);
                            break;
                        case Materials.Material.Texture.TextureSource.GLOBAL:
                            availableTextures.Add(LoadedTexturesGlobal[Pair.BinIndex].texture);
                            break;
                        default:
                            availableTextures.Add(null);
                            break;
                    }
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
            BinaryReader cstReader = new BinaryReader(new MemoryStream(Result.ModelsMTL.CSTData[2]));
            int baseOffset = (InMaterial.ConstantBuffers[2].CstIndex * 4);
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

        return LoadedMaterials[MTLIndex];
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

    public Textures GetTexturesTable(int TableIndex)
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
        FLOW_TEXTURE_MAP,
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
    public UInt16 DiffuseSamplerIndex = 1; 
    public int OpacityUVMultiplierIndex = -1;
    public int DiffuseUVMultiplierIndex = -1;
    public int DiffuseUVAdderIndex = -1;
    public int SpecularFactorIndex = -1;
    public int MetallicFactorIndex = -1;
    public int SecondaryDiffuseUVMultiplierIndex = -1;
    public int NormalUVMultiplierIndex = -1;
    public int NormalUVMultiplierOfMultiplierIndex = -1;
    public int NormalMapStrength0Index = -1;
    public int NormalMapStrength1Index = -1;
    public int SecondaryNormalUVMultiplierIndex = -1;
    public int SpecularUVMultiplierIndex = -1;
    public int SecondarySpecularUVMultiplierIndex = -1;
    public int SecondarySpecularFactorIndex = -1;
    public int SecondaryMetallicFactorIndex = -1;
    public int DirtMapUVMultiplier0Index = -1;
    public int DirtMapUVMultiplier1Index = -1;
    public int DirtDiffuseIndex = -1;
    public int DirtStrengthIndex = -1;
    public int EmissiveFactorIndex = -1;
    public int EmissiveIndex = -1;
    public int EnvironmentMapStrengthIndex = -1;
    public int OpacityNoiseUVMultiplierIndex1 = -1;
    public int OpacityNoiseAmplitudeIndex = -1;
    public int DiffuseIndex = -1;
    public int SecondaryDiffuseIndex = -1;
    public int OcclusionUVMultiplierIndex = -1;
    public int OcclusionTintIndex = -1;
    public int IsTransparentIndex = -1;
    public int EnvironmentMapStrength2Index = -1;
    public int Unknown3_Index = -1;
    public int ParallaxUVMultiplierIndex = -1;
    public int ParallaxFactorIndex = -1;
    public int ParallaxOffsetIndex = -1;
}

public class AlienTexture
{
    public Cubemap cubemap = null;
    public Texture2D texture = null;

    public bool HasLoaded { get { return cubemap != null || texture != null; } }
    public bool IsCubemap { get { return cubemap != null; } }
    public bool IsTexture { get { return texture != null; } }
}