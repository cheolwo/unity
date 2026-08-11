using System;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class FarmHeroShowcaseBuilder
    {
        public const string ScenePath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/농장대표풍경전시.unity";
        public const string VolumeProfilePath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Profiles/FarmHeroVolumeProfile.asset";
        public const string RootName = "FARM-HERO Art Pass";

        private const string FarmRoot = "Assets/Synty/PolygonFarm/Prefabs/";
        private const string FarmFocus = "camera-focus:zone.farm-production";

        [MenuItem("Ssalddel/SHOWCASE/Build Farm Hero Slice")]
        public static void Build()
        {
            FarmCityGraphicalShowcaseBuilder.Build();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var world = GameObject.Find("WorldBootstrap")
                ?? throw new InvalidOperationException("FarmHeroWorldRootMissing");
            var previous = world.transform.Find(RootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);

            var root = new GameObject(RootName);
            root.transform.SetParent(world.transform, false);
            BuildEntranceAndRoad(root.transform);
            BuildWorkingYard(root.transform);
            BuildCropAndVegetationRhythm(root.transform);
            ConfigureLighting();
            ConfigureVolume();
            ConfigureCamera();
            ConfigureTimeOfDay(root.transform);
            ValidateOpenScene();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("FarmHeroSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            SceneView.RepaintAll();
            Debug.Log("FarmHeroShowcaseBuilt:" + ScenePath);
        }

        [MenuItem("Ssalddel/SHOWCASE/Validate Farm Hero Slice")]
        public static void ValidateOpenScene()
        {
            var root = GameObject.Find("WorldBootstrap/" + RootName)
                ?? throw new InvalidOperationException("FarmHeroRootMissing");
            var wrappers = root.transform.Cast<Transform>()
                .SelectMany(group => group.Cast<Transform>())
                .Where(value => value.name.StartsWith("Environment_", StringComparison.Ordinal))
                .ToArray();
            if (wrappers.Length < 70 || wrappers.Any(value => value.Find("VisualRoot") == null))
                throw new InvalidOperationException("FarmHeroVisualWrapperInvalid");
            if (root.GetComponentsInChildren<MonoBehaviour>(true).Any(value =>
                    value.GetType().Name.Contains("Command", StringComparison.Ordinal)
                    || value.GetType().Name.Contains("Simulation", StringComparison.Ordinal)
                    || value.GetType().Name.Contains("Operational", StringComparison.Ordinal)))
                throw new InvalidOperationException("FarmHeroAuthorityLeak");
            var sway = root.GetComponentsInChildren<농장환경SwayPresenter>(true);
            if (sway.Length < 59 || sway.Any(value => !value.ValidateWiring()))
                throw new InvalidOperationException("FarmHeroAmbientMotionInvalid");
            var tractor = root.GetComponentInChildren<절차형VehicleRouteFollower>(true);
            if (tractor == null || !tractor.ValidateWiring())
                throw new InvalidOperationException("FarmHeroTractorRouteInvalid");

            var rig = UnityEngine.Object.FindFirstObjectByType<DioramaTopDownCameraRig>()
                ?? throw new InvalidOperationException("FarmHeroCameraRigMissing");
            rig.ApplyNowForTests();
            if (Mathf.Abs(rig.ConfiguredZoneDistance - 33f) > .01f
                || rig.CurrentFocusAnchorId != FarmFocus)
                throw new InvalidOperationException("FarmHeroCameraCompositionInvalid");
            var light = GameObject.Find("WorldBootstrap/GlobalLighting")?.GetComponent<Light>();
            if (light == null || light.shadows != LightShadows.Soft
                || light.shadowStrength < .75f)
                throw new InvalidOperationException("FarmHeroLightingInvalid");
            var timeOfDay = root.GetComponent<월드시간대Presenter>();
            if (timeOfDay == null || !timeOfDay.ValidateWiring()
                || timeOfDay.SourceMode != 월드시간대SourceMode.FixedReference
                || timeOfDay.SurfaceBindingCount < 70)
                throw new InvalidOperationException("FarmHeroTimeOfDayInvalid");
        }

        [MenuItem("Ssalddel/SHOWCASE/Open Farm Hero Slice")]
        public static void Open()
            => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        private static void BuildEntranceAndRoad(Transform parent)
        {
            var group = Group(parent, "Hero Entrance And Curved Road");
            Vendor(group, "Environments/SM_Env_Road_Dirt_Swerve_01.prefab",
                "RoadCurveForeground", new Vector3(-34f, .04f, 5f), 18f, .72f);
            Vendor(group, "Environments/SM_Env_Road_Dirt_Straight_01.prefab",
                "RoadStraightApproach", new Vector3(-29f, .04f, 9f), 32f, .72f);
            Vendor(group, "Environments/SM_Env_Road_Dirt_Swerve_02.prefab",
                "RoadCurveYard", new Vector3(-25f, .04f, 13f), 38f, .72f);
            for (var index = 0; index < 11; index++)
            {
                Vendor(group, index == 5
                        ? "Props/SM_Prop_Fence_Wood_Gate_01.prefab"
                        : "Props/SM_Prop_Fence_Wood_01.prefab",
                    "EntranceFence_" + index,
                    new Vector3(-48f + index * 3.1f, 0f, 4.2f + index % 2 * .25f),
                    -4f + index % 3 * 2f, .72f);
            }
            for (var index = 0; index < 14; index++)
            {
                var sunflower = Vendor(group, index % 2 == 0
                        ? "Plants/SM_Prop_Sunflower_01.prefab"
                        : "Plants/SM_Prop_Sunflower_02.prefab",
                    "RoadsideSunflower_" + index,
                    new Vector3(-45f + index * 1.55f, .02f, 6.1f + index % 3 * .55f),
                    index * 29f, .78f + index % 3 * .06f);
                sunflower.AddComponent<농장환경SwayPresenter>()
                    .Configure(1.1f + index % 3 * .2f, .35f + index % 4 * .05f, index * .47f);
            }
        }

        private static void BuildWorkingYard(Transform parent)
        {
            var group = Group(parent, "Hero Working Yard");
            Vendor(group, "Buildings/SM_Bld_Greenhouse_01.prefab",
                "Greenhouse", new Vector3(-17f, 0f, 28f), -22f, .5f);
            Vendor(group, "Buildings/SM_Bld_Barn_02.prefab",
                "SecondaryBarn", new Vector3(-42f, 0f, 31f), 164f, .42f);
            var tractor = Vendor(group, "Vehicles/SM_Veh_Tractor_01.prefab",
                "WorkingTractor", new Vector3(-21f, 0f, 10.5f), -38f, .62f);
            var routeStart = new GameObject("PresentationRoute_Tractor_Start").transform;
            routeStart.SetParent(group, false);
            routeStart.position = new Vector3(-24f, 0f, 10.5f);
            var routeEnd = new GameObject("PresentationRoute_Tractor_End").transform;
            routeEnd.SetParent(group, false);
            routeEnd.position = new Vector3(-17.5f, 0f, 15f);
            tractor.AddComponent<절차형VehicleRouteFollower>()
                .Configure(routeStart, routeEnd, 1.35f, true);
            for (var index = 0; index < 9; index++)
            {
                var path = index % 3 == 0 ? "Props/SM_Prop_Crate_01.prefab"
                    : index % 3 == 1 ? "Props/SM_Prop_Hay_Bale_Square_01.prefab"
                    : "Props/SM_Prop_Barrel_01.prefab";
                Vendor(group, path, "YardProp_" + index,
                    new Vector3(-23f + index % 5 * 1.25f, .02f, 14.5f + index / 5 * 1.2f),
                    index * 37f, .72f);
            }
        }

        private static void BuildCropAndVegetationRhythm(Transform parent)
        {
            var group = Group(parent, "Hero Crop And Vegetation Rhythm");
            for (var row = 0; row < 5; row++)
            for (var column = 0; column < 9; column++)
            {
                var crop = Vendor(group, row % 2 == 0
                        ? "Plants/SM_Prop_Plant_Wheat_Optimised_01.prefab"
                        : "Plants/SM_Prop_Corn_01_Group.prefab",
                    $"HeroCrop_{row}_{column}",
                    new Vector3(-50f + column * 1.45f, .02f, 12f + row * 1.35f),
                    row % 2 * 7f, .62f + (column + row) % 3 * .05f);
                crop.AddComponent<농장환경SwayPresenter>()
                    .Configure(.75f + (column + row) % 3 * .2f,
                        .28f + row * .025f, row * .61f + column * .19f);
            }
            for (var index = 0; index < 12; index++)
            {
                var path = index % 3 == 0 ? "Environments/SM_Env_Flowers_01.prefab"
                    : index % 3 == 1 ? "Environments/SM_Env_Flowers_02.prefab"
                    : "Generic/SM_Generic_Grass_Patch_02.prefab";
                Vendor(group, path, "FieldEdgeDetail_" + index,
                    new Vector3(-51f + index * 2.75f, .02f, 19f + index % 2 * .7f),
                    index * 41f, .72f);
            }
        }

        private static void ConfigureLighting()
        {
            var light = GameObject.Find("WorldBootstrap/GlobalLighting")?.GetComponent<Light>()
                ?? throw new InvalidOperationException("FarmHeroGlobalLightMissing");
            light.intensity = 1.18f;
            light.color = new Color(1f, .89f, .72f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = .78f;
            light.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.65f, .74f, .86f);
            RenderSettings.ambientEquatorColor = new Color(.69f, .61f, .48f);
            RenderSettings.ambientGroundColor = new Color(.22f, .25f, .17f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.7f, .73f, .64f);
            RenderSettings.fogDensity = .0022f;
        }

        private static void ConfigureVolume()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(VolumeProfilePath)!);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }
            var color = profile.TryGet<ColorAdjustments>(out var currentColor)
                ? currentColor : profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(.1f);
            color.contrast.Override(7f);
            color.saturation.Override(-7f);
            color.colorFilter.Override(new Color(1f, .93f, .82f));
            var tone = profile.TryGet<Tonemapping>(out var currentTone)
                ? currentTone : profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.TryGet<Bloom>(out var currentBloom)
                ? currentBloom : profile.Add<Bloom>(true);
            bloom.intensity.Override(.08f);
            bloom.threshold.Override(1.12f);
            var vignette = profile.TryGet<Vignette>(out var currentVignette)
                ? currentVignette : profile.Add<Vignette>(true);
            vignette.intensity.Override(.075f);
            vignette.smoothness.Override(.28f);
            EditorUtility.SetDirty(profile);

            var volume = UnityEngine.Object.FindFirstObjectByType<Volume>()
                ?? throw new InvalidOperationException("FarmHeroVolumeMissing");
            volume.sharedProfile = profile;
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>()
                ?? throw new InvalidOperationException("FarmHeroCameraMissing");
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.backgroundColor = new Color(.64f, .72f, .76f);
        }

        [MenuItem("Ssalddel/SHOWCASE/Capture Farm Hero Play Mode")]
        public static void CapturePlayMode()
        {
            CapturePlayModeTo(
                "Documentation/Changes/2026-08-10-farm-hero-slice/farm-hero-slice.png");
        }

        [MenuItem("Ssalddel/SHOWCASE/Capture Farm Hero ART4 Play Mode")]
        public static void CaptureArt4PlayMode()
            => CapturePlayModeTo(
                "Documentation/Changes/2026-08-10-farm-hero-slice/farm-hero-art4-motion.png");

        [MenuItem("Ssalddel/SHOWCASE/TIME/Capture 01 Dawn")]
        public static void CaptureDawn()
            => ApplyTimeAndCapture("01-dawn", 5.5f / 24f);

        [MenuItem("Ssalddel/SHOWCASE/TIME/Capture 02 Morning")]
        public static void CaptureMorning()
            => ApplyTimeAndCapture("02-morning", 8.5f / 24f);

        [MenuItem("Ssalddel/SHOWCASE/TIME/Capture 03 Midday")]
        public static void CaptureMidday()
            => ApplyTimeAndCapture("03-midday", 12.5f / 24f);

        [MenuItem("Ssalddel/SHOWCASE/TIME/Capture 04 Afternoon")]
        public static void CaptureAfternoon()
            => ApplyTimeAndCapture("04-afternoon", 16f / 24f);

        [MenuItem("Ssalddel/SHOWCASE/TIME/Capture 05 Golden Dusk")]
        public static void CaptureGoldenDusk()
            => ApplyTimeAndCapture("05-golden-dusk", 18.5f / 24f);

        [MenuItem("Ssalddel/SHOWCASE/TIME/Capture 06 Night")]
        public static void CaptureNight()
            => ApplyTimeAndCapture("06-night", 21f / 24f);

        private static void ApplyTimeAndCapture(string fileStem, float normalizedTime)
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("FarmHeroTimeCaptureRequiresPlayMode");

            var presenter = UnityEngine.Object.FindFirstObjectByType<월드시간대Presenter>()
                ?? throw new InvalidOperationException("FarmHeroTimePresenterMissing");
            presenter.ApplyNowForTests(normalizedTime);
            var relativePath =
                $"Documentation/Changes/2026-08-10-farm-time-of-day/{fileStem}.png";
            EditorApplication.delayCall += () => CapturePlayModeTo(relativePath);
            Debug.Log($"Farm Hero time preview applied: {fileStem} ({normalizedTime * 24f:0.0}h)");
        }

        private static void CapturePlayModeTo(string relativePath)
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("FarmHeroCaptureRequiresPlayMode");

            var absolutePath = Path.GetFullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            ScreenCapture.CaptureScreenshot(absolutePath, 1);
            Debug.Log("Farm Hero Game View capture requested: " + absolutePath);
        }

        private static void ConfigureCamera()
        {
            var rig = UnityEngine.Object.FindFirstObjectByType<DioramaTopDownCameraRig>()
                ?? throw new InvalidOperationException("FarmHeroCameraRigMissing");
            rig.ConfigureComposition(46f, 96f, 33f, 20f, 35f, 31f, 28f);
            rig.ConfigureInitialFocus(FarmFocus);
            rig.ApplyNowForTests();
            var occlusion = rig.GetComponent<DioramaForegroundOcclusionController>();
            if (occlusion != null) occlusion.ApplyNow();
            EditorUtility.SetDirty(rig);
        }

        private static void ConfigureTimeOfDay(Transform root)
        {
            var light = GameObject.Find("WorldBootstrap/GlobalLighting")?.GetComponent<Light>()
                ?? throw new InvalidOperationException("FarmHeroTimeLightMissing");
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>()
                ?? throw new InvalidOperationException("FarmHeroTimeCameraMissing");
            root.gameObject.AddComponent<월드시간대Presenter>()
                .Configure(light, camera, root, 12.5f / 24f, false, 180f);
        }

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static GameObject Vendor(
            Transform parent, string relativePath, string name,
            Vector3 position, float yaw, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FarmRoot + relativePath)
                         ?? throw new InvalidOperationException("FarmHeroPrefabMissing:" + relativePath);
            var wrapper = new GameObject("Environment_" + name);
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.position = position;
            wrapper.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(wrapper.transform, false);
            var instance = PrefabUtility.InstantiatePrefab(prefab, visualRoot) as GameObject
                           ?? throw new InvalidOperationException("FarmHeroPrefabInstantiateFailed:" + relativePath);
            instance.name = "SyntyPrefabInstance";
            instance.transform.localScale = Vector3.one * scale;
            return wrapper;
        }
    }
}
