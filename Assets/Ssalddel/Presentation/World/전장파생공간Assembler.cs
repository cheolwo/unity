using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Battles;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 서버가 확정한 BattleLocalMeters 전장 계획만 독립 공간으로 조립한다.
    /// H5 좌표를 확대하거나 전투 결과를 계산하지 않는다.
    /// </summary>
    public sealed class 전장파생공간Assembler
    {
        public GameObject Build(BattleInstanceApiModel battle, Transform parent)
        {
            if (battle == null) throw new ArgumentNullException(nameof(battle));
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            Validate(battle);
            var root = new GameObject("Battlefield_" + SafeName(battle.BattleStableId));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            try
            {
                BuildTerrain(battle.BattlefieldDerivation.BattlefieldPlan, root.transform);
                BuildAnchors(battle.BattlefieldDerivation, root.transform);
                BuildZones(battle.BattlefieldDerivation.BattlefieldPlan, root.transform);
                BuildUnits(battle.UnitRoster, root.transform);
                return root;
            }
            catch
            {
                Destroy(root);
                throw;
            }
        }

        private static void Validate(BattleInstanceApiModel battle)
        {
            var derivation = battle.BattlefieldDerivation;
            var plan = derivation?.BattlefieldPlan;
            if (!battle.SimulationOnly || battle.IsOperationalState
                || derivation == null || !derivation.CanConfirm
                || derivation.BlockingReasonCodes == null
                || derivation.BlockingReasonCodes.Length > 0
                || derivation.WorldContext == null
                || !derivation.WorldContext.SimulationOnly
                || derivation.WorldContext.IsOperationalState
                || string.IsNullOrWhiteSpace(derivation.WorldContext.ContextHashSha256)
                || plan == null || !plan.SimulationOnly || plan.IsOperationalState
                || plan.CoordinateSpaceCode != BattlefieldPresentationCodes.BattleLocalMeters
                || plan.ValidationCodes == null || plan.ValidationCodes.Length > 0
                || plan.WidthMeters <= 0d || plan.DepthMeters <= 0d
                || plan.GridCellSizeMeters <= 0d
                || string.IsNullOrWhiteSpace(plan.BattlefieldPlanHashSha256)
                || battle.UnitRoster == null || battle.UnitRoster.Units == null)
                throw new InvalidOperationException("BattlefieldPresentationBoundaryInvalid");
        }

        private static void BuildTerrain(BattlefieldPlanApiModel plan, Transform parent)
        {
            var cells = plan.TerrainCells ?? Array.Empty<BattlefieldTerrainCellApiModel>();
            var vertices = new List<Vector3>(cells.Length * 4);
            var triangles = new List<int>(cells.Length * 6);
            var colors = new List<Color>(cells.Length * 4);
            var halfWidth = (float)plan.WidthMeters * 0.5f;
            var halfDepth = (float)plan.DepthMeters * 0.5f;
            var size = (float)plan.GridCellSizeMeters;
            foreach (var cell in cells.OrderBy(value => value.CellZ)
                         .ThenBy(value => value.CellX))
            {
                var x = -halfWidth + cell.CellX * size;
                var z = -halfDepth + cell.CellZ * size;
                var y = cell.HeightCentimeters / 100f;
                var index = vertices.Count;
                vertices.Add(new Vector3(x, y, z));
                vertices.Add(new Vector3(x + size, y, z));
                vertices.Add(new Vector3(x + size, y, z + size));
                vertices.Add(new Vector3(x, y, z + size));
                triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 1);
                triangles.Add(index); triangles.Add(index + 3); triangles.Add(index + 2);
                var color = TerrainColor(cell.TerrainCode, cell.Walkable);
                colors.Add(color); colors.Add(color); colors.Add(color); colors.Add(color);
            }
            var terrain = new GameObject("전장지형");
            terrain.transform.SetParent(parent, false);
            var mesh = new Mesh
            {
                name = "서버전장지형Mesh",
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colors);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = terrain.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color");
            if (shader != null) renderer.sharedMaterial = new Material(shader)
            {
                name = "전장지형표시Material",
                color = Color.white,
            };
            terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void BuildAnchors(BattlefieldDerivationApiModel derivation,
            Transform parent)
        {
            var root = new GameObject("전장기억지점");
            root.transform.SetParent(parent, false);
            var definitions = derivation.WorldContext.Anchors.ToDictionary(
                value => value.BattlefieldAnchorStableId, StringComparer.Ordinal);
            foreach (var placement in derivation.BattlefieldPlan.AnchorPlacements
                         .OrderBy(value => value.BattlefieldAnchorStableId,
                             StringComparer.Ordinal))
            {
                if (!definitions.TryGetValue(placement.BattlefieldAnchorStableId,
                        out var definition))
                    throw new InvalidOperationException("BattlefieldAnchorDefinitionMissing");
                var view = GameObject.CreatePrimitive(PrimitiveType.Cube);
                view.name = "기억지점_" + SafeName(placement.BattlefieldAnchorStableId);
                view.transform.SetParent(root.transform, false);
                ApplyPose(view.transform, placement.BattlePose);
                view.transform.localScale = new Vector3(
                    Math.Max(2f, (float)placement.WidthMeters), 4f,
                    Math.Max(2f, (float)placement.DepthMeters));
                view.AddComponent<전장파생표시Tag>().Configure(
                    placement.BattlefieldAnchorStableId, definition.SourceStableId,
                    definition.WorldEffectTargetStableId,
                    definition.PreservationPolicyCode, "Anchor");
            }
        }

        private static void BuildZones(BattlefieldPlanApiModel plan, Transform parent)
        {
            var root = new GameObject("전술구역");
            root.transform.SetParent(parent, false);
            foreach (var zone in plan.Zones.OrderBy(value => value.ZoneStableId,
                         StringComparer.Ordinal))
            {
                var view = GameObject.CreatePrimitive(PrimitiveType.Cube);
                view.name = "전술구역_" + SafeName(zone.ZoneStableId);
                view.transform.SetParent(root.transform, false);
                ApplyPose(view.transform, zone.CenterPose);
                view.transform.localPosition += Vector3.up * 0.1f;
                view.transform.localScale = new Vector3((float)zone.WidthMeters,
                    0.2f, (float)zone.DepthMeters);
                var collider = view.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
                view.AddComponent<전장파생표시Tag>().Configure(zone.ZoneStableId,
                    zone.SourceAnchorStableId, string.Empty, string.Empty, "Zone");
            }
        }

        private static void BuildUnits(BattleUnitRosterApiModel roster, Transform parent)
        {
            var root = new GameObject("전투부대");
            root.transform.SetParent(parent, false);
            foreach (var unit in roster.Units.OrderBy(value => value.UnitStableId,
                         StringComparer.Ordinal))
            {
                var view = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                view.name = "부대_" + SafeName(unit.UnitStableId);
                view.transform.SetParent(root.transform, false);
                ApplyPose(view.transform, unit.InitialPose);
                view.AddComponent<전장파생표시Tag>().Configure(unit.UnitStableId,
                    string.Join(",", unit.MemberActorStableIds), string.Empty,
                    unit.SideCode, "Unit");
            }
        }

        private static void ApplyPose(Transform target, BattleSpatialPoseApiModel pose)
        {
            if (pose == null || pose.CoordinateSpaceCode !=
                BattlefieldPresentationCodes.BattleLocalMeters)
                throw new InvalidOperationException("BattlefieldPoseSpaceInvalid");
            target.localPosition = new Vector3((float)pose.XMeters, 0f,
                (float)pose.ZMeters);
            target.localRotation = Quaternion.Euler(0f,
                (float)pose.RotationDegrees, 0f);
        }

        private static Color TerrainColor(string code, bool walkable)
        {
            if (!walkable) return new Color(0.18f, 0.20f, 0.22f);
            if (code != null && code.IndexOf("Forest", StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.20f, 0.36f, 0.18f);
            if (code != null && code.IndexOf("Farm", StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.45f, 0.36f, 0.20f);
            return new Color(0.32f, 0.45f, 0.25f);
        }

        private static string SafeName(string value)
            => (value ?? string.Empty).Replace(':', '_').Replace('/', '_');

        private static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }

    [DisallowMultipleComponent]
    public sealed class 전장파생표시Tag : MonoBehaviour
    {
        [SerializeField] private string battleStableId = string.Empty;
        [SerializeField] private string sourceStableId = string.Empty;
        [SerializeField] private string worldEffectTargetStableId = string.Empty;
        [SerializeField] private string policyCode = string.Empty;
        [SerializeField] private string kindCode = string.Empty;

        public string BattleStableId => battleStableId;
        public string SourceStableId => sourceStableId;
        public string WorldEffectTargetStableId => worldEffectTargetStableId;
        public string PolicyCode => policyCode;
        public string KindCode => kindCode;

        public void Configure(string battleId, string sourceId, string targetId,
            string policy, string kind)
        {
            battleStableId = battleId ?? string.Empty;
            sourceStableId = sourceId ?? string.Empty;
            worldEffectTargetStableId = targetId ?? string.Empty;
            policyCode = policy ?? string.Empty;
            kindCode = kind ?? string.Empty;
        }
    }
}
