using System;
using NUnit.Framework;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class H5세계배치Tests
    {
        [Test]
        public void 부모가90도회전하면_자식로컬축도함께회전한다()
        {
            var child = new H5배치TransformData
            {
                CoordinateSpaceCode = H5세계배치Codes.ParentLocalMeters,
                LocalXMeters = 100d,
                LocalZMeters = 0d,
                RotationDegrees = 15d,
            };

            var result = H5좌표합성.Compose(new H5합성Pose(1000d, 500d, 90d), child);

            Assert.That(result.X, Is.EqualTo(1000d).Within(.000001d));
            Assert.That(result.Z, Is.EqualTo(400d).Within(.000001d));
            Assert.That(result.RotationDegrees, Is.EqualTo(105d).Within(.000001d));
        }

        [Test]
        public void H5직접자식이_부모로컬좌표를쓰면거부한다()
        {
            var definition = ValidDefinition();
            definition.AreaSetInstances[0].PlacementTransform.CoordinateSpaceCode =
                H5세계배치Codes.ParentLocalMeters;

            Assert.Throws<InvalidOperationException>(() => definition.Validate());
        }

        [Test]
        public void FloatingOrigin은_권위좌표와배치해시를바꾸지않는다()
        {
            var definition = ValidDefinition();
            var hash = definition.WorldLayoutHashSha256;
            var authority = new H5합성Pose(4200d, -900d, 30d);

            var runtime = H5좌표합성.ProjectToRuntime(authority, 4096d, -1024d);

            Assert.That(runtime.X, Is.EqualTo(104d));
            Assert.That(runtime.Z, Is.EqualTo(124d));
            Assert.That(authority.X, Is.EqualTo(4200d));
            Assert.That(definition.WorldLayoutHashSha256, Is.EqualTo(hash));
        }

        [Test]
        public void E6준비도는_ScenarioRelative권위를대신하지않는다()
        {
            var definition = ValidDefinition();
            var binding = new H5현실결속BindingData
            {
                SchemaVersion = H5세계배치Codes.BindingSchema,
                WorldLayoutStableId = definition.WorldLayoutStableId,
                WorldLayoutRevision = definition.WorldLayoutRevision,
                WorldLayoutHashSha256 = definition.WorldLayoutHashSha256,
                PlacementAuthorityCode = H5세계배치Codes.ScenarioRelative,
                WorldGroundingStateCode = H5세계배치Codes.NotApplied,
            };
            var readiness = new H5현실결속준비도Data
            {
                SchemaVersion = H5세계배치Codes.ReadinessSchema,
                WorldLayoutStableId = definition.WorldLayoutStableId,
                GroundingReadinessStateCode = H5세계배치Codes.Partial,
                AppliesAuthority = false,
            };

            Assert.DoesNotThrow(() => binding.Validate(definition));
            Assert.DoesNotThrow(() => readiness.Validate(definition));
        }

        private static H5세계배치DefinitionData ValidDefinition()
        {
            var areas = new H5지역InstanceData[4];
            for (var i = 0; i < areas.Length; i++)
            {
                areas[i] = new H5지역InstanceData
                {
                    AreaSetInstanceStableId = "area:" + i,
                    PlacementTransform = new H5배치TransformData
                    {
                        CoordinateSpaceCode = H5세계배치Codes.ScenarioLocalMeters,
                    },
                    GraphInstances = new[]
                    {
                        new H5경관GraphInstanceData
                        {
                            PlacementTransform = new H5배치TransformData
                            {
                                CoordinateSpaceCode = H5세계배치Codes.ParentLocalMeters,
                            },
                        },
                    },
                };
            }
            var corridors = new H5회랑InstanceData[3];
            for (var i = 0; i < corridors.Length; i++)
                corridors[i] = new H5회랑InstanceData
                {
                    CorridorInstanceStableId = "corridor:" + i,
                    PlacementTransform = new H5배치TransformData
                    {
                        CoordinateSpaceCode = H5세계배치Codes.ScenarioLocalMeters,
                    },
                };
            var relations = new H5공간관계Data[8];
            for (var i = 0; i < relations.Length; i++)
                relations[i] = new H5공간관계Data
                {
                    SpatialRealizationCode = i < 3
                        ? H5세계배치Codes.PhysicalCorridor
                        : H5세계배치Codes.AbstractTravel,
                    CorridorInstanceStableId = i < 3 ? "corridor:" + i : string.Empty,
                };
            return new H5세계배치DefinitionData
            {
                SchemaVersion = H5세계배치Codes.DefinitionSchema,
                WorldLayoutStableId = "world-layout:test",
                WorldLayoutRevision = 1,
                CoordinateSpaceCode = H5세계배치Codes.ScenarioLocalMeters,
                WorldGroundingPolicyCode = H5세계배치Codes.Optional,
                AreaSetInstances = areas,
                CorridorInstances = corridors,
                Relations = relations,
                WorldLayoutHashSha256 = new string('a', 64),
                PresentationOnly = true,
            };
        }
    }
}
