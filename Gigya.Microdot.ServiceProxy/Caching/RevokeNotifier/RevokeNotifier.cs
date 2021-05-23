using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Interfaces.Logging;
using Metrics;
using Timer = System.Threading.Timer;

namespace Gigya.Microdot.ServiceProxy.Caching.RevokeNotifier
{
    public class RevokeNotifier : IRevokeNotifier
    {
        private ILog _logger;
        private readonly IRevokeKeyIndexer _revokeIndexer;
        private readonly Func<RevokeNotifierConfig> _configFunc;
        private Timer _timer;
        private int _timerInterval;


        public RevokeNotifier(ILog logger, 
                              IRevokeListener revokeListener,
                              IRevokeKeyIndexerFactory indexerFactory,
                              Func<RevokeNotifierConfig> configFunc)
        {
            _logger = logger;
            _revokeIndexer = indexerFactory.Create();
            ITargetBlock<string> actionBlock = new ActionBlock<string>(OnRevoke); //TODO: move to new class
            _configFunc = configFunc;
            var config = _configFunc();
            _timerInterval = config.CleanupIntervalInSec;
            var ts = TimeSpan.FromSeconds(_timerInterval);
            _timer = new Timer(RevokeNotifierTimerCallback, null, ts, ts);
            revokeListener.RevokeSource.LinkTo(actionBlock);

        }

        public int TimerInterval => _timerInterval;


        private void RevokeNotifierTimerCallback(object state)
        {
            _revokeIndexer.Cleanup();
            ModifyTimerIntervalIfNeeded();
        }


        private void ModifyTimerIntervalIfNeeded()
        {

            var config = _configFunc();
            if (config.CleanupIntervalInSec != _timerInterval)
            {
                var prev = _timerInterval;
                _timerInterval = config.CleanupIntervalInSec;
                _timer.Change(TimeSpan.FromSeconds(_timerInterval), TimeSpan.FromSeconds(_timerInterval));
                _logger.Info(logger => logger("Timer interval changed for cleanup",
                                 unencryptedTags: new
                                 {
                                     from = prev,
                                     to = _timerInterval
                                 }));
            }
        }


        protected virtual Task OnRevoke(string revokeKey)
        {
            int revokeeCount = 0;
            foreach (var context in _revokeIndexer.GetLiveRevokeesAndSafelyRemoveDeadOnes(revokeKey))
            {
                context.TryInvoke(revokeKey);
                revokeeCount++;
            }
            //Don't want to spam too much logs
            if (revokeeCount > 0)
            {
                _logger.Debug(logger => logger("Notifying listeners about revoke key",
                                  unencryptedTags: new
                                  {
                                      revokeKey,
                                      revokeeCount
                                  }));
            }
            
            return Task.CompletedTask;
        }

        public  void NotifyOnRevoke(object @this, IRevokeKey revokeKey, params string[] revokeKeys)
        {
            if (null == @this)
            {
                throw new NullReferenceException("Object can't be null");
            }

            if (null == revokeKey)
            {
                throw new NullReferenceException("IRevokeKey can't be null");
            }

            if (null == revokeKeys)
            {
                throw new NullReferenceException("Revoke keys can't be null");
            }

            foreach (var key in revokeKeys)
            {
                NotifyOnRevokeOnce(key, @this, revokeKey);
            }
            
        }

        protected  void NotifyOnRevokeOnce(string key, object @this, IRevokeKey revokeKey)
        {
            var newContext = new RevokeContext(@this, revokeKey, TaskScheduler.Current);
            _revokeIndexer.AddRevokeContext(key, newContext);
        }

        public  void RemoveNotifications(object @this, params string[] revokeKeys)
        {
            if (null == @this)
            {
                throw new NullReferenceException("Object can't be null");
            }

            if (null == revokeKeys)
            {
                throw new NullReferenceException("Revoke keys can't be null");
            }

            foreach (var key in revokeKeys)
            {
                RemoveNotificationsOnce(@this, key);
            }
        }

        protected  void RemoveNotificationsOnce(object @this, string key)
        {
            if (null == @this)
            {
                throw new NullReferenceException("Object can't be null");
            }

            if (null == key)
            {
                throw new NullReferenceException("Key can't be null");
            }
            _revokeIndexer.Remove(@this, key);
        }

        public  void RemoveAllNotifications(object @this)
        {
            if (null == @this)
            {
                throw new NullReferenceException("Object can't be null");
            }
            _revokeIndexer.Remove(@this);
        }
    }
}
