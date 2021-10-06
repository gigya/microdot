using Gigya.Microdot.ServiceDiscovery;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Gigya.Microdot.UnitTests.Discovery
{
    public sealed class ConsulClientMock: IConsulClient
    {
        private readonly BroadcastBlock<EndPointsResult> _resultChanged = new BroadcastBlock<EndPointsResult>(null);
        private EndPointsResult _lastResult;
        private bool _initialized = false;
        private bool _disposed = false;
        private readonly object _lastResultLocker = new object();
        private Timer _resultsTimer;

        public TaskCompletionSource<bool> InitFinished { get; } = new TaskCompletionSource<bool>();

        public void SetResult(EndPointsResult result)
        {
            lock (_lastResultLocker)
            {
                var lastResult = _lastResult;

                _lastResult = result;
                Result = result;

                if (lastResult?.Equals(result)==false && _initialized)
                    _resultChanged.Post(result);
            }
        }
        public void Dispose()
        {
            lock (this)
            {
                _disposed = true;
                _resultsTimer?.Dispose();
            }
        }

        public async Task Init()
        {
            lock (_lastResultLocker)
            {
                _initialized = true;
                if (_lastResult != null)
                    _resultChanged.Post(_lastResult);

                if (_resultsTimer == null)
                {
                    _resultsTimer = new Timer(_ =>
                    {
                        _resultChanged.Post(_lastResult);
                    }, null, 100, 100);
                }

            }
            InitFinished.SetResult(true);
        }

        public EndPointsResult Result { get; set; } = new EndPointsResult();
        public ISourceBlock<EndPointsResult> ResultChanged => _resultChanged;
        public Uri ConsulAddress => new Uri("http://fakeConsulAddress:8500");
    }
}