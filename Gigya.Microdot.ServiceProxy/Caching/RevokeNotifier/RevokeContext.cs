using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceProxy.Caching.RevokeNotifier
{
    public class RevokeContext
    {
        private WeakReference<object> _revokee;
        private WeakReference<Func<string, Task>> _callback;
        private WeakReference<TaskFactory> _revokeeTaskScheduler;


        public RevokeContext(object revokee, Func<string, Task> callback, TaskScheduler revokeeTaskScheduler)
        {
            _revokee = new WeakReference<object>(revokee);
            _callback = new WeakReference<Func<string, Task>>(callback);
            _revokeeTaskScheduler = new WeakReference<TaskFactory>(new TaskFactory(revokeeTaskScheduler));
        }


        public object Revokee => UnWrapWeakReference(_revokee);
        private Func<string, Task> Callback => UnWrapWeakReference(_callback);
        private TaskFactory RevokeeTaskScheduler => UnWrapWeakReference(_revokeeTaskScheduler);

        private static T UnWrapWeakReference<T>(WeakReference<T> reference) where T:class
        {
            return reference.TryGetTarget(out var target) ? target : null;
        }

        public bool TryInvoke(string key)
        {
            var scheduler = RevokeeTaskScheduler;
            var callback = Callback;

            //Those objects might have been collected by the GC need to verify before invoking
            if (scheduler == null || callback == null)
            {
                return false;
            }

            scheduler.StartNew(() => callback(key));

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
