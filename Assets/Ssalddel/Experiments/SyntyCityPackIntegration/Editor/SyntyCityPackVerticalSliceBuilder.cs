using System;
using System.Linq;
using Ssalddel.Unity.Samples.UrbanLogisticsCenter;
using Ssalddel.Unity.Samples.UrbanLogisticsCenter.Editor;
using Ssalddel.Unity.Samples.UrbanMarket;
using Ssalddel.Unity.Samples.UrbanMarket.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor.Experiments
{
    public static class SyntyCityPackVerticalSliceBuilder
    {
        public const string MarketScenePath =
            "Assets/Ssalddel/Experiments/SyntyCityPackIntegration/UrbanMarketCityPackVerticalSlice.unity";
        public const string LogisticsScenePath =
            "Assets/Ssalddel/Experiments/SyntyCityPackIntegration/UrbanLogisticsCityPackVerticalSlice.unity";

        private const string CityRoot = "Assets/Synty/PolygonCity/Prefabs/";
        private const string RepresentativePrefab =
            CityRoot + "Characters/Character_BusinessWoman.prefab";
        private const string ManagerPrefab =
            CityRoot + "Characters/Character_BusinessMan_Suit.prefab";
        private const string MarketPrefab = CityRoot + "Buildings/SM_Bld_Shop_05.prefab";
        private const string ApartmentPrefab = CityRoot + "Buildings/SM_Bld_Apartment_01.prefab";
        private const string LogisticsBuildingPrefab = CityRoot + "Buildings/SM_Bld_Station_03.prefab";
        private const string DeskPrefab = CityRoot + "Props/SM_Prop_ShopInterior_Desk_01.prefab";
        private const string ShelfPrefab = CityRoot + "Props/SM_Prop_ShopInterior_Shelf_01.prefab";
        private const string VanPrefab = CityRoot + "Vehicles/SM_Veh_Car_Van_01.prefab";
        private const string PalletPrefab = CityRoot + "Props/SM_Prop_Pallet_01.prefab";
        private const string BoxPrefab = CityRoot + "Props/SM_Prop_CardboardBox_01.prefab";

        [MenuItem("Ssalddel/City Pack/Build Urban Market Vertical Slice")]
        public static void BuildMarket()
        {
            EnsureAssets();
            도심마트ManagerPrimitiveSceneBuilder.CreateScene();
            var scene = SceneManager.GetActiveScene();
            var root = GameObject.Find("UrbanMarketManagerZone")
                ?? throw new InvalidOperationException("UrbanMarketManagerZoneMissing");
            var presentation = Group("CityPackPresentation", root.transform);

            var market = InstantiateAt(
                MarketPrefab,
                presentation.transform,
                "CityPackUrbanMarketVisualRoot",
                new Vector3(0f, 0f, 9.5f),
                new Vector3(0f, 180f, 0f),
                1.8f);
            market.isStatic = true;
            var apartments = InstantiateAt(
                ApartmentPrefab,
                presentation.transform,
                "CityPackResidentialCommunityVisualRoot",
                new Vector3(-11.5f, 0f, 8.5f),
                new Vector3(0f, 145f, 0f),
                1.05f);
            apartments.isStatic = true;

            ReplaceRepresentative();
            AddManager(presentation.transform);
            ReplaceDesk();
            ReplaceShelves();
            var deck = GameObject.Find("ResidentialGroupConceptCardDeck");
            if (deck != null) deck.transform.localScale = Vector3.one * .72f;

            SaveAs(scene, MarketScenePath);
            ValidateMarket();
            Debug.Log("[Ssalddel City Pack] Built market vertical slice: " + MarketScenePath);
        }

        [MenuItem("Ssalddel/City Pack/Validate Urban Market Vertical Slice")]
        public static void ValidateMarket()
        {
            var representative = UnityEngine.Object.FindFirstObjectByType<공동주택대표NpcView>();
            if (representative == null || !representative.ValidateWiring()
                || representative.VisualRoot == null
                || representative.VisualRoot.name != "CityPackRepresentativeVisualRoot")
                throw new InvalidOperationException("CityPackRepresentativeWiringInvalid");
            var animator = representative.VisualRoot.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                throw new InvalidOperationException("CityPackRepresentativeHumanoidAvatarMissing");
            if (GameObject.Find("CityPackUrbanMarketVisualRoot") == null
                || GameObject.Find("CityPackMarketManagerVisualRoot") == null
                || GameObject.Find("CityPackManagerDeskVisualRoot") == null
                || UnityEngine.Object.FindObjectsByType<도심마트ManagerShelfView>(
                    FindObjectsSortMode.None).Length != 2)
                throw new InvalidOperationException("CityPackMarketVisualSocketMissing");
            EnsureRenderersUseValidShaders(GameObject.Find("CityPackPresentation"));
            Debug.Log("[Ssalddel City Pack] Validated market VisualRoot, Humanoid avatar and Synty shaders.");
        }

        [MenuItem("Ssalddel/City Pack/Build Urban Logistics Vertical Slice")]
        public static void BuildLogistics()
        {
            EnsureAssets();
            도심물류센터PrimitiveSceneBuilder.CreateScene();
            var scene = SceneManager.GetActiveScene();
            var root = GameObject.Find("UrbanLogisticsCenterZone")
                ?? throw new InvalidOperationException("UrbanLogisticsCenterZoneMissing");
            var presentation = Group("CityPackLogisticsPresentation", root.transform);

            var overview = UnityEngine.Object.FindFirstObjectByType<LogisticsFacilityOverviewView>()
                ?? throw new InvalidOperationException("LogisticsFacilityOverviewMissing");
            NormalizeFacilityHeader(overview.BuildingVisualRoot.transform, presentation.transform);
            DisableOwnRenderer(overview.BuildingVisualRoot);
            var building = InstantiateAt(
                LogisticsBuildingPrefab,
                presentation.transform,
                "CityPackLogisticsBuildingVisualRoot",
                new Vector3(0f, 0f, 8f),
                new Vector3(0f, 180f, 0f),
                .4f);
            building.isStatic = true;

            var cargo = CreateCargoVisual(presentation.transform, overview.CargoVisualRoot.transform.position);
            overview.CargoVisualRoot.SetActive(false);
            SetSerializedReference(overview, "buildingVisualRoot", building);
            SetSerializedReference(overview, "cargoVisualRoot", cargo);

            var truck = UnityEngine.Object.FindFirstObjectByType<TransportCorridorTruckView>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("TransportCorridorTruckMissing");
            DisableOwnRenderer(truck.gameObject);
            var van = InstantiateAsWorldScale(
                VanPrefab,
                truck.transform,
                "CityPackTransportVehicleVisualRoot",
                truck.transform.position,
                new Vector3(0f, 90f, 0f),
                .9f);
            van.SetActive(true);
            truck.transform.position = new Vector3(-7f, .5f, -4f);
            truck.gameObject.SetActive(true);
            var truckCargo = truck.transform.Find("CargoVisualRoot");
            if (truckCargo != null)
            {
                DisableOwnRenderer(truckCargo.gameObject);
                InstantiateAsWorldScale(BoxPrefab, truckCargo, "CityPackTruckCargoVisualRoot",
                    truckCargo.position, Vector3.zero, .55f);
            }

            SaveAs(scene, LogisticsScenePath);
            ValidateLogistics();
            Debug.Log("[Ssalddel City Pack] Built logistics vertical slice: " + LogisticsScenePath);
        }

        [MenuItem("Ssalddel/City Pack/Validate Urban Logistics Vertical Slice")]
        public static void ValidateLogistics()
        {
            var overview = UnityEngine.Object.FindFirstObjectByType<LogisticsFacilityOverviewView>()
                ?? throw new InvalidOperationException("LogisticsFacilityOverviewMissing");
            var truck = UnityEngine.Object.FindFirstObjectByType<TransportCorridorTruckView>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("TransportCorridorTruckMissing");
            if (!overview.ValidateWiring() || !truck.ValidateWiring()
                || overview.BuildingVisualRoot.name != "CityPackLogisticsBuildingVisualRoot"
                || overview.CargoVisualRoot.name != "CityPackFacilityCargoVisualRoot"
                || truck.transform.Find("CityPackTransportVehicleVisualRoot") == null)
                throw new InvalidOperationException("CityPackLogisticsVisualSocketInvalid");
            EnsureRenderersUseValidShaders(GameObject.Find("CityPackLogisticsPresentation"));
            Debug.Log("[Ssalddel City Pack] Validated logistics building, cargo, van and Synty shaders.");
        }

        private static void ReplaceRepresentative()
        {
            var representative = UnityEngine.Object.FindFirstObjectByType<공동주택대표NpcView>()
                ?? throw new InvalidOperationException("ResidentialRepresentativeMissing");
            var wrapper = representative.gameObject;
            DisableOwnRenderer(wrapper);
            var primitiveAnimator = wrapper.GetComponent<Animator>()
                ?? throw new InvalidOperationException("RepresentativeAnimatorSocketMissing");
            var visual = InstantiateAt(
                RepresentativePrefab,
                wrapper.transform,
                "CityPackRepresentativeVisualRoot",
                wrapper.transform.position + new Vector3(0f, -1f, 0f),
                wrapper.transform.eulerAngles,
                1.5f);
            var animator = visual.GetComponentInChildren<Animator>(true)
                ?? throw new InvalidOperationException("CityPackRepresentativeAnimatorMissing");
            animator.runtimeAnimatorController = primitiveAnimator.runtimeAnimatorController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            primitiveAnimator.enabled = false;
            PoseCharacter(animator);
            SetSerializedReference(representative, "visualRoot", visual);
            SetSerializedReference(representative, "animator", animator);
        }

        private static void AddManager(Transform parent)
        {
            var manager = InstantiateAt(
                ManagerPrefab,
                parent,
                "CityPackMarketManagerVisualRoot",
                new Vector3(5.5f, 0f, -.25f),
                new Vector3(0f, 205f, 0f),
                1.5f);
            var animator = manager.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                throw new InvalidOperationException("CityPackManagerHumanoidAvatarMissing");
            PoseCharacter(animator);
        }

        private static void ReplaceDesk()
        {
            var wrapper = GameObject.Find("ManagerDeskVisualRoot")
                ?? throw new InvalidOperationException("ManagerDeskVisualRootMissing");
            DisableOwnRenderer(wrapper);
            InstantiateAsWorldScale(DeskPrefab, wrapper.transform, "CityPackManagerDeskVisualRoot",
                wrapper.transform.position, new Vector3(0f, 90f, 0f), 1.35f);
        }

        private static void ReplaceShelves()
        {
            foreach (var shelf in UnityEngine.Object.FindObjectsByType<도심마트ManagerShelfView>(
                         FindObjectsSortMode.None))
            {
                DisableOwnRenderer(shelf.gameObject);
                InstantiateAsWorldScale(
                    ShelfPrefab,
                    shelf.transform,
                    "CityPackShelfVisualRoot",
                    shelf.transform.position,
                    Vector3.zero,
                    1.25f);
            }
        }

        private static GameObject CreateCargoVisual(Transform parent, Vector3 position)
        {
            var root = Group("CityPackFacilityCargoVisualRoot", parent);
            root.transform.position = position;
            InstantiateAsWorldScale(PalletPrefab, root.transform, "PalletVisual", position,
                Vector3.zero, .75f);
            InstantiateAsWorldScale(BoxPrefab, root.transform, "CargoBoxVisual1",
                position + new Vector3(-.35f, .45f, 0f), Vector3.zero, .55f);
            InstantiateAsWorldScale(BoxPrefab, root.transform, "CargoBoxVisual2",
                position + new Vector3(.35f, .45f, 0f), new Vector3(0f, 90f, 0f), .55f);
            return root;
        }

        private static void NormalizeFacilityHeader(Transform placeholder, Transform parent)
        {
            var summary = placeholder.Find("FacilitySummary");
            if (summary != null)
            {
                summary.SetParent(parent, false);
                summary.position = new Vector3(0f, 4.7f, 4.5f);
                summary.localScale = Vector3.one;
            }
            var boundary = placeholder.Find("FacilityBoundary");
            if (boundary != null)
            {
                boundary.SetParent(parent, false);
                boundary.position = new Vector3(0f, 4.15f, 4.5f);
                boundary.localScale = Vector3.one;
            }
        }

        private static void PoseCharacter(Animator animator)
        {
            var left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var right = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (left != null) left.localRotation *= Quaternion.Euler(0f, 0f, 82f);
            if (right != null) right.localRotation *= Quaternion.Euler(0f, 0f, 82f);
        }

        private static void EnsureAssets()
        {
            foreach (var path in new[]
                     {
                         RepresentativePrefab, ManagerPrefab, MarketPrefab, ApartmentPrefab,
                         LogisticsBuildingPrefab, DeskPrefab, ShelfPrefab, VanPrefab,
                         PalletPrefab, BoxPrefab,
                     })
                LoadRequired(path);
        }

        private static GameObject InstantiateAt(
            string path,
            Transform parent,
            string name,
            Vector3 position,
            Vector3 euler,
            float scale)
        {
            var prefab = LoadRequired(path);
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject
                ?? throw new InvalidOperationException("CityPackPrefabInstantiationFailed:" + path);
            instance.name = name;
            instance.transform.SetParent(parent, true);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(euler);
            instance.transform.localScale = Vector3.one * scale;
            return instance;
        }

        private static GameObject InstantiateAsWorldScale(
            string path,
            Transform parent,
            string name,
            Vector3 position,
            Vector3 euler,
            float worldScale)
        {
            var instance = InstantiateAt(path, parent, name, position, euler, 1f);
            var scale = parent.lossyScale;
            instance.transform.localScale = new Vector3(
                SafeScale(worldScale, scale.x),
                SafeScale(worldScale, scale.y),
                SafeScale(worldScale, scale.z));
            return instance;
        }

        private static float SafeScale(float desired, float parentScale)
            => Mathf.Abs(parentScale) < .0001f ? desired : desired / parentScale;

        private static GameObject LoadRequired(string path)
            => AssetDatabase.LoadAssetAtPath<GameObject>(path)
               ?? throw new InvalidOperationException("RequiredCityPackPrefabMissing:" + path);

        private static GameObject Group(string name, Transform parent)
        {
            var value = new GameObject(name);
            value.transform.SetParent(parent, false);
            return value;
        }

        private static void DisableOwnRenderer(GameObject value)
        {
            var renderer = value.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
        }

        private static void SetSerializedReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)
                ?? throw new InvalidOperationException("SerializedPropertyMissing:" + propertyName);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureRenderersUseValidShaders(GameObject root)
        {
            if (root == null) throw new InvalidOperationException("CityPackPresentationRootMissing");
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("CityPackRendererMissing");
            foreach (var material in renderers.SelectMany(value => value.sharedMaterials).Where(value => value != null))
            {
                if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                    throw new InvalidOperationException("CityPackShaderInvalid:" + material.name);
            }
        }

        private static void SaveAs(Scene scene, string path)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new InvalidOperationException("CityPackSceneSaveFailed:" + path);
            AssetDatabase.SaveAssets();
        }
    }
}
