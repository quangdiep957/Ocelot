using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Newtonsoft.Json;
using Ocelot.Configuration.File;

namespace Ocelot.DependencyInjection
{
    /// <summary>
    /// Defines extension-methods for the <see cref="IConfigurationBuilder"/> interface.
    /// </summary>
    public static partial class ConfigurationBuilderExtensions
    {
        public const string PrimaryConfigFile = "ocelot.json";
        public const string GlobalConfigFile = "ocelot.global.json";

#if NET7_0_OR_GREATER
        [GeneratedRegex(@"^ocelot\.(.*?)\.json$", RegexOptions.IgnoreCase | RegexOptions.Singleline, "en-US")]
        private static partial Regex SubConfigRegex();
#else
        private static readonly Regex SubConfigRegexVar = new(@"^ocelot\.(.*?)\.json$", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromMilliseconds(1000));
        private static Regex SubConfigRegex()
        {
            return SubConfigRegexVar;
        }
#endif

        [Obsolete("Please set BaseUrl in ocelot.json GlobalConfiguration.BaseUrl")]
        public static IConfigurationBuilder AddOcelotBaseUrl(this IConfigurationBuilder builder, string baseUrl)
        {
            var memorySource = new MemoryConfigurationSource
            {
                InitialData = new List<KeyValuePair<string, string>>
                {
                    new("BaseUrl", baseUrl),
                },
            };

            builder.Add(memorySource);

            return builder;
        }

        /// <summary>
        /// Adds Ocelot configuration by environment, reading the required files from the default path.
        /// </summary>
        /// <param name="builder">Configuration builder to extend.</param>
        /// <param name="env">Web hosting environment object.</param>
        /// <returns>An <see cref="IConfigurationBuilder"/> object.</returns>
        public static IConfigurationBuilder AddOcelot(this IConfigurationBuilder builder, IWebHostEnvironment env)
        {
            return builder.AddOcelot(".", env);
        }

        /// <summary>
        /// Adds Ocelot configuration by environment, reading the required files from the specified folder.
        /// </summary>
        /// <param name="builder">Configuration builder to extend.</param>
        /// <param name="folder">Folder to read files from.</param>
        /// <param name="env">Web hosting environment object.</param>
        /// <returns>An <see cref="IConfigurationBuilder"/> object.</returns>
        public static IConfigurationBuilder AddOcelot(this IConfigurationBuilder builder, string folder, IWebHostEnvironment env)
        {
            var excludeConfigName = env?.EnvironmentName != null ? $"ocelot.{env.EnvironmentName}.json" : string.Empty;

            var reg = SubConfigRegex();

            var files = new DirectoryInfo(folder)
                .EnumerateFiles()
                .Where(fi => reg.IsMatch(fi.Name) && fi.Name != excludeConfigName)
                .ToArray();

            dynamic fileConfiguration = new ExpandoObject();
            fileConfiguration.GlobalConfiguration = new ExpandoObject();
            fileConfiguration.Aggregates = new List<object>();
            fileConfiguration.ReRoutes = new List<object>();

            foreach (var file in files)
            {
                if (files.Length > 1 && file.Name.Equals(PrimaryConfigFile, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var lines = File.ReadAllText(file.FullName);

                dynamic config = JsonConvert.DeserializeObject<ExpandoObject>(lines);

                if (file.Name.Equals(GlobalConfigFile, StringComparison.OrdinalIgnoreCase))
                {
                    TryAddSection(fileConfiguration, config, nameof(FileConfiguration.GlobalConfiguration));
                }

                TryAddSection(fileConfiguration, config, nameof(FileConfiguration.Aggregates));
                TryAddSection(fileConfiguration, config, nameof(FileConfiguration.ReRoutes));
            }

            return builder.AddOcelot(fileConfiguration);
        }

        /// <summary>
        /// Adds Ocelot configuration by ready configuration object and writes JSON to the primary configuration file.<br/>
        /// Finally, adds JSON file as configuration provider.
        /// </summary>
        /// <param name="builder">Configuration builder to extend.</param>
        /// <param name="fileConfiguration">File configuration to add as JSON provider.</param>
        /// <returns>An <see cref="IConfigurationBuilder"/> object.</returns>
        public static IConfigurationBuilder AddOcelot(this IConfigurationBuilder builder, FileConfiguration fileConfiguration)
        {
            var json = JsonConvert.SerializeObject(fileConfiguration);

            File.WriteAllText(PrimaryConfigFile, json);

            return builder.AddJsonFile(PrimaryConfigFile, false, false);
        }

        private static void TryAddSection(ExpandoObject mergedConfig, ExpandoObject config, string sectionName)
        {
            var configAsDict = config as IDictionary<string, object>;
            var mergedConfigAsDict = mergedConfig as IDictionary<string, object>;
            if (configAsDict.ContainsKey(sectionName) && mergedConfigAsDict.ContainsKey(sectionName))
            {
                var mergedSectionAsExpando = mergedConfigAsDict[sectionName] as ExpandoObject;
                if (mergedSectionAsExpando != null)
                {
                    mergedConfigAsDict[sectionName] = configAsDict[sectionName];                    
                }
                else
                {
                    var mergedSectionAsList = mergedConfigAsDict[sectionName] as List<object>;
                    var sectionAsList = configAsDict[sectionName] as List<object>;

                    mergedSectionAsList.AddRange(sectionAsList);
                }
            }
        }
    }
}
