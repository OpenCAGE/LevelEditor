using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using CATHODE.LEGACY;

//THIS IS ALL TEMP FOR TESTING
public class TestTextureEd : MonoSingleton<TestTextureEd>
{
    [SerializeField] IconGrid appGrid;
    [SerializeField] UnityEngine.UI.Image previewImg;
    [SerializeField] TMPro.TextMeshProUGUI previewText;

    CathodeTextures LevelTextures;
    AlienTexture[] LoadedTexturesLevel;
    AlienTexture selectedTex = null;

    void Start()
    { 
        string levelPath = @"G:\SteamLibrary\steamapps\common\Alien Isolation\DATA\ENV\PRODUCTION\BSP_TORRENS\";
        LevelTextures = new CathodeTextures(levelPath + "/RENDERABLE/LEVEL_TEXTURES.ALL.PAK", levelPath + "/RENDERABLE/LEVEL_TEXTURE_HEADERS.ALL.BIN");

        LoadedTexturesLevel = new AlienTexture[LevelTextures.Header.EntryCount];
        bool[] TextureLoadTrackerLevel = new bool[LevelTextures.Header.EntryCount];
        for (int x = 0; x < LevelTextures.Header.EntryCount; x++)
        {
            int binIndex = LevelTextures.entryHeaders[x].BINIndex;
            LoadedTexturesLevel[binIndex] = LoadTexture(x, !TextureLoadTrackerLevel[binIndex]);
            TextureLoadTrackerLevel[binIndex] = true;
        }

        appGrid.GenerateGrid(LoadedTexturesLevel.Length);
        int i = 0;
        foreach (LoadedTextureUI app in appGrid.transform.GetComponentsInChildren<LoadedTextureUI>(true))
        {
            app.Setup(LoadedTexturesLevel[i], LevelTextures.TextureFilePaths[i]);
            i++;
        }
        //scrollRect.normalizedPosition = new Vector2(0, 1);
    }

    private AlienTexture LoadTexture(int EntryIndex, bool loadV1 = true)
    {
        AlienTexture toReturn = new AlienTexture();

        if (EntryIndex < 0 || EntryIndex >= LevelTextures.Header.EntryCount)
        {
            Debug.LogWarning("Asked to load texture at index " + EntryIndex + ", which is out of bounds!");
            return null;
        }

        GenericPAKEntry Entry = LevelTextures.entryHeaders[EntryIndex];
        TextureEntry InTexture = LevelTextures.Textures[Entry.BINIndex];

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

        BinaryReader tempReader = new BinaryReader(new MemoryStream(LevelTextures.dataStart));
        tempReader.BaseStream.Position = Entry.Offset;

        if (InTexture.Type == 7)
        {
            Cubemap cubemapTex = new Cubemap((int)textureDims.x, format, true);
            cubemapTex.name = LevelTextures.TextureFilePaths[Entry.BINIndex];
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
            texture.name = LevelTextures.TextureFilePaths[Entry.BINIndex];
            texture.LoadRawTextureData(tempReader.ReadBytes(textureLength));
            texture.Apply();
            toReturn.texture = texture;
        }

        tempReader.Close();
        return toReturn;
    }

    public void SetLoadedTex(AlienTexture tex, string name)
    {
        selectedTex = tex;
        previewImg.sprite = Sprite.Create(selectedTex.texture, new Rect(0, 0, selectedTex.texture.width, selectedTex.texture.height), new Vector2(0.5f, 0.5f));
        previewText.text = name;
    }

    public void SaveLoadedTex()
    {
        if (selectedTex == null || !selectedTex.IsTexture) return; //todo

        byte[] itemBGBytes = selectedTex.texture.EncodeToPNG();
        File.WriteAllBytes("out.png", itemBGBytes);
    }
    /*
    public void ReplaceLoadedTex()
    {
        Texture2D texture = new Texture2D((int)textureDims[0], (int)textureDims[1], format, mipLevels, true);
        texture.name = LevelTextures.BIN.TextureFilePaths[Entry.BINIndex];
        texture.LoadRawTextureData(tempReader.ReadBytes(textureLength));
        texture.Apply();
        toReturn.texture = texture;
    }
    */
}
