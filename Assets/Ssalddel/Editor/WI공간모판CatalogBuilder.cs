using System;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    internal readonly struct WI공간모판CatalogBuildResult
    {
        internal WI공간모판CatalogBuildResult(WI공간모판VisualCatalog catalog, bool changed)
        {
            Catalog = catalog;
            Changed = changed;
        }

        internal WI공간모판VisualCatalog Catalog { get; }
        internal bool Changed { get; }
    }

    internal static class WI공간모판CatalogBuilder
    {
        internal static WI공간모판CatalogBuildResult Build(string visualCatalogPath)
        {
            var sourceRoot = WI공간모판AuthoringSource.AuthoritativeRoot();
            var sourceCatalogPath = System.IO.Path.Combine(sourceRoot, "catalog.json");
            var sourceCatalog =
                WI공간모판AuthoringSource.ReadJson<WI공간모판SourceCatalog>(sourceCatalogPath);
            if (!sourceCatalog.PresentationOnly || sourceCatalog.DefinitionRefs.Length != 5)
                throw new InvalidOperationException("WiSpatialSeedbedSourceCatalogInvalid");

            var compositionCatalog = WI공간모판AuthoringSource.Required<공간문법CompositionCatalog>(
                공간문법CompositionCatalogBuilder.CatalogPath);
            var bindingCatalog = WI공간모판AuthoringSource.Required<공간문법SyntyBindingCatalog>(
                공간문법SyntyBindingCatalogBuilder.CatalogPath);
            compositionCatalog.Validate();
            bindingCatalog.Validate();

            if (!string.Equals(sourceCatalog.LandscapeGrammarRevision,
                    compositionCatalog.CatalogRevision, StringComparison.Ordinal))
                throw new InvalidOperationException("WiSpatialSeedbedLandscapeGrammarRevisionMismatch");

            var entries = sourceCatalog.DefinitionRefs.Select(definitionRef =>
            {
                WI공간모판AuthoringSource.ValidateRelativeJsonPath(definitionRef);
                var path = System.IO.Path.Combine(sourceRoot,
                    definitionRef.Replace('/', System.IO.Path.DirectorySeparatorChar));
                return BuildEntry(
                    WI공간모판AuthoringSource.ReadJson<WI공간모판SourceDefinition>(path),
                    WI공간모판AuthoringSource.Sha256(path), compositionCatalog, bindingCatalog);
            }).ToArray();

            var sourceCatalogHash = WI공간모판AuthoringSource.Sha256(sourceCatalogPath);
            var compositionHash = compositionCatalog.BuildSafeCatalogHashSha256();
            var bindingHash = bindingCatalog.BuildBindingHashSha256();
            WI공간모판AuthoringSource.EnsureAssetFolder(
                System.IO.Path.GetDirectoryName(visualCatalogPath)!.Replace('\\', '/'));
            var asset = AssetDatabase.LoadAssetAtPath<WI공간모판VisualCatalog>(visualCatalogPath);
            var changed = asset == null || !IsCurrent(asset, sourceCatalog,
                sourceCatalogHash, compositionCatalog, compositionHash,
                bindingCatalog, bindingHash, entries);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<WI공간모판VisualCatalog>();
                AssetDatabase.CreateAsset(asset, visualCatalogPath);
            }
            if (changed)
            {
                asset.Configure(
                    sourceCatalog.Revision,
                    sourceCatalogHash,
                    sourceCatalog.WorldInteractionCatalogRevision,
                    sourceCatalog.LandscapeGrammarRevision,
                    compositionCatalog.CatalogRevision,
                    compositionHash,
                    bindingCatalog.BindingRevision,
                    bindingHash,
                    entries);
                asset.Validate();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }
            else
            {
                asset.Validate();
            }
            return new WI공간모판CatalogBuildResult(asset, changed);
        }

        private static bool IsCurrent(
            WI공간모판VisualCatalog asset,
            WI공간모판SourceCatalog sourceCatalog,
            string sourceCatalogHash,
            공간문법CompositionCatalog compositionCatalog,
            string compositionHash,
            공간문법SyntyBindingCatalog bindingCatalog,
            string bindingHash,
            WI공간모판VisualEntry[] entries)
        {
            if (!string.Equals(asset.SourceCatalogRevision, sourceCatalog.Revision, StringComparison.Ordinal)
                || !string.Equals(asset.SourceCatalogHashSha256, sourceCatalogHash, StringComparison.Ordinal)
                || !string.Equals(asset.WorldInteractionCatalogRevision,
                    sourceCatalog.WorldInteractionCatalogRevision, StringComparison.Ordinal)
                || !string.Equals(asset.LandscapeGrammarRevision,
                    sourceCatalog.LandscapeGrammarRevision, StringComparison.Ordinal)
                || !string.Equals(asset.UnityCompositionCatalogRevision,
                    compositionCatalog.CatalogRevision, StringComparison.Ordinal)
                || !string.Equals(asset.UnityCompositionCatalogHashSha256,
                    compositionHash, StringComparison.Ordinal)
                || !string.Equals(asset.SyntyBindingRevision,
                    bindingCatalog.BindingRevision, StringComparison.Ordinal)
                || !string.Equals(asset.SyntyBindingHashSha256, bindingHash, StringComparison.Ordinal)
                || asset.Entries.Count != entries.Length)
                return false;

            for (var index = 0; index < entries.Length; index++)
            {
                if (!string.Equals(asset.Entries[index].StableId, entries[index].StableId,
                        StringComparison.Ordinal)
                    || !string.Equals(asset.Entries[index].SourceDefinitionHashSha256,
                        entries[index].SourceDefinitionHashSha256, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static WI공간모판VisualEntry BuildEntry(
            WI공간모판SourceDefinition definition,
            string sourceHash,
            공간문법CompositionCatalog compositionCatalog,
            공간문법SyntyBindingCatalog bindingCatalog)
        {
            if (!definition.PresentationOnly || definition.IsOperationalState
                || definition.ReviewStatusCode != "ApprovedForSimulation")
                throw new InvalidOperationException(
                    "WiSpatialSeedbedDefinitionBoundaryInvalid:" + definition.StableId);

            var spaces = definition.InternalSpaces.Select(sourceSpace =>
            {
                var capacities = sourceSpace.BaseCapacities.Select(value =>
                {
                    var item = new WI공간모판CapacityView();
                    item.Configure(value.CapacityCode, value.Quantity, value.UnitCode);
                    return item;
                }).ToArray();
                var candidates = sourceSpace.AllowedLandscapeCompositionKeys.Select(key =>
                {
                    var grammar = compositionCatalog.Resolve(key);
                    var binding = bindingCatalog.Resolve(key);
                    var item = new WI공간모판CandidateView();
                    item.Configure(key, binding.Candidate.SourceCompositionKey,
                        grammar.TopologyCode, grammar.Descriptor.Footprint,
                        binding.Candidate.Prefab);
                    return item;
                }).ToArray();
                var space = new WI공간모판SpaceView();
                space.Configure(sourceSpace.SpaceCode, sourceSpace.SpatialRoleCode,
                    sourceSpace.CapabilityCodes, capacities, candidates);
                return space;
            }).ToArray();
            var relations = definition.InternalRelations.Select(value =>
            {
                var relation = new WI공간모판RelationView();
                relation.Configure(value.RelationCode, value.FromSpaceCode,
                    value.ToSpaceCode, value.ConnectorTypeCode);
                return relation;
            }).ToArray();
            var connectors = definition.ExternalConnectorStubs.Select(value =>
            {
                var connector = new WI공간모판ConnectorView();
                connector.Configure(value.StubCode, value.InternalSpaceCode,
                    value.ConnectorTypeCode, value.FlowDirectionCode,
                    value.AdjacentWorldInteractionId);
                return connector;
            }).ToArray();
            var transform = definition.TransformConstraint;
            var entry = new WI공간모판VisualEntry();
            entry.Configure(definition.StableId, definition.Revision, definition.Title,
                definition.Summary, definition.IncludedWiIds, spaces, relations, connectors,
                new Vector2(transform.MinimumWidthMeters, transform.MinimumDepthMeters),
                new Vector2(transform.PreferredWidthMeters, transform.PreferredDepthMeters),
                new Vector2(transform.MaximumWidthMeters, transform.MaximumDepthMeters), sourceHash);
            if (!entry.Validate())
                throw new InvalidOperationException(
                    "WiSpatialSeedbedVisualEntryInvalid:" + definition.StableId);
            return entry;
        }
    }
}
