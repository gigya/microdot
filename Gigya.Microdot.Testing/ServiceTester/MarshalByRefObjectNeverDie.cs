using System;
using System.Security.Permissions;

namespace Gigya.Microdot.Testing.ServiceTester
{

    /// <summary>
    /// If we desire to achieve singleton semantics for the remote object, it’s simplest to ensure that it never dies.  This can be done by overriding the InitializeLifetimeService method on your MarshalByRefObject-derived class and returning null .
    /// Otherwise you may get System.Runtime.Remoting.RemotingException: Object [...] has been disconnected or does not exist at the server.
    /// http://stackoverflow.com/questions/2410221/appdomain-and-marshalbyrefobject-life-time-how-to-avoid-remotingexceptions
    /// http://blogs.microsoft.co.il/sasha/2008/07/19/appdomains-and-remoting-life-time-service/
    /// </summary>
    public abstract class MarshalByRefObjectThatNeverDie : MarshalByRefObject
    {
        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.Infrastructure)]
        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}