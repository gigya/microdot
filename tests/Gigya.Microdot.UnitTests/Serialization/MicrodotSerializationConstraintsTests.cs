using System;
using System.Collections.Generic;
using System.Linq;
using Gigya.Microdot.SharedLogic.Configurations.Serialization;
using NUnit.Framework;

namespace Gigya.Microdot.UnitTests.Serialization
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class MicrodotSerializationConstraintsTests
    {
        [Test]
        public void ShouldNotThrowIfTypeIsNotExcluded()
        {
            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () => new MicrodotSerializationSecurityConfig
                    {
                        DeserializationForbiddenTypes = new Dictionary<string, bool>(){{"foo",  true}}
                    });
            
            Assert.DoesNotThrow(()=>serializationConstraints.ThrowIfExcluded("bar"));
        }
        
        [Test]
        public void ShouldThrowIfTypeIsExcluded()
        {
            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () => new MicrodotSerializationSecurityConfig()
                    {
                        DeserializationForbiddenTypes = new Dictionary<string, bool>
                        {
                            {"foo", true}
                        }
                    });
                        

            Assert.Throws<UnauthorizedAccessException>(()=>serializationConstraints.ThrowIfExcluded("foo"));
            Assert.Throws<UnauthorizedAccessException>(()=>serializationConstraints.ThrowIfExcluded("barfoobuzz"));
            Assert.Throws<UnauthorizedAccessException>(()=>serializationConstraints.ThrowIfExcluded("barfOobuzz"));
        }
    
        [Test]
        public void ShouldClearAssemblyCacheOnConfigChange()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"foobar", "buz"}
                }
            };
            
            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () =>
                    {
                        
                        return microdotSerializationSecurityConfig;
                    });

            var result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("foobar", "bar");

            Assert.AreEqual("buz", result.AssemblyName);
            Assert.AreEqual("bar", result.TypeName);
            
            microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"foobar", "ding"}
                }
            };
            
            result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("foobar", "bar");

            Assert.AreEqual("ding", result.AssemblyName);
            Assert.AreEqual("bar", result.TypeName);
        }
        
        [Test]
        public void ShouldClearTypeNameCacheOnConfigChange()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"foobar", "buz"}
                }
            };
           
            
            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () =>
                    {
                        
                        return microdotSerializationSecurityConfig;
                    });

            var result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("bar", "foobar");

            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual("buz", result.TypeName);

            microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"foobar", "ding"}
                }
            };
            
            result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("bar", "foobar");

            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual("ding", result.TypeName);
        }

        [Test]
        public void ShouldClearTypeCacheOnConfigChange()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"foobar", "buz"}
                }
            };
            
            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () =>
                    {
                        
                        return microdotSerializationSecurityConfig;
                    });

            var result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string),
                "bar", 
                "foobar");

            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual("buz", result.TypeName);

            microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"foobar", "ding"}
                }
            };

            result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string),
                "bar", 
                "foobar");

            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual("ding", result.TypeName);
        }


        [Test]
        public void TryGetAssemblyNameReplacementWhenReplacementExists()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"foobar", "buz"}
                }
            };
            
            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () =>
                    {
                        
                        return microdotSerializationSecurityConfig;
                    });

            var result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("foobar", "bar");

            Assert.AreEqual("buz", result.AssemblyName);
            Assert.AreEqual("bar", result.TypeName);
            
            result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string),
                "foobar", 
                "bar");
            
            Assert.AreEqual("buz", result.AssemblyName);
            Assert.AreEqual("bar", result.TypeName);
        }
        
        [Test]
        public void TryGetAssemblyNameReplacementWhenReplacementDoesNotExists()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"moobar", "buz"}
                }
            };
            
            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () =>
                    {
                        
                        return microdotSerializationSecurityConfig;
                    });

            var result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("foobar", "bar");

            Assert.AreEqual("foobar", result.AssemblyName);
            Assert.AreEqual("bar", result.TypeName);
            
            result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string), "foobar", "bar");
            
            Assert.AreEqual("foobar", result.AssemblyName);
            Assert.AreEqual("bar", result.TypeName);
        }
        
        [Test]
        public void TryGetAssemblyNameReplacementWhenReplacementExistsForFirstResult()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"foobar", "buz"}
                }
            };
            
            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () =>
                    {
                        
                        return microdotSerializationSecurityConfig;
                    });

            var result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("foobar", "bar");

            Assert.AreEqual("buz", result.AssemblyName);
            Assert.AreEqual("bar", result.TypeName);
            
            result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string), "foobar", "bar");
            
            Assert.AreEqual("buz", result.AssemblyName);
            Assert.AreEqual("bar", result.TypeName);
        }
        
        [Test]
        public void TryGetTypeNameReplacementWhenReplacementExists()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"foobar", "buz"}
                }
            };
            
            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () =>
                    {
                        
                        return microdotSerializationSecurityConfig;
                    });

            var result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("bar", "foobar");

            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual("buz", result.TypeName);
            
            result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string), "bar", "foobar");
            
            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual("buz", result.TypeName);
        }
        
        [Test]
        public void TryGetTypeNameReplacementWhenReplacementDoesNotExists()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"mobar", "buz"}
                }
            };
            
            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () =>
                    {
                        
                        return microdotSerializationSecurityConfig;
                    });

            var result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("bar", "foobar");

            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual("foobar", result.TypeName);
            
            result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string), "bar", "foobar");
            
            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual("foobar", result.TypeName);
        }
        
        [Test]
        public void TryGetTypeNameReplacementWhenReplacementExistsForFirstResult()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"foobar", "buz"}
                }
            };
            
            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () =>
                    {
                        
                        return microdotSerializationSecurityConfig;
                    });

            var result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("bar", "foobar");

            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual("buz", result.TypeName);
            
            result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string), "bar", "foobar");
            
            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual("buz", result.TypeName);
        }
        
        [Test]
        public void TryGetAssemblyNameAndTypeNameReplacementWhenReplacementExists()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"foobar", "buz"},
                    {"carmel", "gilboa"}
                }
            };
            
            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () =>
                    {
                        
                        return microdotSerializationSecurityConfig;
                    });

            var result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("carmel", "foobar");

            Assert.AreEqual("gilboa", result.AssemblyName);
            Assert.AreEqual("buz", result.TypeName);
            
            result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string), "carmel", "foobar");
            
            Assert.AreEqual("gilboa", result.AssemblyName);
            Assert.AreEqual("buz", result.TypeName);
        }
        
        [Test]
        public void TryGetAssemblyNameAndTypeNameReplacementWhenReplacementDoesNotExists()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"buz", "foobar"},
                    {"gilboa", "carmel"}
                }
            };
            
            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () =>
                    {
                        
                        return microdotSerializationSecurityConfig;
                    });

            var result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("carmel", "foobar");

            Assert.AreEqual("carmel", result.AssemblyName);
            Assert.AreEqual("foobar", result.TypeName);
            
            result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string), "carmel", "foobar");
            
            Assert.AreEqual("carmel", result.AssemblyName);
            Assert.AreEqual("foobar", result.TypeName);
        }
        
        [Test]
        public void TryGetAssemblyNameAndTypeNameReplacementWhenReplacementExistsForFirstResult()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"foobar", "buz"},
                    {"carmel", "gilboa"},
                }
            };
            
            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () =>
                    {
                        
                        return microdotSerializationSecurityConfig;
                    });

            var result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("carmel", "foobar");

            Assert.AreEqual("gilboa", result.AssemblyName);
            Assert.AreEqual("buz", result.TypeName);
            
            result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string), "carmel", "foobar");
            
            Assert.AreEqual("gilboa", result.AssemblyName);
            Assert.AreEqual("buz", result.TypeName);
        }

        [Test]
        public void TryGetAssemblyAndTypeNameReplacementFromType()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"foobar", "buz"}
                }
            };
            
            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () =>
                    {
                        
                        return microdotSerializationSecurityConfig;
                    });

            var result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string),
                "bar", 
                "foobar");

            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual("buz", result.TypeName);

            microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = new Dictionary<string, string>
                {
                    {"foobar", "ding"}
                }
            };
            
            result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string),
                "bar", 
                "foobar");

            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual("ding", result.TypeName);
            
            result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string), "foobar", "ding");
            
            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual("ding", result.TypeName);
        }

    }
}