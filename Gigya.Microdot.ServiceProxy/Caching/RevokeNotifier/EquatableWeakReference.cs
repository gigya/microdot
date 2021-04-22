using System;

namespace Gigya.Microdot.ServiceProxy.Caching.RevokeNotifier
{
    /// <summary>
    /// The purpose of this class is to be able to have weakly references objects as keys for collections,
    /// The equality is reference based.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class EquatableWeakReference<T> : IEquatable<EquatableWeakReference<T>>
        where T : class
    {
        private readonly int _hashcode;
        private WeakReference<T> _target;


        public EquatableWeakReference(T target)
        {
            if (null == target)
            {
                throw new NullReferenceException("Revokee can't be null");
            }

            _hashcode = target.GetHashCode();

            _target = new WeakReference<T>(target);
        }


        public T Target => _target.TryGetTarget(out var target) ? target : null;


        public bool Equals(EquatableWeakReference<T> other)
        {
            if (other == null)
            {
                return false;
            }

            if (_hashcode != other._hashcode)
            {
                return false;
            }

            var ourTarget = Target;
            var theirTarget = other.Target;

            if (null == ourTarget && null == theirTarget)
            {
                return true;
            }

            return ReferenceEquals(ourTarget, theirTarget);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((EquatableWeakReference<T>) obj);
        }


        public override int GetHashCode()
        {
            return _hashcode;
        }
    }
}
