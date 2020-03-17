using System;
using System.Collections.Generic;
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
            var nameOfConfigFile = typeof(TConfig).Name + ".config";
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

            var creator = k.Get<Func<Type, IConfigObjectCreator>>()(typeof(TConfig));
            return ((TConfig) creator.GetLatest());

        }

        [Test]
        [Description("Checks that we can read an array property")]
        public void CanUseArrayProperyInConfigObject()
        {
            var config = @"<configuration>
                            <IntArrayConfig IntArray-list=""1,2,3,4""/>
                          </configuration>";
            var configObject = GetConfig<IntArrayConfig>(config);
            configObject.IntArray.GetType().ShouldBe(typeof(int[]));
            foreach (var i in Enumerable.Range(0,4))
            {
                configObject.IntArray[i].ShouldBe(i+1); 
            }
        }

        [Test]
        [Description("Checks that we can read an array property in node format")]
        public void CanUseArrayPropertyInConfigObjectInNodeStructure()
        {
            var config = @"<configuration>
	                        <IntArrayConfig>
		                        <IntArray-list>
			                        <Item>1</Item>
			                        <Item>2</Item>
			                        <Item>3</Item>  
			                        <Item>4</Item>
		                        </IntArray-list>
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
		                        <PersonArray-list>
			                        <Item><Person Name='Bob' Age='35'/></Item>
			                        <Item><Person Name='John' Age='21'/></Item>
			                        <Item><Person Name='Sarah' Age='45'/></Item>  
			                        <Item><Person Name='Mira' Age='27'/></Item>
		                        </PersonArray-list>
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
        [Description("Checks that we can read nested complex object")]
        public void CanUseListOfComplexObjectsInsideListOfComplexObjects()
        {
            var config = @"<configuration>
	                        <PersonArrayConfig>
		                        <PersonArray-list>
			                        <Item>
                                        <Person Name='Bob' Age='35'>
                                            <Pets-list>
	                                            <Item>
                                                    <Pet Name='Smelly' Type='Cat' />                                            
                                                </Item>
                                                <Item>
                                                    <Pet Name='Hairy' Type='Dog' />                                            
                                                </Item>
                                            </Pets-list>
                                        </Person>
                                    </Item>
		                        </PersonArray-list>
	                        </PersonArrayConfig>
                        </configuration>";
            var configObject = GetConfig<PersonArrayConfig>(config);
            configObject.PersonArray.GetType().ShouldBe(typeof(Person[]));
            var person = configObject.PersonArray.Single();
            person.Name.ShouldBe("Bob");
            person.Pets.GetType().ShouldBe(typeof(Pet[]));
            person.Pets.Length.ShouldBe(2);
            person.Pets[0].Name.ShouldBe("Smelly");
            person.Pets[1].Name.ShouldBe("Hairy");
        }

        [Test]
        [Description("Checks that we can read nested simple object")]
        public void CanUseListOfSimpleValuesInsideListOfComplexObjects()
        {
            var config = @"<configuration>
	                    <PersonArrayConfig>
		                    <PersonArray-list>
			                    <Item><Person Name='Bob' Age='35' FavoriteLotteryNumbers-list='3,7,13,28,31,42'/></Item>
		                    </PersonArray-list>
	                    </PersonArrayConfig>
                    </configuration>";
            var configObject = GetConfig<PersonArrayConfig>(config);
            configObject.PersonArray.GetType().ShouldBe(typeof(Person[]));
            var person = configObject.PersonArray[0];
            person.Name.ShouldBe("Bob");
            person.FavoriteLotteryNumbers.GetType().ShouldBe(typeof(int[]));
            person.FavoriteLotteryNumbers.Length.ShouldBe(6);
            person.FavoriteLotteryNumbers[0].ShouldBe(3);
            person.FavoriteLotteryNumbers[1].ShouldBe(7);
            person.FavoriteLotteryNumbers[2].ShouldBe(13);
            person.FavoriteLotteryNumbers[3].ShouldBe(28);
            person.FavoriteLotteryNumbers[4].ShouldBe(31);
            person.FavoriteLotteryNumbers[5].ShouldBe(42);
        }

        [Test]
        [Description("Checks that we throw if one of the elements in the list is not of the same type")]
        public void HavingAnElementWithDifferentXMLTypeInTheListShouldThrow()
        {
            var config = @"<configuration>
	                        <PersonArrayConfig>
		                        <PersonArray-list>
			                        <Item><Person Name='Bob' Age='35'/></Item>
			                        <Item><Person Name='John' Age='21'/></Item>
			                        <Item><Person Name='Sarah' Age='45'/></Item>  
			                        <Item><Person2 Name='Mira' Age='27'/></Item>
		                        </PersonArray-list>
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
		                        <PersonArray-list>
			                        <Item><Person Name='Bob' Age='35'/></Item>
			                        <Item><Person Name='John' Age='21'/></Item>
			                        <Item><Person Name='Sarah' Age='45'/></Item>  
			                        <Item2><Person Name='Mira' Age='27'/></Item>
		                        </PersonArray-list>
	                        </PersonArrayConfig>
                        </configuration>";
            Should.Throw<ConfigurationException>(() => GetConfig<PersonArrayConfig>(config));
        }

        [Test]
        [Description("Checks that we throw if a list is empty")]
        public void HavingNoItemElementOnAListShouldThrow()
        {
            var config = @"<configuration>
	                        <PersonArrayConfig>
		                        <PersonArray-list>
		                        </PersonArray-list>
	                        </PersonArrayConfig>
                        </configuration>";
            Should.Throw<ConfigurationException>(() => GetConfig<PersonArrayConfig>(config));
        }

        [Test]
        [Description("Checks that we throw if one of the elements has no content and no attributes")]
        public void ListItemsMustContainEitherAttributesOrContent()
        {
            var config = @"<configuration>
	                        <PersonArrayConfig>
		                        <PersonArray-list>
			                        <Item><Person Name='Bob' Age='35'/></Item>
			                        <Item><Person/></Item>
			                        <Item><Person Name='Sarah' Age='45'/></Item>  
			                        <Item2><Person Name='Mira' Age='27'/></Item>
		                        </PersonArray-list>
	                        </PersonArrayConfig>
                        </configuration>";
            Should.Throw<ConfigurationException>(() => GetConfig<PersonArrayConfig>(config));
        }

        [Test]
        [Description("Checks that we support shortcuts inside lists")]
        public void ShortcutPathsInsideListsShouldWork()
        {
            var expectedValue = "expectedValue";
            var config = $@"<configuration>
	                        <NestedConfig>
		                        <Internals-list>
			                        <Item><InternalConfig.Value>{expectedValue}</InternalConfig.Value></Item>
		                        </Internals-list>
	                        </NestedConfig>
                        </configuration>";
            var nestedConfig = GetConfig<NestedConfig>(config);
            nestedConfig.Internals.Single().Value.ShouldBe(expectedValue);
        }

        [Test]
        [Description("Checks that we override lists atomically")]
        public void OverridingListsShouldBeAtomic()
        {
            throw new NotImplementedException("TODO:Write this test");
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
        public Pet[] Pets {get; set;}
        public int[] FavoriteLotteryNumbers { get; set; }
    }

    internal class Pet
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }

    internal class NestedConfig
    {
        public InternalConfig[] Internals { get; set; }
    }

    internal class InternalConfig
    {
        public string Value { get; set; }
    }
}
