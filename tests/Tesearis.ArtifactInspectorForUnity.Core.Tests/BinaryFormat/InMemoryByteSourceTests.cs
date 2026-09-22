using System;
using Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.BinaryFormat
{
    [TestFixture]
    public class InMemoryByteSourceTests
    {
        [Test]
        public void Constructor_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new InMemoryByteSource(null));
        }

        [Test]
        public void Length_ReturnsBufferLength()
        {
            var data = new byte[42];
            var source = new InMemoryByteSource(data);
            Assert.That(source.Length, Is.EqualTo(42L));
        }

        [Test]
        public void Read_NullBuffer_ThrowsArgumentNullException()
        {
            var source = new InMemoryByteSource(new byte[10]);
            Assert.Throws<ArgumentNullException>(() => source.Read(0, null, 0, 4));
        }

        [Test]
        public void Read_NegativeBufferOffset_ThrowsArgumentOutOfRangeException()
        {
            var source = new InMemoryByteSource(new byte[10]);
            Assert.Throws<ArgumentOutOfRangeException>(() => source.Read(0, new byte[10], -1, 4));
        }

        [Test]
        public void Read_NegativeCount_ThrowsArgumentOutOfRangeException()
        {
            var source = new InMemoryByteSource(new byte[10]);
            Assert.Throws<ArgumentOutOfRangeException>(() => source.Read(0, new byte[10], 0, -1));
        }

        [Test]
        public void Read_BufferTooSmall_ThrowsArgumentException()
        {
            var source = new InMemoryByteSource(new byte[10]);
            Assert.Throws<ArgumentException>(() => source.Read(0, new byte[10], 8, 4));
        }

        [Test]
        public void Read_NegativeOffset_ThrowsArgumentOutOfRangeException()
        {
            var source = new InMemoryByteSource(new byte[10]);
            Assert.Throws<ArgumentOutOfRangeException>(() => source.Read(-1, new byte[10], 0, 4));
        }

        [Test]
        public void Read_OffsetAtOrBeyondLength_ReturnsZero()
        {
            var source = new InMemoryByteSource(new byte[10]);
            var buffer = new byte[10];

            Assert.That(source.Read(10, buffer, 0, 4), Is.EqualTo(0));
            Assert.That(source.Read(15, buffer, 0, 4), Is.EqualTo(0));
        }

        [Test]
        public void Read_CountZero_ReturnsZero()
        {
            var source = new InMemoryByteSource(new byte[10]);
            var buffer = new byte[10];

            Assert.That(source.Read(0, buffer, 0, 0), Is.EqualTo(0));
        }

        [Test]
        public void Read_ValidRange_CopiesBytesAndReturnsCount()
        {
            var data = new byte[] { 10, 20, 30, 40, 50 };
            var source = new InMemoryByteSource(data);
            var buffer = new byte[5];

            var read = source.Read(1, buffer, 0, 3);

            Assert.That(read, Is.EqualTo(3));
            Assert.That(buffer[0], Is.EqualTo(20));
            Assert.That(buffer[1], Is.EqualTo(30));
            Assert.That(buffer[2], Is.EqualTo(40));
        }

        [Test]
        public void Read_PartialRangeAtEnd_CopiesAvailableBytes()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var source = new InMemoryByteSource(data);
            var buffer = new byte[5];

            var read = source.Read(3, buffer, 0, 5);

            Assert.That(read, Is.EqualTo(2));
            Assert.That(buffer[0], Is.EqualTo(4));
            Assert.That(buffer[1], Is.EqualTo(5));
        }
    }
}
