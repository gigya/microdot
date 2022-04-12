using Gigya.Microdot.SharedLogic.Configurations.Serialization;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

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
                        DeserializationForbiddenTypes = new[] {"foo"}.ToList(),
                        ShouldHandleEmptyPartition = true
                    });
                
            
            Assert.DoesNotThrow(()=>serializationConstraints.ThrowIfExcluded("bar"));
        }
        
        [Test]
        public void ShouldThrowIfTypeIsExcluded()
        {
            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () => new MicrodotSerializationSecurityConfig
                    {
                        DeserializationForbiddenTypes = new[] {"foo"}.ToList(),
                        ShouldHandleEmptyPartition = true
                    }
                );

            Assert.Throws<UnauthorizedAccessException>(()=>serializationConstraints.ThrowIfExcluded("foo"));
            Assert.Throws<UnauthorizedAccessException>(()=>serializationConstraints.ThrowIfExcluded("barfoobuzz"));
            Assert.Throws<UnauthorizedAccessException>(()=>serializationConstraints.ThrowIfExcluded("barfOobuzz"));
        }

        [Test]
        public void ShouldClearAssemblyCacheOnConfigChange()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements = 
                    new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new[]
                    {
                        new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "buz")
                    }
            ),
                ShouldHandleEmptyPartition = true
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
            
            microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig{
                AssemblyNamesRegexReplacements = 
                new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new []
                {
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "ding")
                }),
                ShouldHandleEmptyPartition = true
            };
            
            result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("foobar", "bar");

            Assert.AreEqual("ding", result.AssemblyName);
            Assert.AreEqual("bar", result.TypeName);
        }
        
        [Test]
        public void ShouldClearTypeNameCacheOnConfigChange()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig{
                AssemblyNamesRegexReplacements = 
                new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new []
                {
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "buz")
                }),
                ShouldHandleEmptyPartition = true
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

            microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig{
                AssemblyNamesRegexReplacements = 
                new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new []
                {
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "ding")
                }),
                ShouldHandleEmptyPartition = true
            };
            
            result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("bar", "foobar");

            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual("ding", result.TypeName);
        }

        [Test]
        public void ShouldClearTypeCacheOnConfigChange()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig{
                AssemblyNamesRegexReplacements = 
                new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new []
                {
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "buz")
                }),
                ShouldHandleEmptyPartition = true
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

            microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig{
                AssemblyNamesRegexReplacements = 
                new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new []
                {
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "ding")
                }),
                ShouldHandleEmptyPartition = true
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
                    AssemblyNamesRegexReplacements =
                        new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new[]
                        {
                            new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "buz")
                        }),
                ShouldHandleEmptyPartition = true
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
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig{
                AssemblyNamesRegexReplacements = 
                new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new []
                {
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("moobar", "buz")
                }),
                ShouldHandleEmptyPartition = true
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
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig{
                AssemblyNamesRegexReplacements = 
                new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new []
                {
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "buz"),
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "bull")
                }), ShouldHandleEmptyPartition = true};
            
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
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig{
                AssemblyNamesRegexReplacements = 
                new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new []
                {
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "buz")
                }),
                ShouldHandleEmptyPartition = true
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
        [TestCase(true, "foobar[], bar")]
        [TestCase(false, "System.Linq.EmptyPartition`1[[foobar,bar]]")]
        public void TryGetTypeNameReplacementWhenEmptyPartitionExist(bool shouldHandleEmptyPartition, string newTypeName)
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {                
                ShouldHandleEmptyPartition = shouldHandleEmptyPartition
            };

            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () =>
                    {
                        return microdotSerializationSecurityConfig;
                    });

            var result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("bar", "System.Linq.EmptyPartition`1[[foobar,bar]]");

            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual(newTypeName, result.TypeName);

            result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string), "bar", "System.Linq.EmptyPartition`1[[foobar,bar]]");

            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual(newTypeName, result.TypeName);
        }

        [Test]
        [TestCase(true, "foo[], rar")]
        [TestCase(false, "System.Linq.EmptyPartition`1[[foo,rar]]")]
        public void TryGetTypeNameReplacementWhenReplacementAndEmptyPartitionExist(bool shouldHandleEmptyPartition, string newTypeName)
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements =
                new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new[]
                {
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("bar", "rar")
                }),
                ShouldHandleEmptyPartition = shouldHandleEmptyPartition
            };

            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () =>
                    {
                        return microdotSerializationSecurityConfig;
                    });

            var result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("bar", "System.Linq.EmptyPartition`1[[foo,bar]]");

            Assert.AreEqual("rar", result.AssemblyName);
            Assert.AreEqual(newTypeName, result.TypeName);

            result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string), "bar", "System.Linq.EmptyPartition`1[[foo,bar]]");

            Assert.AreEqual("rar", result.AssemblyName);
            Assert.AreEqual(newTypeName, result.TypeName);
        }

        [Test]
        [TestCase(true, "far[[foo,bar]]")]
        [TestCase(false, "far[[foo,bar]]")]
        public void TryGetTypeNameReplacementWhenReplacementAndEmptyPartitionExist2(bool shouldHandleEmptyPartition, string newTypeName)
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig
            {
                AssemblyNamesRegexReplacements =
                new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new[]
                {
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("System.Linq.EmptyPartition`1", "far")
                }),
                ShouldHandleEmptyPartition = shouldHandleEmptyPartition
            };

            var serializationConstraints =
                new MicrodotSerializationConstraints(
                    () =>
                    {
                        return microdotSerializationSecurityConfig;
                    });

            var result = serializationConstraints.TryGetAssemblyNameAndTypeReplacement("bar", "System.Linq.EmptyPartition`1[[foo,bar]]");

            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual(newTypeName, result.TypeName);

            result = serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                typeof(string), "bar", "System.Linq.EmptyPartition`1[[foo,bar]]");

            Assert.AreEqual("bar", result.AssemblyName);
            Assert.AreEqual(newTypeName, result.TypeName);
        }

        [Test]
        public void TryGetTypeNameReplacementWhenReplacementDoesNotExists()
        {
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig{
            AssemblyNamesRegexReplacements = 
                new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new []
                {
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("mobar", "buz")
                }),
                ShouldHandleEmptyPartition = true
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
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig{
                 AssemblyNamesRegexReplacements = 
                new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new []
                {
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "buz"),
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "bull")
                }),
                ShouldHandleEmptyPartition = true
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
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig{
                AssemblyNamesRegexReplacements = 
                new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new []
                {
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "buz"),
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("carmel", "gilboa")
                }),
                ShouldHandleEmptyPartition = true
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
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig{
                AssemblyNamesRegexReplacements = 
                new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new []
                {
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("buz", "foobar"),
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("gilboa", "carmel")
                }),
                ShouldHandleEmptyPartition = true
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
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig{
                AssemblyNamesRegexReplacements = 
                new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new []
                {
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "buz"),
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "bull"),
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("carmel", "gilboa"),
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("carmel", "megido"),
                }),
                ShouldHandleEmptyPartition = true
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
            var microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig{
                AssemblyNamesRegexReplacements = 
                new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new []
                {
                    new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "buz")
                }),
                ShouldHandleEmptyPartition = true
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

            microdotSerializationSecurityConfig = new MicrodotSerializationSecurityConfig{
                 AssemblyNamesRegexReplacements = 
                    new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>(new []
                    {
                        new MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement("foobar", "ding")
                    }),
                ShouldHandleEmptyPartition = true
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