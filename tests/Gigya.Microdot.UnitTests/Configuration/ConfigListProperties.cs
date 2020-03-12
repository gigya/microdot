using System;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.SharedLogic.Exceptions;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Configuration
{
    [TestFixture, Parallelizable(ParallelScope.Fixtures)]
    public class ConfigListProperties: ConfigTestBase
    {
        public TConfig GetConfig<TConfig>(string configFile)
        {
            var nameOfConfigFile = nameof(TConfig) + ".config";
            var (k, providerMock, fileSystemMock) = Setup();

            providerMock.GetAllTypes().Returns(info => new[]
            {
                typeof(TConfig),
            });

            fileSystemMock.GetFilesInFolder(Arg.Any<string>(), Arg.Any<string>()).Returns(info => new[]
            {
                nameOfConfigFile,
            });

            fileSystemMock.ReadAllTextFromFileAsync(Arg.Any<string>()).Returns(callinfo =>
            {
                string content;
                if (callinfo.ArgAt<string>(0) == nameOfConfigFile)
                {
                    content = configFile;
                }
                else
                    throw new ArgumentException("Invalid config file name...");

                return Task.FromResult(content);
            });

            var creator = k.Get<Func<Type, IConfigObjectCreator>>()(typeof(IntArrayConfig));
            return ((TConfig) creator.GetLatest());

        }

        [Test]
        [Description("Checks that we can read an array property")]
        public void CanUseArrayProperyInConfigObject()
        {
            var config = "<configuration>\r\n\t<IntArrayConfig IntArray=\"1,2,3,4\"/>\r\n</configuration>";
            var configObject = GetConfig<IntArrayConfig>(config);
            configObject.IntArray.GetType().ShouldBe(typeof(int[]));
            foreach (var i in Enumerable.Range(0,4))
            {
                configObject.IntArray[i].ShouldBe(i+1); 
            }
        }

        [Test]
        [Description("Checks that we can read an array property in node format")]
        public void CanUseArrayProperyInConfigObjectInNodeStructure()
        {
            var config = @"<configuration>
	                        <IntArrayConfig>
		                        <IntArray_list_>
			                        <Item>1</Item>
			                        <Item>2</Item>
			                        <Item>3</Item>  
			                        <Item>4</Item>
		                        </IntArray_list_>
	                        </IntArrayConfig>
                        </configuration>";
            var configObject = GetConfig<IntArrayConfig>(config);
            configObject.IntArray.GetType().ShouldBe(typeof(int[]));
            foreach (var i in Enumerable.Range(0, 4))
            {
                configObject.IntArray[i].ShouldBe(i + 1);
            }
        }

        [Test]
        [Description("Checks that we can read an array property of complex object")]
        public void CanUseArrayPropertyOfComplexTypeInConfigObjectInNodeStructure()
        {
            var config = @"<configuration>
	                        <PersonArrayConfig>
		                        <PersonArray_list_>
			                        <Item><Person Name='Bob' Age='35'/></Item>
			                        <Item><Person Name='John' Age='21'/></Item>
			                        <Item><Person Name='Sarah' Age='45'/></Item>  
			                        <Item><Person Name='Mira' Age='27'/></Item>
		                        </PersonArray_list_>
	                        </PersonArrayConfig>
                        </configuration>";
            var configObject = GetConfig<PersonArrayConfig>(config);
            configObject.PersonArray.GetType().ShouldBe(typeof(Person[]));
            configObject.PersonArray[0].Name.ShouldBe("Bob");
            configObject.PersonArray[1].Name.ShouldBe("John");
            configObject.PersonArray[2].Name.ShouldBe("Sarah");
            configObject.PersonArray[3].Name.ShouldBe("Mira");
        }

        [Test]
        [Description("Checks that we throw if one of the elements in the list is not of the same type")]
        public void HavingAnElementWithDifferentXMLTypeInTheListShouldThrow()
        {
            var config = @"<configuration>
	                        <PersonArrayConfig>
		                        <PersonArray_list_>
			                        <Item><Person Name='Bob' Age='35'/></Item>
			                        <Item><Person Name='John' Age='21'/></Item>
			                        <Item><Person Name='Sarah' Age='45'/></Item>  
			                        <Item><Person2 Name='Mira' Age='27'/></Item>
		                        </PersonArray_list_>
	                        </PersonArrayConfig>
                        </configuration>";
            Should.Throw<ConfigurationException>(() => GetConfig<PersonArrayConfig>(config));
        }

        [Test]
        [Description("Checks that we throw if one of the elements in the list is not an item")]
        public void ListMustContainOnlyItemElements()
        {
            var config = @"<configuration>
	                        <PersonArrayConfig>
		                        <PersonArray_list_>
			                        <Item><Person Name='Bob' Age='35'/></Item>
			                        <Item><Person Name='John' Age='21'/></Item>
			                        <Item><Person Name='Sarah' Age='45'/></Item>  
			                        <Item2><Person Name='Mira' Age='27'/></Item>
		                        </PersonArray_list_>
	                        </PersonArrayConfig>
                        </configuration>";
            Should.Throw<ConfigurationException>(() => GetConfig<PersonArrayConfig>(config));
        }
    }

    [ConfigurationRoot("IntArrayConfig", RootStrategy.ReplaceClassNameWithPath)]
    internal class IntArrayConfig : IConfigObject
    {
        public int[] IntArray { get; set; }
    }

    internal class PersonArrayConfig : IConfigObject
    {
        public Person[] PersonArray { get; set; }
    }

    internal class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}
