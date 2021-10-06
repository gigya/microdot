using Gigya.Microdot.Interfaces;
using Gigya.Microdot.SharedLogic.Exceptions;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Gigya.Microdot.UnitTests.Configuration
{
    [TestFixture, Parallelizable(ParallelScope.Fixtures)]
    public class ConfigListProperties: ConfigTestBase
    {
        public TConfig GetConfig<TConfig>(string configFile1,string configFile1Name, string configFile2, string configFile2Name)
        {
            var (k, providerMock, fileSystemMock) = Setup();
            providerMock.GetAllTypes().Returns(info => new[]
            {
                typeof(TConfig),
            });
            fileSystemMock.GetFilesInFolder(Arg.Any<string>(), Arg.Any<string>() ).Returns(info =>
            {
                if(info.ArgAt<string>(1) == $"{configFile1Name}.config")
                    return new[] { $@"{configFile1Name}.config" };
                if (info.ArgAt<string>(1) == $"{configFile2Name}.config")
                    return new[] { $@"{configFile2Name}.config" };
                throw new ArgumentException("Invalid pattern ...");
            });

            fileSystemMock.ReadAllTextFromFileAsync(Arg.Any<string>()).Returns(callinfo =>
            {
                string content;
                if (callinfo.ArgAt<string>(0) == $@"{configFile1Name}.config")
                {
                    content = configFile1;
                }
                else if (callinfo.ArgAt<string>(0) == $@"{configFile2Name}.config")
                {
                    content = configFile2;
                }
                else 
                    throw new ArgumentException("Invalid config file name...");

                return Task.FromResult(content);
            });

            var creator = k.Get<Func<Type, IConfigObjectCreator>>()(typeof(TConfig));
            return ((TConfig)creator.GetLatest());
        }
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
        public void CanUseArrayPropertyInConfigObject()
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
        [Description("Checks that we can read an array property as child element")]
        public void CanUseArrayPropertyInConfigObjectAsChildElement()
        {
            var config = @"<configuration>
                            <IntArrayConfig>
                                <IntArray-list>
                                    1,2,3,4
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
        [Description("Checks that we can parse array of strings containing whitespaces")]
        public void CanProperlyParseStringArrayInConfigObject()
        {
            var config = @"<configuration>
                            <StringArrayConfig StringArray-list=""a b , c d""/>
                          </configuration>";
            var configObject = GetConfig<StringArrayConfig>(config);
            configObject.StringArray.GetType().ShouldBe(typeof(string[]));
            configObject.StringArray.Length.ShouldBe(2);
            configObject.StringArray[0].ShouldBe("a b");
            configObject.StringArray[1].ShouldBe("c d");
        }

        [Test]
        [Description("Checks that we can deserialise to an IEnumerable")]
        public void CanUseIEnumerablePropertyInConfigObject()
        {
            var config = @"<configuration>
                            <IEnumerableConfig IntEnumerable-list=""1,2,3,4""/>
                          </configuration>";
            var configObject = GetConfig<IEnumerableConfig>(config);
            var value = 1;
            foreach (var i in configObject.IntEnumerable)
            {
                i.ShouldBe(value++);   
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
			                        <Item2><Person Name='Mira' Age='27'/></Item2>
		                        </PersonArray-list>
	                        </PersonArrayConfig>
                        </configuration>";
            Should.Throw<ConfigurationException>(() => GetConfig<PersonArrayConfig>(config));
        }

        [Test]
        [Description("Checks that we can parse an empty array")]
        public void HavingNoItemElementOnAListShouldThrow()
        {
            var config = @"<configuration>
	                        <PersonArrayConfig>
		                        <PersonArray-list>
		                        </PersonArray-list>
	                        </PersonArrayConfig>
                        </configuration>";
            var configObject = GetConfig<PersonArrayConfig>(config);
            configObject.PersonArray.GetType().ShouldBe(typeof(Person[]));
            configObject.PersonArray.Length.ShouldBe(0);
        }

        [Test]
        [Description("checks that we construct a default object for an element without attributes nor children")]
        public void ListItemsMustContainEitherAttributesOrContent()
        {
            var config = @"<configuration>
	                        <PersonArrayConfig>
		                        <PersonArray-list>
			                        <Item><Person/></Item>
		                        </PersonArray-list>
	                        </PersonArrayConfig>
                        </configuration>";
            var configObject = GetConfig<PersonArrayConfig>(config);
            configObject.PersonArray.GetType().ShouldBe(typeof(Person[]));
            configObject.PersonArray.Length.ShouldBe(1);
            var person = configObject.PersonArray[0];
            person.Age.ShouldBe(default);
            person.FavoriteLotteryNumbers.ShouldBe(default);
            person.Name.ShouldBe(default);
            person.Pets.ShouldBe(default);
        }

        [Test]
        [Description("Checks that we throw if one of the elements has no content and no attributes")]
        public void ListItemsMustContainASingleChildElement()
        {
            var config = @"<configuration>
	                        <PersonArrayConfig>
		                        <PersonArray-list>
			                        <Item>
                                        <Person Name='Bob' Age='35'/>
                                        <Person Name='Sarah' Age='45'/>
                                    </Item>
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
            LoadPaths =
                @"[{ ""Pattern"": "".\\Config1.config"", ""Priority"": 1 },{ ""Pattern"": "".\\Config2.config"", ""Priority"": 2 }]";
            var config1 = @"<configuration>
	                        <PersonArrayConfig>
		                        <PersonArray-list>
			                        <Item><Person Name='Bob' Age='35'/></Item>
			                        <Item><Person Name='John' Age='21'/></Item>
		                        </PersonArray-list>
	                        </PersonArrayConfig>
                        </configuration>";

            var config2 = @"<configuration>
	                        <PersonArrayConfig>
		                        <PersonArray-list>
			                        <Item><Person Name='Sarah' Age='45'/></Item>  
			                        <Item><Person Name='Mira' Age='27'/></Item>
		                        </PersonArray-list>
	                        </PersonArrayConfig>
                        </configuration>";

            var configObject = GetConfig<PersonArrayConfig>(config1, "Config1", config2, "Config2");
            configObject.PersonArray.Length.ShouldBe(2);
            configObject.PersonArray[0].Name.ShouldBe("Sarah");
            configObject.PersonArray[1].Name.ShouldBe("Mira");
        }
    }
}
