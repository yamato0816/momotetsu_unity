using UnityEngine;

namespace RailTrip3D
{
    // Select this object in the Scene/Hierarchy to move the station or change properties.
    [DisallowMultipleComponent]
    public sealed class SceneRailNode : MonoBehaviour
    {
        [Min(0)] public int nodeId;
        public string stationName;
        public TileKind kind;
        [Min(0)] public int price = 200;
        [Min(0)] public int income = 40;
        public bool overWater;
        public Renderer tileRenderer;
        public Transform flatNamePlate;
        public TextMesh labelText;

        public void RefreshLabel()
        {
            if (flatNamePlate != null)
                flatNamePlate.gameObject.SetActive(!string.IsNullOrWhiteSpace(stationName));
            if (labelText != null)
            {
                labelText.text = GameLanguage.City(stationName);
                Font font = GameLanguage.JapaneseFont;
                if (font == null)
                {
                    try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                    catch (System.Exception) { /* Missing fallback remains blank, not an error. */ }
                }
                if (font != null)
                {
                    labelText.font = font;
                    var renderer = labelText.GetComponent<MeshRenderer>();
                    if (renderer != null) renderer.sharedMaterial = font.material;
                }
            }
        }
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += SafeRefresh;
        }
        private void SafeRefresh()
        {
            if (this == null || Application.isPlaying) return;
            RefreshLabel();
        }
#endif
    }

}
