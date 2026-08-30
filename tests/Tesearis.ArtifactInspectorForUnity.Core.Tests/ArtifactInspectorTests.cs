using System;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests
{
    [TestFixture]
    public class ArtifactInspectorTests
    {
        [Test]
        public void OpenAssetBundle_NullFilePath_ThrowsArgumentNullException()
        {
            // This guard runs before the lazily-loaded real native library is ever touched,
            // so it's safely testable without a native binary present.
            Assert.Throws<ArgumentNullException>(() => ArtifactInspector.OpenAssetBundle(null));
        }

        [Test]
        public void AddTypeTreeSource_NullFilePath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ArtifactInspector.AddTypeTreeSource(null));
        }

        [Test]
        public void RemoveTypeTreeSource_NullFilePath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ArtifactInspector.RemoveTypeTreeSource(null));
        }

        [Test]
        public void SetupLibraryPath_NullPath_ThrowsArgumentNullException()
        {
            // Also runs before any native library is touched -- SetupLibraryPath just assigns a
            // static field, so this guard is safely testable without a native binary present.
            Assert.Throws<ArgumentNullException>(() => ArtifactInspector.SetupLibraryPath(null));
        }
    }
}
