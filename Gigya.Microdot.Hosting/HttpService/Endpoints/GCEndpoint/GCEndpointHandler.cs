using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints.GCEndpoint
{
    public interface IGCEndpointHandler
    {
        Task<GCHandlingResult> Handle(Uri url, NameValueCollection queryString);
    }
    
    public class GCEndpointHandler : IGCEndpointHandler
    {
        private readonly Func<MicrodotHostingConfig> _microdotHostingConfigFactory;
        private readonly ILog _logger;
        private readonly IGCCollectionRunner _gcCollectionRunner;
        private readonly IDateTime _dateTime;

        public DateTime LastCalled = DateTime.MinValue;

        public GCEndpointHandler(Func<MicrodotHostingConfig> microdotHostingConfigFactory, ILog logger, IGCCollectionRunner gcCollectionRunner, IDateTime dateTime)
        {
            _microdotHostingConfigFactory = microdotHostingConfigFactory;
            _logger = logger;
            _gcCollectionRunner = gcCollectionRunner;
            _dateTime = dateTime;
        }

        public async Task<GCHandlingResult> Handle(Uri url, NameValueCollection queryString)
        {
            if (url.AbsolutePath != "/force-traffic-affecting-gc")
                return new GCHandlingResult(false);

            var config = _microdotHostingConfigFactory();


            if (config.GCEndpointEnabled)
            {
                var now = _dateTime.UtcNow;
                var configGcEndpointCooldown = config.GcEndpointCooldown;

                var gcTypeQueryParam = queryString.Get("gcType");
                
                if (false == Enum.TryParse(gcTypeQueryParam, out GCType gcType))
                {
                    return new GCHandlingResult(true, "GCEndpoint called with unsupported GCType" );
                }

                if (false == AssertCoolDownTime(configGcEndpointCooldown, now, out var gcEndpointCooldownWaitTimeLeft)) 
                    return new GCHandlingResult(true,
                        $"GC call cooldown in effect, will be ready in {gcEndpointCooldownWaitTimeLeft}");

                LastCalled = now; 
                    
                var gcCollectionResult = _gcCollectionRunner.Collect(gcType);

                _logger.Info(log=>log("GC endpoint was called",unencryptedTags:new
                {
                    GcType = gcType,
                    TotalMemoryAfterGC = gcCollectionResult.TotalMemoryAfterGC,
                    TotalMemoryBeforeGC = gcCollectionResult.TotalMemoryBeforeGC,
                    GCDuration = gcCollectionResult.ElapsedMilliseconds
                    
                }));

                return new GCHandlingResult(
                    successful:true, 
                    message:"GC ran successfully",
                    gcCollectionResult: gcCollectionResult);
                
            }
            else
            {
                _logger.Info(log=>log("GC endpoint was called, but config is turned off"));
                return new GCHandlingResult(false);
            }

            bool AssertCoolDownTime(TimeSpan? configGcEndpointCooldown, DateTime now, out TimeSpan gcEndpointCooldownWaitTimeLeft)
            {
                if (configGcEndpointCooldown != null
                    && configGcEndpointCooldown.HasValue)
                {
                    gcEndpointCooldownWaitTimeLeft =
                        now - this.LastCalled - configGcEndpointCooldown.Value;

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
}