using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TypeTree
{
    /// <summary>
    /// Covers <see cref="TypeTreeNode"/>'s constructor-time validation, in particular the
    /// documented (README "Known limitations") guarantee that a managed-reference
    /// ([SerializeReference]) shaped node throws rather than being silently misread.
    /// </summary>
    [TestFixture]
    public class TypeTreeNodeTests
    {
        [Test]
        public void Constructor_IsManagedReferenceFlag_Throws()
        {
            AssertThrowsForFlag(TypeTreeFlags.IsManagedReference);
        }

        [Test]
        public void Constructor_IsManagedReferenceRegistryFlag_Throws()
        {
            AssertThrowsForFlag(TypeTreeFlags.IsManagedReferenceRegistry);
        }

        [Test]
        public void Constructor_IsArrayOfRefsFlag_Throws()
        {
            AssertThrowsForFlag(TypeTreeFlags.IsArrayOfRefs);
        }

        private static void AssertThrowsForFlag(TypeTreeFlags flag)
        {
            Assert.Throws<UnsupportedManagedReferenceShapeException>(() => new TypeTreeNode(
                "field", "ManagedReferenceType", -1, flag, TypeTreeMetaFlags.None,
                new List<TypeTreeNode>()));
        }

        [Test]
        public void Constructor_NoManagedReferenceFlags_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new TypeTreeNode(
                "field", "int", 4, TypeTreeFlags.None, TypeTreeMetaFlags.None,
                new List<TypeTreeNode>()));
        }
    }
}
