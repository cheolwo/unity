using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Bootstrap;

namespace Ssalddel.Unity.EditorTools
{
    public static class 사가정운영디오라마검증
    {
        private static 사가정운영디오라마View View()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("PlayModeRequired");
            return UnityEngine.Object.FindFirstObjectByType<사가정운영디오라마View>()
                ?? throw new InvalidOperationException("DioramaMissing");
        }

        [MenuItem("Ssalddel/통합 월드/운영 관찰/사가정 Runtime 검증")]
        public static void Validate()
        {
            var view=View();var world=view.GetComponent<운영관찰WorldView>();
            var controller=view.GetComponent<운영관찰WorldController>();
            if (view.MapStatus!="Ready" || world.PlacementStatus!="Ready" || world.UnplacedCount!=0)
                throw new InvalidOperationException("DioramaPlacementNotReady");
            if (world.ObjectCount!=6 || world.Objects.Select(x=>x.OperatingSystemId).Distinct().Count()!=4)
                throw new InvalidOperationException("DioramaSixSampleObjectsRequired");
            if (world.Objects.Any(x=>x.SourceKindCode!="VerificationSample"))
                throw new InvalidOperationException("DioramaSampleScopeInvalid");
            var renderers=view.GetComponentsInChildren<Renderer>();
            if (renderers.Length<5 || renderers.Any(x=>!x.enabled))
                throw new InvalidOperationException("DioramaRendererSuppressed");
            Debug.Log($"SAGAJEONG-DIORAMA: map={view.MapStatus}; objects={world.ObjectCount}; unplaced={world.UnplacedCount}; controller={controller.StatusCode}; readOnly=true; animation=false");
        }
    }
}
