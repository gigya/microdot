using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Castle.Core.Internal;
using Gigya.Microdot.Hosting;
using Gigya.Microdot.SharedLogic.Events;
using NUnit.Framework;
using Gigya.ServiceContract.Attributes;
using Shouldly;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    [TestFixture]
    public class ReflectionMetaDataExtensionTests
    {
        private int _numOfProperties;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _numOfProperties = typeof(PersonMockData).GetProperties().Length;
        }

        [Test]
        public void GetProperties_Extract_All_Public_Properties()
        {
            var mock = new PersonMockData();



            //MethodInfo method = typeof(CacheMetadata).GetMethod("ParseIntoParams");
            //MethodInfo genericMethod = method.MakeGenericMethod(typeof(PersonMockData));
            //var tmpParams = (IEnumerable<Param>)genericMethod.Invoke(mock, new[] { argument.Value });



            var reflectionMetadataInfos = CacheMetadata.ExtracMetadata<PersonMockData>().ToList();
            reflectionMetadataInfos.Count.ShouldBe(_numOfProperties);

            foreach (var reflectionMetadata in reflectionMetadataInfos)
            {
                var propertyInfo = typeof(PersonMockData).GetProperty(reflectionMetadata.PropertyName);

                var result = reflectionMetadata.ValueExtractor(mock);

                if (propertyInfo.GetValue(mock).Equals(result) == false)
                {
                    throw new InvalidDataException($"Propery name {propertyInfo.Name} doesn't exists.");
                }

            }
        }


        [Test]
        public void GetProperties_Extract_Sensitive_Attribute()
        {
            const string crypticPropertyName = nameof(PersonMockData.Cryptic);
            const string sensitivePropertyName = nameof(PersonMockData.Sensitive);

            //--------------------------------------------------------------------------------------------------------------------------------------

            var cache = new CacheMetadata();
            var mock = new PersonMockData();

            var @params = cache.ParseIntoParams(mock);

            foreach (var metadataInfo in @params.Where(x => x.Sensitivity != null))
            {
                if (metadataInfo.Name == crypticPropertyName)
                {
                    metadataInfo.Sensitivity.ShouldBe(Sensitivity.Secretive);
                    typeof(PersonMockData).GetProperty(crypticPropertyName).GetValue(mock).ShouldBe(mock.Cryptic);
                }

                if (metadataInfo.Name == sensitivePropertyName)
                {
                    metadataInfo.Sensitivity.ShouldBe(Sensitivity.Sensitive);
                    typeof(PersonMockData).GetProperty(sensitivePropertyName).GetValue(mock).ShouldBe(mock.Sensitive);

                }
            }
        }



        [Test]
        public void CacheMetadata_Extract_All_Public_Properties()
        {
            var cache = new CacheMetadata();
            var mock = new PersonMockData();


            var @params = cache.ParseIntoParams(mock).ToList();


            @params.Count.ShouldBe(_numOfProperties);

            foreach (var param in @params)
            {
                var propertyInfo = typeof(PersonMockData).GetProperty(param.Name);

                if (propertyInfo.GetValue(mock).ToString().Equals(param.Value.ToString()) == false)
                {
                    throw new InvalidDataException($"Propery name {propertyInfo.Name} doesn't exists.");
                }
            }
        }

        [Test]
        public void CacheMetadata_Strength_Test()
        {
            var cache = new CacheMetadata();

            var people = GeneratePeople(10000).ToList();

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            foreach (var person in people)
            {
                var @params = cache.ParseIntoParams(person).ToList();

                @params.Count.ShouldBe(_numOfProperties);
            }
            stopWatch.Stop();
        }



        private IEnumerable<PersonMockData> GeneratePeople(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                yield return new PersonMockData { ID = i, Name = "Name", Cryptic = true };
            }
        }
    }

    internal class PersonOperationMock
    {
        public void PrintPerson([LogFields] PersonMockData person, PersonMockData person2, PersonMockData person3)
        {

        }

    }


    internal class PersonMockData
    {

        public int ID { get; set; } = 10;

        public string Name { get; set; } = "Mocky";

        public bool IsMale { get; set; } = false;

        [Sensitive(Secretive = false)]

        public bool Sensitive { get; set; } = true;

        [Sensitive(Secretive = true)]

        public bool Cryptic { get; set; } = true;



        //public short NotWorking { get; set; } = 0;


    }

}



//private IEnumerable<Param> CreateParamDelegat<TType>(TType instance) where TType : class
//{
//    var paramListType = typeof(List<Param>);
//    var properties = instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);//Cached!

//    var propertiesExpression = Expression.Constant(properties);

//    ParameterExpression count = Expression.Variable(typeof(int), "count");
//    ConstantExpression totoalProps = Expression.Constant(properties.Length);
//    ParameterExpression entity = Expression.Parameter(typeof(TType));
//    var result = Expression.New(paramListType); //typeof(List<Param>)

//    Expression.Block(new ParameterExpression[] { count },
//        Expre

//    );


//    foreach (var propertyInfo in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
//    {
//        var @param = Expression.Constant(new Param());

//        var methodInfo = propertyInfo.GetGetMethod();
//        var getterCall = Expression.Call(entity, methodInfo);
//        var castToObject = Expression.Convert(getterCall, typeof(object));




//    }

//    return new List<Param>();
//}