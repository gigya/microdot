using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.SharedLogic.Configurations.Serialization;
using System;
using System.Linq;

namespace Gigya.Microdot.SharedLogic.HttpService
{
    public interface IServiceSchemaPostProcessor
    {
        void PostProcessServiceSchema(ServiceSchema serviceSchema);
    }

    public class ServiceSchemaPostProcessor : IServiceSchemaPostProcessor
    {
        private readonly IMicrodotSerializationConstraints _serializationConstraints;

        public ServiceSchemaPostProcessor(IMicrodotSerializationConstraints serializationConstraints)
        {
            _serializationConstraints = serializationConstraints;
        }

        public void PostProcessServiceSchema(ServiceSchema serviceSchema)
        {
            foreach (var curInterface in serviceSchema.Interfaces)
            {
                PostProcessInterface(curInterface);
            }
        }

        private void PostProcessInterface(InterfaceSchema curInterface)
        {
            if (curInterface.Attributes != null)
            {
                PostProcessAttributes(curInterface.Attributes);
            }

            foreach (var curMethod in curInterface.Methods)
            {
                if (curMethod.Attributes != null)
                {
                    PostProcessAttributes(curMethod.Attributes);
                }

                if (curMethod.Parameters != null)
                {
                    foreach (var curMethodParameter in curMethod.Parameters)
                    {
                        var alternagiveQuaAlternativeFullyQualifiedName =
                            GetAlternagiveQuaAlternativeFullyQualifiedName(curMethodParameter.Type);
                        curMethodParameter.TypeName = alternagiveQuaAlternativeFullyQualifiedName ??
                                                      curMethodParameter.TypeName;

                        if (curMethodParameter.Fields != null)
                        {
                            PostProcessFields(curMethodParameter.Fields);
                        }
                    }
                }

                if (curMethod.Response != null)
                {
                    if (curMethod.Response.Attributes != null)
                    {
                        PostProcessAttributes(curMethod.Response.Attributes);
                    }

                    if (curMethod.Response.Fields != null)
                    {
                        PostProcessFields(curMethod.Response.Fields);
                    }
                }
            }
        }

        private void PostProcessFields(FieldSchema[] fields)
        {
            foreach (var curMethodField in fields)
            {
                var curMethodFieldAlternativeFullyQualifiedName =
                    GetAlternagiveQuaAlternativeFullyQualifiedName(curMethodField.Type);

                curMethodField.TypeName =
                    curMethodFieldAlternativeFullyQualifiedName ?? curMethodField.TypeName;

                if (curMethodField.Attributes != null)
                {
                    PostProcessAttributes(curMethodField.Attributes);
                }
            }
        }

        private void PostProcessAttributes(AttributeSchema[] attributes)
        {
            foreach (var curAttribute in attributes.Where(x => x.Attribute != null))
            {
                var alternagiveQuaAlternativeFullyQualifiedName =
                    GetAlternagiveQuaAlternativeFullyQualifiedName(curAttribute.Attribute.GetType());
                curAttribute.TypeName = alternagiveQuaAlternativeFullyQualifiedName ?? curAttribute.TypeName;
            }
        }

        private string GetAlternagiveQuaAlternativeFullyQualifiedName(Type serializedType)
        {
            var typeConverstionResult = _serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                serializedType, serializedType.Assembly.FullName, serializedType.FullName);

            if (typeConverstionResult.AssemblyName != serializedType.Assembly.FullName ||
                typeConverstionResult.TypeName != serializedType.FullName)
            {
                return $"{typeConverstionResult.TypeName}, {typeConverstionResult.AssemblyName}";
            }

            return null;
        }
    }
}