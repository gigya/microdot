using System;

namespace Gigya.Microdot.Hosting.UnitTests.Caching.RevokeNotifier
{
    public class RevokeNotifierTestClass
    {
        private readonly int _hashcode;

        public RevokeNotifierTestClass(int hashcodeSeed = 1234)
        {
            _hashcode = hashcodeSeed;
        }
        public override int GetHashCode()
        {
            return _hashcode;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((RevokeNotifierTestClass)obj);
        }

        public bool Equals(RevokeNotifierTestClass other)
        {
            if (other == null)
            {
                return false;
            }

            return _hashcode == other._hashcode;
        }

        public static void CallGC()
        {
            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCApproach();
            GC.Collect(2);
        }
    }
}
