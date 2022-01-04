using System;
using System.Collections.Concurrent;
using Gigya.Microdot.Interfaces.SystemWrappers;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints.GCEndpoint
{
    public interface IGCTokenContainer
    {
        Guid GenerateToken();
        bool ValidateToken(Guid tokenToValidate);
    }

    public class GCTokenContainer : IGCTokenContainer
    {
        private readonly IDateTime _dateTimeFactory;
        private ConcurrentDictionary<Guid, DateTime> _gcCollectionTokens = new ConcurrentDictionary<Guid, DateTime>();


        public GCTokenContainer(IDateTime _dateTimeFactory)
        {
            this._dateTimeFactory = _dateTimeFactory;
        }
        
        
        public Guid GenerateToken()
        {
            var now = _dateTimeFactory.UtcNow;

            foreach (var tokenKvp in _gcCollectionTokens)
            {
                if (now - tokenKvp.Value > TimeSpan.FromMinutes(30))
                {
                    _gcCollectionTokens.TryRemove(tokenKvp.Key, out _);
                }
            }

            var newToken = Guid.NewGuid();
            _gcCollectionTokens.TryAdd(newToken, now);

            return newToken;
        }

        public bool ValidateToken(Guid tokenToValidate)
        {
            var now = _dateTimeFactory.UtcNow;
            
            if (_gcCollectionTokens.TryGetValue(tokenToValidate, out var toeknCreationTime))
            {
                if (now - toeknCreationTime < TimeSpan.FromMinutes(30))
                    return true;
                
                _gcCollectionTokens.TryRemove(tokenToValidate, out toeknCreationTime);
            }

            return false;
        }
    }
}