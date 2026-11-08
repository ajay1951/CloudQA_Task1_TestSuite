using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace CloudQA.ResilientAutomation.Configuration
{
    /// <summary>
    /// Configuration model for test execution settings.
    /// Supports CLI parameters, environment variables, and appsettings.json.
    /// </summary>
    public class TestSettings
    {
        public string TargetUrl { get; set; } = "https://app.cloudqa.io/home/AutomationPracticeForm";
        public int TimeoutSeconds { get; set; } = 15;
        public bool Headless { get; set; } = false;
        public string Browser { get; set; } = "Chrome";
        public string FirstName { get; set; } = "Ajay";
        public string LastName { get; set; } = "Gollapudi";
        public string Gender { get; set; } = "Male";
        public string State { get; set; } = "India";

        public static TestSettings Load()
        {
            var basePath = AppContext.BaseDirectory;
            if (!File.Exists(Path.Combine(basePath, "appsettings.json")))
            {
                basePath = Directory.GetCurrentDirectory();
            }

            var config = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            var settings = new TestSettings();
            var section = config.GetSection("AutomationSettings");

            // Priority 3: appsettings.json
            if (!string.IsNullOrWhiteSpace(section["TargetUrl"]))
                settings.TargetUrl = section["TargetUrl"]!.Trim();

            if (int.TryParse(section["TimeoutSeconds"], out int timeout))
                settings.TimeoutSeconds = timeout;

            if (bool.TryParse(section["Headless"], out bool headless))
                settings.Headless = headless;

            if (!string.IsNullOrWhiteSpace(section["Browser"]))
                settings.Browser = section["Browser"]!.Trim();

            if (!string.IsNullOrWhiteSpace(section["FirstName"]))
                settings.FirstName = section["FirstName"]!.Trim();

            if (!string.IsNullOrWhiteSpace(section["LastName"]))
                settings.LastName = section["LastName"]!.Trim();

            if (!string.IsNullOrWhiteSpace(section["Gender"]))
                settings.Gender = section["Gender"]!.Trim();

            if (!string.IsNullOrWhiteSpace(section["State"]))
                settings.State = section["State"]!.Trim();

            // Priority 2: Direct Environment Variables
            var envUrl = Environment.GetEnvironmentVariable("TARGET_URL") 
                         ?? Environment.GetEnvironmentVariable("TargetUrl");
            if (!string.IsNullOrWhiteSpace(envUrl))
                settings.TargetUrl = envUrl.Trim();

            var envHeadless = Environment.GetEnvironmentVariable("HEADLESS")
                              ?? Environment.GetEnvironmentVariable("Headless");
            if (bool.TryParse(envHeadless, out bool envH))
                settings.Headless = envH;

            var envTimeout = Environment.GetEnvironmentVariable("TIMEOUT_SECONDS")
                             ?? Environment.GetEnvironmentVariable("TimeoutSeconds");
            if (int.TryParse(envTimeout, out int envT))
                settings.TimeoutSeconds = envT;

            var envBrowser = Environment.GetEnvironmentVariable("BROWSER");
            if (!string.IsNullOrWhiteSpace(envBrowser))
                settings.Browser = envBrowser.Trim();

            var envFirstName = Environment.GetEnvironmentVariable("FIRST_NAME")
                               ?? Environment.GetEnvironmentVariable("FirstName");
            if (!string.IsNullOrWhiteSpace(envFirstName))
                settings.FirstName = envFirstName.Trim();

            var envLastName = Environment.GetEnvironmentVariable("LAST_NAME")
                              ?? Environment.GetEnvironmentVariable("LastName");
            if (!string.IsNullOrWhiteSpace(envLastName))
                settings.LastName = envLastName.Trim();

            var envGender = Environment.GetEnvironmentVariable("GENDER");
            if (!string.IsNullOrWhiteSpace(envGender))
                settings.Gender = envGender.Trim();

            var envState = Environment.GetEnvironmentVariable("STATE");
            if (!string.IsNullOrWhiteSpace(envState))
                settings.State = envState.Trim();

            // Priority 1: NUnit CLI Test Parameters (e.g., dotnet test -- NUnit.TargetUrl="...")
            try
            {
                var nunitUrl = TestContext.Parameters.Get("TargetUrl") 
                               ?? TestContext.Parameters.Get("targetUrl")
                               ?? TestContext.Parameters.Get("url");
                if (!string.IsNullOrWhiteSpace(nunitUrl))
                    settings.TargetUrl = nunitUrl.Trim();

                var nunitHeadless = TestContext.Parameters.Get("Headless")
                                    ?? TestContext.Parameters.Get("headless");
                if (bool.TryParse(nunitHeadless, out bool nH))
                    settings.Headless = nH;

                var nunitTimeout = TestContext.Parameters.Get("TimeoutSeconds")
                                   ?? TestContext.Parameters.Get("timeout");
                if (int.TryParse(nunitTimeout, out int nT))
                    settings.TimeoutSeconds = nT;

                var nunitBrowser = TestContext.Parameters.Get("Browser")
                                   ?? TestContext.Parameters.Get("browser");
                if (!string.IsNullOrWhiteSpace(nunitBrowser))
                    settings.Browser = nunitBrowser.Trim();

                var nunitFirstName = TestContext.Parameters.Get("FirstName")
                                     ?? TestContext.Parameters.Get("firstName");
                if (!string.IsNullOrWhiteSpace(nunitFirstName))
                    settings.FirstName = nunitFirstName.Trim();

                var nunitLastName = TestContext.Parameters.Get("LastName")
                                    ?? TestContext.Parameters.Get("lastName");
                if (!string.IsNullOrWhiteSpace(nunitLastName))
                    settings.LastName = nunitLastName.Trim();

                var nunitGender = TestContext.Parameters.Get("Gender")
                                  ?? TestContext.Parameters.Get("gender");
                if (!string.IsNullOrWhiteSpace(nunitGender))
                    settings.Gender = nunitGender.Trim();

                var nunitState = TestContext.Parameters.Get("State")
                                 ?? TestContext.Parameters.Get("state");
                if (!string.IsNullOrWhiteSpace(nunitState))
                    settings.State = nunitState.Trim();
            }
            catch
            {
                // TestContext is not active when instantiated outside of NUnit execution
            }

            settings.Validate();
            return settings;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(TargetUrl) || !Uri.TryCreate(TargetUrl, UriKind.Absolute, out var uriResult) 
                || (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException($"Invalid configuration: TargetUrl '{TargetUrl}' must be a valid absolute HTTP or HTTPS URL.");
            }

            if (TimeoutSeconds <= 0 || TimeoutSeconds > 300)
            {
                throw new InvalidOperationException($"Invalid configuration: TimeoutSeconds must be between 1 and 300 seconds. Found: {TimeoutSeconds}.");
            }

            if (string.IsNullOrWhiteSpace(FirstName))
            {
                throw new InvalidOperationException("Invalid configuration: FirstName cannot be null or empty.");
            }

            if (string.IsNullOrWhiteSpace(Gender))
            {
                throw new InvalidOperationException("Invalid configuration: Gender cannot be null or empty.");
            }

            if (string.IsNullOrWhiteSpace(State))
            {
                throw new InvalidOperationException("Invalid configuration: State cannot be null or empty.");
            }
        }
    }
}
