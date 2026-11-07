using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using CloudQA.ResilientAutomation.Configuration;

namespace CloudQA.ResilientAutomation.Core
{
    /// <summary>
    /// Factory for configuring and instantiating WebDriver instances following SDET industry standards.
    /// Avoids mixed implicit waits, sets sensible timeouts, and uses clean Chrome configuration.
    /// </summary>
    public static class DriverFactory
    {
        public static IWebDriver CreateDriver(TestSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();

            var options = new ChromeOptions();

            if (settings.Headless)
            {
                // Modern, reliable Chrome headless implementation
                options.AddArgument("--headless=new");
            }

            options.AddArgument("--start-maximized");
            options.AddArgument("--disable-notifications");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            
            // Sensible page load strategy: Normal ensures full DOM + subresource ready state
            options.PageLoadStrategy = PageLoadStrategy.Normal;

            var service = ChromeDriverService.CreateDefaultService();
            service.SuppressInitialDiagnosticInformation = true;
            service.HideCommandPromptWindow = true;

            IWebDriver driver;
            try
            {
                driver = new ChromeDriver(service, options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize ChromeDriver: {ex.Message}", ex);
            }

            // SDET Best Practice: Set page load and script timeouts; DO NOT set ImplicitWait (rely strictly on explicit WebDriverWait).
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(Math.Max(settings.TimeoutSeconds, 30));
            driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(settings.TimeoutSeconds);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;

            return driver;
        }
    }
}
