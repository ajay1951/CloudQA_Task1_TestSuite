using System;
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

            // Override TargetUrl if provided as CLI argument: dotnet run -- "https://modified-url"
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) && args[0].StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                settings.TargetUrl = args[0].Trim();
            }

            Console.WriteLine($"[Config] Target URL: {settings.TargetUrl}");
            Console.WriteLine($"[Config] First Name: {settings.FirstName}");
            Console.WriteLine($"[Config] Last Name:  {settings.LastName}");
            Console.WriteLine($"[Config] Gender:     {settings.Gender}");
            Console.WriteLine($"[Config] State:      {settings.State}");
            Console.WriteLine($"[Config] Headless:   {settings.Headless}");
            Console.WriteLine("------------------------------------------------------------------");

            try
            {
                using var driver = DriverFactory.CreateDriver(settings);
                Console.WriteLine($"[1/4] Navigating to target URL: {settings.TargetUrl}");
                driver.Navigate().GoToUrl(settings.TargetUrl);

                var finder = new ResilientElementFinder(driver, settings.TimeoutSeconds);
                var root = finder.DiscoverMainRootForm(driver);
                var page = new PracticeFormPage(driver, settings.TimeoutSeconds, root);

                Console.WriteLine("\n[2/4] Automating fields using resilient discovery engine...");
                page.SetFirstName(settings.FirstName);
                Console.WriteLine($"  ✓ Entered First Name: '{settings.FirstName}'");

                page.SetLastName(settings.LastName);
                Console.WriteLine($"  ✓ Entered Last Name:  '{settings.LastName}'");

                page.SelectGender(settings.Gender);
                Console.WriteLine($"  ✓ Selected Gender:    '{settings.Gender}'");

                page.SelectState(settings.State);
                Console.WriteLine($"  ✓ Selected State:     '{settings.State}'");

                Console.WriteLine("\n[3/4] Verifying all operations...");
                var actualName = page.GetFirstNameValue();
                var isGenderSelected = page.IsGenderSelected(settings.Gender);
                var actualState = page.GetSelectedState();

                bool nameVerified = actualName == settings.FirstName;
                bool genderVerified = isGenderSelected;
                bool stateVerified = actualState.Contains(settings.State, StringComparison.OrdinalIgnoreCase);

                Console.WriteLine($"  • First Name Verification: '{actualName}' -> {(nameVerified ? "PASS" : "FAIL")}");
                Console.WriteLine($"  • Gender Selection State:  '{settings.Gender}' -> {(genderVerified ? "PASS" : "FAIL")}");
                Console.WriteLine($"  • State Selection State:   '{actualState}' -> {(stateVerified ? "PASS" : "FAIL")}");

                if (!nameVerified || !genderVerified || !stateVerified)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n❌ VERIFICATION FAILURE: One or more fields failed verification.");
                    Console.ResetColor();
                    return 1;
                }

                Console.WriteLine("\n[4/4] Submitting form...");
                page.ClickSubmit();
                Console.WriteLine("  ✓ Form submitted successfully.");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n==================================================================");
                Console.WriteLine(" ✅ SUCCESS: Task 1 automation and verification passed 100%!");
                Console.WriteLine("==================================================================");
                Console.ResetColor();
                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n❌ ERROR: {ex.Message}");
                Console.ResetColor();
                return 1;
            }
        }
    }
}

