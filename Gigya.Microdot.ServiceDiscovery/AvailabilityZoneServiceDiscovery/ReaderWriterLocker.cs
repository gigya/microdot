using System;
using System.Threading;

namespace Gigya.Microdot.ServiceDiscovery.AvailabilityZoneServiceDiscovery
{
    public class ReaderWriterLocker : IDisposable
    {
        public enum LockType
        {
            ReadLock = 0,
            WriteLock
        }

        private readonly LockType _lockType;
        private readonly ReaderWriterLockSlim _readerWriterLock;

        public ReaderWriterLocker(ReaderWriterLockSlim readerWriterLockSlim, LockType lockType)
        {
            _lockType = lockType;
            _readerWriterLock = readerWriterLockSlim;

            if (_lockType == LockType.ReadLock)
                _readerWriterLock.EnterReadLock();
            else
                _readerWriterLock.EnterWriteLock();
        }

        public void Dispose()
        {
            if (_lockType == LockType.ReadLock)
                _readerWriterLock.ExitReadLock();
            else
                _readerWriterLock.ExitWriteLock();
        }
    }
}