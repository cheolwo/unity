using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 공간WorldPipelineView : MonoBehaviour
    {
        [SerializeField] private WorldBuildRecipe recipe = null!;
        [SerializeField] private 토지피복CompositionProfile compositionProfile = null!;
        [SerializeField] private WorldBuildManifest manifest = null!;
        [SerializeField] private Transform tileLayerRoot = null!;
        [SerializeField] private Transform areaRoot = null!;
        [SerializeField] private Transform areaSetRoot = null!;
        [SerializeField] private Transform referenceTileRoot = null!;
        [SerializeField] private string finalVisualCatalogRevision = string.Empty;
        [SerializeField] private int finalVisualBindingCount;
        [SerializeField] private int finalVisualRejectedCount;

        public WorldBuildRecipe Recipe => recipe;
        public 토지피복CompositionProfile CompositionProfile => compositionProfile;
        public WorldBuildManifest Manifest => manifest;
        public Transform TileLayerRoot => tileLayerRoot;
        public Transform AreaRoot => areaRoot;
        public Transform AreaSetRoot => areaSetRoot;
        public Transform ReferenceTileRoot => referenceTileRoot;
        public string FinalVisualCatalogRevision => finalVisualCatalogRevision;
        public int FinalVisualBindingCount => finalVisualBindingCount;
        public int FinalVisualRejectedCount => finalVisualRejectedCount;

        public void Configure(
            WorldBuildRecipe buildRecipe,
            토지피복CompositionProfile profile,
            WorldBuildManifest buildManifest,
            Transform tiles,
            Transform areas,
            Transform areaSet,
            Transform referenceTile,
            string visualCatalogRevision,
            int visualBindingCount,
            int visualRejectedCount)
        {
            recipe = buildRecipe;
            compositionProfile = profile;
            manifest = buildManifest;
            tileLayerRoot = tiles;
            areaRoot = areas;
            areaSetRoot = areaSet;
            referenceTileRoot = referenceTile;
            finalVisualCatalogRevision = visualCatalogRevision;
            finalVisualBindingCount = visualBindingCount;
            finalVisualRejectedCount = visualRejectedCount;
        }

        public bool ValidateWiring()
        {
            if (recipe == null || compositionProfile == null || manifest == null
                || tileLayerRoot == null || areaRoot == null || areaSetRoot == null
                || referenceTileRoot == null
                || !tileLayerRoot.IsChildOf(transform)
                || !areaRoot.IsChildOf(transform)
                || !areaSetRoot.IsChildOf(transform)
                || !referenceTileRoot.IsChildOf(transform))
                return false;
            compositionProfile.Validate();
            WorldBuildManifestValidator.Validate(manifest);
            return recipe.CalculateHash() == manifest.RecipeHash
                && compositionProfile.CalculateHash() == manifest.CompositionProfileHash
                && recipe.PhysicalElevation.UsedForSlope
                && recipe.PhysicalElevation.UsedForPlacementEligibility
                && recipe.PhysicalElevation.UsedForHydrology
                && recipe.VisualElevation.PresentationOnly
                && !string.IsNullOrWhiteSpace(finalVisualCatalogRevision)
                && finalVisualBindingCount > 0
                && finalVisualRejectedCount == 0;
        }
    }
}
