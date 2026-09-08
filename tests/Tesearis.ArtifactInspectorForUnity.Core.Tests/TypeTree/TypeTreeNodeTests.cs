using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TypeTree
{
    /// <summary>
    /// Covers <see cref="TypeTreeNode"/>'s constructor-time shape validation, in particular that a
    /// managed-reference ([SerializeReference]) shaped node no longer fails tree construction --
    /// it's flagged via <see cref="TypeTreeNode.HasUnsupportedManagedReferenceShape"/> instead, and
    /// the documented (README "Known limitations") guarantee that the shape is never silently
    /// misread is enforced lazily, at size-computation time, by
    /// <see cref="TypeTreeOffsetWalkerTests"/>.
    /// </summary>
    [TestFixture]
    public class TypeTreeNodeTests
    {
        [Test]
        public void Constructor_IsManagedReferenceFlag_SetsHasUnsupportedManagedReferenceShape()
        {
            AssertFlagIsRecorded(TypeTreeFlags.IsManagedReference);
        }

        [Test]
        public void Constructor_IsManagedReferenceRegistryFlag_SetsHasUnsupportedManagedReferenceShape()
        {
            AssertFlagIsRecorded(TypeTreeFlags.IsManagedReferenceRegistry);
        }

        [Test]
        public void Constructor_IsArrayOfRefsFlag_SetsHasUnsupportedManagedReferenceShape()
        {
            AssertFlagIsRecorded(TypeTreeFlags.IsArrayOfRefs);
        }

        private static void AssertFlagIsRecorded(TypeTreeFlags flag)
        {
            TypeTreeNode node = null;
            Assert.DoesNotThrow(() => node = new TypeTreeNode(
                "field", "ManagedReferenceType", -1, flag, TypeTreeMetaFlags.None,
                new List<TypeTreeNode>()));
            Assert.That(node.HasUnsupportedManagedReferenceShape, Is.True);
        }

        [Test]
        public void Constructor_NoManagedReferenceFlags_DoesNotSetHasUnsupportedManagedReferenceShape()
        {
            TypeTreeNode node = null;
            Assert.DoesNotThrow(() => node = new TypeTreeNode(
                "field", "int", 4, TypeTreeFlags.None, TypeTreeMetaFlags.None,
                new List<TypeTreeNode>()));
            Assert.That(node.HasUnsupportedManagedReferenceShape, Is.False);
        }
    }
}
