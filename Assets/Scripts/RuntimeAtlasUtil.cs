using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using DaVikingCode.AssetPacker;
using System.Text;
using SkiaSharp;

namespace DaVikingCode.RectanglePacking
{
    public class AtlasPixelsInfo
    {
        public TextureAsset[] AtlasData;
        public Color32[] Pixels;
    }

    public class AtlasAsset
    {
        /// <summary>
        /// You can create hash from array of keys and comparing with SpritesHash of atlas to find out is this atlas contains all keys or not
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        public static int CreateHash(string[] keys)
        {
            Array.Sort(keys);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < keys.Length; ++i)
            {
                sb.Append(keys[i]);
            }
            return sb.ToString().GetHashCode();
        }


        public Texture2D Atlas;
        public TextureAsset[] AtlasMetaData;
        public readonly int SpritesHash;

        public AtlasAsset(Texture2D atlas, TextureAsset[] metaData)
        {
            Atlas = atlas;
            AtlasMetaData = metaData;
            var names = AtlasMetaData.Select(x => x.name).ToArray();
            SpritesHash = CreateHash(names);
        }

        public string[] GetAllSpriteNames()
        {
            return AtlasMetaData.Select(x => x.name).ToArray();
        }

        public Dictionary<string, Sprite> GetSprites()
        {
            Dictionary<string, Sprite> ret = new();
            foreach (var t in AtlasMetaData)
            {
                ret.Add(
                    t.name,
                    Sprite.Create(
                    Atlas,
                    new Rect(t.x, t.y, t.width, t.height),
                    Vector2.zero,
                    t.pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect)
                );
            }
            return ret;
        }
    }

    public class RuntimeAtlasUtil
    {
        public static async Task<AtlasAsset> BuildAtlasAsync(Dictionary<string, byte[]> texturesMap, bool allow4096Textures, int pixelsPerUnit)
        {
            if (texturesMap.Count == 0)
            {
                Debug.LogError($"You can't build atlas texture for 0 elements of {nameof(texturesMap)} argument");
            }

            int textureSize = allow4096Textures ? 4096 : 2048;

            Texture2D atlas = new Texture2D(textureSize, textureSize, TextureFormat.ARGB32, false);
            var atlasPixelsInfo = await Task.Run(() => BuildAtlasPixels(texturesMap, allow4096Textures, pixelsPerUnit));

            atlas.SetPixels32(atlasPixelsInfo.Pixels);
            atlas.Apply();

            return new AtlasAsset(atlas, atlasPixelsInfo.AtlasData);
        }

        /// <summary>
        /// You can run this function on another thread because it doesn't use UnityEngine API
        /// </summary>
        /// <param name="texturesMap"></param>
        /// <param name="allow4096Textures"></param>
        /// <param name="pixelsPerUnit"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static AtlasPixelsInfo BuildAtlasPixels(Dictionary<string, byte[]> texturesMap, bool allow4096Textures, int pixelsPerUnit)
        {
            AtlasPixelsInfo ret = new AtlasPixelsInfo();
            var keys = texturesMap.Keys.ToList();
            List<TextureAsset> atlasMetaData = new List<TextureAsset>();

            List<Rect> rects = new List<Rect>();
            int atlasSize = allow4096Textures ? 4096 : 2048;
            var texturesData = texturesMap.Values.ToArray();

            const int padding = 1;
            RectanglePacker packer = new RectanglePacker(atlasSize, atlasSize, padding);

            List<Color32[]> colorData = new();
            for (int i = 0; i < texturesData.Length; ++i)
            {
                using (MemoryStream ms = new MemoryStream(texturesData[i]))
                {
                    using (SKBitmap image = SKBitmap.Decode(ms))
                    {
                        colorData.Add(GetPixelData(image));
                        if (image.Width > atlasSize || image.Height > atlasSize)
                            throw new Exception("A texture size is bigger than the sprite sheet size!");
                        else
                            packer.insertRectangle(image.Width, image.Height, i);
                    }

                }
            }

            packer.packRectangles();

            if (packer.rectangleCount > 0)
            {
                IntegerRectangle rect = new IntegerRectangle();
                Color32[] atlasPixels = new Color32[atlasSize * atlasSize];

                for (int j = 0; j < packer.rectangleCount; j++)
                {
                    rect = packer.getRectangle(j, rect);

                    int index = packer.getRectangleId(j);

                    var pixels = texturesData[index];

                    CopyPixelsToAtlas(atlasPixels, atlasSize, atlasSize, rect, colorData[index]);

                    TextureAsset textureAsset = new TextureAsset
                    {
                        x = rect.x,
                        y = rect.y,
                        width = rect.width,
                        height = rect.height,
                        name = keys[index],
                        pixelsPerUnit = pixelsPerUnit
                    };
                    atlasMetaData.Add(textureAsset);
                }
                ret.AtlasData = atlasMetaData.ToArray();
                ret.Pixels = atlasPixels;
            }
            return ret;
        }

        public static Color32[] GetPixelData(SKBitmap image)
        {
            int width = image.Width;
            int height = image.Height;

            SKBitmap rotatedImage = new SKBitmap(image.Width, image.Height);
            using (SKCanvas canvas = new SKCanvas(rotatedImage))
            {
                canvas.Scale(-1, 1, image.Width / 2f, image.Height / 2f);
                canvas.RotateDegrees(180, image.Width / 2f, image.Height / 2f);
                canvas.DrawBitmap(image, 0, 0);
            }

            Color32[] pixelData = new Color32[width * height];

            using (SKImage img = SKImage.FromBitmap(rotatedImage))
            using (SKPixmap pixmap = img.PeekPixels())
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * width + x;
                        SKColor pixelColor = pixmap.GetPixelColor(x, y);

                        pixelData[index] = new Color32
                        {
                            r = (byte)pixelColor.Red,
                            g = (byte)pixelColor.Green,
                            b = (byte)pixelColor.Blue,
                            a = (byte)pixelColor.Alpha
                        };
                    }
                }
            }

            return pixelData;
        }

        private static void CopyPixelsToAtlas(Color32[] atlasColors, int atlasWidth, int atlasHeight, IntegerRectangle rect, Color32[] pixels)
        {
            int startIdx = rect.x + rect.y * atlasWidth;
            int endIdx = startIdx + rect.width + (rect.height - 1) * atlasWidth;
            int pixelIdx = 0;

            for (int y = startIdx; y <= endIdx; y += atlasWidth)
            {
                for (int x = y; x < y + rect.width; x++)
                {
                    atlasColors[x] = pixels[pixelIdx];
                    pixelIdx++;
                }
            }
        }
    }
}
