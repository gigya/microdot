using Ninject;

namespace Gigya.Microdot.Ninject.SystemInitializer
{
    public class SystemInitializer : SystemInitializerBase
    {
        public SystemInitializer(IKernel kernel) : base(kernel)
        {
        }

        private void SetDefaultTCPHTTPSettings()
        {
            // See feature https://gigya.tpondemand.com/entity/36211
            //ServicePointManager.DefaultConnectionLimit = _unencryptedConfig.GetInt(
            //    "Networking.ServicePointManager.DefaultConnectionLimit", 500, updated => ServicePointManager.DefaultConnectionLimit = updated);
            //ServicePointManager.UseNagleAlgorithm = _unencryptedConfig.GetBool(
            //    "Networking.ServicePointManager.UseNagleAlgorithm", false, updated => ServicePointManager.UseNagleAlgorithm = updated);
            //ServicePointManager.Expect100Continue = _unencryptedConfig.GetBool(
            //    "Networking.ServicePointManager.Expect100Continue", false, updated => ServicePointManager.Expect100Continue = updated);
        }
    }
}
