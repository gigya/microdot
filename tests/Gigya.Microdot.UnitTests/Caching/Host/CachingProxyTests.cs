using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.Testing.Shared.Service;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gigya.Microdot.UnitTests.Caching.Host
{
    public static class Extension
    {
        public static async Task IgnoreExceptions(this Task task)
        {
            try
            {
                await task;
            }
            catch
            {
                // ignored
            }
        }
    }

    [TestFixture,Parallelizable(ParallelScope.All)]
    public class CachingProxyTests
    {
        private ISlowService Service { get; set; }

        [OneTimeSetUp]
        public void SetUp()
        {
            try
            {
                Service = new NonOrleansServiceTester<SlowServiceHost>(
                        new ServiceArguments(ServiceStartupMode.CommandLineNonInteractive, basePortOverride: DisposablePort.GetPort().Port))
                    .GetServiceProxyWithCaching<ISlowService>();
            }
            catch (Exception ex)
            {
                Console.Write(ex.ToString());
                throw;
            }
        }

        [OneTimeTearDown]
        public void TearDown()
        {
        }

        public enum Parameters { Identical, Different }

        public enum Execute { Consecutively, InParallel }

        public enum Cache { Enabled, Disabled }

        public enum Calls { Succeed, Throw }

        public enum DataType { Simple, Complex }

        public enum ResultsShouldBe { Identical, Different }

        [Theory]
        public async Task MultipleCalls_BehavesAsExpected(Cache cache, Parameters parameters, Execute execute, Calls calls, DataType dataType)
        {
            const int callCount = 30;
            int delay = execute == Execute.Consecutively ? 10 : 100;
            bool shouldThrow = calls == Calls.Throw;
            Func<int, Task<int>> call;

            if (dataType == DataType.Simple)
            {
                var method = cache == Cache.Enabled ? new SimpleDelegate(Service.SimpleSlowMethod) : Service.SimpleSlowMethodUncached;
                call = i => method(parameters == Parameters.Identical ? 0 : i, delay, shouldThrow);
            }
            else // DataType.Complex
            {
                var method = cache == Cache.Enabled ? new ComplexDelegate(Service.ComplexSlowMethod) : Service.ComplexSlowMethodUncached;
                var datas = Enumerable.Range(0, 10).Select(i => new SlowData());
                call = async i => (await method(delay, new[] { new SlowData { SerialNumber = parameters == Parameters.Identical ? 0 : i } }, shouldThrow)).First().SerialNumber;
            }

            List<Task<int>> tasks;

            if (execute == Execute.Consecutively)
            {
                tasks = new List<Task<int>>();
                for (int i = 0; i < callCount; i++)
                {
                    var task = call(i);
                    tasks.Add(task);

                    await task.IgnoreExceptions();
                }
            }
            else // Execute.InParallel
            {
                tasks = Enumerable.Range(0, callCount).Select(i => call(i)).ToList();
                await Task.WhenAll(tasks.Select(t => t.IgnoreExceptions()));
            }

            int distinctResultCount;

            if (calls == Calls.Succeed)
                distinctResultCount = tasks.Select(t => t.Result).Distinct().Count();
            else // Calls.Throws
                distinctResultCount = tasks.Select(t => t.Exception.InnerException.InnerException.Message).ToArray().Distinct().Count();

            var resultsShouldBe = ResultsShouldBe.Different;

            if (cache == Cache.Enabled && parameters == Parameters.Identical)
            {
                if (calls == Calls.Succeed || (calls == Calls.Throw && execute == Execute.InParallel))
                    resultsShouldBe = ResultsShouldBe.Identical;
            }

            distinctResultCount.ShouldBe(resultsShouldBe == ResultsShouldBe.Identical ? 1 : callCount);
        }
    }
}