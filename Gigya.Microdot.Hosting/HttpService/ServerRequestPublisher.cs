using System;
using System.Collections.Generic;
using Gigya.Microdot.Hosting.Events;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic.Events;

namespace Gigya.Microdot.Hosting.HttpService
{

    public interface IServerRequestPublisher
    {
        void TryPublish(ServiceCallEvent callEvent, object[] arguments, ServiceMethod serviceMethod);
        ServiceCallEvent GetNewCallEvent();
    }

    public class ServerRequestPublisher : IServerRequestPublisher
    {
        private readonly IEventPublisher<ServiceCallEvent> _eventPublisher;
        private readonly IMembersToLogExtractor _membersToLogExtractor;
        private readonly IServiceEndPointDefinition _serviceEndPointDefinition;
        private readonly ILog _log;

        public ServerRequestPublisher(IEventPublisher<ServiceCallEvent> eventPublisher,
                                      IMembersToLogExtractor membersToLogExtractor,
                                      IServiceEndPointDefinition serviceEndPointDefinition
                                      ,ILog log)
        {
            _eventPublisher = eventPublisher;
            _membersToLogExtractor = membersToLogExtractor;
            _serviceEndPointDefinition = serviceEndPointDefinition;
            _log = log;
        }

        public ServiceCallEvent GetNewCallEvent()
        {
            return _eventPublisher.CreateEvent();
        }

        public void TryPublish(ServiceCallEvent callEvent, object[] arguments, ServiceMethod serviceMethod)
        {
            try
            {
                if (arguments != null)
                {
                    callEvent.Params = ExtractParamValues(arguments, serviceMethod);
                }
            }
            catch (Exception e)
            {
                _log.Error("Can not extract params from request", exception: e,
                    unencryptedTags: new {serviceInterfaceMethod = serviceMethod.ServiceInterfaceMethod.Name});
            }
            
            _eventPublisher.TryPublish(callEvent);
        }

        private IEnumerable<Param> ExtractParamValues(object[] arguments, ServiceMethod serviceMethod)
        {
            EndPointMetadata metaData = _serviceEndPointDefinition.GetMetaData(serviceMethod);
            var methodParameterInfos = serviceMethod.ServiceInterfaceMethod.GetParameters();
            for (int i = 0; i < arguments.Length; i++)
            {
                var parameterInfo = methodParameterInfos[i];
                var value = arguments[i];
                var sensitivity = metaData.ParameterAttributes[parameterInfo.Name].Sensitivity
                                  ?? metaData.MethodSensitivity ?? Sensitivity.Sensitive;

                if (metaData.ParameterAttributes[parameterInfo.Name].IsLogFieldAttributeExists == true
                    && (value is string) == false && value?.GetType().IsClass == true)
                {
                    var metaParams = _membersToLogExtractor.ExtractMembersToLog(value);

                    foreach (var metaParam in metaParams)
                    {
                       yield return new Param
                        {
                            Name = string.Intern($"{parameterInfo.Name}_{metaParam.Name}"),
                            Value = metaParam.Value,
                            Sensitivity = metaParam.Sensitivity ?? sensitivity
                        };
                    }
                }
                else
                {
                    yield return new Param { Name = parameterInfo.Name, Value = value, Sensitivity = sensitivity };
                }
            }
;
        }
    }
}
