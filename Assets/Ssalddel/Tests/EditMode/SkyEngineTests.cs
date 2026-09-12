using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class SkyEngineTests
    {
        [Test]
        public void 뇌우는_같은시간대의맑은하늘보다_어둡고안개가짙다()
        {
            var time = 월드시간대Interpreter.Evaluate(15f / 24f);
            var clear = SkyEngineProjector.Compose(time,
                Atmosphere(WorldWeatherCodes.Clear, 80, 0, 0), true);
            var storm = SkyEngineProjector.Compose(time,
                Atmosphere(WorldWeatherCodes.Thunderstorm, 1000, 1000, 3),
                true);

            Assert.That(storm.ComposedTimeOfDay.SunIntensity,
                Is.LessThan(clear.ComposedTimeOfDay.SunIntensity));
            Assert.That(storm.ComposedTimeOfDay.FogDensity,
                Is.GreaterThan(clear.ComposedTimeOfDay.FogDensity));
            Assert.That(storm.LightningEnabled, Is.True);
            var clearLighting = 방향광표현Projector.Project(
                clear.ComposedTimeOfDay,
                World방향광표현Catalog.DefaultProfileStableId);
            var stormLighting = 방향광표현Projector.Project(
                storm.ComposedTimeOfDay,
                World방향광표현Catalog.DefaultProfileStableId);
            Assert.That(Vector3.Distance(
                    clearLighting.SurfaceToLightDirectionWorld,
                    stormLighting.SurfaceToLightDirectionWorld),
                Is.LessThan(.000001f));
        }

        [Test]
        public void 같은비상태라도_차폐공간에서는강수표현을숨긴다()
        {
            var time = 월드시간대Interpreter.Evaluate(.5f);
            var raining = Atmosphere(WorldWeatherCodes.Rain, 930, 720, 0);

            var outside = SkyEngineProjector.Compose(time, raining, true);
            var inside = SkyEngineProjector.Compose(time, raining, false);

            Assert.That(outside.PrecipitationVisible, Is.True);
            Assert.That(inside.PrecipitationVisible, Is.False);
            Assert.That(inside.Precipitation,
                Is.EqualTo(outside.Precipitation));
        }

        [Test]
        public void LocalFixture대기상태는_Nature시계에서결정적으로파생된다()
        {
            var engine = new Nature생존LocalEngine(
                Nature생존LocalEngine.CreateDefaultFixture());
            for (var index = 0; index < 10; index++)
                engine.AdvanceRealtime(60, false, false);

            var state = engine.Snapshot();

            Assert.That(state.Atmosphere.WeatherCode,
                Is.EqualTo(WorldWeatherCodes.Rain));
            Assert.That(state.Atmosphere.ElapsedSecondsInCycle,
                Is.EqualTo(state.ElapsedSecondsInCycle));
        }

        private static WorldAtmosphereState Atmosphere(string weather,
            int cloud, int precipitation, int lightning)
            => new()
            {
                WeatherCode = weather,
                NextWeatherCode = weather,
                CloudCoverPermille = cloud,
                PrecipitationPermille = precipitation,
                LightningSequenceIndex = lightning,
            };
    }
}
