using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 면목동디오라마표현Tests
    {
        private static TextAsset MapAsset()=>Resources.Load<TextAsset>("SagajeongReference");
        private static 공간참고자료 Map()=>JsonUtility.FromJson<공간참고자료>(MapAsset().text);
        private static byte[] Bytes()=>면목동디오라마표현검사.ReadFrozen(면목동주소연결View.ProjectionPath,MapAsset().bytes);
        private static 면목동디오라마표현자료 Data()=>JsonUtility.FromJson<면목동디오라마표현자료>(Encoding.UTF8.GetString(Bytes()));

        [Test] public void 상호와상세주소없이_204건물801관측을_표현한다()
        {
            var data=Data();면목동디오라마표현검사.Validate(data,Map());
            Assert.That(data.buildingOverlays.Length,Is.EqualTo(204));
            Assert.That(data.buildingOverlays.Sum(x=>x.observationCount),Is.EqualTo(801));
            Assert.That(data.layers.Length,Is.EqualTo(7));
            var text=Encoding.UTF8.GetString(Bytes());
            foreach(var field in new[]{"\"name\"","\"roadAddress\"","\"addressKey\"","\"buildingManagementNumber\"","\"observationId\""})
                Assert.That(text.Contains(field,StringComparison.Ordinal),Is.False,field);
        }

        [Test] public void 검증상태와_미배치표본을_강제연결하지않는다()
        {
            var data=Data();
            Assert.That(data.counts.officialBuildingLinks,Is.Zero);
            Assert.That(data.counts.addressSingleCandidates,Is.EqualTo(14));
            Assert.That(data.counts.multipleCandidates,Is.EqualTo(8));
            Assert.That(data.counts.unlinkedSamples,Is.EqualTo(8));
            Assert.That(data.unplacedSampleSummary.Sum(x=>x.count),Is.EqualTo(30));
            Assert.That(data.buildingOverlays.All(x=>x.connectionStatus=="주소단일후보"),Is.True);
        }

        [Test] public void 동결파일과지도_변경은_차단한다()
        {
            Assert.Throws<InvalidDataException>(()=>면목동디오라마표현검사.ReadFrozen(면목동주소연결View.ProjectionPath,new byte[]{1}));
            var data=Data();data.distributionApproved=true;
            Assert.Throws<InvalidDataException>(()=>면목동디오라마표현검사.Validate(data,Map()));
        }

        [Test] public void 표현소비자는_DB와게임상태를_쓰지않는다()
        {
            var go=new GameObject("면목동 디오라마 시험");
            try
            {
                var map=Map();var before=JsonUtility.ToJson(map);
                var view=go.AddComponent<면목동주소연결View>();view.Initialize(map,MapAsset().bytes,null);
                Assert.That(view.ConnectedBuildings,Is.EqualTo(204));
                Assert.That(view.ConnectedRecords,Is.EqualTo(801));
                Assert.That(go.transform.childCount,Is.Zero);
                Assert.That(go.GetComponentsInChildren<Collider>(),Is.Empty);
                Assert.That(JsonUtility.ToJson(map),Is.EqualTo(before));
                var roads=new GameObject("사가정 · 도로 선 · 폭은 기호");roads.transform.SetParent(go.transform);
                var buildings=new GameObject("사가정 · 건물 외곽 · 미상 높이 4m");buildings.transform.SetParent(go.transform);
                view.SetLayerVisibility(false,false,false);
                Assert.That(roads.activeSelf,Is.False);
                Assert.That(buildings.activeSelf,Is.False);
                view.SetLayerVisibility(true,true,true);
                Assert.That(roads.activeSelf,Is.True);
                Assert.That(buildings.activeSelf,Is.True);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
