using System;
using System.Linq;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Model
{
    [TestFixture]
    public class GuidFormattingTests
    {
        [Test]
        public void FormatUnityGuid_KnownValue_ReversesNibblesWithinEachWord()
        {
            // Same worked example as BinaryFormat.GuidFormattingTests: 0xe60765cd's nibbles,
            // written MSB-first, are e,6,0,7,6,5,c,d (nibble index 7 down to 0). The algorithm
            // writes nibble(j) to output position j (0-indexed from the start of this word's
            // 8-char span), so nibble order is reversed relative to normal big-endian hex
            // formatting -- nibble(0)='d' comes first, nibble(7)='e' comes last: "dc56706e".
            var result = GuidFormatting.FormatUnityGuid(0xe60765cd, 0, 0, 0);

            Assert.That(result.Substring(0, 8), Is.EqualTo("dc56706e"));
        }

        [Test]
        public void FormatUnityGuid_AllZero_ReturnsThirtyTwoZeroChars()
        {
            var result = GuidFormatting.FormatUnityGuid(0, 0, 0, 0);

            Assert.That(result, Is.EqualTo(new string('0', 32)));
        }

        [Test]
        public void FormatUnityGuid_AnyInput_IsAlways32LowercaseHexChars()
        {
            var result = GuidFormatting.FormatUnityGuid(0x89ABCDEFu, 0x01234567u, 0xFEDCBA98u, 0x76543210u);

            Assert.That(result.Length, Is.EqualTo(32));
            Assert.That(result.All(c => "0123456789abcdef".Contains(c)), Is.True);
        }

        [Test]
        public void FormatUnityHash128_KnownValue_EmitsBytesInOrderWithNoReversal()
        {
            byte[] bytes =
            {
                0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF,
                0xFE, 0xDC, 0xBA, 0x98, 0x76, 0x54, 0x32, 0x10,
            };

            var result = GuidFormatting.FormatUnityHash128(bytes);

            Assert.That(result, Is.EqualTo("0123456789abcdeffedcba9876543210"));
        }

        [Test]
        public void FormatUnityHash128_AllZero_ReturnsThirtyTwoZeroChars()
        {
            var result = GuidFormatting.FormatUnityHash128(new byte[16]);

            Assert.That(result, Is.EqualTo(new string('0', 32)));
        }

        [TestCase(15)]
        [TestCase(17)]
        public void FormatUnityHash128_WrongLength_Throws(int length)
        {
            Assert.Throws<ArgumentException>(() => GuidFormatting.FormatUnityHash128(new byte[length]));
        }
    }
}
