using System;
using System.Collections.Generic;
using UnityEngine;

namespace RailTrip3D
{
    /// <summary>
    /// Japanese text is enabled only when a font supporting its glyphs is available.
    /// WebGL can ship a user-provided Resources/Fonts/GameUIFont asset.
    /// No third-party font is redistributed in this repository.
    /// </summary>
    public static class GameLanguage
    {
        private static Font cachedJapanese;
        private static bool searched;
        private static readonly Dictionary<string,string> Cities = new Dictionary<string,string>
        {
            ["Sapporo"]="札幌",["Hakodate"]="函館",["Aomori"]="青森",["Morioka"]="盛岡",
            ["Sendai"]="仙台",["Fukushima"]="福島",["Tokyo"]="東京",["Chiba"]="千葉",
            ["Yokohama"]="横浜",["Shizuoka"]="静岡",["Nagoya"]="名古屋",["Kyoto"]="京都",
            ["Osaka"]="大阪",["Kobe"]="神戸",["Himeji"]="姫路",["Okayama"]="岡山",
            ["Hiroshima"]="広島",["Yamaguchi"]="山口",["Shimonoseki"]="下関",
            ["Fukuoka"]="福岡",["Kumamoto"]="熊本",["Kagoshima"]="鹿児島",
            ["Nagasaki"]="長崎",["Niigata"]="新潟",["Nagano"]="長野",
            ["Toyama"]="富山",["Kanazawa"]="金沢",["Fukui"]="福井",
            ["Tottori"]="鳥取",["Matsue"]="松江",["Takamatsu"]="高松",
            ["Tokushima"]="徳島",["Kochi"]="高知",["Matsuyama"]="松山"
        };
        public static Font JapaneseFont
        {
            get
            {
                if (searched) return cachedJapanese;
                searched = true;
                cachedJapanese = Resources.Load<Font>("Fonts/GameUIFont");
#if !UNITY_WEBGL
                if (cachedJapanese == null)
                {
                    string[] installed;
                    try { installed = Font.GetOSInstalledFontNames(); }
                    catch (Exception) { installed = Array.Empty<string>(); }
                    foreach (string candidate in new[] { "Yu Gothic", "Meiryo", "Noto Sans CJK JP" })
                    {
                        if (Array.Exists(installed, name =>
                            name.IndexOf(candidate,StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            try { cachedJapanese = Font.CreateDynamicFontFromOSFont(candidate,22); }
                            catch (Exception) { cachedJapanese = null; }
                            if (cachedJapanese != null) break;
                        }
                    }
                }
#endif
                return cachedJapanese;
            }
        }
        public static bool IsJapanese => JapaneseFont != null;
        public static string City(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name ?? "";
            return IsJapanese && Cities.TryGetValue(name,out var translated) ? translated : name;
        }
    }
}
