using Gigya.Microdot.SharedLogic.Utils;

using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class StringExtensionsTest
    {
        [TestCase(@"c:\foo", @"c:", true)]
        [TestCase(@"c:\foo", @"c:\", true)]
        [TestCase(@"c:\foo", @"c:\foo", true)]
        [TestCase(@"c:\foo", @"c:\foo\", true)]
        [TestCase(@"c:\foo\", @"c:\foo", true)]
        [TestCase(@"c:\foo\bar\", @"c:\foo\", true)]
        [TestCase(@"c:\foo\bar", @"c:\foo\", true)]
        [TestCase(@"c:\foo\a.txt", @"c:\foo", true)]
        [TestCase(@"c:\FOO\a.txt", @"c:\foo", true)]
        [TestCase(@"c:/foo/a.txt", @"c:\foo", true)]
        [TestCase(@"c:\foobar", @"c:\foo", false)]
        [TestCase(@"c:\foobar\a.txt", @"c:\foo", false)]
        [TestCase(@"c:\foobar\a.txt", @"c:\foo\", false)]
        [TestCase(@"c:\foo\a.txt", @"c:\foobar", false)]
        [TestCase(@"c:\foo\a.txt", @"c:\foobar\", false)]
        [TestCase(@"c:\foo\..\bar\baz", @"c:\foo", false)]
        [TestCase(@"c:\foo\..\bar\baz", @"c:\bar", true)]
        [TestCase(@"c:\foo\..\bar\baz", @"c:\barr", false)]
        public void IsSubPathOfTest(string path, string baseDirPath, bool isSubPath)
        {
            path.IsSubPathOf(baseDirPath).ShouldBe(isSubPath);
        }
    }
}