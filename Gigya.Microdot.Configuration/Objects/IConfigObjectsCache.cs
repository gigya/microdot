using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces;

namespace Gigya.Microdot.Configuration.Objects
{
    public interface IConfigObjectsCache
    {
        void RegisterConfigObjectCreator(IConfigObjectCreator configObjectCreator);
        void DecryptAndReloadConfigObjects(Func<string, string> configDecryptor, Func<string, bool> isValidEncryptedStringFormat);
    }
}
