using System;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Configuration.Objects;
using Ninject;
#pragma warning disable 1591

namespace Gigya.Microdot.Ninject
{
    public class ConfigObjectCreatorWrapper : IConfigObjectCreatorWrapper
    {
        private IConfigObjectCreator _configObjectCreator;
        private readonly Type _configType;
        private readonly IKernel _kernel;

        public ConfigObjectCreatorWrapper(IKernel kernel, Type type)
        {
            _kernel = kernel;
            _configType = type;
        }

        public object GetLatest()
        {
            EnsureCreator();
            return _configObjectCreator.GetLatest();
        }

        public Func<T> GetTypedLatestFunc<T>() where T : class => () => GetLatest() as T;
        public Func<T> GetChangeNotificationsFunc<T>() where T : class => () => GetChangeNotifications() as T;

        public object GetChangeNotifications()
        {
            EnsureCreator();
            return _configObjectCreator.ChangeNotifications;
        }

        private void EnsureCreator()
        {
            if (_configObjectCreator == null)
            {
                var getCreator = _kernel.Get<Func<Type, IConfigObjectCreator>>();
                lock (this)
                {
                    if (_configObjectCreator == null)
                    {
                        var uninitializedCreator = getCreator(_configType);
                        uninitializedCreator.Init();

                        _configObjectCreator = uninitializedCreator;
                    }
                }
            }
        }
    }
}
