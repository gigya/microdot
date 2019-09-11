using System.Linq;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Logging;
using NUnit.Framework;

namespace Gigya.Microdot.UnitTests
{
	[TestFixture,Parallelizable(ParallelScope.Fixtures)]
	public class TagsExtractorTests
	{
		[Test]
		public void AddTagsFromException_SingleExceptionNoConflict_UnencryptedTagsAreSame()
		{
			var exception = new RequestException("", unencrypted: new Tags { { "ut1", "v1" }, { "ut2", "v2" } }).ThrowAndCatch();

		    var encryptedTags = exception.GetEncryptedTagsAndExtendedProperties();
            var unencryptedTags = exception.GetUnencryptedTags();

            CollectionAssert.IsEmpty(encryptedTags.Select(_ => _.Key));
			CollectionAssert.AreEquivalent(new[] { "ut1", "ut2" }, unencryptedTags.Select(_ => _.Key));
		}

		[Test]
		public void AddTagsFromException_SingleExceptionNoConflict_EncryptedTagsAreEncrypted()
		{
			var exception = new RequestException("", encrypted: new Tags { { "et1", "v1" }, { "et2", "v2" } }).ThrowAndCatch();

            var encryptedTags = exception.EncryptedTags.ToList();

            CollectionAssert.AreEquivalent(new[] { "et1", "et2" }, encryptedTags.Select(_ => _.Key));
			CollectionAssert.AreEquivalent(new Tags { { "et1", "v1" }, { "et2", "v2" } }, encryptedTags);
		}

		[Test]
		public void AddTagsFromException_SingleExceptionWithConflict_MixedTagsAreMergedAndEncrypted()
		{
			var exception = new RequestException("", null, new Tags { { "et1", "v1" }, { "mix2", "v2" } }, new Tags { { "ut1", "v1" }, { "mix2", "v3" } }).ThrowAndCatch();

            var allTags = exception.GetEncryptedTagsAndExtendedProperties().Concat(exception.GetUnencryptedTags()).FormatTagsWithoutTypeSuffix().MergeDuplicateTags();

            CollectionAssert.AreEquivalent(new Tags { { "tags.et1", "v1" }, { "tags.ut1", "v1" }, { "tags.mix2", "v2\nv3" } }, allTags);
		}

		[Test]
		public void AddTagsFromException_MultipleExceptionsNoConflict_UnencryptedTagsAreAggregated()
		{
			var innerException = new EnvironmentException("", unencrypted: new Tags { { "ut2", "v2" } }).ThrowAndCatch();
			var exception = new RequestException("", innerException, unencrypted: new Tags { { "ut1", "v1" } }).ThrowAndCatch();

            var unencryptedTags = exception.GetUnencryptedTags().FormatTagsWithoutTypeSuffix().MergeDuplicateTags();

			CollectionAssert.IsEmpty(exception.GetEncryptedTagsAndExtendedProperties());
			CollectionAssert.AreEquivalent(new Tags { { "tags.ut1", "v1" }, { "tags.ut2", "v2" } }, unencryptedTags);
		}

		[Test]
		public void AddTagsFromException_MultipleExceptionsWithConflict_UnencryptedSameKeyTagsAreMerged()
		{
			var innerException = new EnvironmentException("", unencrypted: new Tags { { "ut1", "v1b" }, { "ut3", "v3" } }).ThrowAndCatch();
			var exception = new RequestException("", innerException, unencrypted: new Tags { { "ut1", "v1a" }, { "ut2", "v2" } }).ThrowAndCatch();

            var unencryptedTags = exception.GetUnencryptedTags().FormatTagsWithoutTypeSuffix().MergeDuplicateTags();

            CollectionAssert.IsEmpty(exception.GetEncryptedTagsAndExtendedProperties());
			CollectionAssert.AreEquivalent(new Tags { { "tags.ut1", "v1a\nv1b" }, { "tags.ut2", "v2" }, { "tags.ut3", "v3" } }, unencryptedTags);
		}

		[Test]
		public void AddTagsFromException_MultipleExceptionsWithConflict_EncryptedSameKeyTagsAreMerged()
		{
			var innerException = new EnvironmentException("", encrypted: new Tags { { "et1", "v1b" }, { "et3", "v3" } }).ThrowAndCatch();
			var exception = new RequestException("", innerException, encrypted: new Tags { { "et1", "v1a" }, { "et2", "v2" } }).ThrowAndCatch();

            var encryptedTags = exception.GetEncryptedTagsAndExtendedProperties().FormatTagsWithoutTypeSuffix().MergeDuplicateTags().ToList();

			CollectionAssert.AreEquivalent(new[] { "tags.et1", "tags.et2", "tags.et3" }, encryptedTags.Select(_ => _.Key));
			CollectionAssert.AreEquivalent(new Tags { { "tags.et1", "v1a\nv1b" }, { "tags.et2", "v2" }, { "tags.et3", "v3" } }, encryptedTags);
		}

		[Test]
		public void AddTagsFromException_MultipleExceptionsWithConflict_MixedTagsAreMergedAndEncrypted()
		{
			var innerException = new EnvironmentException("", null, new Tags { { "et1", "v1b" }, { "mix1", "v2" } }, new Tags { { "ut1", "v1b" }, { "mix1", "v4" } }).ThrowAndCatch();
			var exception = new RequestException("", innerException, new Tags { { "et1", "v1a" }, { "mix1", "v1" } }, new Tags { { "ut1", "v1a" }, { "mix1", "v3" } }).ThrowAndCatch();

            var encryptedTags = exception.GetEncryptedTagsAndExtendedProperties().FormatTagsWithoutTypeSuffix().MergeDuplicateTags();
            var allTags = exception.GetEncryptedTagsAndExtendedProperties().Concat(exception.GetUnencryptedTags()).FormatTagsWithoutTypeSuffix().MergeDuplicateTags();

			CollectionAssert.AreEquivalent(new[] { "tags.et1", "tags.mix1" }, encryptedTags.Select(_ => _.Key));
			CollectionAssert.AreEquivalent(new Tags { { "tags.et1", "v1a\nv1b" }, { "tags.ut1", "v1a\nv1b" }, { "tags.mix1", "v1\nv2\nv3\nv4" } }, allTags);
		}

	}
}
