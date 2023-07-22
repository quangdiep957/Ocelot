using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Newtonsoft.Json;
using Ocelot.Configuration.File;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ocelot.DependencyInjection
{
    public static class ConfigurationBuilderExtensions
    {
        private static readonly Regex _reg = new Regex(@"^ocelot\.(.*?)\.json$", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

        [Obsolete("Please set BaseUrl in ocelot.json GlobalConfiguration.BaseUrl")]
        public static IConfigurationBuilder AddOcelotBaseUrl(this IConfigurationBuilder builder, string baseUrl)
        {
            Regex.CacheSize += 100;

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

        public static IConfigurationBuilder AddOcelot(this IConfigurationBuilder builder, IWebHostEnvironment env)
        {
            return builder.AddOcelot(".", env);
        }

        public static IConfigurationBuilder AddOcelot(this IConfigurationBuilder builder, string folder, IWebHostEnvironment env)
        {
            Regex.CacheSize += 100;

            const string primaryConfigFile = "ocelot.json";

            const string globalConfigFile = "ocelot.global.json";

            var excludeConfigName = env?.EnvironmentName != null ? $"ocelot.{env.EnvironmentName}.json" : string.Empty;

            var files = new DirectoryInfo(folder)
                .EnumerateFiles()
                .Where(fi => _reg.IsMatch(fi.Name) && (fi.Name != excludeConfigName))
                .ToArray();

            var fileConfiguration = new FileConfiguration();

            foreach (var file in files)
            {
                if (files.Length > 1 && file.Name.Equals(primaryConfigFile, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var lines = File.ReadAllText(file.FullName);

                var config = JsonConvert.DeserializeObject<FileConfiguration>(lines);

                if (file.Name.Equals(globalConfigFile, StringComparison.OrdinalIgnoreCase))
                {
                    fileConfiguration.GlobalConfiguration = config.GlobalConfiguration;
                }

                fileConfiguration.Aggregates.AddRange(config.Aggregates);
                fileConfiguration.Routes.AddRange(config.Routes);
            }

            var json = JsonConvert.SerializeObject(fileConfiguration);

            File.WriteAllText(primaryConfigFile, json);

            builder.AddJsonFile(primaryConfigFile, false, false);

            return builder;
        }
    }
}
