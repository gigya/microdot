using System.Threading.Tasks;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.SharedLogic.Measurement.Workload;
using NSubstitute;
using NUnit.Framework;

namespace Gigya.Microdot.UnitTests.SystemInitializer
{
    [TestFixture]
    public class SysInitCalledFromHostTest
    {
        [Test]
        public async Task ValidatorCalledOnce()
        {
            IValidator validatorFake = Substitute.For<IValidator>();
            ServiceHostFake<IValidator> srvHost = new ServiceHostFake<IValidator>(validatorFake);
            Task.Run(() => srvHost.Run());

            await srvHost.WaitForServiceStartedAsync();
            srvHost.Dispose();

            validatorFake.Received(1).Validate();
        }

        [Test]
        public async Task WorkloadMetricsCalledOnce()
        {
            IWorkloadMetrics workloadMetricsFake = Substitute.For<IWorkloadMetrics>();
            ServiceHostFake<IWorkloadMetrics> srvHost = new ServiceHostFake<IWorkloadMetrics>(workloadMetricsFake);
            Task.Run(() => srvHost.Run());
            await srvHost.WaitForServiceStartedAsync();
            srvHost.Dispose();

            workloadMetricsFake.Received(1).Init();
            workloadMetricsFake.Received().Dispose();
        }
    }
}
