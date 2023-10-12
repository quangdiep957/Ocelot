﻿using Ocelot.Configuration.File;
using Ocelot.DependencyInjection;

namespace Ocelot.AcceptanceTests;

public class ConfigurationMergeTests : IDisposable
{
    private readonly FileConfiguration _globalConfig;
    private readonly Steps _steps;

    public ConfigurationMergeTests()
    {
        _steps = new Steps();

        if (File.Exists(TestConfiguration.PrimaryConfigurationPath))
        {
            try
            {
                File.Delete(TestConfiguration.PrimaryConfigurationPath);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        _globalConfig = new FileConfiguration
        {
            GlobalConfiguration = new FileGlobalConfiguration
            {
                RequestIdKey = "initialKey",
            },
        };
    }

    [Fact]
    public void Should_run_with_config_merged_to_memory()
    {
        this.Given(x => _steps.GivenThereIsAConfiguration(_globalConfig, TestConfiguration.ConfigurationPartPath("global")))
            .When(x => _steps.WhenOcelotIsRunningMergedConfig(MergeOcelotJson.ToMemory))
            .Then(x => TheOcelotJsonFileExists(false))
            .BDDfy();
    }

    [Fact]
    public void Should_run_with_config_merged_to_file()
    {
        this.Given(x => _steps.GivenThereIsAConfiguration(_globalConfig))
            .When(x => _steps.WhenOcelotIsRunningMergedConfig(MergeOcelotJson.ToFile))
            .Then(x => TheOcelotJsonFileExists(true))
            .BDDfy();
    }

    public void Dispose()
    {
        _steps.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void TheOcelotJsonFileExists(bool expected)
    {
        var primaryConfigFile = Path.Combine(string.Empty, "ocelot.json");
        File.Exists(primaryConfigFile).ShouldBe(expected);
    }
}
