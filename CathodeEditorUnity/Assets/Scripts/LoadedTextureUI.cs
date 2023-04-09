/*
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LoadedTextureUI : MonoBehaviour
{
    [SerializeField] UnityEngine.UI.Image texturePreview;
    [SerializeField] TextMeshProUGUI textureName;

    AlienTexture thisTex = null;

    public void Setup(AlienTexture preview, string name)
    {
        if (!preview.IsTexture) return; //TODO: support cubemap etc
        thisTex = preview;

        texturePreview.sprite = Sprite.Create(preview.texture, new Rect(0, 0, preview.texture.width, preview.texture.height), new Vector2(0.5f, 0.5f));
        textureName.text = name;

        GetComponentInChildren<UnityEngine.UI.Button>().onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        TestTextureEd.instance.SetLoadedTex(thisTex, textureName.text);
    }
}
*/