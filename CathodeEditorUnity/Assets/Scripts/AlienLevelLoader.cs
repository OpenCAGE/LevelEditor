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
    private Textures GlobalTextures = null;

    private GameObject levelParent = null;

    private AlienTexture[] LoadedTexturesGlobal;
    private AlienTexture[] LoadedTexturesLevel;
    private GameObjectHolder[] LoadedModels;
    private Material[] LoadedMaterials;

    static readonly ProfilerMarker marker_LoadAssets = new ProfilerMarker("LOADER.LoadingAssets");
    static readonly ProfilerMarker marker_Populating = new ProfilerMarker("LOADER.PopulatingLevel");

    private WebsocketClient _client;

    void Start()
    {
        _client = GetComponent<WebsocketClient>();
    }

    public void LoadLevel(string level)
    {
        LEVEL_NAME = level;
        if (levelParent != null) Destroy(levelParent);
        marker_LoadAssets.Begin();

        if (GlobalTextures == null)
            GlobalTextures = new Textures(_client.PathToAI + "/DATA/ENV/GLOBAL/WORLD/GLOBAL_TEXTURES.ALL.PAK");

        //Load level assets
        Result = AlienLevel.Load(LEVEL_NAME, _client.PathToAI + "/DATA/ENV/");

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
        //for (int i = 0; i < Result.ModelsMTL.Entries.Count; i++) LoadMaterial(i);

        //Load all models
        LoadedModels = new GameObjectHolder[99999];
        //for (int i = 0; i < Result.ModelsPAK.Entries.Count; i++) LoadModel(i);

        //Set skybox
        for (int i = 0; i < LoadedTexturesGlobal.Length; i++)
        {
            if (LoadedTexturesGlobal[i].IsCubemap)
            {
                Material toReturn = new Material(UnityEngine.Shader.Find("Skybox/Cubemap"));
                toReturn.SetTexture("_Tex", LoadedTexturesGlobal[i].cubemap);
                RenderSettings.skybox = toReturn;
                break;
            }
        }

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
        Textures.TEX4.Part TexPart = loadV1 ? InTexture.tex_HighRes : InTexture.tex_LowRes;

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
            Models.CS2.Component.LOD.Submesh submesh = Result.ModelsPAK.GetAtWriteIndex(EntryIndex);
            if (submesh == null) return null;
            Models.CS2.Component.LOD lod = Result.ModelsPAK.FindModelLODForSubmesh(submesh);
            Models.CS2 mesh = Result.ModelsPAK.FindModelForSubmesh(submesh);
            Mesh thisMesh = submesh.ToMesh();

            GameObjectHolder ThisModelPart = new GameObjectHolder();
            ThisModelPart.Name = ((mesh == null) ? "" : mesh.Name) + ": " + ((lod == null) ? "" : lod.Name);
            ThisModelPart.MainMesh = thisMesh;
            ThisModelPart.DefaultMaterial = submesh.MaterialLibraryIndex;
            LoadedModels[EntryIndex] = ThisModelPart;
        }

        return LoadedModels[EntryIndex];
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

            ShaderMaterialMetadata metadata = Result.ShadersPAK.GetMaterialMetadataFromShader(InMaterial, Result.ShadersIDXRemap);

            switch (metadata.shaderCategory)
            { 
                //Unsupported shader slot types - draw transparent for now
                case ShaderCategory.CA_SHADOWCASTER:
                case ShaderCategory.CA_DEFERRED:
                case ShaderCategory.CA_DEBUG:
                case ShaderCategory.CA_OCCLUSION_CULLING:
                case ShaderCategory.CA_FOGSPHERE:
                case ShaderCategory.CA_FOGPLANE:
                case ShaderCategory.CA_EFFECT_OVERLAY:
                case ShaderCategory.CA_DECAL:
                case ShaderCategory.CA_VOLUME_LIGHT:
                    toReturn.name += " (NOT RENDERED: " + metadata.shaderCategory.ToString() + ")";
                    toReturn.color = new Color(0, 0, 0, 0);
                    toReturn.SetFloat("_Mode", 1.0f);
                    toReturn.EnableKeyword("_ALPHATEST_ON");
                    LoadedMaterials[MTLIndex] = toReturn;
                    return LoadedMaterials[MTLIndex];
            }
            toReturn.name += " " + metadata.shaderCategory.ToString();

            List<Texture> availableTextures = new List<Texture>();
            for (int SlotIndex = 0; SlotIndex < Shader.Header.TextureLinkCount; ++SlotIndex)
            {
                int PairIndex = Shader.TextureLinks[SlotIndex];
                // NOTE: PairIndex == 255 means no index.
                if (PairIndex < InMaterial.TextureReferences.Length)
                {
                    Materials.Material.Texture Pair = InMaterial.TextureReferences[PairIndex];
                    switch (Pair.Source)
                    {
                        case Materials.Material.Texture.TextureSource.LEVEL:
                            availableTextures.Add(Pair.BinIndex == -1 ? null : LoadedTexturesLevel[Pair.BinIndex]?.texture);
                            break;
                        case Materials.Material.Texture.TextureSource.GLOBAL:
                            availableTextures.Add(LoadedTexturesGlobal[Pair.BinIndex]?.texture);
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
            for (int i = 0; i < metadata.textures.Count; i++)
            {
                if (i >= availableTextures.Count) continue;
                switch (metadata.textures[i].Type)
                {
                    case ShaderSlot.DIFFUSE_MAP:
                        toReturn.SetTexture("_MainTex", availableTextures[i]);
                        break;
                    case ShaderSlot.DETAIL_MAP:
                        toReturn.EnableKeyword("_DETAIL_MULX2");
                        toReturn.SetTexture("_DetailMask", availableTextures[i]);
                        break;
                    case ShaderSlot.EMISSIVE:
                        toReturn.EnableKeyword("_EMISSION");
                        toReturn.SetTexture("_EmissionMap", availableTextures[i]);
                        break;
                    case ShaderSlot.PARALLAX_MAP:
                        toReturn.EnableKeyword("_PARALLAXMAP");
                        toReturn.SetTexture("_ParallaxMap", availableTextures[i]);
                        break;
                    case ShaderSlot.OCCLUSION:
                        toReturn.SetTexture("_OcclusionMap", availableTextures[i]);
                        break;
                    case ShaderSlot.SPECULAR_MAP:
                        toReturn.EnableKeyword("_METALLICGLOSSMAP");
                        toReturn.SetTexture("_MetallicGlossMap", availableTextures[i]); //TODO _SPECGLOSSMAP?
                        toReturn.SetFloat("_Glossiness", 0.0f);
                        toReturn.SetFloat("_GlossMapScale", 0.0f);
                        break;
                    case ShaderSlot.NORMAL_MAP:
                        toReturn.EnableKeyword("_NORMALMAP");
                        toReturn.SetTexture("_BumpMap", availableTextures[i]);
                        break;
                }
            }

            //Apply properties
            BinaryReader cstReader = new BinaryReader(new MemoryStream(Result.ModelsMTL.CSTData[2]));
            int baseOffset = (InMaterial.ConstantBuffers[2].Offset * 4);
            if (CSTIndexValid(metadata.cstIndexes.DiffuseIndex, ref Shader))
            {
                Vector4 colour = LoadFromCST<Vector4>(ref cstReader, baseOffset + (Shader.CSTLinks[2][metadata.cstIndexes.DiffuseIndex] * 4));
                toReturn.SetColor("_Color", colour);
                if (colour.w != 1)
                {
                    toReturn.SetFloat("_Mode", 1.0f);
                    toReturn.EnableKeyword("_ALPHATEST_ON");
                }
            }
            if (CSTIndexValid(metadata.cstIndexes.DiffuseUVMultiplierIndex, ref Shader))
            {
                float offset = LoadFromCST<float>(ref cstReader, baseOffset + (Shader.CSTLinks[2][metadata.cstIndexes.DiffuseUVMultiplierIndex] * 4));
                toReturn.SetTextureScale("_MainTex", new Vector2(offset, offset));
            }
            if (CSTIndexValid(metadata.cstIndexes.DiffuseUVAdderIndex, ref Shader))
            {
                float offset = LoadFromCST<float>(ref cstReader, baseOffset + (Shader.CSTLinks[2][metadata.cstIndexes.DiffuseUVAdderIndex] * 4));
                toReturn.SetTextureOffset("_MainTex", new Vector2(offset, offset));
            }
            if (CSTIndexValid(metadata.cstIndexes.NormalUVMultiplierIndex, ref Shader))
            {
                float offset = LoadFromCST<float>(ref cstReader, baseOffset + (Shader.CSTLinks[2][metadata.cstIndexes.NormalUVMultiplierIndex] * 4));
                toReturn.SetTextureScale("_BumpMap", new Vector2(offset, offset));
                toReturn.SetFloat("_BumpScale", offset);
            }
            if (CSTIndexValid(metadata.cstIndexes.OcclusionUVMultiplierIndex, ref Shader))
            {
                float offset = LoadFromCST<float>(ref cstReader, baseOffset + (Shader.CSTLinks[2][metadata.cstIndexes.OcclusionUVMultiplierIndex] * 4));
                toReturn.SetTextureScale("_OcclusionMap", new Vector2(offset, offset));
            }
            if (CSTIndexValid(metadata.cstIndexes.SpecularUVMultiplierIndex, ref Shader))
            {
                float offset = LoadFromCST<float>(ref cstReader, baseOffset + (Shader.CSTLinks[2][metadata.cstIndexes.SpecularUVMultiplierIndex] * 4));
                toReturn.SetTextureScale("_MetallicGlossMap", new Vector2(offset, offset));
                toReturn.SetFloat("_GlossMapScale", offset);
            }
            if (CSTIndexValid(metadata.cstIndexes.SpecularFactorIndex, ref Shader))
            {
                float spec = LoadFromCST<float>(ref cstReader, baseOffset + (Shader.CSTLinks[2][metadata.cstIndexes.SpecularFactorIndex] * 4));
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
}

//Temp wrapper for GameObject while we just want it in memory
public class GameObjectHolder
{
    public string Name;
    public Mesh MainMesh; //TODO: should this be contained in a globally referenced array?
    public int DefaultMaterial; 
}

public class AlienTexture
{
    public Cubemap cubemap = null;
    public Texture2D texture = null;

    public bool HasLoaded { get { return cubemap != null || texture != null; } }
    public bool IsCubemap { get { return cubemap != null; } }
    public bool IsTexture { get { return texture != null; } }
}