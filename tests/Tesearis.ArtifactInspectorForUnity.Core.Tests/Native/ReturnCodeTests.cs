using System;
using System.Linq;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Native
{
    [TestFixture]
    public class ReturnCodeTests
    {
        [Test]
        public void Success_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ReturnCode.Success.ThrowIfNotSuccess("test operation"));
        }

        [TestCaseSource(nameof(NonSuccessCodes))]
        public void NonSuccessCode_ThrowsNativeCallExceptionCarryingTheCode(ReturnCode code)
        {
            var exception = Assert.Throws<NativeCallException>(() => code.ThrowIfNotSuccess("test operation"));

            Assert.That(exception.ReturnCode, Is.EqualTo(code));
        }

        // Parametrized over every declared enum value (rather than a hand-picked
        // subset) so that adding a new ReturnCode without updating this test still
        // proves it maps to a thrown exception instead of silently passing through.
        private static ReturnCode[] NonSuccessCodes()
        {
            return Enum.GetValues<ReturnCode>()
                .Where(code => code != ReturnCode.Success)
                .ToArray();
        }
    }
}