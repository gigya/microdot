using System;
using Gigya.Microdot.Configuration.Objects;
using Ninject;

namespace Gigya.Microdot.Ninject
{
    public class ConfigObjectCreatorWrapper
    {
        private ConfigObjectCreator _configObjectCreator;
        private Type _configType;
        private IKernel _kernel;

        public object ChangeNotifications
        {
            get
            {
                EnsureCreator();
                return _configObjectCreator.ChangeNotifications;
            }
        }

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

        public Func<T> GetTypedLatestFunc<T>() => () => (T)GetLatest();
        public Func<T> GetChangeNotificationsFunc<T>() => () => (T)ChangeNotifications;

        public object GetChangeNotifications()
        {
            return ChangeNotifications;
        }

        private void EnsureCreator()
        {
            if (_configObjectCreator == null)
            {
                lock (this)
                {
                    if (_configObjectCreator == null)
                    {
                        var getCreator = _kernel.Get<Func<Type, ConfigObjectCreator>>();
                        var uninitializedCreator = getCreator(_configType);
                        uninitializedCreator.Init();

                        _configObjectCreator = uninitializedCreator;
                    }
                }
            }
        }
    }
}
