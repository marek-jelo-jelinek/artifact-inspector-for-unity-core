using System;
using Tesearis.ArtifactInspectorForUnity.Core.Adapters;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Adapters
{
    [TestFixture]
    public class ArtifactAdapterContextTests
    {
        [Test]
        public void Constructor_NullReader_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ArtifactAdapterContext(default(ObjectRef), null, null, null));
        }
    }
}
