using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Ninject;
using NUnit.Framework;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.OrleansToNinjectBinding
{
    [TestFixture, Parallelizable(ParallelScope.All)]
    public class OrleansToNinjectBindingTests
    {
        [Test]
        public void When_Create_Same_Object_On_Scope_Should_create_one_object()
        {
            IKernel kernel = new StandardKernel();
            // kernel.Load<FuncModule>();
            var registerBinding = new Ninject.Host.NinjectOrleansBinding.OrleansToNinjectBinding(kernel);

            registerBinding.ConfigureServices(new ServiceCollection().AddScoped<SmocoScpe>());

            var serviceScopeFactory = kernel.Get<IServiceScopeFactory>();
            var serviceScope = serviceScopeFactory.CreateScope();

            var object1 = serviceScope.ServiceProvider.GetService(typeof(SmocoScpe));
            var object2 = serviceScope.ServiceProvider.GetService(typeof(SmocoScpe));
            Assert.AreEqual(object1, object2);

        }

        [Test]
        public void When_Create_Same_Object_On_Different_Scope_Should_create_Multiple_object()
        {
            IKernel kernel = new StandardKernel();
            // kernel.Load<FuncModule>();
            var registerBinding = new Ninject.Host.NinjectOrleansBinding.OrleansToNinjectBinding(kernel);

            registerBinding.ConfigureServices(new ServiceCollection().AddScoped<SmocoScpe>());
            var serviceScopeFactory = kernel.Get<IServiceScopeFactory>();

            //Scope 1
            var serviceScope = serviceScopeFactory.CreateScope();
            var object1 = serviceScope.ServiceProvider.GetService(typeof(SmocoScpe));
            //Scope 2
            var serviceScope2 = serviceScopeFactory.CreateScope();
            var object2 = serviceScope2.ServiceProvider.GetService(typeof(SmocoScpe));

            Assert.AreNotEqual(object1, object2);

        }

        [Test]
        public void Calling_dispose_on_Scope_should_call_Idisposable_scope_dependency()
        {

            IKernel kernel = new StandardKernel();
            // kernel.Load<FuncModule>();
            var registerBinding = new Ninject.Host.NinjectOrleansBinding.OrleansToNinjectBinding(kernel);

            registerBinding.ConfigureServices(new ServiceCollection().AddScoped<SmocoScpe>());
            registerBinding.ConfigureServices(new ServiceCollection().AddScoped<DisposableSmocoScope>());
            var serviceScopeFactory = kernel.Get<IServiceScopeFactory>();

            //Scope 1
            var serviceScope = serviceScopeFactory.CreateScope();
            DisposableSmocoScope object1 = (DisposableSmocoScope)serviceScope.ServiceProvider.GetService(typeof(DisposableSmocoScope));
            serviceScope.Dispose();


            Assert.AreEqual(object1.DisposeCounter, 1);
        }

        class SmocoScpe
        {

        }

        class DisposableSmocoScope : IDisposable
        {
            public int DisposeCounter = 0;
            public void Dispose()
            {
                Interlocked.Increment(ref DisposeCounter);
            }
        }
    }
}
