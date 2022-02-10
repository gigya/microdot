
using Gigya.Microdot.LanguageExtensions;
using NUnit.Framework;
using Shouldly;
using System;

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


        [Test]
        public void GetDeterministicHashCode_ThrowsForNullInput()
        {
            string str = null;
            Should.Throw<ArgumentNullException>(() => str.GetDeterministicHashCode());
        }

        [TestCase("dlshjksdjkfhasdjk", 1472961354)]
        [TestCase("", 23)]
        public void GetDeterministicHashCode_ReturnsCorrectHashCode(string str, int expectedHashCode)
        {
            str.GetDeterministicHashCode().ShouldBe(expectedHashCode);            
        }
    }
}