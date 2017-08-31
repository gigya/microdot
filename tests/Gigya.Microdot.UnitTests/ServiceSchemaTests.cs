using System;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Newtonsoft.Json;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests
{
    [TestFixture]
    public class ServiceSchemaTests
    {

        [Test]
        public void ResponseShouldHaveFieldsWithAttributes()
        {
            InterfaceSchema schema = new InterfaceSchema(typeof(IHasReturnType));
            schema.Methods.First().Response.Fields.Length.ShouldBe(2);
            (schema.Methods.First().Response.Fields.First().Attributes.Single().Attribute is JsonPropertyAttribute).ShouldBeTrue();
        }

        [TestCase(typeof(ISimpleReturnObjectString))]
        [TestCase(typeof(ISimpleReturnObject))]
        public void ResponseShouldNotHaveFields(Type type)
        {
            InterfaceSchema schema = new InterfaceSchema(type);
            schema.Methods.First().Response.Fields.ShouldBeEmpty();
        }

        [TestCase(typeof(ISimpleGetter))]
        [TestCase(typeof(ISimpleGetterString))]
        [TestCase(typeof(ISimpleReturnObject))]
        public void ReqestParamShouldBeWithOutAttrbute(Type type)
        {
            InterfaceSchema schema = new InterfaceSchema(type);
            foreach (var parameter in schema.Methods.First().Parameters)
            {
                parameter.Attributes.ShouldBeEmpty();
            }
        }


        [TestCase(typeof(ISimpleGetter))]
        [TestCase(typeof(ISimpleGetterString))]
        [TestCase(typeof(IComplexGetter))]

        public void ReqestParamShouldHaveOneField(Type type)
        {
            InterfaceSchema schema = new InterfaceSchema(type);
            schema.Methods.First().Parameters.Length.ShouldBe(1);
        }

        public void ReqestParamShouldBeWithOutAttrbute11()
        {
            InterfaceSchema schema = new InterfaceSchema(typeof(INoRetrunObject));
            schema.Methods.First().Response.ShouldBeNull();
       
        }

        public void ReqestParamWithComplexObjectShouldHaveAttrbute(Type type)
        {
            InterfaceSchema schema = new InterfaceSchema(type);
            schema.Methods.First().Parameters.First().Fields.First().Attributes.Length.ShouldBe(1);
        }

    }


        internal interface ISimpleGetter
        {
            Task SimpleGet(int someValue);
        }

        internal interface ISimpleGetterString
        {
            Task SimpleGet(string someValue);
        }

        internal interface IComplexGetter
        {
            Task Getter(ComplexType someValue);
        }

        internal interface ISimpleReturnObject
        {
            Task<int> SimpleReturnObject();
        }

        internal interface ISimpleReturnObjectString
        {
            Task<string> SimpleReturnObject();

        }

        internal interface INoRetrunObject
        {
            Task NoRetrunObject();
        }
        internal interface IHasReturnType
        {
            Task<ComplexType> HasReturnType();
        }


        internal class ComplexType
        {
            [JsonProperty]

            public int HasAttribute { get; set; }

            [JsonProperty]
            public int HasAttribute2 { get; set; }
        }
    
}