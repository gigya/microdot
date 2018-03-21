using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using Gigya.Microdot.Hosting.Events;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.HttpService;
using Gigya.Microdot.SharedLogic.Events;
using Newtonsoft.Json;

namespace Gigya.Microdot.Hosting.HttpService
{
    public interface IServerRequestPublisher
    {
        void ConstructEvent(HttpServiceRequest requestData, Exception ex, ServiceMethod serviceMethod, double requestTime);
    }

    public class ServerRequestPublisher : IServerRequestPublisher
    {
        private readonly IEventPublisher<ServiceCallEvent> _eventPublisher;
        private readonly IPropertiesMetadataPropertiesCache _metadataPropertiesCache;
        private readonly IServiceEndPointDefinition _serviceEndPointDefinition;

        public ServerRequestPublisher(IEventPublisher<ServiceCallEvent> eventPublisher,
                                      IPropertiesMetadataPropertiesCache metadataPropertiesCache,
                                      IServiceEndPointDefinition serviceEndPointDefinition)
        {
            _eventPublisher = eventPublisher;
            _metadataPropertiesCache = metadataPropertiesCache;
            _serviceEndPointDefinition = serviceEndPointDefinition;
        }

        public void ConstructEvent(HttpServiceRequest requestData, Exception ex, ServiceMethod serviceMethod,
            double requestTime)
        {
            var callEvent = _eventPublisher.CreateEvent();

            callEvent.CalledServiceName = serviceMethod?.GrainInterfaceType.Name;
            callEvent.ClientMetadata = requestData.TracingData;
            callEvent.ServiceMethod = requestData.Target?.MethodName;

            var metaData = _serviceEndPointDefinition.GetMetaData(serviceMethod);
            var arguments = (requestData.Arguments ?? new OrderedDictionary()).Cast<DictionaryEntry>();
            var @params = new List<Param>();

            foreach (var argument in arguments)
            {
                foreach (var (name, value, sensitivity) in ExtractParamValues(argument, metaData))
                {
                    @params.Add(new Param{
                        Name = name,
                        Value = value is string ? value.ToString() : JsonConvert.SerializeObject(value),
                        Sensitivity = sensitivity
                    });
                }
            }

            callEvent.Params = @params;
            callEvent.Exception = ex;
            callEvent.ActualTotalTime = requestTime;
            callEvent.ErrCode = ex != null ? null : (int?)0;

            _eventPublisher.TryPublish(callEvent);
        }


        private IEnumerable<(string name, object value, Sensitivity sensitivity)> ExtractParamValues(DictionaryEntry pair, EndPointMetadata metaData)
        {
            var key = pair.Key.ToString();
            var sensitivity = metaData.ParameterAttributes[key].Sensitivity ?? metaData.MethodSensitivity ?? Sensitivity.Sensitive;
            if (metaData.ParameterAttributes[key].IsLogFieldAttributeExists == true && (pair.Value is string) == false)
            {
                var type = pair.Value.GetType();
                if (type.IsClass)
                {
                    var metaParams = _metadataPropertiesCache.ParseIntoParams(pair.Value);

                    foreach (var metaParam in metaParams)
                    {
                        var tuple = (
                            name: $"{key}.{metaParam.Name}",
                            value: metaParam.Value,
                            sensitivity: metaParam.Sensitivity ?? sensitivity);

                        yield return tuple;
                    }
                }
            }
            else
            {
                yield return (key, pair.Value, sensitivity);
            }
        }

    }
}
