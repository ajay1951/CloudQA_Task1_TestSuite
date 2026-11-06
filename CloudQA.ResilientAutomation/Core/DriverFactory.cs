using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using CloudQA.ResilientAutomation.Configuration;

namespace CloudQA.ResilientAutomation.Core
{
    public static class DriverFactory
    {
        public static IWebDriver CreateDriver(TestSettings settings)
        {
            var options = new ChromeOptions
            {
                // Eager strategy returns as soon as DOM is interactive, skipping slow external analytics/trackers
                PageLoadStrategy = PageLoadStrategy.Eager
            };

            if (settings.Headless)
            {
                options.AddArgument("--headless=new");
            }

            options.AddArgument("--start-maximized");
            options.AddArgument("--disable-notifications");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--dns-prefetch-disable");

            var service = ChromeDriverService.CreateDefaultService();
            service.SuppressInitialDiagnosticInformation = true;
            service.HideCommandPromptWindow = true;

            var driver = new ChromeDriver(service, options);

            // Fast implicit wait: immediate element retrieval, low penalty on missing fields
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(300);
            return driver;
        }
    }
}
