using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#pragma warning disable 1591

namespace Gigya.Microdot.Configuration
{
    public interface IConfigObjectCreator
    {
        object ChangeNotifications { get; }
        void Init();
        object GetLatest();
    }
}
