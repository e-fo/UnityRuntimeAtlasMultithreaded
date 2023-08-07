using DaVikingCode.RectanglePacking;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SetImage : MonoBehaviour
{
    public Image _image01;
    public Image _image02;
    AtlasAsset _atlas;
    Dictionary<string, Sprite> _sprites;

    async void Start()
    {
        Dictionary<string, byte[]> dict = new();

        dict.Add("A", Resources.Load<Texture2D>("A").EncodeToPNG());
        dict.Add("B", Resources.Load<Texture2D>("B").EncodeToPNG());

        _atlas = await RuntimeAtlasUtil.BuildAtlasAsync(dict, false, 128);
        _sprites = _atlas.GetSprites();

        _image01.sprite = _sprites["A"];
        _image02.sprite = _sprites["B"];
    }
}