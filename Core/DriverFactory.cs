using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using CloudQA.ResilientAutomation.Configuration;

namespace CloudQA.ResilientAutomation.Core
{
    /// <summary>
    /// Factory for configuring and instantiating WebDriver instances.
    /// Supports Chrome, Firefox, and Edge with configurable timeouts and headless execution.
    /// </summary>
    public static class DriverFactory
    {
        public static IWebDriver CreateDriver(TestSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();

            var browser = settings.Browser.Trim().ToLowerInvariant();
            IWebDriver driver = browser switch
            {
                "firefox" => CreateFirefoxDriver(settings),
                "edge" => CreateEdgeDriver(settings),
                _ => CreateChromeDriver(settings)
            };

            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(Math.Max(settings.TimeoutSeconds, 30));
            driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(settings.TimeoutSeconds);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;

            return driver;
        }

        private static IWebDriver CreateChromeDriver(TestSettings settings)
        {
            var options = new ChromeOptions();
            if (settings.Headless)
            {
                options.AddArgument("--headless=new");
            }

            options.AddArgument("--start-maximized");
            options.AddArgument("--disable-notifications");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.PageLoadStrategy = PageLoadStrategy.Normal;

            var service = ChromeDriverService.CreateDefaultService();
            service.SuppressInitialDiagnosticInformation = true;
            service.HideCommandPromptWindow = true;

            try
            {
                return new ChromeDriver(service, options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize ChromeDriver: {ex.Message}", ex);
            }
        }

        private static IWebDriver CreateFirefoxDriver(TestSettings settings)
        {
            var options = new FirefoxOptions();
            if (settings.Headless)
            {
                options.AddArgument("-headless");
            }

            options.PageLoadStrategy = PageLoadStrategy.Normal;

            var service = FirefoxDriverService.CreateDefaultService();
            service.SuppressInitialDiagnosticInformation = true;
            service.HideCommandPromptWindow = true;

            try
            {
                return new FirefoxDriver(service, options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize FirefoxDriver: {ex.Message}", ex);
            }
        }

        private static IWebDriver CreateEdgeDriver(TestSettings settings)
        {
            var options = new EdgeOptions();
            if (settings.Headless)
            {
                options.AddArgument("--headless=new");
            }

            options.AddArgument("--start-maximized");
            options.AddArgument("--disable-notifications");
            options.PageLoadStrategy = PageLoadStrategy.Normal;

            var service = EdgeDriverService.CreateDefaultService();
            service.SuppressInitialDiagnosticInformation = true;
            service.HideCommandPromptWindow = true;

            try
            {
                return new EdgeDriver(service, options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize EdgeDriver: {ex.Message}", ex);
            }
        }
    }
}
