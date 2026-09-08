using System;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Native
{
    [TestFixture]
    public class BufferedByteSourceTests
    {
        private const int BufferSize = 64 * 1024;

        private static byte[] SequentialBytes(int count)
        {
            var data = new byte[count];
            for (var i = 0; i < count; i++)
            {
                data[i] = (byte)i;
            }

            return data;
        }

        [Test]
        public void Read_WithinCurrentWindow_DoesNotCallInnerSourceAgain()
        {
            var inner = new CountingByteSource(new InMemoryByteSource(SequentialBytes(BufferSize * 2)));
            var source = new BufferedByteSource(inner);

            var first = new byte[4];
            source.Read(0, first, 0, 4);
            Assert.That(inner.ReadCallCount, Is.EqualTo(1));

            // Still inside the window the first read filled -- no second inner call.
            var second = new byte[4];
            source.Read(100, second, 0, 4);
            Assert.That(inner.ReadCallCount, Is.EqualTo(1));
            Assert.That(second, Is.EqualTo(new byte[] { 100, 101, 102, 103 }));
        }

        [Test]
        public void Read_OutsideCurrentWindow_TriggersExactlyOneRefill()
        {
            var inner = new CountingByteSource(new InMemoryByteSource(SequentialBytes(BufferSize * 3)));
            var source = new BufferedByteSource(inner);

            var buffer = new byte[4];
            source.Read(0, buffer, 0, 4);
            Assert.That(inner.ReadCallCount, Is.EqualTo(1));

            // Past the end of the first window -- must refill exactly once, not once per byte.
            source.Read(BufferSize + 10, buffer, 0, 4);
            Assert.That(inner.ReadCallCount, Is.EqualTo(2));
            Assert.That(buffer, Is.EqualTo(new byte[]
            {
                unchecked((byte)(BufferSize + 10)), unchecked((byte)(BufferSize + 11)),
                unchecked((byte)(BufferSize + 12)), unchecked((byte)(BufferSize + 13)),
            }));
        }

        [Test]
        public void Read_TwoDistantCursorsAlternating_RefillsOncePerJumpNotPerByte()
        {
            // The exact shape of the bug this class exists to avoid at the call-site level
            // (SerializedFileDetector.ParseExtendedMetadata's two-pass split): two offsets far
            // apart, read alternately. A single shared window can't serve both without refilling
            // on every alternation -- confirms that behavior explicitly, so a future change to
            // BufferedByteSource that silently regresses to per-call refills is caught here rather
            // than only showing up as a multi-second slowdown against real archive data.
            var inner = new CountingByteSource(new InMemoryByteSource(SequentialBytes(BufferSize * 3)));
            var source = new BufferedByteSource(inner);

            var buffer = new byte[4];
            for (var i = 0; i < 5; i++)
            {
                source.Read(0, buffer, 0, 4);
                source.Read(BufferSize * 2, buffer, 0, 4);
            }

            Assert.That(inner.ReadCallCount, Is.EqualTo(10));
        }

        [Test]
        public void Read_LargerThanWindow_BypassesBufferAndReadsInnerDirectly()
        {
            var data = SequentialBytes(BufferSize * 2);
            var inner = new CountingByteSource(new InMemoryByteSource(data));
            var source = new BufferedByteSource(inner);

            var big = new byte[BufferSize + 1];
            var read = source.Read(0, big, 0, big.Length);

            Assert.That(read, Is.EqualTo(big.Length));
            Assert.That(inner.ReadCallCount, Is.EqualTo(1));
            Assert.That(big, Is.EqualTo(new ArraySegment<byte>(data, 0, big.Length)));

            // The bypass must not have left stale/corrupt buffer state behind for a later small read.
            var small = new byte[4];
            source.Read(0, small, 0, 4);
            Assert.That(small, Is.EqualTo(new byte[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void Read_PastEndOfSource_ReturnsShortReadWithoutThrowing()
        {
            var inner = new InMemoryByteSource(SequentialBytes(10));
            var source = new BufferedByteSource(inner);

            var buffer = new byte[8];
            var read = source.Read(6, buffer, 0, 8);

            Assert.That(read, Is.EqualTo(4));
        }

        [Test]
        public void Length_DelegatesToInnerSource()
        {
            var inner = new InMemoryByteSource(SequentialBytes(123));
            var source = new BufferedByteSource(inner);

            Assert.That(source.Length, Is.EqualTo(123));
        }

        [Test]
        public void Constructor_NullInner_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new BufferedByteSource(null));
        }
    }
}
