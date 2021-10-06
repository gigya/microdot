using Gigya.Microdot.SharedLogic.Configurations.Serialization;
using Gigya.Microdot.SharedLogic.Security;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using System;
using System.Net.Http;

namespace Gigya.Microdot.UnitTests.Serialization
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class GigyaTypePolicySerializationBinderTests
    {
        public class MyException:Exception
        {
            public MyException(string messge):base(messge)
            {
                
            }    
        }
        
        [Test]
        public void ShouldReplaceTypesWhenHasAReplacement()
        {
            var type = typeof(string);
            var assemblyFullName = type.Assembly.FullName;
            var typeFullName = type.FullName;

            var expectedType = typeof(HttpClient);
            var expectedAssemblyFullName = expectedType.Assembly.FullName;
            var expectedTypeFullName = expectedType.FullName;
            
            var constraints = Substitute.For<IMicrodotSerializationConstraints>();

            constraints.TryGetAssemblyNameAndTypeReplacement(assemblyFullName, typeFullName)
                .Returns(new AssemblyAndTypeName(expectedAssemblyFullName, expectedTypeFullName));
            
            var binder = new MicrodotTypePolicySerializationBinder(constraints);
            
            var result = binder.BindToType(assemblyFullName, typeFullName);
            
            Assert.AreEqual(expectedType, result);
        }
        
        [Test]
        public void ShouldThrowExcluded()
        {
            var type = typeof(string);
            var assemblyFullName = type.Assembly.FullName;
            var typeFullName = type.FullName;
            var expectedExceptionMessage = "My Thrown Exception";
            
            var constraints = Substitute.For<IMicrodotSerializationConstraints>();
            
            constraints.When(x=>x.ThrowIfExcluded(typeFullName)).Do(call =>
            {
                throw new MyException(expectedExceptionMessage);
            });

            var binder = new MicrodotTypePolicySerializationBinder(constraints);
            
            Action action = ()=> binder.BindToType(assemblyFullName, typeFullName);

            action.ShouldThrow<MyException>().Message.ShouldBe(expectedExceptionMessage);
        }
    }
}