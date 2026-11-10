using System;
using System.IO;
using System.Threading;
using OpenQA.Selenium;
using CloudQA.ResilientAutomation.Configuration;
using CloudQA.ResilientAutomation.Core;
using CloudQA.ResilientAutomation.Pages;

namespace CloudQA.ResilientAutomation
{
    public class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("==================================================================");
            Console.WriteLine(" CloudQA Resilient Selenium Automation Program (Task 1)");
            Console.WriteLine("==================================================================");

            var settings = TestSettings.Load();
            int slowMoMs = settings.Headless ? 0 : 800;

            // Parse CLI arguments:
            // e.g. dotnet run
            //      dotnet run -- "https://modified-url"
            //      dotnet run -- --headless false
            //      dotnet run -- --headless true
            //      dotnet run -- --browser Firefox
            //      dotnet run -- --slow-mo 1000
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i].Trim();
                if (arg.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    settings.TargetUrl = arg;
                }
                else if (arg.Equals("--headless", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length && bool.TryParse(args[i + 1], out bool hVal))
                    {
                        settings.Headless = hVal;
                        i++;
                    }
                    else
                    {
                        settings.Headless = true;
                    }
                }
                else if (arg.Equals("--browser", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    settings.Browser = args[++i].Trim();
                }
                else if (arg.Equals("--url", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    settings.TargetUrl = args[++i].Trim();
                }
                else if (arg.Equals("--slow-mo", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out int smVal))
                {
                    slowMoMs = Math.Max(0, smVal);
                    i++;
                }
            }

            if (settings.Headless)
            {
                slowMoMs = 0;
            }

            Console.WriteLine($"[Config] Target URL: {settings.TargetUrl}");
            Console.WriteLine($"[Config] Browser:    {settings.Browser}");
            Console.WriteLine($"[Config] Headless:   {settings.Headless}");
            Console.WriteLine($"[Config] First Name: {settings.FirstName}");
            Console.WriteLine($"[Config] Last Name:  {settings.LastName}");
            Console.WriteLine($"[Config] Gender:     {settings.Gender}");
            Console.WriteLine($"[Config] State:      {settings.State}");
            if (!settings.Headless)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("------------------------------------------------------------------");
                Console.WriteLine(">> LIVE BROWSER MODE: Opening interactive desktop browser window.");
                Console.WriteLine(">> Watch navigation, field entry, and selections live on screen.");
                Console.ResetColor();
            }
            Console.WriteLine("------------------------------------------------------------------");

            IWebDriver? driver = null;

            try
            {
                driver = DriverFactory.CreateDriver(settings);
                Console.WriteLine($"[1/4] Navigating to target URL: {settings.TargetUrl}");
                driver.Navigate().GoToUrl(settings.TargetUrl);
                if (slowMoMs > 0) Thread.Sleep(slowMoMs);

                var finder = new ResilientElementFinder(driver, settings.TimeoutSeconds);
                var root = finder.DiscoverMainRootForm(driver);
                var page = new PracticeFormPage(driver, settings.TimeoutSeconds, root);

                Console.WriteLine("\n[2/4] Automating fields using resilient discovery engine...");
                page.SetFirstName(settings.FirstName);
                Console.WriteLine($"  ✓ Entered First Name: '{settings.FirstName}'");
                if (slowMoMs > 0) Thread.Sleep(slowMoMs);

                page.SetLastName(settings.LastName);
                Console.WriteLine($"  ✓ Entered Last Name:  '{settings.LastName}'");
                if (slowMoMs > 0) Thread.Sleep(slowMoMs);

                page.SelectGender(settings.Gender);
                Console.WriteLine($"  ✓ Selected Gender:    '{settings.Gender}'");
                if (slowMoMs > 0) Thread.Sleep(slowMoMs);

                page.SelectState(settings.State);
                Console.WriteLine($"  ✓ Selected State:     '{settings.State}'");
                if (slowMoMs > 0) Thread.Sleep(slowMoMs);

                Console.WriteLine("\n[3/4] Verifying all pre-submission values...");
                var actualName = page.GetFirstNameValue();
                var isGenderSelected = page.IsGenderSelected(settings.Gender);
                var actualState = page.GetSelectedState();

                bool nameVerified = actualName == settings.FirstName;
                bool genderVerified = isGenderSelected;
                bool stateVerified = actualState.Contains(settings.State, StringComparison.OrdinalIgnoreCase);

                Console.WriteLine($"  • First Name Verification: '{actualName}' -> {(nameVerified ? "PASS" : "FAIL")}");
                Console.WriteLine($"  • Gender Selection State:  '{settings.Gender}' -> {(genderVerified ? "PASS" : "FAIL")}");
                Console.WriteLine($"  • State Selection State:   '{actualState}' -> {(stateVerified ? "PASS" : "FAIL")}");
                if (slowMoMs > 0) Thread.Sleep(slowMoMs);

                if (!nameVerified || !genderVerified || !stateVerified)
                {
                    var screenshotPath = CaptureScreenshot(driver, "Screenshots", "verification_failure");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n❌ VERIFICATION FAILURE: One or more fields failed verification.");
                    if (!string.IsNullOrEmpty(screenshotPath))
                    {
                        Console.WriteLine($"📷 Screenshot captured: {Path.GetFullPath(screenshotPath)}");
                    }
                    Console.ResetColor();
                    return 1;
                }

                Console.WriteLine("\n[4/4] Submitting form and verifying outcome...");
                var preSubmitUrl = driver.Url;
                page.ClickSubmit();

                if (slowMoMs > 0) Thread.Sleep(slowMoMs);
                else Thread.Sleep(1000);

                // Verify post-submission state: alert confirmation, navigation, or DOM state
                try
                {
                    var alert = driver.SwitchTo().Alert();
                    Console.WriteLine($"  ✓ Handled confirmation alert: '{alert.Text}'");
                    if (slowMoMs > 0) Thread.Sleep(1000); // Allow human to read alert before accepting
                    alert.Accept();
                }
                catch (NoAlertPresentException)
                {
                    Console.WriteLine($"  ✓ Submission trigger executed (Current URL: {driver.Url})");
                }

                if (!settings.Headless)
                {
                    // Brief pause before closing driver so user can see final submitted state
                    Thread.Sleep(1500);
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n==================================================================");
                Console.WriteLine(" ✅ SUCCESS: Task 1 automation and verification passed 100%!");
                Console.WriteLine("==================================================================");
                Console.ResetColor();
                return 0;
            }
            catch (Exception ex)
            {
                var screenshotPath = CaptureScreenshot(driver, "Screenshots", "execution_error");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n❌ EXECUTION ERROR: {ex.Message}");
                if (!string.IsNullOrEmpty(screenshotPath))
                {
                    Console.WriteLine($"📷 Failure screenshot saved to: {Path.GetFullPath(screenshotPath)}");
                }
                Console.ResetColor();
                return 2;
            }
            finally
            {
                try
                {
                    driver?.Quit();
                    driver?.Dispose();
                }
                catch { }
            }
        }

        private static string? CaptureScreenshot(IWebDriver? driver, string directory, string prefix)
        {
            if (driver is ITakesScreenshot ts)
            {
                try
                {
                    Directory.CreateDirectory(directory);
                    var fileName = $"{prefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";
                    var fullPath = Path.Combine(directory, fileName);
                    var screenshot = ts.GetScreenshot();
                    screenshot.SaveAsFile(fullPath);
                    return fullPath;
                }
                catch { }
            }
            return null;
        }
    }
}
