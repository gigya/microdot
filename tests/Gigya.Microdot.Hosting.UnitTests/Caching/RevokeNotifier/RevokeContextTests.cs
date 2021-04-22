using Gigya.Microdot.ServiceProxy.Caching.RevokeNotifier;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gigya.Microdot.Hosting.UnitTests.Caching.RevokeNotifier
{
    [TestFixture]
    public class RevokeContextTests
    {
        private Func<string, Task> subFunc;
        private TaskScheduler subTaskScheduler;

        //Rooting anchors
        private static ThreadLocal<RevokeNotifierTestClass> _revokee1 = new ThreadLocal<RevokeNotifierTestClass>(()=> new RevokeNotifierTestClass());
        private static ThreadLocal<RevokeNotifierTestClass> _revokee2 = new ThreadLocal<RevokeNotifierTestClass>(() => new RevokeNotifierTestClass());

        private RevokeNotifierTestClass Revokee1
        {
            get
            {
                return _revokee1.Value;
            }
            set
            {
                _revokee1.Value = value;
            }
        }

        private RevokeNotifierTestClass Revokee2
        {
            get
            {
                return _revokee2.Value;
            }
            set
            {
                _revokee2.Value = value;
            }
        }

        private static object SubstituteLocker = new Object();
        [SetUp]
        public void SetUp()
        {
            this.subFunc = Substitute.For<Func<string, Task>>();
            this.subTaskScheduler = TaskScheduler.Current;
        }

        private RevokeContext CreateRevokeContext(RevokeNotifierTestClass revokee = null, bool collectObject = false)
        {
            RevokeContext res = null;
            Action notRootByDebuger = () =>
            {
                res = new RevokeContext(revokee??new RevokeNotifierTestClass(), 
                    this.subFunc,
                    this.subTaskScheduler);

            };
            
            notRootByDebuger();
            
            if (collectObject)
            {
                RevokeNotifierTestClass.CallGC();
            }
            return res;
        }

        [Test]
        public void TryInvoke_RevokeContext_Invokes_Func_With_Key_On_TaskScheduler()
        {
            // Arrange
            var revokeContext = CreateRevokeContext(Revokee1);
            string key = "foo";

            // Act
            var result = revokeContext.TryInvoke(
                key);

            // Assert
            Assert.True(result);
        }

        [Test]
        public void TryInvoke_RevokeContext__Doesnt_Invokes_Func_If_Revokee_Collected()
        {
            // Arrange
            var revokeContext = CreateRevokeContext(collectObject:true);
            string key = "foo";

            // Act
            var result = revokeContext.TryInvoke(
                key);

            // Assert
            Assert.False(result);
            subFunc.DidNotReceive().Invoke(Arg.Is("foo"));
        }

        [Test]
        public void ObjectEqual_RevokeContext_Is_Not_Equal_To_Null()
        {
            // Arrange
            var revokeContext = CreateRevokeContext(Revokee1);
            RevokeContext entry = null;

            // Act
            var result = revokeContext.ObjectEqual(
                entry);

            // Assert
            Assert.False(result);
        }



        [Test]
        public void ObjectEqual_RevokeContext_Is_Not_Equal_To_Null_Even_If_Collected()
        {
            // Arrange
            var revokeContext = CreateRevokeContext(collectObject:true);
            RevokeContext entry = null;

            // Act
            var result = revokeContext.ObjectEqual(
                entry);

            // Assert
            Assert.False(result);
        }

        [Test]
        public void ObjectEqual_RevokeContext_Is_Not_Equal_While_Revokees_Are()
        {
            // Arrange
            var revokeContext = CreateRevokeContext(Revokee1);
            RevokeContext entry = CreateRevokeContext(Revokee2);

            // Act
            var result = revokeContext.ObjectEqual(
                entry);

            // Assert
            Assert.AreEqual(revokeContext.Revokee,entry.Revokee);
            Assert.False(result);
        }

        [Test]
        public void ObjectEqual_RevokeContext_Is_Not_Equal_While_Revokee_Collected()
        {
            // Arrange
            var revokeContext = CreateRevokeContext(Revokee1);
            RevokeContext entry = CreateRevokeContext(Revokee2);

            // Act
            var result = revokeContext.ObjectEqual(
                entry);

            // Assert
            Assert.AreEqual(revokeContext.Revokee, entry.Revokee);
            Assert.False(result);
        }

        [Test]
        public void ObjectEqual_RevokeContext_Is_Equal_If_Revokee_Equal()
        {
            // Arrange
            var revokee = new RevokeNotifierTestClass();
            var revokeContext1 = CreateRevokeContext(revokee);
            var revokeContext2 = CreateRevokeContext(revokee);

            // Act
            var result = revokeContext1.ObjectEqual(
                revokeContext2);

            // Assert
            Assert.AreEqual(revokeContext1.Revokee, revokeContext2.Revokee);
            Assert.True(result);
        }

        [Test]
        public void ObjectEqual_RevokeContext_Is_Equal_If_Revokee_Equal2()
        {
            // Arrange
            var revokee = new RevokeNotifierTestClass();
            var revokeContext1 = CreateRevokeContext(revokee);

            // Act
            var result = revokeContext1.ObjectEqual(
                revokee);

            // Assert
            Assert.True(result);
        }
    }
}

