using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CATHODE.Textures;
using System.IO;

//THIS IS ALL TEMP FOR TESTING
public class TestTextureEd : MonoSingleton<TestTextureEd>
{
    [SerializeField] IconGrid appGrid;
    [SerializeField] UnityEngine.UI.Image previewImg;
    [SerializeField] TMPro.TextMeshProUGUI previewText;

    alien_textures LevelTextures;
    AlienTexture[] LoadedTexturesLevel;
    AlienTexture selectedTex = null;

    void Start()
    { 
        string levelPath = @"G:\SteamLibrary\steamapps\common\Alien Isolation\DATA\ENV\PRODUCTION\BSP_TORRENS\";
        LevelTextures = TexturePAK.Load(levelPath + "/RENDERABLE/LEVEL_TEXTURES.ALL.PAK", levelPath + "/RENDERABLE/LEVEL_TEXTURE_HEADERS.ALL.BIN");

        LoadedTexturesLevel = new AlienTexture[LevelTextures.BIN.Header.EntryCount];
        bool[] TextureLoadTrackerLevel = new bool[LevelTextures.BIN.Header.EntryCount];
        for (int x = 0; x < LevelTextures.PAK.Header.EntryCount; x++)
        {
            int binIndex = LevelTextures.PAK.Entries[x].BINIndex;
            LoadedTexturesLevel[binIndex] = LoadTexture(x, !TextureLoadTrackerLevel[binIndex]);
            TextureLoadTrackerLevel[binIndex] = true;
        }

        appGrid.GenerateGrid(LoadedTexturesLevel.Length);
        int i = 0;
        foreach (LoadedTextureUI app in appGrid.transform.GetComponentsInChildren<LoadedTextureUI>(true))
        {
            app.Setup(LoadedTexturesLevel[i], LevelTextures.BIN.TextureFilePaths[i]);
            i++;
        }
        //scrollRect.normalizedPosition = new Vector2(0, 1);
    }

    private AlienTexture LoadTexture(int EntryIndex, bool loadV1 = true)
    {
        AlienTexture toReturn = new AlienTexture();

        if (EntryIndex < 0 || EntryIndex >= LevelTextures.PAK.Header.EntryCount)
        {
            Debug.LogWarning("Asked to load texture at index " + EntryIndex + ", which is out of bounds!");
            return null;
        }

        alien_pak_entry Entry = LevelTextures.PAK.Entries[EntryIndex];
        alien_texture_bin_texture InTexture = LevelTextures.BIN.Textures[Entry.BINIndex];

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

        BinaryReader tempReader = new BinaryReader(new MemoryStream(LevelTextures.PAK.DataStart));
        tempReader.BaseStream.Position = Entry.Offset;

        if (InTexture.Type == 7)
        {
            Cubemap cubemapTex = new Cubemap((int)textureDims.x, format, true);
            cubemapTex.name = LevelTextures.BIN.TextureFilePaths[Entry.BINIndex];
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
            texture.name = LevelTextures.BIN.TextureFilePaths[Entry.BINIndex];
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
