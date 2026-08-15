using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [CreateAssetMenu(
        fileName = "FigmaMauiWarehouseUiThemeCatalog",
        menuName = "Ssalddel/Figma MAUI Warehouse UI Theme")]
    public sealed class FigmaMauiWarehouseUiThemeCatalog : ScriptableObject
    {
        [SerializeField] private string designProfileRevision =
            진부Hub입고UiCodes.SupportedDesignProfileRevision;
        [SerializeField] private Color background = new Color(.973f, .965f, .953f, .98f);
        [SerializeField] private Color surface = new Color(1f, 1f, 1f, .98f);
        [SerializeField] private Color text = new Color(.149f, .129f, .118f, 1f);
        [SerializeField] private Color muted = new Color(.506f, .463f, .431f, 1f);
        [SerializeField] private Color border = new Color(.918f, .827f, .741f, 1f);
        [SerializeField] private Color warehouseAccent = new Color(.937f, .424f, 0f, 1f);
        [SerializeField] private Color warehouseAccentSoft = new Color(1f, .941f, .863f, 1f);
        [SerializeField] private Color preview = new Color(.125f, .486f, .565f, 1f);
        [SerializeField] private Color success = new Color(.18f, .49f, .20f, 1f);
        [SerializeField] private Color blocked = new Color(.78f, .16f, .14f, 1f);
        [SerializeField] private Color stale = new Color(.62f, .42f, .08f, 1f);

        public string DesignProfileRevision => designProfileRevision;
        public Color Background => background;
        public Color Surface => surface;
        public Color Text => text;
        public Color Muted => muted;
        public Color Border => border;
        public Color WarehouseAccent => warehouseAccent;
        public Color WarehouseAccentSoft => warehouseAccentSoft;
        public Color Preview => preview;
        public Color Success => success;

        public bool Supports(string revision) => designProfileRevision == revision;

        public Color ResolveState(string styleSemanticKey)
        {
            if (styleSemanticKey == "State.Completed") return success;
            if (styleSemanticKey == "State.Blocked" || styleSemanticKey == "State.Error")
                return blocked;
            if (styleSemanticKey == "State.Stale") return stale;
            if (styleSemanticKey == "State.Preview" || styleSemanticKey == "State.PreviewReady"
                || styleSemanticKey == "State.Active" || styleSemanticKey == "State.InProgress")
                return preview;
            return warehouseAccent;
        }

        public Color ResolveAction(string styleSemanticKey)
        {
            if (styleSemanticKey == "Action.Confirm") return warehouseAccent;
            if (styleSemanticKey == "Action.Preview") return preview;
            return muted;
        }
    }
}
