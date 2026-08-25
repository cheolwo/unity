using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.PresentationContracts;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class Nature감각표현ProjectorTests
    {
        private readonly Nature감각표현상태Projector projector = new();

        [Test]
        public void 최초상태사본은_과거효과음을재생하지않는다()
        {
            var state = Nature생존LocalEngine.CreateDefaultFixture();

            var frame = projector.Project(null, state);

            Assert.That(frame.PhaseCode,
                Is.EqualTo(감각표현단계Codes.Available));
            Assert.That(frame.AudioCueCode, Is.Empty);
            Assert.That(frame.CueStableId, Is.Empty);
            Assert.That(frame.PresentationOnly, Is.True);
            Assert.That(frame.MutatesCanonicalState, Is.False);
            Assert.That(frame.ConfirmsBusinessCompletion, Is.False);
        }

        [Test]
        public void 도끼획득은_권위개정관찰뒤한번의획득Cue로투영한다()
        {
            var before = Nature생존LocalEngine.CreateDefaultFixture();
            var engine = new Nature생존LocalEngine(before);
            engine.AcquireAxe();

            var frame = projector.Project(before, engine.Snapshot());

            Assert.That(frame.PhaseCode,
                Is.EqualTo(감각표현단계Codes.Acquired));
            Assert.That(frame.AudioCueCode,
                Is.EqualTo(감각표현CueCodes.AxePickup));
            Assert.That(frame.CueStableId, Does.StartWith("presentation-cue:"));
            Assert.That(frame.ObservedWorldRevision, Is.GreaterThan(0));
        }

        [Test]
        public void 벌목진행초가증가할때만_타격AnimationAudioFx를투영한다()
        {
            var engine = ReadyHarvestEngine();
            var started = engine.Snapshot();
            engine.AdvanceRealtime(1, true, false);

            var frame = projector.Project(started, engine.Snapshot());

            Assert.That(frame.PhaseCode,
                Is.EqualTo(감각표현단계Codes.Working));
            Assert.That(frame.AnimationIntentCode,
                Is.EqualTo("animation:nature:axe-swing"));
            Assert.That(frame.AudioCueCode,
                Is.EqualTo(감각표현CueCodes.AxeImpact));
            Assert.That(frame.FxCueCode,
                Is.EqualTo(감각표현CueCodes.WoodChip));
            Assert.That(frame.CueOrdinal, Is.EqualTo(1));

            var unchanged = projector.Project(engine.Snapshot(), engine.Snapshot());
            Assert.That(unchanged.AudioCueCode, Is.Empty);
            Assert.That(unchanged.FxCueCode, Is.Empty);
        }

        [Test]
        public void 취소는Idle복귀를표현하지만_나무완료를주장하지않는다()
        {
            var engine = ReadyHarvestEngine();
            var working = engine.Snapshot();
            engine.CancelActiveWork();

            var frame = projector.Project(working, engine.Snapshot());

            Assert.That(frame.PhaseCode,
                Is.EqualTo(감각표현단계Codes.Cancelled));
            Assert.That(frame.AnimationIntentCode,
                Is.EqualTo("animation:common:idle"));
            Assert.That(frame.AudioCueCode,
                Is.EqualTo(감각표현CueCodes.HarvestCancelled));
            Assert.That(frame.ConfirmsBusinessCompletion, Is.False);
        }

        [Test]
        public void 권위벌목완료뒤에만_나무낙하Cue를투영한다()
        {
            var engine = ReadyHarvestEngine();
            var beforeCompletion = engine.Snapshot();
            engine.AdvanceRealtime(4, true, false);

            var frame = projector.Project(beforeCompletion, engine.Snapshot());

            Assert.That(frame.PhaseCode,
                Is.EqualTo(감각표현단계Codes.Completed));
            Assert.That(frame.AudioCueCode,
                Is.EqualTo(감각표현CueCodes.TreeFall));
            Assert.That(frame.FxCueCode,
                Is.EqualTo(감각표현CueCodes.TreeFallDust));
            Assert.That(frame.ConfirmsBusinessCompletion, Is.False);
        }

        private static Nature생존LocalEngine ReadyHarvestEngine()
        {
            var engine = new Nature생존LocalEngine(
                Nature생존LocalEngine.CreateDefaultFixture());
            engine.AcquireAxe();
            engine.BeginHarvest("resource:nature-tree:01");
            return engine;
        }
    }
}
