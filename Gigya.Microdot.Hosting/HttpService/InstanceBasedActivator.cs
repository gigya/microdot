namespace Gigya.Microdot.Hosting.HttpService
{
    public class InstanceBasedActivator<T> : AbstractServiceActivator
    {
        private readonly T _callable;

        public InstanceBasedActivator(T callable)
        {
            _callable = callable;
        }

        protected override object GetInvokeTarget(ServiceMethod serviceMethod)
        {
            return _callable;
        }
    }
}