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
    public class FileBasedConfigItemsSource : IConfigItemsSource
    {
        private readonly IConfigurationLocationsParser _configurationLocations;
        private readonly IEnvironmentVariableProvider _environmentVariableProvider;
        private readonly IFileSystem _fileSystem;
        private readonly ConfigDecryptor _configDecryptor;

        private readonly Regex paramMatcher = new Regex(@"([^\\]|^)%(?<envName>[^%]+)%", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        public FileBasedConfigItemsSource(IConfigurationLocationsParser configurationLocations, IEnvironmentVariableProvider environmentVariableProvider, IFileSystem fileSystem, ConfigDecryptor configDecryptor)
        {
            _configurationLocations = configurationLocations;
            _environmentVariableProvider = environmentVariableProvider;
            _fileSystem = fileSystem;
            _configDecryptor = configDecryptor;
        }

        public async Task<ConfigItemsCollection> GetConfiguration()
        {
            var conf = new Dictionary<string, ConfigItem>(StringComparer.OrdinalIgnoreCase);

            foreach(var configFile in _configurationLocations.ConfigFileDeclarations.SelectMany(FindConfigFiles))
            {
                await ReadConfiguration(configFile, conf).ConfigureAwait(false);
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
                                           Value = _environmentVariableProvider.GetEnvironmentVariable(match.Groups[1].Value)
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
            return collection;
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

        private async Task ReadConfiguration(ConfigFile configFile, Dictionary<string, ConfigItem> conf)
        {
            try
            {
                var xmlDocument = new XmlDocument();
                var xmlString = await _fileSystem.ReadAllTextFromFileAsync(configFile.FullName).ConfigureAwait(false);
                xmlDocument.LoadXml(xmlString);
                ParseConfigXml(configFile.FullName, xmlDocument, configFile.Priority, conf);
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
                        if (child.NodeType == XmlNodeType.Text || child.NodeType == XmlNodeType.CDATA)
                            PutOrUpdateEntry(conf, parentName, child.Value, priority, fileName);
                        else
                        {
                            string name = child.Name;
                            if (name.EndsWith(ListMarker))
                            {
                                name = RemoveListMarker(name);
                                name = ToFullName(parentName, name);
                                var arrayString = ToValidJsonArrayString(child);
                                PutOrUpdateEntry(conf, name, arrayString, priority, fileName, true);
                            }
                            else
                            {
                                name = ToFullName(parentName, name);
                                ParseConfiguration(child, name, conf, priority, fileName);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("parentName=" + parentName, e);
            }
        }

        private const string ListMarker = "-list";

        private (string Key, string Value, bool IsArray) ParseStringEntry(string key, string value)
        {
            (string Key, string Value, bool IsArray) res = (key, value, false);

            if (key.EndsWith(ListMarker))
            {
                res.Key = RemoveListMarker(key);
                res.Value = ToValidJsonArrayString(value);
                res.IsArray = true;
                return res;
            }

            return res;
        }

        private void PutOrUpdateEntry(Dictionary<string, ConfigItem> conf, string key, string value, uint priority,
            string fileName, XmlNode node = null)
        {
            (string Key, string Value, bool IsArray) = ParseStringEntry(key, value);
            PutOrUpdateEntry(conf, Key, Value, priority, fileName, IsArray, node);
        }

        private void PutOrUpdateEntry(Dictionary<string, ConfigItem> conf, string key, string value, uint priority, 
            string fileName, bool isArray, XmlNode node = null)
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
                var configItem = new ConfigItem(_configDecryptor) { Key = key, Value = value, Priority = priority, Node = node ,isArray = isArray};
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
                    isArray = isArray
                };
                configItem.Overrides.Add(configItemInfo);
                conf[key] = configItem;
            }
            else
            {
                old.Overrides.Add(configItemInfo);
            }
        }

        private static string RemoveListMarker(string key)
        {
            return key.Substring(0, key.Length - ListMarker.Length);
        }

        private static string ToFullName(string parentName, string childName)
        {
            return (string.IsNullOrEmpty(parentName) ? string.Empty : parentName + ".") + childName;
        }

        private readonly char[] _splitArrayBy = new[] {','};
        private string ToValidJsonArrayString(string value)
        {
            var array = SplitArrayElements(value);
            return array.ToString();
        }

        private JArray SplitArrayElements(string value)
        {
            var elements = value.Split(_splitArrayBy, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim());
            var array = new JArray(elements);
            return array;
        }

        private JArray ToValidJsonArray(string value)
        {
            return SplitArrayElements(value);
        }

        private const string ListItemElementName = "Item";
        private string ToValidJsonArrayString(XmlNode node)
        {
            return ToValidJsonArray(node).ToString();
        }

        private JArray ToValidJsonArray(XmlNode node)
        {
            ValidateListStructure(node);
            var res = new JArray(node.ChildNodes.Cast<XmlNode>().Select(ParseXmlToJToken));
            return res;
        }

        private JToken ParseXmlToJToken(XmlNode xmlNode)
        {
            //if we have an array of objects e.g. <MyArray-list><Item><Person Name='Bob'/></Item></MyArray-list>
            //The result of the recursive call will be [{'Person':{'Name':'Bob'}}] where it should be [{'Name':'Bob'}]
            //We need to unwrap one layer of JObject to construct the correct array structure for a json
            var node = ParseXmlToJToken_Recursive(xmlNode);
            //Primitive type
            if (node is JValue)
                return node;
            //Complex object
            var prop = (JProperty)node.Single(p => p is JProperty);
            return prop.Value;
        }

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
                //This is just a value node
                if(string.IsNullOrEmpty(xmlNode.InnerText))
                    throw new ConfigurationException($"Unexpected empty configuration node of type {xmlNode.Name}");

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
                else
                {
                    PopulateChildNodeInPlace(jObject, node.Name, ParseXmlToJToken_Recursive(node));
                }
            }

            return jObject;
        }

        private static readonly char pathDelimiter = '.';
        private static readonly char[] pathDelimiters = { pathDelimiter };
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

                jObject[name] = value;
            }

            return jObject;
        }

        private static void ValidateListStructure(XmlNode node)
        {
            var nodes = node.ChildNodes.Cast<XmlNode>().ToArray();
            //There should be at least one item in the list, an empty list is probably a configuration error.
            if (nodes.Any() == false)
            {
                throw new ConfigurationException(
                    $"Node {node.Name} is marked as a list but contains no child elements");
            }
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
