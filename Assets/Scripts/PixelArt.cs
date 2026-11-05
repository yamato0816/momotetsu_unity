using System.Collections.Generic;
using UnityEngine;

namespace RailTrip3D
{
    // Procedural 2D sprites: no commercial game artwork or external assets are required.
    public static class PixelArt
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        public static Sprite Get(string kind, Color color)
        {
            string key = kind + ColorUtility.ToHtmlStringRGB(color);
            if (cache.TryGetValue(key, out var found)) return found;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var colors = new Color32[size * size];
            Color32 c = color, white = new Color32(255, 253, 232, 255), dark = new Color32(24, 35, 52, 255);
            void Set(int x, int y, Color32 v) { if (x >= 0 && y >= 0 && x < size && y < size) colors[y * size + x] = v; }
            void Box(int x, int y, int w, int h, Color32 v)
            { for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) Set(xx, yy, v); }
            if (kind == "train")
            {
                Box(4, 11, 24, 15, dark); Box(6, 13, 20, 11, c);
                Box(8, 18, 6, 5, white); Box(18, 18, 6, 5, white);
                Box(8, 7, 5, 5, dark); Box(19, 7, 5, 5, dark);
                Box(8, 8, 5, 3, white); Box(19, 8, 5, 3, white);
                Box(11, 26, 10, 3, dark); Box(13, 27, 6, 2, c);
            }
            else if (kind == "ghost")
            {
                for (int y = 7; y <= 25; y++)
                    for (int x = 5; x <= 26; x++)
                        if ((x - 15.5f) * (x - 15.5f) + (y - 20) * (y - 20) < 111 || (y < 19 && y > 7 && x >= 6 && x <= 25))
                            Set(x, y, c);
                Box(9, 17, 5, 6, dark); Box(19, 17, 5, 6, dark);
                Box(12, 9, 8, 3, dark);
            }
            else if (kind == "card")
            {
                Box(6, 3, 20, 26, dark); Box(8, 5, 16, 22, c);
                Box(11, 9, 10, 4, white); Box(13, 15, 6, 7, white);
            }
            else if (kind == "flag")
            {
                Box(8, 4, 3, 25, dark); Box(11, 17, 16, 11, c); Box(11, 17, 6, 3, white);
            }
            tex.SetPixels32(colors); tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 32, 0, SpriteMeshType.FullRect);
            cache[key] = sprite; return sprite;
        }
    }
}
