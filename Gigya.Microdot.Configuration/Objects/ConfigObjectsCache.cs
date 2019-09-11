using System;
using System.Collections.Generic;
using Gigya.Microdot.Interfaces;

namespace Gigya.Microdot.Configuration.Objects
{
    public class ConfigObjectsCache : IConfigObjectsCache
    {
        private readonly ConfigDecryptor _configDecryptor;

        public  ConfigObjectsCache(ConfigDecryptor configDecryptor)
        {
            _configDecryptor = configDecryptor;
        }
    
        private List<IConfigObjectCreator> _configObjectCreatorsList = new List<IConfigObjectCreator>();

        public void RegisterConfigObjectCreator(IConfigObjectCreator configObjectCreator)
        {
            if (_configObjectCreatorsList.Contains(configObjectCreator))
                throw new InvalidOperationException();

            _configObjectCreatorsList.Add(configObjectCreator);
        }

        public void DecryptAndReloadConfigObjects(Func<string, string> configDecryptor, Func<string, bool> isValidEncryptedStringFormat)
        {
            _configDecryptor.ConfigDecryptorFunc = configDecryptor;
            _configDecryptor.IsValidEncryptedStringFormat = isValidEncryptedStringFormat;

            foreach (IConfigObjectCreator configObjectCreator in _configObjectCreatorsList)
            {
                configObjectCreator.Reload();
            }
        }
    }
}
