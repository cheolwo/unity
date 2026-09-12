using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정운영디오라마Tests
    {
        private static TextAsset MapAsset => Resources.Load<TextAsset>("SagajeongReference");
        private static 공간참고자료 Map => JsonUtility.FromJson<공간참고자료>(MapAsset.text);
        private static 운영관찰지역배치자료 Catalog => JsonUtility.FromJson<운영관찰지역배치자료>(
            Resources.Load<TextAsset>("SagajeongObservationPlacement").text);

        [Test] public void 지도판본에결속된_여섯가상지점만허용한다()
        {
            var places = 운영관찰지역배치검사.Validate(Catalog,Map,MapAsset.bytes);
            Assert.That(places.Count,Is.EqualTo(6));
            Assert.That(places.Values.Select(x=>x.role).Distinct().Count(),Is.EqualTo(5));
        }

        [Test] public void 지도변경과_중복과_영역밖을거부한다()
        {
            var catalog=Catalog; catalog.mapRevision="changed";
            Assert.Throws<ArgumentException>(()=>운영관찰지역배치검사.Validate(catalog,Map,MapAsset.bytes));
            catalog=Catalog; catalog.places[1].semanticPlaceStableId=catalog.places[0].semanticPlaceStableId;
            Assert.Throws<ArgumentException>(()=>운영관찰지역배치검사.Validate(catalog,Map,MapAsset.bytes));
            catalog=Catalog; catalog.places[0].x=100000;
            Assert.Throws<ArgumentException>(()=>운영관찰지역배치검사.Validate(catalog,Map,MapAsset.bytes));
            catalog=Catalog; catalog.places[0].y=float.NaN;
            Assert.Throws<ArgumentException>(()=>운영관찰지역배치검사.Validate(catalog,Map,MapAsset.bytes));
        }

        [Test] public void 미등록위치를_임의좌표로대체하지않는다()
        {
            var root=new GameObject("placement-test");
            try
            {
                var view=root.AddComponent<운영관찰WorldView>();
                view.Apply(Plan(Item("synthetic-place:unknown")),Array.Empty<OperationalWorldSceneApplyDiagnostic>());
                Assert.That(view.ObjectCount,Is.Zero);
                Assert.That(view.UnplacedCount,Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test] public void 가상위치결속과_통신없는만료제거를확인한다()
        {
            var root=new GameObject("placement-test");
            try
            {
                var view=root.AddComponent<운영관찰WorldView>();
                var item=Item("synthetic-place:food-route-a");
                view.Apply(Plan(item),Array.Empty<OperationalWorldSceneApplyDiagnostic>());
                Assert.That(view.Objects.Single().transform.localPosition.x,Is.EqualTo(Catalog.places[0].x));
                view.Expire(item.ExpiresAtUtc.AddSeconds(1));
                Assert.That(view.ObjectCount,Is.Zero);
                Assert.That(root.GetComponentsInChildren<운영관찰ObjectView>(),Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test] public void 같은판본은_동결해제를하지않고_높은판본만회복한다()
        {
            var root=new GameObject("freeze-test");
            try
            {
                var view=root.AddComponent<운영관찰WorldView>();
                var item=Item("synthetic-place:food-route-a");
                view.Apply(Plan(item),new[]{new OperationalWorldSceneApplyDiagnostic{SnapshotStableId=item.ObjectStableId,ExistingObjectFrozen=true}});
                view.Apply(Plan(item),Array.Empty<OperationalWorldSceneApplyDiagnostic>());
                Assert.That(view.Objects.Single().Frozen,Is.True);
                item.Revision++;
                view.Apply(Plan(item),Array.Empty<OperationalWorldSceneApplyDiagnostic>());
                Assert.That(view.Objects.Single().Frozen,Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test] public void 독립지도조립은_지도원문을변경하지않는다()
        {
            var map=Map;var original=JsonUtility.ToJson(map);var calls=0;
            사가정지리MeshBuilder.Build(map,(name,vertices,triangles,color)=>
            {
                calls++;
                Assert.That(vertices.Count,Is.GreaterThan(0));
                Assert.That(triangles.All(i=>i>=0&&i<vertices.Count),Is.True);
            });
            Assert.That(calls,Is.EqualTo(2));
            Assert.That(JsonUtility.ToJson(map),Is.EqualTo(original));
        }

        [Test] public void 가까운여섯업무라벨은_서로겹치지않는다()
        {
            var placed=new System.Collections.Generic.List<Rect>();
            for(var i=0;i<6;i++)
            {
                var rect=사가정운영디오라마View.PlaceLabel(new Rect(500,300,116,26),placed,124,600);
                Assert.That(rect.width,Is.GreaterThan(0));
                Assert.That(placed.Any(x=>x.Overlaps(rect)),Is.False);
                placed.Add(rect);
            }
        }

        private static OperationalWorldPlacementPlan Plan(params OperationalWorldPlacementInstruction[] items)
            => new(){Accepted=true,Instructions=items};
        private static OperationalWorldPlacementInstruction Item(string place) => new()
        {
            ObjectStableId="snapshot:sample", OperatingSystemId=OperationalWorldOperatingSystemIds.FoodDelivery,
            SemanticPlaceStableId=place, Revision=1, ExpiresAtUtc=DateTime.UtcNow.AddMinutes(5),
            SourceKindCode=OperationalWorldSceneSourceKinds.VerificationSample,
            LifecycleStageCode="ReceiptConfirmed", AttentionStateCode=OperationalWorldAttentionStateCodes.Completed
        };
    }
}
