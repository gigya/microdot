using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceProxy.Caching.RevokeNotifier;
using Metrics;
using NSubstitute;
using NUnit.Framework;
using System;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.UnitTests.Caching;
using Ninject;
using Ninject.Extensions.Factory;
using Ninject.Parameters;

namespace Gigya.Microdot.Hosting.UnitTests.Caching.RevokeNotifier
{
    [TestFixture]
    public class RevokeNotifierTests
    {
        private ILog subLog;
        private IRevokeListener subRevokeListener;
        private IRevokeKeyIndexerFactory subFuncRevokeKeyIndexer;
        private Func<RevokeNotifierConfig> subFuncRevokeNotifierConfig;
        private IRevokeKeyIndexer subRevokeKeyIndexer;

        [SetUp]
        public void SetUp()
        {
            this.subLog = Substitute.For<ILog>();
            this.subRevokeListener = Substitute.For<IRevokeListener>();
            this.subRevokeKeyIndexer = Substitute.For<IRevokeKeyIndexer>();
            this.subFuncRevokeKeyIndexer = Substitute.For<IRevokeKeyIndexerFactory>();
            subFuncRevokeKeyIndexer.Create().Returns(_ => subRevokeKeyIndexer);
            this.subFuncRevokeNotifierConfig = ()=>new RevokeNotifierConfig{CleanupIntervalInSec = 1};
        }

        private ServiceProxy.Caching.RevokeNotifier.RevokeNotifier CreateRevokeNotifier()
        {
            return new ServiceProxy.Caching.RevokeNotifier.RevokeNotifier(
                this.subLog,
                this.subRevokeListener,
                this.subFuncRevokeKeyIndexer,
                this.subFuncRevokeNotifierConfig);
        }

        [Test]
        public void NotifyOnRevoke_Null_Object_Should_Throw()
        {
            // Arrange
            var revokeNotifier = this.CreateRevokeNotifier();
            object @this = null;

            string[] revokeKeys = {"key"};

            // Act & Assert
            Assert.Throws<NullReferenceException>(()=>revokeNotifier.NotifyOnRevoke(
                @this,
                RevokeContextConcurrentCollectionTests.RevokeKeySubstitute.Instance,
                revokeKeys));
        }

        [Test]
        public void NotifyOnRevoke_Null_RevokeKey_Should_Throw()
        {
            // Arrange
            var revokeNotifier = this.CreateRevokeNotifier();
            object @this = new object();

            string[] revokeKeys = { "key" };

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => revokeNotifier.NotifyOnRevoke(
                @this,
                null,
                revokeKeys));
        }

        [Test]
        public void NotifyOnRevoke_Null_Keys_Should_Throw()
        {
            // Arrange
            var revokeNotifier = this.CreateRevokeNotifier();
            object @this = new object();
            
            string[] revokeKeys = null;

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => revokeNotifier.NotifyOnRevoke(
                @this,
                RevokeContextConcurrentCollectionTests.RevokeKeySubstitute.Instance,
                revokeKeys));
        }

        [Test]
        public void NotifyOnRevoke_With_Valid_Args_Should_Be_Indexed()
        {
            // Arrange
            var revokeNotifier = this.CreateRevokeNotifier();
            object @this = new object();
            
            var key = "Foo";
            string[] revokeKeys = { key };

            // Act 
            revokeNotifier.NotifyOnRevoke(
                @this,
                RevokeContextConcurrentCollectionTests.RevokeKeySubstitute.Instance,
                revokeKeys);

            //Assert
            subRevokeKeyIndexer.Received(1).AddRevokeContext(key,Arg.Any<RevokeContext>());
        }

        [Test]
        public void NotifyOnRevoke_With_Multiple_Keys_Should_Indexed_Both_Keys()
        {
            // Arrange
            // Arrange
            var revokeNotifier = this.CreateRevokeNotifier();
            object obj = new object();

            var key1 = "Foo1";
            var key2 = "Foo2";

            // Act 
            revokeNotifier.NotifyOnRevoke(obj, RevokeContextConcurrentCollectionTests.RevokeKeySubstitute.Instance, key1, key2);

            //Assert
            subRevokeKeyIndexer.Received(1).AddRevokeContext(key1, Arg.Any<RevokeContext>());
            subRevokeKeyIndexer.Received(1).AddRevokeContext(key2, Arg.Any<RevokeContext>());
        }

        [Test]
        public void RemoveNotifications_Null_Object_Throws()
        {
            // Arrange
            var revokeNotifier = this.CreateRevokeNotifier();
            object @this = null;
            string[] revokeKeys = {"key"};

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => revokeNotifier.RemoveNotifications(@this, revokeKeys));
        }

        [Test]
        public void RemoveNotifications_Null_Keys_Throws()
        {
            // Arrange
            var revokeNotifier = this.CreateRevokeNotifier();
            object @this = new object();
            string[] revokeKeys = null;

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => revokeNotifier.RemoveNotifications(@this, revokeKeys));
        }

        [Test]
        public void RemoveNotifications_Valid_Args_Succeed()
        {
            // Arrange
            var revokeNotifier = this.CreateRevokeNotifier();
            object @this = new object();
            
            var key = "Foo";
            string[] revokeKeys = { key };
            
            revokeNotifier.NotifyOnRevoke(
                @this,
                RevokeContextConcurrentCollectionTests.RevokeKeySubstitute.Instance,
                revokeKeys);
            
            // Act
            revokeNotifier.RemoveNotifications(@this, revokeKeys);
            
            //Assert
            subRevokeKeyIndexer.Received(1).Remove(@this, key);
        }

        [Test]
        public void RemoveAllNotifications_Null_Object_Should_Throw()
        {
            // Arrange
            var revokeNotifier = this.CreateRevokeNotifier();
            object @this = null;

            // Act & Assert
            Assert.Throws<NullReferenceException>(()=> revokeNotifier.RemoveAllNotifications(
                @this));
        }

        [Test]
        public void RemoveAllNotifications_Valid_Object_Should_Removed_All()
        {
            // Arrange
            var revokeNotifier = this.CreateRevokeNotifier();
            object obj1 = new object();
            object obj2 = new object();

            var key1 = "Foo1";
            var key2 = "Foo2";

            revokeNotifier.NotifyOnRevoke(obj1, RevokeContextConcurrentCollectionTests.RevokeKeySubstitute.Instance, key1,key2);
            revokeNotifier.NotifyOnRevoke(obj2, RevokeContextConcurrentCollectionTests.RevokeKeySubstitute.Instance, key1, key2);
            
            // Act
            revokeNotifier.RemoveAllNotifications(obj1);

            //Assert
            subRevokeKeyIndexer.Received(4).AddRevokeContext(Arg.Any<string>(),Arg.Any<RevokeContext>());
            subRevokeKeyIndexer.Received(1).Remove(Arg.Any<object>());
        }

        [Test]
        public void RevokeNotifier_Timer_Callback_Should_Detact_Timer_Interval_Change()
        {
            //Arrange
            bool first = true;
            var revokeNotifier = new ServiceProxy.Caching.RevokeNotifier.RevokeNotifier(
                subLog,
                subRevokeListener,
                subFuncRevokeKeyIndexer,
                ()=>ChangeConfigFunc(ref first));

            RevokeNotifierConfig ChangeConfigFunc(ref bool isFirst)
            {
                if (isFirst)
                {
                    isFirst = false;
                    return new RevokeNotifierConfig {CleanupIntervalInSec = 1};
                }

                return new RevokeNotifierConfig { CleanupIntervalInSec = 7 };
            }

            //Act
            SpinForSeconds(2);

            //Asert
            Assert.AreEqual(7,revokeNotifier.TimerInterval);
        }

        [Test]
        public void RevokeNotifier_Timer_Callback_Should_Cleanup()
        {
            //Arrange
            var revokeNotifier = new ServiceProxy.Caching.RevokeNotifier.RevokeNotifier(
                subLog,
                subRevokeListener,
                subFuncRevokeKeyIndexer,
                () => new RevokeNotifierConfig
                {
                    CleanupIntervalInSec = 1
                });

            //Act
            SpinForSeconds(2);

            //Asert
            subRevokeKeyIndexer.Received().Cleanup();
        }

        private static void SpinForSeconds(int seconds)
        {
            DateTime spinUntil = DateTime.Now + TimeSpan.FromSeconds(seconds);
            SpinWait.SpinUntil(() => DateTime.Now >= spinUntil);
        }

        [Test]
        public void RevokeNotifier_On_Revoke_Event_Should_Be_Invoked()
        {
            //Arrange
            var rm = new FakeRevokingManager();
            var key = "key";
            var revokeNotifier = new ServiceProxy.Caching.RevokeNotifier.RevokeNotifier(
                subLog,
                rm,
                subFuncRevokeKeyIndexer,
                subFuncRevokeNotifierConfig);

            //Act
            rm.Revoke(key);

            SpinForSeconds(2);

            //Asert
            subRevokeKeyIndexer.Received(1).GetLiveRevokeesAndSafelyRemoveDeadOnes(key);
        }

        [Test]
        public void Can_Create_RevokeNotifier_If_All_Bindings_Are_Set()
        {
            var kernel = new StandardKernel(new MicrodotModule());
            kernel.Bind<ILog>().To<TraceLog>().InSingletonScope();
            Assert.DoesNotThrow(()=>kernel.Get<IRevokeNotifier>());
            Assert.DoesNotThrow(() => kernel.Get<IRevokeContextConcurrentCollection>());
            Assert.DoesNotThrow(() => kernel.Get<EquatableWeakReference<object>>(new ConstructorArgument("target",new object())));
            Assert.DoesNotThrow(() => kernel.Get<IRevokeContextConcurrentCollectionFactory>());
            Assert.DoesNotThrow(() => kernel.Get<IRevokeKeyIndexer>());
            Assert.DoesNotThrow(() => kernel.Get<IRevokeKeyIndexerFactory>());
        }
    }
}
