using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceProxy.Caching.RevokeNotifier
{
    public class RevokeContext
    {
        private WeakReference<object> _revokee;
        private WeakReference<IRevokeKey> _revokeKey;
        private TaskFactory _revokeeTaskFactory;


        public RevokeContext(object revokee, IRevokeKey revokeKey, TaskScheduler revokeeTaskScheduler)
        {
            _revokee = new WeakReference<object>(revokee);
            _revokeKey = new WeakReference<IRevokeKey>(revokeKey);
            _revokeeTaskFactory = new TaskFactory(revokeeTaskScheduler);
        }


        public object Revokee => UnWrapWeakReference(_revokee);
        private IRevokeKey RevokeKey => UnWrapWeakReference(_revokeKey);
        public TaskFactory RevokeeTaskFactory => _revokeeTaskFactory;

        private static T UnWrapWeakReference<T>(WeakReference<T> reference) where T:class
        {
            return reference.TryGetTarget(out var target) ? target : null;
        }

        public bool TryInvoke(string key)
        {
            var revokeeTaskFactory = RevokeeTaskFactory;
            var revokeKey = RevokeKey;

            //Those objects might have been collected by the GC need to verify before invoking
            if (revokeeTaskFactory == null || revokeKey == null)
            {
                return false;
            }

            revokeeTaskFactory.StartNew(() => revokeKey.OnKeyRevoked(key));

            return true;
        }


        public bool ObjectEqual(RevokeContext entry)
        {
            if (entry == null)
            {
                return false;
            }
            var ourObj = Revokee;
            var theirObj = entry.Revokee;

            return ObjectEqualInternal(ourObj, theirObj);
        }
        public bool ObjectEqual(object theirObj)
        {
            var ourObj = Revokee;

            return ObjectEqualInternal(ourObj, theirObj);
        }

        private static bool ObjectEqualInternal(object ourObj, object theirObj)
        {

            if (ourObj == null && theirObj == null)
            {
                return true;
            }

            if (ourObj == null || theirObj == null)
            {
                return false;
            }

            return ReferenceEquals(ourObj, theirObj);
        }

    }
}
