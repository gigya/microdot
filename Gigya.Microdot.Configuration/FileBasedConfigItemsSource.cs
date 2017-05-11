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

namespace Gigya.Microdot.Configuration
{
    public class FileBasedConfigItemsSource : IConfigItemsSource
    {
        private readonly IConfigurationLocationsParser _configurationLocations;
        private readonly IEnvironmentVariableProvider _environmentVariableProvider;
        private readonly IFileSystem _fileSystem;

        private readonly Regex paramMatcher = new Regex(@"([^\\]|^)%(?<envName>[^%]+)%", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        public FileBasedConfigItemsSource(IConfigurationLocationsParser configurationLocations, IEnvironmentVariableProvider environmentVariableProvider, IFileSystem fileSystem)
        {
            _configurationLocations = configurationLocations;
            _environmentVariableProvider = environmentVariableProvider;
            _fileSystem = fileSystem;
        }

        public async Task<ConfigItemsCollection> GetConfiguration()
        {
            var conf = new Dictionary<string, ConfigItem>();

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
                            string childName = (string.IsNullOrEmpty(parentName) ? string.Empty : parentName + ".") + child.Name;
                            ParseConfiguration(child, childName, conf, priority, fileName);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("parentName=" + parentName, e);
            }
        }

        private void PutOrUpdateEntry(Dictionary<string, ConfigItem> conf, string key, string value, uint priority, string fileName, XmlNode node = null)
        {
            var configItemInfo = new ConfigItemInfo
            {
                FileName = fileName,
                Priority = priority,
                Value = value
            };

            ConfigItem old;
            conf.TryGetValue(key, out old);
            if (old == null)
            {
                var configItem = new ConfigItem { Key = key, Value = value, Priority = priority, Node = node };
                configItem.Overrides.Add(configItemInfo);
                conf[key] = configItem;
            }
            else if (old.Priority < priority)
            {
                var configItem = new ConfigItem
                {
                    Key = key,
                    Value = value,
                    Priority = priority,
                    Node = node,
                    Overrides = old.Overrides
                };
                configItem.Overrides.Add(configItemInfo);
                conf[key] = configItem;
            }
            else
            {
                old.Overrides.Add(configItemInfo);
            }
        }
    }
}
