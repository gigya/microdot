using System;
using System.Collections.Generic;
using System.Text;

namespace Gigya.Microdot.ServiceProxy.Caching.RevokeNotifier
{
    public interface IRevokeKeyIndexerFactory
    {
        IRevokeKeyIndexer Create();
    }
}
