using Gigya.Microdot.Interfaces.Logging;
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
    public class RevokeKeyIndexerTests
    {
        private IRevokeContextConcurrentCollectionFactory subFunc;
        private ILog subLog;

        [SetUp]
        public void SetUp()
        {
            this.subFunc = Substitute.For<IRevokeContextConcurrentCollectionFactory>();
            this.subLog = Substitute.For<ILog>();
        }

        private RevokeKeyIndexer CreateRevokeKeyIndexer()
        {
            return new RevokeKeyIndexer(
                this.subFunc,
                this.subLog);
        }

        private ConcurrentBag<RevokeNotifierTestClass> _anchor = new ConcurrentBag<RevokeNotifierTestClass>();
        private RevokeContext CreateRevokeContextRooted(int hashcode = 1234)
        {
            var target = new RevokeNotifierTestClass();
            RevokeContext res = new RevokeContext(target,
                RevokeContextConcurrentCollectionTests.RevokeKeySubstitute.Instance,
                TaskScheduler.Current);
            _anchor.Add(target);
            return res;
        }

        private RevokeContext CreateRevokeContext(int hashcode = 1234)
        {
            RevokeContext res = null;
            Action notRootByDebuger = () =>
            {
                res = new RevokeContext(new RevokeNotifierTestClass(),
                    RevokeContextConcurrentCollectionTests.RevokeKeySubstitute.Instance,
                    TaskScheduler.Current);

            };

            notRootByDebuger();

            RevokeNotifierTestClass.CallGC();

            return res;
        }

        [Test]
        public void GetLiveRevokeesAndSafelyRemoveDeadOnes_Null_Key_Should_Throw()
        {
            // Arrange
            var revokeKeyIndexer = this.CreateRevokeKeyIndexer();
            string revokeKey = null;

            // Act & Assert
            Assert.Throws<NullReferenceException>(()=>revokeKeyIndexer.GetLiveRevokeesAndSafelyRemoveDeadOnes(
                revokeKey));
        }

        [Test]
        public void GetLiveRevokeesAndSafelyRemoveDeadOnes_None_Existing_Key_Should_Return_Empty_Enumerable()
        {
            // Arrange
            var revokeKeyIndexer = this.CreateRevokeKeyIndexer();
            string revokeKey = "missing";

            // Act
            var res = revokeKeyIndexer.GetLiveRevokeesAndSafelyRemoveDeadOnes(
                revokeKey);

            // Assert
            Assert.IsEmpty(res);
        }

        [Test]
        public void GetLiveRevokeesAndSafelyRemoveDeadOnes_Existing_Key_Should_Return_None_Empty_Enumerable()
        {
            // Arrange
            var rcc = new RevokeContextConcurrentCollection();
            var sub = Substitute.For<IRevokeContextConcurrentCollectionFactory>();
            sub.Create().Returns(_ => rcc);
            IRevokeContextConcurrentCollectionFactory subFunc = sub;
            var revokeKeyIndexer = new RevokeKeyIndexer(
                sub,
                this.subLog); ;
            string revokeKey = "existing";
            revokeKeyIndexer.AddRevokeContext(revokeKey,CreateRevokeContextRooted());
            // Act
            var res = revokeKeyIndexer.GetLiveRevokeesAndSafelyRemoveDeadOnes(
                revokeKey);

            // Assert
            Assert.AreEqual(1,res.Count());
        }

        [Test]
        public void GetLiveRevokeesAndSafelyRemoveDeadOnes_Existing_Key_For_Collected_Context_Should_Return_Empty_Enumerable()
        {
            // Arrange
            var rcc = new RevokeContextConcurrentCollection();
            var sub = Substitute.For<IRevokeContextConcurrentCollectionFactory>();
            sub.Create().Returns(_ => rcc);
            var revokeKeyIndexer = new RevokeKeyIndexer(
                sub,
                this.subLog); ;
            string revokeKey = "existing";
            revokeKeyIndexer.AddRevokeContext(revokeKey, CreateRevokeContext());
            // Act
            var res = revokeKeyIndexer.GetLiveRevokeesAndSafelyRemoveDeadOnes(
                revokeKey);

            // Assert
            Assert.IsEmpty(res);
        }

        [Test]
        public void GetLiveRevokeesAndSafelyRemoveDeadOnes_dead_Entries_Should_Be_Cleaned()
        {
            // Arrange
            var rcc = new RevokeContextConcurrentCollection();
            var sub = Substitute.For<IRevokeContextConcurrentCollectionFactory>();
            sub.Create().Returns(_ => rcc);
            var revokeKeyIndexer = new RevokeKeyIndexer(
                sub,
                this.subLog); ;
            string revokeKey = "existing";
            revokeKeyIndexer.AddRevokeContext(revokeKey, CreateRevokeContextRooted());
            revokeKeyIndexer.AddRevokeContext(revokeKey, CreateRevokeContext());

            // Act
            var res = revokeKeyIndexer.GetLiveRevokeesAndSafelyRemoveDeadOnes(
                revokeKey);

            // Assert
            Assert.AreEqual(1,res.Count());
        }

        [Test]
        public void GetLiveRevokeesAndSafelyRemoveDeadOnes_dead_Entries_Should_Be_Cleaned2()
        {
            // Arrange
            var rcc = new RevokeContextConcurrentCollection();
            var sub = Substitute.For<IRevokeContextConcurrentCollectionFactory>();
            sub.Create().Returns(_ => rcc);
            var revokeKeyIndexer = new RevokeKeyIndexer(
                sub,
                this.subLog); ;
            string revokeKey = "existing";
            revokeKeyIndexer.AddRevokeContext(revokeKey, CreateRevokeContext());

            // Act
            var res = revokeKeyIndexer.GetLiveRevokeesAndSafelyRemoveDeadOnes(
                revokeKey);

            // Assert
            Assert.False(revokeKeyIndexer.ContainsKey(revokeKey));
        }
        [Test]
        public void AddRevokeContext_Null_Key_Should_Throw()
        {
            // Arrange
            var revokeKeyIndexer = this.CreateRevokeKeyIndexer();
            string key = null;
            RevokeContext context = CreateRevokeContext();

            // Act & Assert
            Assert.Throws<NullReferenceException>(()=>revokeKeyIndexer.AddRevokeContext(
                key,
                context));
        }

        [Test]
        public void AddRevokeContext_Null_Context_Should_Throw()
        {
            // Arrange
            var revokeKeyIndexer = this.CreateRevokeKeyIndexer();
            string key = "someKey";
            RevokeContext context = null;

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => revokeKeyIndexer.AddRevokeContext(
                key,
                context));
        }

        [Test]
        public void AddRevokeContext_Valid_Context_Should_Succeed()
        {
            // Arrange
            var revokeKeyIndexer = this.CreateRevokeKeyIndexer();
            string key = "someKey";
            RevokeContext context = CreateRevokeContextRooted();

            // Act & Assert
            revokeKeyIndexer.AddRevokeContext(
                key,
                context);

            Assert.True(revokeKeyIndexer.ContainsKey(key));
        }

        [Test]
        [TestCase(100)]
        [TestCase(50)]
        [TestCase(0)]
        public void AddRevokeContext_Concurrently_Should_Succeed(int active)
        {
            // Arrange
            var sub = Substitute.For<IRevokeContextConcurrentCollectionFactory>();
            sub.Create().Returns(_ => new RevokeContextConcurrentCollection());
            var revokeKeyIndexer = new RevokeKeyIndexer(
                sub,
                this.subLog); ;

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

                revokeKeyIndexer.AddRevokeContext((i%10).ToString(),rc);
            });

            var count = 0;
            for (var i = 0; i < 10; i++)
            {
                count += revokeKeyIndexer.GetLiveRevokeesAndSafelyRemoveDeadOnes(i.ToString()).Count();
            }

            //Assert
            Assert.AreEqual(active,count);
        }

        [Test]
        public void Remove_Null_Key_Should_Throw()
        {
            // Arrange
            var revokeKeyIndexer = this.CreateRevokeKeyIndexer(); ;

            object obj = null;
            string key = "key";

            // Act & Assert
            Assert.Throws<NullReferenceException>(()=>revokeKeyIndexer.Remove(
                obj,
                key));
        }

        [Test]
        public void Remove_Null_Object_Should_Throw()
        {
            // Arrange
            var revokeKeyIndexer = this.CreateRevokeKeyIndexer(); ;

            object obj = new object();
            string key = null;

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => revokeKeyIndexer.Remove(
                obj,
                key));
        }

        [Test]
        public void Remove_None_Existing_Key_Should_Fail()
        {
            // Arrange
            var sub = Substitute.For<IRevokeContextConcurrentCollectionFactory>();
            sub.Create().Returns(_ => new RevokeContextConcurrentCollection());
            var revokeKeyIndexer = new RevokeKeyIndexer(
                sub,
                this.subLog); ;
            var context = CreateRevokeContextRooted();
            object obj = context.Revokee;
            string realKey = "key";
            string key = "NoSuchKey";
            revokeKeyIndexer.AddRevokeContext(realKey, context);

            // Act
            var res = revokeKeyIndexer.Remove(
                obj,
                key);

            //Assert
            Assert.False(res);
        }

        [Test]
        public void Remove_Existing_Key_Should_Succeed()
        {
            // Arrange
            var sub = Substitute.For<IRevokeContextConcurrentCollectionFactory>();
            sub.Create().Returns(_ => new RevokeContextConcurrentCollection());
            var revokeKeyIndexer = new RevokeKeyIndexer(
                sub,
                this.subLog); ;
            var context = CreateRevokeContextRooted();
            object obj = context.Revokee;
            string key = "key";
            revokeKeyIndexer.AddRevokeContext(key, context);

            // Act
            var res = revokeKeyIndexer.Remove(
                obj,
                key);

            //Assert
            Assert.True(res);
            Assert.False(revokeKeyIndexer.ContainsKey(key));
        }

        [Test]
        public void Remove_Null_Object_Should_Throw2()
        {
            // Arrange
            var revokeKeyIndexer = this.CreateRevokeKeyIndexer();
            object obj = null;

            // Act
            // Act & Assert
            Assert.Throws<NullReferenceException>(() => revokeKeyIndexer.Remove(
                obj));
        }

        [Test]
        public void Remove_Object_Should_Remove_From_All_Keys()
        {
            // Arrange
            var sub = Substitute.For<IRevokeContextConcurrentCollectionFactory>();
            sub.Create().Returns(_ => new RevokeContextConcurrentCollection());
            var revokeKeyIndexer = new RevokeKeyIndexer(
                sub,
                this.subLog); ;
            var context = CreateRevokeContextRooted();
            object obj = context.Revokee;
            string key1 = "key1";
            string key2 = "key2";
            revokeKeyIndexer.AddRevokeContext(key1, context);
            revokeKeyIndexer.AddRevokeContext(key2, context);

            // Act
            revokeKeyIndexer.Remove(obj);

            //Assert
            Assert.IsEmpty(revokeKeyIndexer.GetLiveRevokeesAndSafelyRemoveDeadOnes(key1));
            Assert.IsEmpty(revokeKeyIndexer.GetLiveRevokeesAndSafelyRemoveDeadOnes(key2));
        }

        [Test]
        [TestCase(100)]
        [TestCase(50)]
        [TestCase(0)]
        public void Cleanup_StateUnderTest_ExpectedBehavior(int active)
        {
            // Arrange
            var sub = Substitute.For<IRevokeContextConcurrentCollectionFactory>();
            sub.Create().Returns(_ => new RevokeContextConcurrentCollection());
            var revokeKeyIndexer = new RevokeKeyIndexer(
                sub,
                this.subLog); ;

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

                revokeKeyIndexer.AddRevokeContext((i % 10).ToString(), rc);
            });

            revokeKeyIndexer.Cleanup();

            //Assert
            Assert.AreEqual(active, revokeKeyIndexer.CountRevokees());
        }
    }
}
