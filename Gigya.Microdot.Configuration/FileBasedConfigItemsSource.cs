#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic.Exceptions;
using Newtonsoft.Json.Linq;

namespace Gigya.Microdot.Configuration
{
    ///<inheritdoc cref="IConfigItemsSource"/>
    public class FileBasedConfigItemsSource : IConfigItemsSource
    {
        private readonly IConfigurationLocationsParser _configurationLocations;
        private readonly IFileSystem _fileSystem;
        private readonly IEnvironment _environment;
        private readonly ConfigDecryptor _configDecryptor;

        private readonly Regex paramMatcher = new Regex(@"([^\\]|^)%(?<envName>[^%]+)%", RegexOptions.Compiled | RegexOptions.ExplicitCapture);


        /// <summary>
        /// Constructor for the 'FileBasedConfigItemsSource' class
        /// </summary>
        /// <param name="configurationLocations">Encapsulates the retrieval of all configuration location behavior</param>
        /// <param name="environmentVariableProvider">Encapsulates the retrieval of all environment variables</param>
        /// <param name="fileSystem">Encapsulates the file system behavior</param>
        /// <param name="configDecryptor">Encapsulates the decryption behavior for a config item</param>
        public FileBasedConfigItemsSource(
            IConfigurationLocationsParser configurationLocations,
            IFileSystem fileSystem,
                IEnvironment environment,
                ConfigDecryptor configDecryptor)
        {
            _configurationLocations = configurationLocations;
            _fileSystem = fileSystem;
            _environment = environment;
            _configDecryptor = configDecryptor;
        }

        public async Task<(ConfigItemsCollection Configs, DateTime? LastModified)> GetConfiguration()
        {
            var conf = new Dictionary<string, ConfigItem>(StringComparer.OrdinalIgnoreCase);
            DateTime? latestConfigFileModifyTime = null;

            foreach (var configFile in _configurationLocations.ConfigFileDeclarations.SelectMany(FindConfigFiles))
            {
                var modifyTime = await ReadConfiguration(configFile, conf).ConfigureAwait(false);
                if (latestConfigFileModifyTime == null || modifyTime > latestConfigFileModifyTime)
                    latestConfigFileModifyTime = modifyTime;
            }

            var collection = new ConfigItemsCollection(conf.Values);

            var notFoundEnvVariables = new List<string>();

            foreach (var configItem in collection.Items)
            {
                var list = paramMatcher.Matches(configItem.RawValue)
                                       .Cast<Match>()
                                       .Select(match => new
                                       {
                                           Placehodler = "%" + match.Groups[1].Value + "%",
                                           Value = _environment[match.Groups[1].Value.ToUpperInvariant()]
                                       }).ToList();

                if (list.Any())
                {
                    var notFound = list.Where(a => string.IsNullOrEmpty(a.Value)).Select(a => a.Placehodler.Trim('%')).ToArray();

                    if (!notFound.Any())
                    {
                        foreach (var valToReplace in list)
                        {
                            configItem.Value = configItem.Value.Replace(valToReplace.Placehodler, valToReplace.Value);
                        }
                    }
                    else
                    {
                        notFoundEnvVariables.AddRange(notFound);
                    }
                }
            }

            if (notFoundEnvVariables.Any())
            {
                throw new EnvironmentException("Configuration is dependent on following enviroment variables:" + string.Join("\n", notFoundEnvVariables) + "\n but they are not set.");
            }

            // return merged configuration
            return (collection, latestConfigFileModifyTime);
        }

        // Note: files and folders my change concurrently; handle exceptions and try to return the maximum amount of information.
        private IEnumerable<ConfigFile> FindConfigFiles(ConfigFileDeclaration declaration)
        {
            try
            {
                if (_fileSystem.Exists(Path.GetDirectoryName(declaration.Pattern)))
                {
                    return _fileSystem.GetFilesInFolder(Path.GetDirectoryName(declaration.Pattern), Path.GetFileName(declaration.Pattern))
                                      .Select(filename =>
                                              {
                                                  try { return new ConfigFile(filename, declaration.Priority); }
                                                  catch (FileNotFoundException) { return null; }
                                              })
                                      .Where(_ => _ != null);
                }
                else
                {
                    return Enumerable.Empty<ConfigFile>();
                }
            }
            catch (DirectoryNotFoundException)
            {
                return Enumerable.Empty<ConfigFile>();
            }
        }

        /// <summary>
        /// Reads a single configuration file and populates the configuration items found in it. 
        /// </summary>
        /// <param name="configFile"> The configuration file</param>
        /// <param name="conf">The configuration items collection</param>
        private async Task<DateTime> ReadConfiguration(ConfigFile configFile, Dictionary<string, ConfigItem> conf)
        {
            try
            {
                var xmlDocument = new XmlDocument();
                var xmlString = await _fileSystem.ReadAllTextFromFileAsync(configFile.FullName).ConfigureAwait(false);
                xmlDocument.LoadXml(xmlString);
                ParseConfigXml(configFile.FullName, xmlDocument, configFile.Priority, conf);
                return await _fileSystem.GetFileLastModified(configFile.FullName);
            }
            catch (FileNotFoundException ex)
            {
                var errMsg = string.Format("Missing configuration file: " + configFile.FullName);
                throw new ConfigurationException(errMsg, ex);
            }
            catch (IOException ex)
            {
                // the file didn't finish being written yet
                var errMsg = string.Format("Error loading configuration file: " + configFile.FullName);
                throw new ConfigurationException(errMsg, ex);
            }
            catch (Exception ex)
            {
                var errMsg = string.Format("Missing or invalid configuration file: " + configFile.FullName);
                throw new ConfigurationException(errMsg, ex);
            }
        }

        /// <summary>
        /// Parses a single XML document and populate the configuration collection accordingly.
        /// </summary>
        /// <param name="fileName">The name of the original file that was parsed into an XML document</param>
        /// <param name="xml">The XML document been parsed</param>
        /// <param name="priority">The priority that each config item in this document will get</param>
        /// <param name="data">The configuration collection to be populated</param>
        private void ParseConfigXml(string fileName, XmlDocument xml, uint priority, Dictionary<string, ConfigItem> data)
        {
            if (xml.DocumentElement.Name == "Configuration")
            {
                // Check if this is a App.Config file that has the DynamicAppSettings.Config embeded directly inside it and not in an external file
                XmlNode appSettingsNode = xml.SelectSingleNode("//appSettings");
                if (appSettingsNode != null)
                    ParseConfiguration(appSettingsNode, null, data, priority, fileName); // warning: appSettings priority same as rest of config values in that file; unlike the appSettings section in web.config that receives priority 0
            }
            else
            {
                ParseConfiguration(xml.DocumentElement, null, data, priority, fileName);
            }
        }

        /// <summary>
        /// This method is been invoked recursively on each XML node and populate configuration items.
        /// </summary>
        /// <param name="element">The current XML node in the recursion</param>
        /// <param name="parentName">The XML path for this node's parent</param>
        /// <param name="conf">The configuration collection to be populated</param>
        /// <param name="priority">The priority that config items will get</param>
        /// <param name="fileName">The name of the file that this node reside in</param>
        private void ParseConfiguration(XmlNode element, string parentName, Dictionary<string, ConfigItem> conf, uint priority, string fileName)
        {
            try
            {
                if (element.Name == "add")
                {
                    if (element.Attributes["key"] != null && element.Attributes["value"] != null)
                        PutOrUpdateEntry(conf, element.Attributes["key"].Value, element.Attributes["value"].Value, priority, fileName);
                }
                else
                {
                    if (element.Attributes != null && element.Attributes.Count > 0)
                        foreach (XmlAttribute attr in element.Attributes)
                            PutOrUpdateEntry(conf, parentName + "." + attr.Name, attr.Value, priority, fileName, element);
                    foreach (XmlNode child in element.ChildNodes)
                    {
                        ParseConfigurationNode(parentName, conf, priority, fileName, child);
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("parentName=" + parentName, e);
            }
        }
        /// <summary>
        /// Parses a single child node and populate a config item if this node has any attributes.
        /// </summary>
        /// <param name="parentName">The full path of the parent node</param>
        /// <param name="conf">The configuration collection</param>
        /// <param name="priority">The priority for the config items</param>
        /// <param name="fileName">the name of the file been parsed</param>
        /// <param name="child">The child node been parsed</param>
        private void ParseConfigurationNode(string parentName, Dictionary<string, ConfigItem> conf, uint priority, string fileName, XmlNode child)
        {
            if (child.NodeType == XmlNodeType.Text || child.NodeType == XmlNodeType.CDATA)
                PutOrUpdateEntry(conf, parentName, child.Value, priority, fileName);
            else
            {
                string name = child.Name;
                if (name.EndsWith(ListMarker))
                {
                    try
                    {
                        ParseConfigurationListNode(parentName, conf, priority, fileName, child, name);
                    }
                    //We must not throw during parsing of config (as one bad config will affect all services) so we deffer the throwing to when the config object is actually been requested
                    catch (Exception e)
                    {
                        name = RemoveListMarker(name);
                        name = ToFullName(parentName, name);
                        PutOrUpdateEntry(conf, name, string.Empty, priority, fileName, exception:e);
                    }
                }
                else if (name.EndsWith(CollectionMarker))
                {
                    try
                    {
                        ParseConfigurationCollectionNode(parentName, conf, priority, fileName, child, name);
                    }
                    //We must not throw during parsing of config (as one bad config will affect all services) so we deffer the throwing to when the config object is actually been requested
                    catch (Exception e)
                    {
                        name = RemoveListMarker(name);
                        name = ToFullName(parentName, name);
                        PutOrUpdateEntry(conf, name, string.Empty, priority, fileName, exception: e);
                    }
                }
                else
                {
                    name = ToFullName(parentName, name);
                    ParseConfiguration(child, name, conf, priority, fileName);
                }
            }
        }

        /// <summary>
        /// Parses an XML node that is marked as a collection by the '-collection' suffix 
        /// </summary>
        /// <param name="parentName">The parent path</param>
        /// <param name="conf">The configuration collection</param>
        /// <param name="priority">The priority of the config item</param>
        /// <param name="fileName">The file been parsed</param>
        /// <param name="child">The child XML node been parsed</param>
        /// <param name="name">The name of the child node</param>
        private void ParseConfigurationCollectionNode(string parentName, Dictionary<string, ConfigItem> conf, uint priority, string fileName,
            XmlNode child, string name)
        {
            name = RemoveCollectionMarker(name);
            name = ToFullName(parentName, name);
            var arrayString = CollectionToValidJsonArrayString(child);
            PutOrUpdateEntry(conf, name, arrayString, priority, fileName, ArrayType.Collection);
        }

        /// <summary>
        /// Parses an XML node that is marked as a list by the '-list' suffix 
        /// </summary>
        /// <param name="parentName">The parent path</param>
        /// <param name="conf">The configuration collection</param>
        /// <param name="priority">The priority of the config item</param>
        /// <param name="fileName">The file been parsed</param>
        /// <param name="child">The child XML node been parsed</param>
        /// <param name="name">The name of the child node</param>
        private void ParseConfigurationListNode(string parentName, Dictionary<string, ConfigItem> conf, uint priority, string fileName,
            XmlNode child, string name)
        {
            name = RemoveListMarker(name);
            name = ToFullName(parentName, name);
            var arrayString = ToValidJsonArrayString(child);
            PutOrUpdateEntry(conf, name, arrayString, priority, fileName, ArrayType.List);
        }

        private const string ListMarker = "-list";
        private const string CollectionMarker = "-collection";

        private (string Key, string Value, ArrayType IsArray) ParseStringEntry(string key, string value)
        {
            (string Key, string Value, ArrayType IsArray) res = (key, value, ArrayType.None);

            if (key.EndsWith(ListMarker))
            {
                res.Key = RemoveListMarker(key);
                res.Value = ToValidJsonArrayString(value);
                res.IsArray = ArrayType.List;
                return res;
            }
            
            if (key.EndsWith(CollectionMarker))
            {
                res.Key = RemoveCollectionMarker(key);
                res.Value = ToValidJsonArrayString(value);
                res.IsArray = ArrayType.Collection;
                return res;
            }

            return res;
        }
        /// <summary>
        /// This method is a wrapper method that deals with the parsing of simple lists e.g. MyList-list="1,2,3"
        /// and then invokes the actual overload that populates the value.
        /// </summary>
        /// <param name="conf">The configuration collection</param>
        /// <param name="key">The key for this configuration item</param>
        /// <param name="value">The Value of this configuration item</param>
        /// <param name="priority">The priority of this config item</param>
        /// <param name="fileName">The file name where this config item was found at</param>
        /// <param name="node">The XML node where this config item was found at</param>
        private void PutOrUpdateEntry(Dictionary<string, ConfigItem> conf, string key, string value, uint priority,
            string fileName, XmlNode node = null, Exception exception = null)
        {
            (string Key, string Value, ArrayType IsArray) = ParseStringEntry(key, value);
            PutOrUpdateEntry(conf, Key, Value, priority, fileName, IsArray, node, exception);
        }

        /// <summary>
        /// Populates the config item in the configuration collection according to the following rules:
        /// 1) If there is no config item under the given key will populate the config value under the given key.
        /// 2) If there is already a value for the given key will check if the new item has higher priority and will
        /// populate the new value under the given key and add the old value to the override list.
        /// 3)If the new value has lower priority than the old value it shall be added to the override list.
        /// </summary>
        /// <param name="conf">The configuration collection</param>
        /// <param name="key">The key for this configuration item</param>
        /// <param name="value">The Value of this configuration item</param>
        /// <param name="priority">The priority of this config item</param>
        /// <param name="fileName">The file name where this config item was found at</param>
        /// <param name="isArray">Indicates whether or not this entry is of array type and which kind</param>
        /// <param name="node">The XML node where this config item was found at</param>
        private void PutOrUpdateEntry(Dictionary<string, ConfigItem> conf, string key, string value, uint priority, 
            string fileName, ArrayType isArray, XmlNode node = null, Exception exception = null)
        {
            var configItemInfo = new ConfigItemInfo
            {
                FileName = fileName,
                Priority = priority,
                Value = value
            };

            conf.TryGetValue(key, out ConfigItem old);
            if (old == null)
            {
                var configItem = new ConfigItem(_configDecryptor) { Key = key, Value = value, Priority = priority, Node = node ,isArray = isArray, ParsingException = exception};
                configItem.Overrides.Add(configItemInfo);
                conf[key] = configItem;
            }
            else if (old.Priority < priority)
            {
                var configItem = new ConfigItem(_configDecryptor)
                {
                    Key = key,
                    Value = value,
                    Priority = priority,
                    Node = node,
                    Overrides = old.Overrides,
                    isArray = isArray,
                    ParsingException = exception
                };
                configItem.Overrides.Add(configItemInfo);
                conf[key] = configItem;
            }
            else
            {
                old.Overrides.Add(configItemInfo);
            }
        }

        /// <summary>
        /// Removes the collection suffix from the given string
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private static string RemoveCollectionMarker(string key)
        {
            return key.Substring(0, key.Length - CollectionMarker.Length);
        }

        /// <summary>
        /// Removes the list suffix from the given string
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private static string RemoveListMarker(string key)
        {
            return key.Substring(0, key.Length - ListMarker.Length);
        }

        /// <summary>
        /// Combines the parent path with the child path to generate the complete path for the child node
        /// </summary>
        /// <param name="parentName">Parent Path</param>
        /// <param name="childName">Child name</param>
        /// <returns></returns>
        private static string ToFullName(string parentName, string childName)
        {
            return (string.IsNullOrEmpty(parentName) ? string.Empty : parentName + ".") + childName;
        }

        private const string delimiter = ",";
        private static readonly string[] _splitArrayBy = { delimiter };
        private string ToValidJsonArrayString(string value)
        {
            var array = SplitArrayElements(value);
            return array.ToString();
        }

        /// <summary>
        /// Splits a comma separated list into tokens and populates a JArray.
        /// </summary>
        /// <param name="value">The list been split</param>
        /// <returns>The constructed JArray</returns>
        private JArray SplitArrayElements(string value)
        {
            var elements = value.Split(_splitArrayBy, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim());
            var array = new JArray(elements);
            return array;
        }
        /// <summary>
        /// Converts a simple list into a valid JArray object
        /// </summary>
        /// <param name="value">simple list of items delimited by commas</param>
        /// <returns>a JArray containing the values in the list</returns>
        private JArray ToValidJsonArray(string value)
        {
            return SplitArrayElements(value);
        }

        /// <summary>
        /// A special XML element to mark a list item
        /// </summary>
        private const string ListItemElementName = "Item";

        /// <summary>
        /// Transform a single xml node marked as a collection into a single valid json array string
        /// </summary>
        /// <param name="node">The XML node marked as a collection</param>
        /// <returns>Json array string containing the collection items</returns>
        private string CollectionToValidJsonArrayString(XmlNode node)
        {
            return CollectionToValidJsonArray(node).ToString();
        }

        /// <summary>
        /// Transform a single xml node marked as a list into a single valid json array string
        /// </summary>
        /// <param name="node">The XML node marked as a list</param>
        /// <returns>Json array string containing the list items</returns>
        private string ToValidJsonArrayString(XmlNode node)
        {
            return ToValidJsonArray(node).ToString();
        }


        /// <summary>
        /// Transform a single xml node marked as a collection into a single valid json array
        /// </summary>
        /// <param name="node">The XML node marked as a collection</param>
        /// <returns>Json array containing the collection items</returns>
        private JArray CollectionToValidJsonArray(XmlNode node)
        {
            //This is the case where we have a single text child as a child element and we want to treat it just as a simple array
            if (node.ChildNodes.Count == 1 && node.ChildNodes[0].NodeType == XmlNodeType.Text)
            {
                return ToValidJsonArray(node.ChildNodes[0].Value);
            }
            ValidateCollectionStructure(node);
            return new JArray(node.ChildNodes.Cast<XmlNode>().Select(CollectionParseXmlToJToken));
        }

        /// <summary>
        /// Transform a single xml node marked as a list into a single valid json array
        /// </summary>
        /// <param name="node">The XML node marked as a list</param>
        /// <returns>Json array containing the list items</returns>
        private JArray ToValidJsonArray(XmlNode node)
        {
            //This is the case where we have a single text child as a child element and we want to treat it just as a simple array
            if (node.ChildNodes.Count == 1 && node.ChildNodes[0].NodeType == XmlNodeType.Text)
            {
                return ToValidJsonArray(node.ChildNodes[0].Value);
            }
            ValidateListStructure(node);
            return new JArray(node.ChildNodes.Cast<XmlNode>().Select(ParseXmlToJToken));
        }

        /// <summary>
        /// Parses a single XML node to JToken and extracts its value to be returned.
        /// </summary>
        /// <param name="xmlNode">The node been parsed</param>
        /// <returns>The JToken value</returns>
        private JToken CollectionParseXmlToJToken(XmlNode xmlNode)
        {
            //Leaving this wrapper method so it will keep the structure of the -list path
            return ParseXmlToJToken_Recursive(xmlNode);
        }

        /// <summary>
        /// Parses a single XML node to JToken and extracts its value to be returned.
        /// If we have an array of objects e.g. <MyArray-list><Item><Person Name='Bob'/></Item></MyArray-list>
        /// The result of the recursive call will be [{'Person':{'Name':'Bob'}}] where it should be [{'Name':'Bob'}]
        /// We need to unwrap one layer of JObject to construct the correct array structure for a json
        /// </summary>
        /// <param name="xmlNode">The node been parsed</param>
        /// <returns>The JToken value</returns>
        private JToken ParseXmlToJToken(XmlNode xmlNode)
        {
            
            var node = ParseXmlToJToken_Recursive(xmlNode);
            //Primitive type
            if (node is JValue)
                return node;
            //Complex object
            var prop = (JProperty)node.Single(p => p is JProperty);
            return prop.Value;
        }

        /// <summary>
        /// Recursively parses XML node into a JToken
        /// </summary>
        /// <param name="xmlNode">The node been parsed</param>
        /// <returns>The constructed JToken value</returns>
        private JToken ParseXmlToJToken_Recursive(XmlNode xmlNode)
        {
            //This is a leaf element
            if (xmlNode.ChildNodes.OfType<XmlNode>().Any(x => x.NodeType != XmlNodeType.Text) == false)
            {
                //This element is an object
                if (xmlNode.Attributes?.Count > 0)
                {
                    return ConstructJObjectFromAttributes(xmlNode);
                }

                //Empty XML node should be translated to an empty object
                if (string.IsNullOrEmpty(xmlNode.InnerText))
                    return new JObject();

                return xmlNode.InnerText;
            }

            //If we got to here we have children need to construct the JObject recursively
            var jObject = ConstructJObjectFromAttributes(xmlNode);
            foreach (XmlNode node in xmlNode.ChildNodes)
            {
                string name = node.Name;
                if (name.EndsWith(ListMarker))
                {
                    name = RemoveListMarker(name);
                    PopulateChildNodeInPlace(jObject, name, ToValidJsonArray(node));
                }
                else if (name.EndsWith(CollectionMarker))
                {
                    name = RemoveCollectionMarker(name);
                    PopulateChildNodeInPlace(jObject, name, CollectionToValidJsonArray(node));
                }
                else
                {
                    PopulateChildNodeInPlace(jObject, node.Name, ParseXmlToJToken_Recursive(node));
                }
            }

            return jObject;
        }

        private static readonly char pathDelimiter = '.';
        private static readonly char[] pathDelimiters = { pathDelimiter };

        /// <summary>
        /// Populates a JToken value into the JObject according to its name.
        /// If the name contains dots it will construct a JObject hierarchy e.g.
        /// if name ="X.Y.Z" and the value is "Foo" than the structure of the JObject will be
        /// { "X":{ "Y":{"Z":"Foo"}}}
        /// </summary>
        /// <param name="jObject">The object that should be populated with the given value</param>
        /// <param name="name">The name of the property been populated</param>
        /// <param name="value">The value of the property been populated</param>
        private void PopulateChildNodeInPlace(JToken jObject, string name, JToken value)
        {
            if(name.IndexOf(pathDelimiter) < 0)
            {
                jObject[name] = value;
                return;
            }

            var tokens = name.Split(pathDelimiters);
            foreach (var token in tokens.Take(tokens.Length - 1))
            {
                if (jObject[token] == null || jObject[token] is JObject == false)
                {
                    jObject[token] = new JObject();
                    jObject = jObject[token];
                }
            }

            jObject[tokens.Last()] = value;
        }

        /// <summary>
        /// Creates a new JObject and populates its properties according to the nodes attributes
        /// </summary>
        /// <param name="xmlNode">The node been parsed</param>
        /// <returns>The constructed JObject as JToken</returns>
        private JToken ConstructJObjectFromAttributes(XmlNode xmlNode)
        {
            var jObject = new JObject();

            if (xmlNode.Attributes == null)
                return jObject;

            foreach (XmlAttribute attribute in xmlNode.Attributes)
            {
                //Dealing with nested lists
                string name = attribute.Name;
                JToken value = attribute.Value;
                if (attribute.Name.EndsWith(ListMarker))
                {
                    name = RemoveListMarker(attribute.Name);
                    value = ToValidJsonArray(attribute.Value);
                }

                else if (attribute.Name.EndsWith(CollectionMarker))
                {
                    name = RemoveCollectionMarker(attribute.Name);
                    value = ToValidJsonArray(attribute.Value);
                }

                jObject[name] = value;
            }

            return jObject;
        }

        /// <summary>
        /// Validates the structure of an XML node marked as a collection node.
        /// </summary>
        /// <exception cref="ConfigurationException">will throw if:
        /// 1) Any of the child node is not called 'Item'
        /// 2) At least one child elements is primitive and one isn't
        /// </exception>
        /// <param name="node">The node to be validated</param>
        private static void ValidateCollectionStructure(XmlNode node)
        {
            var nodes = node.ChildNodes.Cast<XmlNode>().ToArray();

            //All nodes should be of type 'Item'
            if (nodes.All(n => n.Name == ListItemElementName) == false)
            {
                throw new ConfigurationException(
                    $"Node {node.Name} is marked as a list but contains a child element that is not a node of type <{ListItemElementName}/>");
            }

            //All nodes are primitive e.g. <Item>4</Item> this is valid
            if (nodes.All(n => n.HasChildNodes && n.ChildNodes.Count == 1 &&
                               n.ChildNodes[0].NodeType == XmlNodeType.Text && string.IsNullOrEmpty(n.InnerText) == false))
                return;

            //There is a node which is primitive but not all nodes e.g. <some-collection><Item>4</Item><Item name="foo"/></some-collection>
            if (nodes.Any(n => n.HasChildNodes && n.ChildNodes.Count == 1 &&
                              n.ChildNodes[0].NodeType == XmlNodeType.Text))
                throw new ConfigurationException(
                    $"Node {node.Name} contains  a primitive <{ListItemElementName}/> element but also contains complex <{ListItemElementName}/> elements");
        }

        /// <summary>
        /// Validates the structure of an XML node marked as a list node.
        /// </summary>
        /// <exception cref="ConfigurationException">will throw if:
        /// 1) Any of the child node is not called 'Item'
        /// 2) Any of 'Item' nodes doesn't have a single value
        /// 3) Not all child elements of 'Item' are of the same type
        /// </exception>
        /// <param name="node">The node to be validated</param>
        private static void ValidateListStructure(XmlNode node)
        {
            var nodes = node.ChildNodes.Cast<XmlNode>().ToArray();

            //All nodes should be of type 'Item'
            if (nodes.All(n => n.Name == ListItemElementName) == false)
            {
                throw new ConfigurationException(
                    $"Node {node.Name} is marked as a list but contains a child element that is not a node of type <{ListItemElementName}/>");
            }
            //All nodes are primitive e.g. <Item>4</Item> this is valid
            if(nodes.All(n => n.HasChildNodes && n.ChildNodes.Count == 1 &&
                              n.ChildNodes[0].NodeType == XmlNodeType.Text && string.IsNullOrEmpty(n.InnerText) == false) )
                return;
            //All nodes contain a single XML element 
            if (nodes.All(n => n.HasChildNodes && n.ChildNodes.Count == 1) == false)
            {
                throw new ConfigurationException(
                    $"Node {node.Name} contains  an <{ListItemElementName}/> element that does not have one child element");
            }

            //Validate all Item elements contain one child element of the same type.
            string innerType = null;
            foreach (XmlNode childNode in nodes)
            {
                var innerNode = childNode.ChildNodes.Cast<XmlNode>().Single();
                if (innerType != null && innerType != innerNode.Name)
                {
                    throw new ConfigurationException($"Node {node.Name} contains <{ListItemElementName}/> of more than one type {innerType} and {innerNode.Name}");
                }

                innerType = innerNode.Name;
            }
        }
    }
}
