using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// Nature 생존 폐루프가 사용하는 승인된 시각 자산 묶음입니다.
    /// 런타임은 이 사본을 읽기만 하며 Simulation 상태를 변경하지 않습니다.
    /// </summary>
    [CreateAssetMenu(menuName = "Ssalddel/Nature 생존 시각 자산 묶음",
        fileName = "Nature생존VisualCatalog")]
    public sealed class Nature생존VisualCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Nature생존VisualCatalog";

        [SerializeField] private GameObject treePrefab = null!;
        [SerializeField] private GameObject stumpPrefab = null!;
        [SerializeField] private GameObject axePrefab = null!;
        [SerializeField] private GameObject timberLogPrefab = null!;
        [SerializeField] private GameObject cabinPrefab = null!;
        [SerializeField] private GameObject workbenchPrefab = null!;
        [SerializeField] private GameObject skeletonPrefab = null!;
        [SerializeField] private GameObject storedTimberPrefab = null!;
        [SerializeField] private GameObject sleepBedPrefab = null!;
        [SerializeField] private GameObject sleepCampfirePrefab = null!;
        [SerializeField] private GameObject expansionPlansPrefab = null!;
        [SerializeField] private GameObject storageRackPrefab = null!;
        [SerializeField] private GameObject workTablePrefab = null!;
        [SerializeField] private GameObject toolBoxPrefab = null!;

        public GameObject TreePrefab => treePrefab;
        public GameObject StumpPrefab => stumpPrefab;
        public GameObject AxePrefab => axePrefab;
        public GameObject TimberLogPrefab => timberLogPrefab;
        public GameObject CabinPrefab => cabinPrefab;
        public GameObject WorkbenchPrefab => workbenchPrefab;
        public GameObject SkeletonPrefab => skeletonPrefab;
        public GameObject StoredTimberPrefab => storedTimberPrefab;
        public GameObject SleepBedPrefab => sleepBedPrefab;
        public GameObject SleepCampfirePrefab => sleepCampfirePrefab;
        public GameObject ExpansionPlansPrefab => expansionPlansPrefab;
        public GameObject StorageRackPrefab => storageRackPrefab;
        public GameObject WorkTablePrefab => workTablePrefab;
        public GameObject ToolBoxPrefab => toolBoxPrefab;

        public void Configure(GameObject tree, GameObject stump, GameObject axe,
            GameObject timberLog, GameObject cabin, GameObject workbench,
            GameObject skeleton, GameObject storedTimber, GameObject sleepBed,
            GameObject sleepCampfire, GameObject expansionPlans)
        {
            treePrefab = tree;
            stumpPrefab = stump;
            axePrefab = axe;
            timberLogPrefab = timberLog;
            cabinPrefab = cabin;
            workbenchPrefab = workbench;
            skeletonPrefab = skeleton;
            storedTimberPrefab = storedTimber;
            sleepBedPrefab = sleepBed;
            sleepCampfirePrefab = sleepCampfire;
            expansionPlansPrefab = expansionPlans;
        }

        public void ConfigureInterior(GameObject storageRack,
            GameObject workTable, GameObject toolBox)
        {
            storageRackPrefab = storageRack;
            workTablePrefab = workTable;
            toolBoxPrefab = toolBox;
        }

        public bool IsComplete()
            => treePrefab != null && stumpPrefab != null && axePrefab != null
                && timberLogPrefab != null && cabinPrefab != null
                && workbenchPrefab != null && skeletonPrefab != null
                && storedTimberPrefab != null && sleepBedPrefab != null
                && sleepCampfirePrefab != null && expansionPlansPrefab != null;

        public bool IsInteriorComplete()
            => storageRackPrefab != null && workTablePrefab != null
                && toolBoxPrefab != null;
    }
}
