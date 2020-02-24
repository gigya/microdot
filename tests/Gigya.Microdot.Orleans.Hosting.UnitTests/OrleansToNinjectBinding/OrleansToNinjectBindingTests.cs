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

            registerBinding.ConfigureServices(new ServiceCollection().AddScoped<Dependency>());

            var serviceScopeFactory = kernel.Get<IServiceScopeFactory>();
            var serviceScope = serviceScopeFactory.CreateScope();

            var object1 = serviceScope.ServiceProvider.GetService(typeof(Dependency));
            var object2 = serviceScope.ServiceProvider.GetService(typeof(Dependency));
            Assert.AreEqual(object1, object2);

        }

        [Test]
        public void When_Create_Same_Object_On_Different_Scope_Should_create_Multiple_object()
        {
            IKernel kernel = new StandardKernel();
            // kernel.Load<FuncModule>();
            var registerBinding = new Ninject.Host.NinjectOrleansBinding.OrleansToNinjectBinding(kernel);

            registerBinding.ConfigureServices(new ServiceCollection().AddScoped<Dependency>());
            var serviceScopeFactory = kernel.Get<IServiceScopeFactory>();

            //Scope 1
            var serviceScope = serviceScopeFactory.CreateScope();
            var object1 = serviceScope.ServiceProvider.GetService(typeof(Dependency));
            //Scope 2
            var serviceScope2 = serviceScopeFactory.CreateScope();
            var object2 = serviceScope2.ServiceProvider.GetService(typeof(Dependency));

            Assert.AreNotEqual(object1, object2);

        }

        [Test]
        public void Calling_dispose_on_Scope_should_call_Idisposable_scope_dependency()
        {

            IKernel kernel = new StandardKernel();
            // kernel.Load<FuncModule>();
            var registerBinding = new Ninject.Host.NinjectOrleansBinding.OrleansToNinjectBinding(kernel);

            registerBinding.ConfigureServices(new ServiceCollection().AddScoped<Dependency>().AddScoped<DisposableDependency>());
            var serviceScopeFactory = kernel.Get<IServiceScopeFactory>();

            //Scope 1
            var serviceScope = serviceScopeFactory.CreateScope();
            DisposableDependency object1 = (DisposableDependency)serviceScope.ServiceProvider.GetService(typeof(DisposableDependency));
            serviceScope.Dispose();


            Assert.AreEqual(object1.DisposeCounter, 1);
        }

        /// <remarks>
        /// This test doing some Tweaks to make sure gc is collecting the object event in debug mode
        /// </remarks>
        [Test]
        public void Scope_should_not_be_Root()
        {

            IKernel kernel = new StandardKernel();
            // kernel.Load<FuncModule>();
            var registerBinding = new Ninject.Host.NinjectOrleansBinding.OrleansToNinjectBinding(kernel);

            registerBinding.ConfigureServices(new ServiceCollection().AddScoped<Dependency>());
            var serviceScopeFactory = kernel.Get<IServiceScopeFactory>();

            WeakReference<object> holder = null;
            Action notRootByDebuger = () =>
            {
                holder = new WeakReference<object>(serviceScopeFactory.CreateScope());

            };
            notRootByDebuger();
            MakeSomeGarbage();

            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCApproach();
            GC.Collect(2);


            Assert.False(holder.TryGetTarget(out _), "scope object in not rooted, it should be collected");
        }

        [Test]
        public void Scope_Dependency_should_not_be_Root()
        {

            IKernel kernel = new StandardKernel();
            // kernel.Load<FuncModule>();
            var registerBinding = new Ninject.Host.NinjectOrleansBinding.OrleansToNinjectBinding(kernel);

            registerBinding.ConfigureServices(new ServiceCollection().AddScoped<Dependency>());
            var serviceScopeFactory = kernel.Get<IServiceScopeFactory>();

            WeakReference<object> holder = null;
            Action notRootByDebuger = () =>
            {

                holder = new WeakReference<object>(serviceScopeFactory.CreateScope().ServiceProvider.GetService(typeof(Dependency)));

            };
            notRootByDebuger();
            MakeSomeGarbage();

            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCApproach();
            GC.Collect(2);


            Assert.False(holder.TryGetTarget(out _), "scope object in not rooted, it should be collected");
        }

        [Test]
        public void Scope_Dependency_should_be_Rooted_To_Scope()
        {

            IKernel kernel = new StandardKernel();
            // kernel.Load<FuncModule>();
            var registerBinding = new Ninject.Host.NinjectOrleansBinding.OrleansToNinjectBinding(kernel);

            registerBinding.ConfigureServices(new ServiceCollection().AddScoped<Dependency>());
            var serviceScopeFactory = kernel.Get<IServiceScopeFactory>();
            var scope = serviceScopeFactory.CreateScope();
            WeakReference<object> holder = null;
            Action notRootByDebuger = () =>
            {

                holder = new WeakReference<object>(scope.ServiceProvider.GetService(typeof(Dependency)));

            };
            notRootByDebuger();
            MakeSomeGarbage();

            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCApproach();
            GC.Collect(2);


            Assert.True(holder.TryGetTarget(out _), "Dependency is rooted to scoped, it should bo be collected");
        }


        void MakeSomeGarbage()
        {
            Version vt;

            for (int i = 0; i < 10000; i++)
            {
                // Create objects and release them to fill up memory
                // with unused objects.

                vt = new Version();

            }
        }

        class Dependency
        {

        }

        class DisposableDependency : IDisposable
        {
            public int DisposeCounter;
            public void Dispose()
            {
                Interlocked.Increment(ref DisposeCounter);
            }
        }
    }
}
