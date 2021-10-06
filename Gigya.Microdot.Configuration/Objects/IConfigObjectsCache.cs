using Gigya.Microdot.Interfaces;
using System;

namespace Gigya.Microdot.Configuration.Objects
{
    public interface IConfigObjectsCache
    {
        void RegisterConfigObjectCreator(IConfigObjectCreator configObjectCreator);
        void DecryptAndReloadConfigObjects(Func<string, string> configDecryptor, Func<string, bool> isValidEncryptedStringFormat);
    }
}
