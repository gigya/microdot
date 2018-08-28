using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#pragma warning disable 1591

namespace Gigya.Microdot.Ninject
{
    public interface IConfigObjectCreatorWrapper
    {
        object GetLatest();
        Func<T> GetTypedLatestFunc<T>() where T : class;
        Func<T> GetChangeNotificationsFunc<T>() where T : class;
        object GetChangeNotifications();
    }
}
