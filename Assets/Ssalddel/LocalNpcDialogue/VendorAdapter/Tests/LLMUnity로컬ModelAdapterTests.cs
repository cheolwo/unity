using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Ssalddel.LocalNpcDialogue.Tests
{
    public class LLMUnity로컬ModelAdapterTests
    {
        [Test]
        public async Task DefaultDisabledDoesNotLoadNativeOrModel()
        {
            var model = new LLMUnity로컬ModelAdapter();
            Assert.That(model.Enabled, Is.False);
            Assert.That(model.IsLocal, Is.True);
            Assert.That(model.IsModelAvailable, Is.False);
            Assert.That(await model.InitializeAsync("https://example.invalid/model.gguf"), Is.EqualTo("Disabled"));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await model.GenerateAsync("hello", CancellationToken.None));
            await model.ShutdownAsync();
        }

        [TestCase("https://example.invalid/model.gguf")]
        [TestCase("http://localhost:8000/model.gguf")]
        [TestCase("\\\\server\\share\\model.gguf")]
        [TestCase("//server/share/model.gguf")]
        [TestCase("relative.gguf")]
        [TestCase("C:/models/not-model.zip")]
        public async Task RemoteOrNonModelPathsAreRejectedBeforeLoading(string path)
        {
            var model = new LLMUnity로컬ModelAdapter(true);
            Assert.That(LLMUnity로컬ModelAdapter.IsLocalModelPath(path), Is.False);
            Assert.That(await model.InitializeAsync(path), Is.EqualTo("RemoteForbidden"));
            Assert.That(model.IsModelAvailable, Is.False);
            await model.ShutdownAsync();
        }

        [Test]
        public async Task EmptyAndAbsentLocalModelsAreExplicitMissing()
        {
            var model = new LLMUnity로컬ModelAdapter(true);
            Assert.That(await model.InitializeAsync(""), Is.EqualTo("ModelMissing"));
            var path = Path.Combine(Path.GetTempPath(), "d337-missing-" + Guid.NewGuid().ToString("N") + ".gguf");
            Assert.That(await model.InitializeAsync(path), Is.EqualTo("ModelMissing"));
            Assert.That(File.Exists(path), Is.False);
            Assert.That(model.IsModelAvailable, Is.False);
            await model.ShutdownAsync();
        }

        [Test]
        public async Task ShutdownCannotReactivateModel()
        {
            var model = new LLMUnity로컬ModelAdapter(true);
            await model.ShutdownAsync();
            Assert.That(await model.InitializeAsync("C:/model.gguf"), Is.EqualTo("Disposed"));
            await model.ShutdownAsync();
        }
    }
}
