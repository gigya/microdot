using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints.GCEndpoint
{
    public interface IGCEndpointHandlerUtils
    {
        bool TryProcessAsTokenGenerationRequest(NameValueCollection queryString, IPAddress ipAddress,
            out string additionalInfo);
        bool ValidateToken(NameValueCollection queryString, out string additionalInfo);
        bool ValidateGcType(NameValueCollection queryString, out string additionalInfo, out GCType gcType);
        GCCollectionResult Collect(GCType gcType);
    }

    public class GCEndpointHandlerUtils : IGCEndpointHandlerUtils
    {
        private readonly Func<MicrodotHostingConfig> _microdotHostingConfigFactory;
        private readonly ILog _logger;
        private readonly IDateTime _dateTimeFactory;
        private readonly IGCTokenContainer _gcTokenContainer;
        private DateTime _lastCalled = DateTime.MinValue;

        public GCEndpointHandlerUtils(Func<MicrodotHostingConfig> microdotHostingConfigFactory, 
            ILog logger, IDateTime dateTimeFactory, IGCTokenContainer gcTokenContainer)
        {
            _microdotHostingConfigFactory = microdotHostingConfigFactory;
            _logger = logger;
            _dateTimeFactory = dateTimeFactory;
            _gcTokenContainer = gcTokenContainer;
        }

        public bool TryProcessAsTokenGenerationRequest(NameValueCollection queryString, IPAddress ipAddress,
            out string additionalInfo)
        {
            var isGetTokenRequest = queryString.AllKeys.Any(x=> x=="getToken");

            if (isGetTokenRequest)
            {
                var config = _microdotHostingConfigFactory();
                var configGcGetTokenCooldown = config.GCGetTokenCooldown;
                var now = _dateTimeFactory.UtcNow;

                if (false == AssertCoolDownTime(configGcGetTokenCooldown, now, out var gcEndpointCooldownWaitTimeLeft))
                {
                    additionalInfo = $"GC getToken cooldown in effect, will be ready in {gcEndpointCooldownWaitTimeLeft}";
                    return true;
                }

                var token = _gcTokenContainer.GenerateToken();

                _logger.Warn(log=>log("GC getToken was called, see result in Token tag",unencryptedTags:new
                {
                    Token = token,
                    IPAddress = ipAddress.ToString()
                }));
                
                _lastCalled = now;

                additionalInfo = $"GC token generated";
                return true;
            }

            additionalInfo = null;
            return false;
        }

        public bool ValidateToken(NameValueCollection queryString, out string additionalInfo)
        {
            var requestToken = queryString.Get("token")?.ToUpper();

            if (    requestToken == null
                    ||  false == Guid.TryParse(requestToken, out var parsedToken)
                    ||  false == _gcTokenContainer.ValidateToken(parsedToken)  
            )
            {
                additionalInfo =  "Illegal request";
                return false;
            }

            additionalInfo = null;
            return true;
        }
        
        public bool ValidateGcType(NameValueCollection queryString, out string additionalInfo, out GCType gcType)
        {
            var gcTypeQueryParam = queryString.Get("gcType");
                
            if (false == Enum.TryParse(gcTypeQueryParam, out  gcType))
            {
                additionalInfo = "GCEndpoint called with unsupported GCType";
                return false;
            }

            additionalInfo = null;
            return true;
        }
        
        public GCCollectionResult Collect(GCType gcType)
        {
            var sp = Stopwatch.StartNew();
            var totalMemoryBeforeGC = System.GC.GetTotalMemory(false);

            switch (gcType)
            {
                case GCType.Gen0:
                    System.GC.Collect(0, GCCollectionMode.Forced);
                    break;
                case GCType.Gen1:
                    System.GC.Collect(1, GCCollectionMode.Forced);
                    break;
                case GCType.Gen2:
                    System.GC.Collect(2, GCCollectionMode.Forced);
                    break;
                case GCType.LOHCompaction:
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    System.GC.Collect(2, GCCollectionMode.Forced,false, true);
                    break;
                case GCType.BlockingLohCompaction:
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    System.GC.Collect(2, GCCollectionMode.Forced,true, true);
                    break;
                default:
                    throw new ArgumentException("GCType");
            }
                
            var totalMemoryAfterGc = System.GC.GetTotalMemory(false);

            return new GCCollectionResult(
                totalMemoryBeforeGc: totalMemoryBeforeGC,
                totalMemoryAfterGc: totalMemoryAfterGc,
                elapsedMilliseconds: sp.ElapsedMilliseconds
            );
        }
        
        private bool AssertCoolDownTime(TimeSpan? configGcEndpointCooldown, DateTime now, out TimeSpan gcEndpointCooldownWaitTimeLeft)
        {
            if (configGcEndpointCooldown != null
                && configGcEndpointCooldown.HasValue)
            {
                gcEndpointCooldownWaitTimeLeft =
                    now - this._lastCalled - configGcEndpointCooldown.Value;

                if (gcEndpointCooldownWaitTimeLeft < TimeSpan.Zero)
                {
                    gcEndpointCooldownWaitTimeLeft = gcEndpointCooldownWaitTimeLeft.Negate();
                    return false;
                }
                else
                {
                    gcEndpointCooldownWaitTimeLeft = TimeSpan.Zero;
                }
            }
            else
            {
                gcEndpointCooldownWaitTimeLeft = TimeSpan.MaxValue;
            }

            return true;
        }
    }
}