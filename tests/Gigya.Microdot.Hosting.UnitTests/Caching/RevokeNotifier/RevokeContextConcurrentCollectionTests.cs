using Gigya.Microdot.ServiceProxy.Caching.RevokeNotifier;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Gigya.Microdot.Hosting.UnitTests.Caching.RevokeNotifier
{
    [TestFixture]
    public class RevokeContextConcurrentCollectionTests
    {

        public class RevokeKeySubstitute : IRevokeKey
        {
            public Task OnKeyRevoked(string key)
            {
                return Task.CompletedTask;
            }

            public static RevokeKeySubstitute Instance = new RevokeKeySubstitute();

        }

        [SetUp]
        public void SetUp()
        {

        }

        private RevokeContextConcurrentCollection CreateRevokeContextConcurrentCollection(int entries = 10)
        {
            var res = new RevokeContextConcurrentCollection();
            for (var i = 0; i< entries; i++)
            {
                var rc = this.CreateRevokeContextRooted(i);
                res.Insert(rc);
            }

            return res;
        }

        [Test]
        [TestCase(100)]
        [TestCase(50)]
        [TestCase(0)]
        public void GetEnumerator_Enumerator_Should_Iterate_All_Active_Contexts(int active)
        {
            // Arrange
            var revokeContextConcurrentCollection = this.CreateRevokeContextConcurrentCollection(0);

            // Act
            Parallel.For(0, 100, (i, _) =>
            {
                var collect = i >= active;
                RevokeContext rc;
                if (collect)
                {
                    rc = CreateRevokeContext(i);
                }
                else
                {
                    rc = CreateRevokeContextRooted(i);
                }

                revokeContextConcurrentCollection.Insert(rc);
            });

            // Assert
            Assert.AreEqual(active, revokeContextConcurrentCollection.Count());
        }

        private ConcurrentBag<RevokeNotifierTestClass> _anchor = new ConcurrentBag<RevokeNotifierTestClass>();
        private RevokeContext CreateRevokeContextRooted(int hashcode = 1234)
        {
            var target = new RevokeNotifierTestClass();
            RevokeContext res = new RevokeContext(target,
                RevokeKeySubstitute.Instance,
                    TaskScheduler.Current);
            _anchor.Add(target);
            return res;
        }

        private RevokeContext CreateRevokeContext(int hashcode = 1234)
        {
            RevokeContext res = null;
            Action notRootByDebuger = () =>
            {
                res = new RevokeContext( new RevokeNotifierTestClass(),
                    RevokeKeySubstitute.Instance,
                    TaskScheduler.Current);

            };

            notRootByDebuger();

            RevokeNotifierTestClass.CallGC();

            return res;
        }

        [Test]
        public void MergeMissingEntriesWith_Collection_With_Null_Should_Not_Throw()
        {
            // Arrange
            var revokeContextConcurrentCollection = this.CreateRevokeContextConcurrentCollection(100);

            IRevokeContextConcurrentCollection other = null;

            // Act
            var result = revokeContextConcurrentCollection.MergeMissingEntriesWith(
                other);

            // Assert
            Assert.AreEqual(100, revokeContextConcurrentCollection.Count());
        }

        [Test]
        public void MergeMissingEntriesWith_Collection_With_Another_Collection_Should_Work()
        {
            // Arrange
            var revokeContextConcurrentCollection = CreateRevokeContextConcurrentCollection(100);

            var other = CreateRevokeContextConcurrentCollection(100);

            // Act
            var result = revokeContextConcurrentCollection.MergeMissingEntriesWith(
                other);

            // Assert
            Assert.AreEqual(200, revokeContextConcurrentCollection.Count());
        }

        [Test]
        public void MergeMissingEntriesWith_Collection_With_Another_Collection_Containing_Same_Entries_Should_Not_Add_Entries()
        {
            // Arrange
            var revokeContextConcurrentCollection = new RevokeContextConcurrentCollection();

            var other = new RevokeContextConcurrentCollection();
            var rc = this.CreateRevokeContextRooted();
            revokeContextConcurrentCollection.Insert(rc);
            other.Insert(rc);

            // Act
            var result = revokeContextConcurrentCollection.MergeMissingEntriesWith(
                other);

            // Assert
            Assert.AreEqual(1, revokeContextConcurrentCollection.Count());
        }

        [Test]
        public void Insert_Null_Should_Throw()
        {
            // Arrange
            var revokeContextConcurrentCollection = this.CreateRevokeContextConcurrentCollection();
            RevokeContext context = null;

            // Act & Assert
            Assert.Throws<NullReferenceException>(()=>revokeContextConcurrentCollection.Insert(
                context));
        }

        [Test]
        public void Insert_Entry_Should_work()
        {
            // Arrange
            var revokeContextConcurrentCollection = this.CreateRevokeContextConcurrentCollection(0);
            // Act 
            revokeContextConcurrentCollection.Insert(CreateRevokeContextRooted());
            // Assert
            Assert.AreEqual(1, revokeContextConcurrentCollection.Count());
        }

        [Test]
        public void Insert_Entry_Twice_Should_work_But_Leave_One_Copy()
        {
            // Arrange
            var revokeContextConcurrentCollection = this.CreateRevokeContextConcurrentCollection(0);
            // Act 
            var context = CreateRevokeContextRooted();
            revokeContextConcurrentCollection.Insert(context);
            revokeContextConcurrentCollection.Insert(context);
            // Assert
            Assert.AreEqual(1, revokeContextConcurrentCollection.Count());
        }

        [Test]
        public void RemoveEntryMatchingObject_Null_Object_Should_Fail()
        {
            // Arrange
            var revokeContextConcurrentCollection = this.CreateRevokeContextConcurrentCollection();
            object obj = null;

            // Act
            var result = revokeContextConcurrentCollection.RemoveEntryMatchingObject(
                obj);

            // Assert
            Assert.False(result);
        }

        [Test]
        public void RemoveEntryMatchingObject_None_Existing_Object_Should_Fail()
        {
            // Arrange
            var revokeContextConcurrentCollection = this.CreateRevokeContextConcurrentCollection();
            object obj = new RevokeNotifierTestClass();

            // Act
            var result = revokeContextConcurrentCollection.RemoveEntryMatchingObject(
                obj);

            // Assert
            Assert.False(result);
        }

        [Test]
        public void RemoveEntryMatchingObject_Existing_Object_Should_Succeed()
        {
            // Arrange
            var revokeContextConcurrentCollection = this.CreateRevokeContextConcurrentCollection(0);
            var context = CreateRevokeContextRooted();
            revokeContextConcurrentCollection.Insert(context);
            // Act
            var result = revokeContextConcurrentCollection.RemoveEntryMatchingObject(
                context.Revokee);

            // Assert
            Assert.True(result);
        }

        [Test]
        public void RemoveEntryMatchingObject_Existing_Object_Twice_Should_Fail()
        {
            // Arrange
            var revokeContextConcurrentCollection = this.CreateRevokeContextConcurrentCollection(0);
            var context = CreateRevokeContextRooted();
            revokeContextConcurrentCollection.Insert(context);
            // Act
            revokeContextConcurrentCollection.RemoveEntryMatchingObject(
                context.Revokee);

            var result = revokeContextConcurrentCollection.RemoveEntryMatchingObject(
                context.Revokee);

            // Assert
            Assert.False(result);
        }
        [Test]
        [TestCase(100)]
        [TestCase(50)]
        [TestCase(0)]
        public void Cleanup_None_Rooted_Entries_Should_Be_Removed(int active)
        {
            // Arrange
            var revokeContextConcurrentCollection = this.CreateRevokeContextConcurrentCollection(0);

            // Act
            Parallel.For(0, 100, (i, _) =>
            {
                var collect = i >= active;
                RevokeContext rc;
                if (collect)
                {
                    rc = CreateRevokeContext(i);
                }
                else
                {
                    rc = CreateRevokeContextRooted(i);
                }

                revokeContextConcurrentCollection.Insert(rc);
            });

            // Act
            var result = revokeContextConcurrentCollection.Cleanup();

            // Assert
            Assert.AreEqual(active, revokeContextConcurrentCollection.Count());
        }
    }
}
