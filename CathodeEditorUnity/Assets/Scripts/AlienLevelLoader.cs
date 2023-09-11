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
using System.Threading.Tasks;
using static CATHODE.Materials.Material;
using static UnityEngine.Networking.UnityWebRequest;

public class AlienLevelLoader : MonoBehaviour
{
    private string _levelName = "BSP_TORRENS";
    public string LevelName => _levelName;

    private alien_level _levelContent = null;
    private Textures _globalTextures = null;

    private GameObject _parentGO = null;

    private Dictionary<int, Texture2D> _texturesGlobal = new Dictionary<int, Texture2D>();
    private Dictionary<int, Texture2D> _texturesLevel = new Dictionary<int, Texture2D>();
    private Dictionary<int, Material> _materials = new Dictionary<int, Material>();
    private Dictionary<int, GameObjectHolder> _modelGOs = new Dictionary<int, GameObjectHolder>();

    private WebsocketClient _client;

    void Start()
    {
        _client = GetComponent<WebsocketClient>();
    }

    private void ResetLevel()
    {
        if (_parentGO != null)
            Destroy(_parentGO);

        _texturesGlobal.Clear();
        _texturesLevel.Clear();
        _materials.Clear();
        _modelGOs.Clear();

        _levelContent = null;

        if (_globalTextures == null)
            _globalTextures = new Textures(_client.PathToAI + "/DATA/ENV/GLOBAL/WORLD/GLOBAL_TEXTURES.ALL.PAK");
    }

    public void LoadLevel(string level)
    {
        ResetLevel();

        _levelName = level;
        _levelContent = new alien_level(_client.PathToAI + "/DATA/ENV/PRODUCTION/" + level);

        //Set skybox
        /*
        for (int i = 0; i < LoadedTexturesGlobal.Length; i++)
        {
            if (LoadedTexturesGlobal[i] != null && LoadedTexturesGlobal[i].IsCubemap)
            {
                Material toReturn = new Material(UnityEngine.Shader.Find("Skybox/Cubemap"));
                toReturn.SetTexture("_Tex", LoadedTexturesGlobal[i].cubemap);
                RenderSettings.skybox = toReturn;
                break;
            }
        }
        */

        //Populate the level with "movers"
        _parentGO = new GameObject(_levelName);
        for (int i = 0; i < _levelContent.ModelsMVR.Entries.Count; i++)
        {
            GameObject thisParent = new GameObject("MVR: " + i + "/" + _levelContent.ModelsMVR.Entries[i].renderableElementIndex + "/" + _levelContent.ModelsMVR.Entries[i].renderableElementCount);
            Matrix4x4 m = _levelContent.ModelsMVR.Entries[i].transform;
            thisParent.transform.position = m.GetColumn(3);
            thisParent.transform.rotation = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
            thisParent.transform.localScale = new Vector3(m.GetColumn(0).magnitude, m.GetColumn(1).magnitude, m.GetColumn(2).magnitude);
            thisParent.transform.parent = _parentGO.transform;
            for (int x = 0; x < _levelContent.ModelsMVR.Entries[i].renderableElementCount; x++)
            {
                RenderableElements.Element RenderableElement = _levelContent.RenderableREDS.Entries[(int)_levelContent.ModelsMVR.Entries[i].renderableElementIndex + x];
                SpawnModel(RenderableElement.ModelIndex, RenderableElement.MaterialIndex, thisParent);
            }
        }

        //Pull content from COMMANDS
        //CommandsLoader cmdLoader = gameObject.AddComponent<CommandsLoader>();
        //StartCoroutine(cmdLoader.LoadCommandsPAK(levelPath));
    }

    private void SpawnModel(int binIndex, int mtlIndex, GameObject parent)
    {
        GameObjectHolder holder = GetModel(binIndex);
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
        newModelSpawn.AddComponent<MeshRenderer>().sharedMaterial = GetMaterial((mtlIndex == -1) ? holder.DefaultMaterial : mtlIndex);

        //todo apply mvr colour scale here
    }

    private Texture2D GetTexture(int index, bool global)
    {
        if ((global && !_texturesGlobal.ContainsKey(index)) || (!global && !_texturesLevel.ContainsKey(index)))
        {
            Textures.TEX4 InTexture = (global ? _globalTextures : _levelContent.LevelTextures).GetAtWriteIndex(index);
            Textures.TEX4.Part TexPart = InTexture.tex_HighRes;

            Vector2 textureDims;
            int textureLength = 0;
            int mipLevels = 0;

            textureDims = new Vector2(TexPart.Width, TexPart.Height);
            if (TexPart.Content == null || TexPart.Content.Length == 0)
            {
                //Debug.LogWarning("LENGTH ZERO - NOT LOADING");
                return null;
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
                    return null;
                case Textures.TextureFormat.DXGI_FORMAT_B8G8R8A8_UNORM:
                    format = UnityEngine.TextureFormat.BGRA32;
                    break;
            }

            Texture2D texture = null;
            using (BinaryReader tempReader = new BinaryReader(new MemoryStream(TexPart.Content)))
            {
                switch (InTexture.Type)
                {
                    case Textures.AlienTextureType.ENVIRONMENT_MAP:
                        break;
                        Cubemap cubemapTex = new Cubemap((int)textureDims.x, format, true);
                        cubemapTex.name = InTexture.Name;
                        cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.PositiveX);
                        cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.NegativeX);
                        cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.PositiveY);
                        cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.NegativeY);
                        cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.PositiveZ);
                        cubemapTex.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.NegativeZ);
                        cubemapTex.Apply();
                        //AssetDatabase.CreateAsset(cubemapTex, "Assets/Cubemaps/" + Path.GetFileNameWithoutExtension(cubemapTex.name) + ".cubemap");
                        break;
                    default:
                        texture = new Texture2D((int)textureDims[0], (int)textureDims[1], format, mipLevels, true);
                        texture.name = InTexture.Name;
                        texture.LoadRawTextureData(tempReader.ReadBytes(textureLength));
                        texture.Apply();
                        break;
                }
            }

            if (global)
                _texturesGlobal.Add(index, texture);
            else
                _texturesLevel.Add(index, texture);
        }

        if (global)
            return _texturesGlobal[index];
        else
            return _texturesLevel[index];
    }

    private GameObjectHolder GetModel(int EntryIndex)
    {
        if (!_modelGOs.ContainsKey(EntryIndex))
        {
            Models.CS2.Component.LOD.Submesh submesh = _levelContent.ModelsPAK.GetAtWriteIndex(EntryIndex);
            if (submesh == null) return null;
            Models.CS2.Component.LOD lod = _levelContent.ModelsPAK.FindModelLODForSubmesh(submesh);
            Models.CS2 mesh = _levelContent.ModelsPAK.FindModelForSubmesh(submesh);
            Mesh thisMesh = submesh.ToMesh();

            GameObjectHolder ThisModelPart = new GameObjectHolder();
            ThisModelPart.Name = ((mesh == null) ? "" : mesh.Name) + ": " + ((lod == null) ? "" : lod.Name);
            ThisModelPart.MainMesh = thisMesh;
            ThisModelPart.DefaultMaterial = submesh.MaterialLibraryIndex;
            _modelGOs.Add(EntryIndex, ThisModelPart);
        }
        return _modelGOs[EntryIndex];
    }

    public Material GetMaterial(int MTLIndex)
    {
        if (!_materials.ContainsKey(MTLIndex))
        {
            Materials.Material InMaterial = _levelContent.ModelsMTL.GetAtWriteIndex(MTLIndex);
            int RemappedIndex = _levelContent.ShadersIDXRemap.Datas[InMaterial.UberShaderIndex].Index;
            ShadersPAK.ShaderEntry Shader = _levelContent.ShadersPAK.Shaders[RemappedIndex];

            Material toReturn = new Material(UnityEngine.Shader.Find("Standard"));
            toReturn.name = InMaterial.Name;

            ShaderMaterialMetadata metadata = _levelContent.ShadersPAK.GetMaterialMetadataFromShader(InMaterial, _levelContent.ShadersIDXRemap);

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
                case ShaderCategory.CA_REFRACTION:
                    toReturn.name += " (NOT RENDERED: " + metadata.shaderCategory.ToString() + ")";
                    toReturn.color = new Color(0, 0, 0, 0);
                    toReturn.SetFloat("_Mode", 1.0f);
                    toReturn.EnableKeyword("_ALPHATEST_ON");
                    return toReturn;
            }
            toReturn.name += " " + metadata.shaderCategory.ToString();

            List<Texture2D> availableTextures = new List<Texture2D>();
            for (int SlotIndex = 0; SlotIndex < Shader.Header.TextureLinkCount; ++SlotIndex)
            {
                int PairIndex = Shader.TextureLinks[SlotIndex];
                // NOTE: PairIndex == 255 means no index.
                if (PairIndex < InMaterial.TextureReferences.Length)
                {
                    Materials.Material.Texture Pair = InMaterial.TextureReferences[PairIndex];
                    availableTextures.Add(Pair.BinIndex == -1 ? null : GetTexture(Pair.BinIndex, Pair.Source == Materials.Material.Texture.TextureSource.GLOBAL));
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
            using (BinaryReader cstReader = new BinaryReader(new MemoryStream(_levelContent.ModelsMTL.CSTData[2])))
            {
                int baseOffset = (InMaterial.ConstantBuffers[2].Offset * 4);
                if (CSTIndexValid(metadata.cstIndexes.DiffuseIndex, ref Shader))
                {
                    Vector4 colour = LoadFromCST<Vector4>(cstReader, baseOffset + (Shader.CSTLinks[2][metadata.cstIndexes.DiffuseIndex] * 4));
                    toReturn.SetColor("_Color", colour);
                    if (colour.w != 1)
                    {
                        toReturn.SetFloat("_Mode", 1.0f);
                        toReturn.EnableKeyword("_ALPHATEST_ON");
                    }
                }
                if (CSTIndexValid(metadata.cstIndexes.DiffuseUVMultiplierIndex, ref Shader))
                {
                    float offset = LoadFromCST<float>(cstReader, baseOffset + (Shader.CSTLinks[2][metadata.cstIndexes.DiffuseUVMultiplierIndex] * 4));
                    toReturn.SetTextureScale("_MainTex", new Vector2(offset, offset));
                }
                if (CSTIndexValid(metadata.cstIndexes.DiffuseUVAdderIndex, ref Shader))
                {
                    float offset = LoadFromCST<float>(cstReader, baseOffset + (Shader.CSTLinks[2][metadata.cstIndexes.DiffuseUVAdderIndex] * 4));
                    toReturn.SetTextureOffset("_MainTex", new Vector2(offset, offset));
                }
                if (CSTIndexValid(metadata.cstIndexes.NormalUVMultiplierIndex, ref Shader))
                {
                    float offset = LoadFromCST<float>(cstReader, baseOffset + (Shader.CSTLinks[2][metadata.cstIndexes.NormalUVMultiplierIndex] * 4));
                    toReturn.SetTextureScale("_BumpMap", new Vector2(offset, offset));
                    toReturn.SetFloat("_BumpScale", offset);
                }
                if (CSTIndexValid(metadata.cstIndexes.OcclusionUVMultiplierIndex, ref Shader))
                {
                    float offset = LoadFromCST<float>(cstReader, baseOffset + (Shader.CSTLinks[2][metadata.cstIndexes.OcclusionUVMultiplierIndex] * 4));
                    toReturn.SetTextureScale("_OcclusionMap", new Vector2(offset, offset));
                }
                if (CSTIndexValid(metadata.cstIndexes.SpecularUVMultiplierIndex, ref Shader))
                {
                    float offset = LoadFromCST<float>(cstReader, baseOffset + (Shader.CSTLinks[2][metadata.cstIndexes.SpecularUVMultiplierIndex] * 4));
                    toReturn.SetTextureScale("_MetallicGlossMap", new Vector2(offset, offset));
                    toReturn.SetFloat("_GlossMapScale", offset);
                }
                if (CSTIndexValid(metadata.cstIndexes.SpecularFactorIndex, ref Shader))
                {
                    float spec = LoadFromCST<float>(cstReader, baseOffset + (Shader.CSTLinks[2][metadata.cstIndexes.SpecularFactorIndex] * 4));
                    toReturn.SetFloat("_Glossiness", spec);
                    toReturn.SetFloat("_GlossMapScale", spec);
                }
            }

            _materials.Add(MTLIndex, toReturn);
        }
        return _materials[MTLIndex];
    }
    private T LoadFromCST<T>(BinaryReader cstReader, int offset)
    {
        cstReader.BaseStream.Position = offset;
        return Utilities.Consume<T>(cstReader);
    }
    private bool CSTIndexValid(int i, ref ShadersPAK.ShaderEntry Shader)
    {
        return i >= 0 && i < Shader.Header.CSTCounts[2] && (int)Shader.CSTLinks[2][i] != -1;
    }
}

//Temp wrapper for GameObject while we just want it in memory
public class GameObjectHolder
{
    public string Name;
    public Mesh MainMesh; //TODO: should this be contained in a globally referenced array?
    public int DefaultMaterial; 
}

public class alien_level
{
    public alien_level(string levelPath)
    {
        Parallel.For(0, 14, (i) =>
        {
            switch (i)
            {
                case 0:
                    ModelsMVR = new Movers(levelPath + "/WORLD/MODELS.MVR");
                    break;
                case 1:
                    CommandsPAK = new Commands(levelPath + "/WORLD/COMMANDS.PAK");
                    break;
                case 2:
                    RenderableREDS = new RenderableElements(levelPath + "/WORLD/REDS.BIN");
                    break;
                case 3:
                    ResourcesBIN = new CATHODE.Resources(levelPath + "/WORLD/RESOURCES.BIN");
                    break;
                case 4:
                    PhysicsMap = new PhysicsMaps(levelPath + "/WORLD/PHYSICS.MAP");
                    break;
                case 5:
                    EnvironmentMap = new EnvironmentMaps(levelPath + "/WORLD/ENVIRONMENTMAP.BIN");
                    break;
                case 6:
                    CollisionMap = new CollisionMaps(levelPath + "/WORLD/COLLISION.MAP");
                    break;
                case 7:
                    EnvironmentAnimation = new EnvironmentAnimations(levelPath + "/WORLD/ENVIRONMENT_ANIMATION.DAT");
                    break;
                case 8:
                    ModelsCST = File.ReadAllBytes(levelPath + "/RENDERABLE/LEVEL_MODELS.CST");
                    break;
                case 9:
                    ModelsMTL = new Materials(levelPath + "/RENDERABLE/LEVEL_MODELS.MTL");
                    break;
                case 10:
                    ModelsPAK = new Models(levelPath + "/RENDERABLE/LEVEL_MODELS.PAK");
                    break;
                case 11:
                    ShadersPAK = new ShadersPAK(levelPath + "/RENDERABLE/LEVEL_SHADERS_DX11.PAK");
                    break;
                case 12:
                    ShadersIDXRemap = new IDXRemap(levelPath + "/RENDERABLE/LEVEL_SHADERS_DX11_IDX_REMAP.PAK");
                    break;
                case 13:
                    LevelTextures = new Textures(levelPath + "/RENDERABLE/LEVEL_TEXTURES.ALL.PAK");
                    break;
            }
        });
    }

    public Movers ModelsMVR;
    public Commands CommandsPAK;
    public RenderableElements RenderableREDS;
    public CATHODE.Resources ResourcesBIN;
    public PhysicsMaps PhysicsMap;
    public EnvironmentMaps EnvironmentMap;
    public CollisionMaps CollisionMap;
    public EnvironmentAnimations EnvironmentAnimation;
    public byte[] ModelsCST;
    public Materials ModelsMTL;
    public Models ModelsPAK;
    public Textures LevelTextures;
    public ShadersPAK ShadersPAK;
    public alien_shader_bin_pak ShadersBIN;
    public IDXRemap ShadersIDXRemap;
};