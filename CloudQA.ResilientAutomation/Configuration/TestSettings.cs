using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace CloudQA.ResilientAutomation.Configuration
{
    public class TestSettings
    {
        public string TargetUrl { get; set; } = "https://app.cloudqa.io/home/AutomationPracticeForm";
        public int TimeoutSeconds { get; set; } = 15;
        public bool Headless { get; set; } = false;

        public static TestSettings Load()
        {
            var basePath = AppContext.BaseDirectory;
            if (!File.Exists(Path.Combine(basePath, "appsettings.json")))
            {
                basePath = Directory.GetCurrentDirectory();
            }

            var config = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var settings = new TestSettings();
            var section = config.GetSection("AutomationSettings");

            // 1. JSON Configuration
            if (!string.IsNullOrWhiteSpace(section["TargetUrl"]))
                settings.TargetUrl = section["TargetUrl"]!;

            if (int.TryParse(section["TimeoutSeconds"], out int timeout))
                settings.TimeoutSeconds = timeout;

            if (bool.TryParse(section["Headless"], out bool headless))
                settings.Headless = headless;

            // 2. Direct Environment Variables (e.g. TARGET_URL, HEADLESS)
            var envUrl = Environment.GetEnvironmentVariable("TARGET_URL") 
                         ?? Environment.GetEnvironmentVariable("TargetUrl");
            if (!string.IsNullOrWhiteSpace(envUrl))
                settings.TargetUrl = envUrl;

            var envHeadless = Environment.GetEnvironmentVariable("HEADLESS")
                              ?? Environment.GetEnvironmentVariable("Headless");
            if (bool.TryParse(envHeadless, out bool envH))
                settings.Headless = envH;

            var envTimeout = Environment.GetEnvironmentVariable("TIMEOUT_SECONDS")
                             ?? Environment.GetEnvironmentVariable("TimeoutSeconds");
            if (int.TryParse(envTimeout, out int envT))
                settings.TimeoutSeconds = envT;

            // 3. NUnit CLI Test Parameters (e.g., dotnet test -- NUnit.TargetUrl="...")
            try
            {
                var nunitUrl = TestContext.Parameters.Get("TargetUrl") 
                               ?? TestContext.Parameters.Get("targetUrl")
                               ?? TestContext.Parameters.Get("url");
                if (!string.IsNullOrWhiteSpace(nunitUrl))
                    settings.TargetUrl = nunitUrl;

                var nunitHeadless = TestContext.Parameters.Get("Headless")
                                    ?? TestContext.Parameters.Get("headless");
                if (bool.TryParse(nunitHeadless, out bool nH))
                    settings.Headless = nH;

                var nunitTimeout = TestContext.Parameters.Get("TimeoutSeconds")
                                   ?? TestContext.Parameters.Get("timeout");
                if (int.TryParse(nunitTimeout, out int nT))
                    settings.TimeoutSeconds = nT;
            }
            catch
            {
                // TestContext might not be initialized outside of NUnit execution context
            }

            return settings;
        }
    }
}
