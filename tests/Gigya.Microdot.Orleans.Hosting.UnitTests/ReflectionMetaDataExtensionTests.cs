using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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

            var properties = ReflectionMetadataExtension.GetProperties<PersonMockData>().ToList();
            properties.Count.ShouldBe(_numOfProperties);

            foreach (var property in properties)
            {
                var propertyInfo = typeof(PersonMockData).GetProperty(property.PropertyName);

                var result = property.ValueExtractor(mock);

                if (propertyInfo.GetValue(mock).Equals(result) == false)
                {
                    throw new InvalidDataException($"Propery name {propertyInfo.Name} doesn't exists.");
                }
            }
        }


        [Test]
        public void CacheMetadata_Extract_All_Public_Properties()
        {
            var cache = new CacheMetadata();
            var mock = new PersonMockData();

            cache.Register<PersonMockData>();

            var @params = cache.Resolve(mock).ToList();


            @params.Count.ShouldBe(_numOfProperties);

            foreach (var param in @params)
            {
                var propertyInfo = typeof(PersonMockData).GetProperty(param.Name);

                if (propertyInfo.GetValue(mock).ToString().Equals(param.Value) == false)
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
                cache.Register<PersonMockData>();
                var @params = cache.Resolve(person).ToList();

                @params.Count.ShouldBe(_numOfProperties);
            }
           stopWatch.Stop();
        }



        private IEnumerable<PersonMockData> GeneratePeople(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                yield return  new PersonMockData {ID = i , Name = "Name" , Cryptic = true};
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
    }

    internal class PersonMockData
    {
        public int ID { get; set; } = 10;
        public string Name { get; set; } = "Mocky";
        public bool IsMale { get; set; } = false;

        [SensitiveAttribute]
        public bool Cryptic { get; set; } = true;

        //public bool IsPercluded {private get; set; } = true;

    }

    internal class CarData
    {
        public int Year { get; set; } = 10;
        public string Model { get; set; } = "Mocky";

        //public bool IsPercluded {private get; set; } = true;

    }



}