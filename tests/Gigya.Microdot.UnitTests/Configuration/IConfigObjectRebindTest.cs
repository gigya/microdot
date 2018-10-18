using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.SystemInitializer;
using Gigya.Microdot.ServiceDiscovery.Config;
using Ninject;
using NSubstitute;
using NUnit.Framework;

namespace Gigya.Microdot.UnitTests.Configuration
{
    [TestFixture]
    public class IConfigObjectRebindTest
    {
        private StandardKernel _testingKernel;
        private IConfigObjectCreator _configObjectCreatorMock = Substitute.For<IConfigObjectCreator>();

        [SetUp]
        public void SetUp()
        {
            _testingKernel = new StandardKernel();
            _testingKernel.Rebind<Func<Type, IConfigObjectCreator>>().ToMethod(t => tp => _configObjectCreatorMock);
            _testingKernel.Load<MicrodotModule>();
            _testingKernel.Rebind<SystemInitializerBase>().To<SystemInitializer>();
            _testingKernel.Get<SystemInitializerBase>().Init();
        }

        [TearDown]
        public void TearDown()
        {
            _testingKernel.Dispose();
            _configObjectCreatorMock.ClearReceivedCalls();
        }

        [Test]
        public void ShouldCallGetLatestWhileResolvingObject()
        {
            _configObjectCreatorMock.GetLatest().Returns(new DiscoveryConfig());
            _testingKernel.Get<DiscoveryConfig>();

            _configObjectCreatorMock.Received(1).GetLatest();
            object notes = _configObjectCreatorMock.DidNotReceive().ChangeNotifications;
        }

        [Test]
        public void ShouldCallChangeNotificationsWhileResolvingISourceBlockObject()
        {
            _configObjectCreatorMock.GetLatest().Returns(new DiscoveryConfig());
            _configObjectCreatorMock.ChangeNotifications.Returns(Substitute.For<ISourceBlock<DiscoveryConfig>>());
            _testingKernel.Get<ISourceBlock<DiscoveryConfig>>();

            _configObjectCreatorMock.DidNotReceive().GetLatest();
            object notifications = _configObjectCreatorMock.Received(1).ChangeNotifications;
        }

        public dynamic GetLambdaOfGetLatest(Type configType)
        {
            return GetGenericFuncCompiledLambda(configType, "GetTypedLatestFunc");
        }

        public dynamic GetLambdaOfChangeNotifications(Type configType)
        {
            return GetGenericFuncCompiledLambda(configType, "GetChangeNotificationsFunc");
        }

        private dynamic GetGenericFuncCompiledLambda(Type configType, string functionName)
        {//happens only once while loading, but can be optimized by creating Method info before sending to this function, if needed
            MethodInfo func = typeof(ConfigObjectCreator).GetMethod(functionName).MakeGenericMethod(configType);
            Expression instance = Expression.Constant(_configObjectCreatorMock);
            Expression callMethod = Expression.Call(instance, func);
            Type delegateType = typeof(Func<>).MakeGenericType(configType);
            Type parentExpressionType = typeof(Func<>).MakeGenericType(delegateType);

            dynamic lambda = Expression.Lambda(parentExpressionType, callMethod).Compile();

            return lambda;
        }
    }
}
