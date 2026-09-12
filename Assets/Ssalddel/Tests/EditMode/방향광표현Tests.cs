using System;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 방향광표현Tests
    {
        [Test]
        public void 표면법선과광원방향의내적은_정면직교후면을구분한다()
        {
            Assert.That(방향광내적Evaluator.Evaluate(Vector3.up,
                Vector3.up), Is.EqualTo(1f).Within(.000001f));
            Assert.That(방향광내적Evaluator.Evaluate(Vector3.up,
                Vector3.right), Is.EqualTo(0f).Within(.000001f));
            Assert.That(방향광내적Evaluator.Evaluate(Vector3.up,
                Vector3.down), Is.EqualTo(0f).Within(.000001f));
        }

        [Test]
        public void 길이가없는방향은_내적입력으로거부한다()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                방향광내적Evaluator.Evaluate(Vector3.zero, Vector3.up));

            Assert.That(exception!.Message,
                Does.Contain(방향광표현FailureCodes.LightDirectionInvalid));
        }

        [Test]
        public void 시간대태양회전은_DirectionalLight의반대Forward를광원방향으로쓴다()
        {
            var time = 월드시간대Interpreter.Evaluate(15f / 24f);
            var value = 방향광표현Projector.Project(time,
                World방향광표현Catalog.DefaultProfileStableId);
            var expected = -(Quaternion.Euler(time.SunPitch, time.SunYaw, 0f)
                             * Vector3.forward);

            Assert.That(Vector3.Distance(
                value.SurfaceToLightDirectionWorld, expected.normalized),
                Is.LessThan(.000001f));
            Assert.That(value.ProfileWasResolved, Is.True);
            Assert.That(value.FailureCode, Is.Empty);
        }

        [Test]
        public void 자연광Profile은_밤환경광을낮보다더들어올린다()
        {
            var midday = 방향광표현Projector.Project(
                월드시간대Interpreter.Evaluate(12.5f / 24f),
                World방향광표현Catalog.DefaultProfileStableId);
            var night = 방향광표현Projector.Project(
                월드시간대Interpreter.Evaluate(21f / 24f),
                World방향광표현Catalog.DefaultProfileStableId);

            Assert.That(midday.AmbientLift, Is.EqualTo(1f).Within(.000001f));
            Assert.That(night.AmbientLift, Is.EqualTo(1.2f).Within(.000001f));
        }

        [Test]
        public void 알수없는Profile은_안전한기본값과검증실패를함께반환한다()
        {
            var value = 방향광표현Projector.Project(
                월드시간대Interpreter.Evaluate(.5f), "lighting:unknown");

            Assert.That(value.ProfileStableId,
                Is.EqualTo(World방향광표현Catalog.DefaultProfileStableId));
            Assert.That(value.ProfileWasResolved, Is.False);
            Assert.That(value.FailureCode,
                Is.EqualTo(방향광표현FailureCodes.ProfileUnknown));
        }

        [Test]
        public void 방향광대상은_법선Lit재질과그림자정책을검증한다()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "DirectionalLightingTarget";
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader!);
            root.GetComponent<Renderer>().sharedMaterial = material;
            try
            {
                var view = root.AddComponent<방향광표현대상View>();
                view.Configure("placement:test:cabin", "Nature.Cabin.Completed",
                    World방향광표현Catalog.DefaultProfileStableId,
                    World방향광표현TargetCatalog.CastReceive);

                var receipt = view.Validate("cell:test");

                Assert.That(receipt.Passed, Is.True,
                    string.Join(",", receipt.FailureCodes));
                Assert.That(receipt.ProfileStableId,
                    Is.EqualTo(World방향광표현Catalog.DefaultProfileStableId));
                Assert.That(receipt.RuleRevision,
                    Is.EqualTo(World방향광표현Catalog.RuleRevision));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void 읽기금지SyntyMesh도_법선속성만으로검증한다()
        {
            var root = new GameObject("NonReadableDirectionalLightingTarget");
            var filter = root.AddComponent<MeshFilter>();
            var renderer = root.AddComponent<MeshRenderer>();
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.forward,
                },
                normals = new[]
                {
                    Vector3.up,
                    Vector3.up,
                    Vector3.up,
                },
                triangles = new[] { 0, 1, 2 },
            };
            mesh.UploadMeshData(true);
            filter.sharedMesh = mesh;
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader!);
            renderer.sharedMaterial = material;
            try
            {
                var view = root.AddComponent<방향광표현대상View>();
                view.Configure("placement:test:synty-tree",
                    "Nature.Tree.Pine",
                    World방향광표현Catalog.DefaultProfileStableId,
                    World방향광표현TargetCatalog.CastReceive);

                var receipt = view.Validate("cell:test");

                Assert.That(mesh.isReadable, Is.False);
                Assert.That(receipt.Passed, Is.True,
                    string.Join(",", receipt.FailureCodes));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void 상세셀은_지면만있고Nature핵심물이없으면_표현승격을거부한다()
        {
            var root = new GameObject("DirectionalLightingCell");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "DynamicGround";
            floor.transform.SetParent(root.transform, false);
            var controllerObject = new GameObject("DirectionalLightingController");
            try
            {
                var controller = controllerObject
                    .AddComponent<방향광표현Controller>();

                var passed = controller.ValidateCell("cell:detail:0:0",
                    root.transform, floor,
                    World방향광표현Catalog.DefaultProfileStableId,
                    lightingTargetRequired: true);
                var receipts = controller.ReceiptsOf("cell:detail:0:0");

                Assert.That(passed, Is.False);
                Assert.That(receipts, Has.Some.Matches<방향광표현ValidationReceipt>(
                    value => Array.IndexOf(value.FailureCodes,
                        방향광표현FailureCodes.TargetMissing) >= 0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(controllerObject);
            }
        }
    }
}
