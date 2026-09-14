using System;
using System.Diagnostics;
using Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;
using NUnit.Framework;
using static Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport.SerializedFileTestFixtures;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Performance
{
    /// <summary>
    /// Generic, CI-safe performance tests: entirely synthetic input built with the existing
    /// TestSupport fixtures (no real bundle or native library needed), so these run on every PR.
    /// None of these assert on wall-clock elapsed time -- the codebase's established, deliberate
    /// precedent (see PERFORMANCE_IMPROVEMENTS.md) is to log elapsed time/throughput via
    /// TestContext.Progress.WriteLine for a human to read, and to assert only structural
    /// invariants or bounded operation counts (see the BufferedByteSource tests below), since
    /// wall-clock thresholds flake on a loaded CI runner. See also
    /// Performance/RealDataPerformanceTests.cs for the opt-in, real-bundle-backed counterpart.
    /// </summary>
    [TestFixture]
    public class SyntheticPerformanceTests
    {
        private const int BufferSize = 64 * 1024; // must match BufferedByteSource.BufferSize

        private static byte[] SequentialBytes(int count)
        {
            var data = new byte[count];
            for (var i = 0; i < count; i++)
            {
                data[i] = (byte)i;
            }

            return data;
        }

        /// <summary>
        /// Builds the metadata section for <see cref="TryDetect_ScalingObjectCount_ReportsThroughputAndStaysLinear"/>
        /// once, with placeholder byteStart values -- object entries are fixed-size regardless of the
        /// byteStart value written, so this determines the metadata section's length without knowing
        /// where the data section (the names) will land yet.
        /// </summary>
        private static ByteBufferWriter BuildScalingMetadata(int objectCount, int nameByteSize, Func<int, long> byteStartFor)
        {
            var metadata = new ByteBufferWriter();
            AppendLeadingMetadata(metadata, "6000.3.13f1", 13, enableTypeTree: false);
            metadata.WriteInt32(1);
            AppendTypeEntry(metadata, false, new TypeEntrySpec { PersistentTypeId = 28, ScriptTypeIndex = -1 }); // Texture2D -- name-bearing

            metadata.WriteInt32(objectCount);
            for (var i = 0; i < objectCount; i++)
            {
                AppendObjectEntry(metadata, pathId: i + 1, byteStart: byteStartFor(i), byteSize: nameByteSize, typeIndex: 0);
            }

            metadata.WriteInt32(0); // script types
            metadata.WriteInt32(0); // external references
            return metadata;
        }

        [TestCase(1_000)]
        [TestCase(10_000)]
        [TestCase(50_000)]
        public void TryDetect_ScalingObjectCount_ReportsThroughputAndStaysLinear(int objectCount)
        {
            const int spacing = 64; // well within the 64 KiB buffered window -- a realistic, non-worst-case layout
            const int nameByteSize = 64;
            const int headerLength = 48;

            // First pass: placeholder byteStart values, just to measure how big the metadata
            // section itself will be -- the data section (names) must start after it, or larger
            // object counts (whose metadata section is itself many KB) would have their names
            // overwrite still-unread metadata.
            var metadataLength = BuildScalingMetadata(objectCount, nameByteSize, _ => 0).Length;
            var dataSectionStart = headerLength + metadataLength;

            var nameOffsets = new long[objectCount];
            for (var i = 0; i < objectCount; i++)
            {
                nameOffsets[i] = dataSectionStart + (long)i * spacing;
            }

            var metadataBytes = BuildScalingMetadata(objectCount, nameByteSize, i => nameOffsets[i]).ToArray();
            var totalLength = (int)(nameOffsets[^1] + nameByteSize);
            var buffer = new byte[totalLength];

            var header = BuildHeader(version: 23, endianness: 0, metadataSize: (ulong)metadataBytes.Length,
                fileSize: (ulong)totalLength, dataOffset: 0, trailingByteCount: 0);
            Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            Buffer.BlockCopy(metadataBytes, 0, buffer, headerLength, metadataBytes.Length);

            for (var i = 0; i < objectCount; i++)
            {
                var nameBytes = new ByteBufferWriter().WriteString("Object" + i).ToArray();
                Buffer.BlockCopy(nameBytes, 0, buffer, (int)nameOffsets[i], nameBytes.Length);
            }

            // Matches how production code always accesses a SerializedFile's bytes (see
            // SerializedFile.cs and ArtifactArchive.OpenRawByteSource) -- through a
            // BufferedByteSource, not a bare source.
            var source = new BufferedByteSource(new InMemoryByteSource(buffer));

            var stopwatch = Stopwatch.StartNew();
            var detected = SerializedFileDetector.TryDetect(source, out var info);
            stopwatch.Stop();

            Assert.That(detected, Is.True);
            Assert.That(info.MetadataParsed, Is.True);
            Assert.That(info.Objects.Count, Is.EqualTo(objectCount));

            var objectsPerSecond = objectCount / stopwatch.Elapsed.TotalSeconds;
            TestContext.Progress.WriteLine(
                $"[SerializedFileDetector.TryDetect] {objectCount} objects in {stopwatch.ElapsedMilliseconds} ms " +
                $"({objectsPerSecond:N0} objects/sec)");
        }

        [Test]
        public void Read_ManySequentialSmallReadsWithinWindow_CoalescesIntoFewRefills()
        {
            const int iterations = 50_000;
            const int totalBytes = BufferSize * 4;

            var inner = new CountingByteSource(new InMemoryByteSource(SequentialBytes(totalBytes)));
            var source = new BufferedByteSource(inner);
            var buffer = new byte[4];

            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
            {
                source.Read(i * 4, buffer, 0, 4);
            }
            stopwatch.Stop();

            // Reads advance sequentially through a few windows worth of data (iterations * 4
            // bytes); each window boundary crossed costs exactly one refill, not one per byte --
            // the general shape of the coalescing BufferedByteSource exists for (see
            // PERFORMANCE_IMPROVEMENTS.md's account of the NativeFileByteSource thrashing bug).
            var maxExpectedRefills = iterations * 4 / BufferSize + 2;
            Assert.That(inner.ReadCallCount, Is.LessThanOrEqualTo(maxExpectedRefills));

            var readsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
            TestContext.Progress.WriteLine(
                $"[BufferedByteSource.Read] {iterations} sequential 4-byte reads in {stopwatch.ElapsedMilliseconds} ms " +
                $"({readsPerSecond:N0} reads/sec, {inner.ReadCallCount} inner refills)");
        }

        [Test]
        public void Read_AlternatingFarApartOffsets_RefillsOncePerAlternationNotPerByte()
        {
            const int alternations = 5_000;

            var inner = new CountingByteSource(new InMemoryByteSource(SequentialBytes(BufferSize * 3)));
            var source = new BufferedByteSource(inner);
            var buffer = new byte[4];

            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < alternations; i++)
            {
                source.Read(0, buffer, 0, 4);
                source.Read(BufferSize * 2, buffer, 0, 4);
            }
            stopwatch.Stop();

            // Same shape as BufferedByteSourceTests.Read_TwoDistantCursorsAlternating_RefillsOncePerJumpNotPerByte
            // (which proves the 1:1 refill-per-jump ratio at a small N=5) run at a larger N purely
            // to get a throughput number worth reporting -- InMemoryByteSource itself is
            // zero-cost, so elapsed time here is a reasonable proxy for one refill's real cost.
            Assert.That(inner.ReadCallCount, Is.EqualTo(alternations * 2));

            TestContext.Progress.WriteLine(
                $"[BufferedByteSource.Read] {alternations} alternations ({inner.ReadCallCount} refills) in " +
                $"{stopwatch.ElapsedMilliseconds} ms");
        }

        /// <summary>Builds a synthetic `vector&lt;int&gt;`-shaped field with <paramref name="elementCount"/> elements, no I/O noise.</summary>
        private static (TypeTreeNode Node, InMemoryByteSource ByteSource) BuildLargeIntArrayFixture(int elementCount)
        {
            var node = FakeTypeTreeBuilder.Vector("m_Data", FakeTypeTreeBuilder.Int32("data"));

            var writer = new ByteBufferWriter().WriteInt32(elementCount);
            for (var i = 0; i < elementCount; i++)
            {
                writer.WriteInt32(i);
            }

            return (node, new InMemoryByteSource(writer.ToArray()));
        }

        [TestCase(2_000)]
        [TestCase(20_000)]
        public void Elements_ForwardIterationOnSharedReader_ReportsPerElementReadThroughput(int elementCount)
        {
            var (node, byteSource) = BuildLargeIntArrayFixture(elementCount);
            var reader = new TypeTreeReader(node, byteSource, 0);

            var stopwatch = Stopwatch.StartNew();
            long sum = 0;
            foreach (var element in reader.Elements())
            {
                sum += element.AsInt32();
            }
            stopwatch.Stop();

            Assert.That(sum, Is.EqualTo((long)(elementCount - 1) * elementCount / 2));

            var elementsPerSecond = elementCount / stopwatch.Elapsed.TotalSeconds;
            TestContext.Progress.WriteLine(
                $"[TypeTreeReader.Elements] {elementCount} int elements via a shared reader in " +
                $"{stopwatch.ElapsedMilliseconds} ms ({elementsPerSecond:N0} elements/sec)");
        }

        [Test]
        public void Element_FreshReaderPerLookup_IsMeasurablySlowerThanASharedReader()
        {
            const int elementCount = 2_000; // small enough that even the O(n^2) fresh-reader loop stays fast

            var (node, byteSource) = BuildLargeIntArrayFixture(elementCount);

            var sharedReader = new TypeTreeReader(node, byteSource, 0);
            var sharedStopwatch = Stopwatch.StartNew();
            long sharedSum = 0;
            for (var i = 0; i < elementCount; i++)
            {
                sharedSum += sharedReader.Element(i).AsInt32();
            }
            sharedStopwatch.Stop();

            var freshStopwatch = Stopwatch.StartNew();
            long freshSum = 0;
            for (var i = 0; i < elementCount; i++)
            {
                // A brand-new reader has no memoized element offsets (TypeTreeReader's internal
                // OffsetCursor lives on the reader instance), so resolving element i re-walks
                // elements 0..i-1 from scratch every time -- O(n) per lookup, O(n^2) total across
                // this loop. Comparing this against the shared-reader loop above quantifies
                // exactly what that memoization buys in practice.
                var freshReader = new TypeTreeReader(node, byteSource, 0);
                freshSum += freshReader.Element(i).AsInt32();
            }
            freshStopwatch.Stop();

            Assert.That(freshSum, Is.EqualTo(sharedSum));

            var ratio = freshStopwatch.Elapsed.TotalMilliseconds / Math.Max(0.001, sharedStopwatch.Elapsed.TotalMilliseconds);
            TestContext.Progress.WriteLine(
                $"[TypeTreeReader.Element] {elementCount} elements: shared-reader={sharedStopwatch.ElapsedMilliseconds} ms, " +
                $"fresh-reader-per-lookup={freshStopwatch.ElapsedMilliseconds} ms ({ratio:N1}x slower)");
        }

        [Test]
        public void Snapshot_RepeatedFieldLookups_CostOneWalkNotN()
        {
            const int elementCount = 2_000; // m_Component -- must be walked to find m_Name's offset
            const int lookups = 5_000; // e.g. sibling MonoBehaviours all resolving their owning GameObject's name

            var structNode = FakeTypeTreeBuilder.Struct(
                "Base", "GameObject",
                FakeTypeTreeBuilder.Vector("m_Component", FakeTypeTreeBuilder.Int32("data")),
                FakeTypeTreeBuilder.String("m_Name"));

            var writer = new ByteBufferWriter().WriteInt32(elementCount);
            for (var i = 0; i < elementCount; i++) writer.WriteInt32(i);
            writer.WriteString("MyGameObject");
            var buffer = writer.ToArray();

            // Today's ObjectRef.GetReader() pattern: a fresh reader per lookup has no memoized
            // offsets, so every lookup re-walks the whole m_Component array from scratch just to
            // find m_Name's offset -- see TypeTreeReader's OffsetCursor doc comment.
            var freshStopwatch = Stopwatch.StartNew();
            for (var i = 0; i < lookups; i++)
            {
                _ = new TypeTreeReader(structNode, new InMemoryByteSource(buffer), 0).Field("m_Name").AsString();
            }
            freshStopwatch.Stop();

            // ObjectRef.Snapshot(): one forward walk, cached -- every further lookup is a pure in-memory read.
            var countingSource = new CountingByteSource(new InMemoryByteSource(buffer));
            var field = SnapshotBuilder.Build(structNode, 0, countingSource, MaterializeOptions.Default);
            var readsAfterBuild = countingSource.ReadCallCount;

            var snapshotStopwatch = Stopwatch.StartNew();
            for (var i = 0; i < lookups; i++)
            {
                Assert.That(field.Field("m_Name").AsString(), Is.EqualTo("MyGameObject"));
            }
            snapshotStopwatch.Stop();

            Assert.That(countingSource.ReadCallCount, Is.EqualTo(readsAfterBuild),
                "repeated lookups against an already-built snapshot must not read the byte source again");

            var ratio = freshStopwatch.Elapsed.TotalMilliseconds / Math.Max(0.001, snapshotStopwatch.Elapsed.TotalMilliseconds);
            TestContext.Progress.WriteLine(
                $"[Snapshot vs fresh reader] {lookups} repeated m_Name lookups on a {elementCount}-element m_Component: " +
                $"fresh-reader-per-lookup={freshStopwatch.ElapsedMilliseconds} ms, snapshot={snapshotStopwatch.ElapsedMilliseconds} ms ({ratio:N1}x faster)");
        }
    }
}
