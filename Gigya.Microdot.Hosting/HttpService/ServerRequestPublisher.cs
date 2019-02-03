using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gigya.Microdot.Hosting.Events;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.HttpService;

namespace Gigya.Microdot.Hosting.HttpService
{

    public interface IServerRequestPublisher
    {
        void TryPublish(ServiceCallEvent callEvent, IEnumerable<DictionaryEntry> arguments, ServiceMethod serviceMethod);
        ServiceCallEvent GetNewCallEvent();
    }

    public class ServerRequestPublisher : IServerRequestPublisher
    {
        private readonly IEventPublisher<ServiceCallEvent> _eventPublisher;
        private readonly IMembersMetadataCache _membersMetadataCache;
        private readonly IServiceEndPointDefinition _serviceEndPointDefinition;

        public ServerRequestPublisher(IEventPublisher<ServiceCallEvent> eventPublisher,
                                      IMembersMetadataCache membersMetadataCache,
                                      IServiceEndPointDefinition serviceEndPointDefinition)
        {
            _eventPublisher = eventPublisher;
            _membersMetadataCache = membersMetadataCache;
            _serviceEndPointDefinition = serviceEndPointDefinition;
        }

        public ServiceCallEvent GetNewCallEvent()
        {
            return _eventPublisher.CreateEvent();
        }

        public void TryPublish(ServiceCallEvent callEvent, IEnumerable<DictionaryEntry> arguments, ServiceMethod serviceMethod)
        {
            EndPointMetadata metaData = _serviceEndPointDefinition.GetMetaData(serviceMethod);
            callEvent.Params = arguments.SelectMany(_ => ExtractParamValues(_, metaData)).Select(_ => new Param
            {
                Name = _.name,
                Value = _.value,
                Sensitivity = _.sensitivity,
            });

            _eventPublisher.TryPublish(callEvent);
        }


        private IEnumerable<(string name, object value, Sensitivity sensitivity)> ExtractParamValues(DictionaryEntry pair, EndPointMetadata metaData)
        {
            var key = pair.Key.ToString();
            var sensitivity = metaData.ParameterAttributes[key].Sensitivity ?? metaData.MethodSensitivity ?? Sensitivity.Sensitive;
            if (metaData.ParameterAttributes[key].IsLogFieldAttributeExists == true && (pair.Value is string) == false)
            {
                var type = pair.Value?.GetType();
                if (type?.IsClass==true)
                {
                    var metaParams = _membersMetadataCache.ParseIntoParams(pair.Value);

                    foreach (var metaParam in metaParams)
                    {
                        var tuple = (
                            name: $"{key}_{metaParam.Name}",
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
