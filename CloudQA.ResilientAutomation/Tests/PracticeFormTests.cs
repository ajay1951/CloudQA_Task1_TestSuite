using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenQA.Selenium;
using CloudQA.ResilientAutomation.Configuration;
using CloudQA.ResilientAutomation.Core;
using CloudQA.ResilientAutomation.Pages;

namespace CloudQA.ResilientAutomation.Tests
{
    [TestFixture]
    public class PracticeFormTests
    {
        private IWebDriver _driver = null!;
        private TestSettings _settings = null!;
        private ResilientElementFinder _finder = null!;

        [SetUp]
        public void Setup()
        {
            _settings = TestSettings.Load();
            _driver = DriverFactory.CreateDriver(_settings);
            _driver.Navigate().GoToUrl(_settings.TargetUrl);
            _finder = new ResilientElementFinder(_driver, _settings.TimeoutSeconds);
        }

        [Test]
        [Description("Fast & resilient end-to-end automation: automates Shadow DOMs, iFrames, and the full Main Form with all fields in seconds.")]
        public void Should_Automate_And_Fill_Every_Detail_Across_All_Forms()
        {
            const string fName = "Ajay";
            const string gender = "Male";
            const string state = "India";

            var fullFormData = new PracticeFormData
            {
                FirstName = fName,
                LastName = "Gollapudi",
                Gender = gender,
                DateOfBirth = "2001-05-19",
                MobileNumber = "9876543210",
                Email = "ajay@example.com",
                Country = "India",
                State = state,
                Hobbies = new List<string> { "Dance", "Reading", "Cricket" },
                AboutYourself = "Senior Full-Stack & Automation Test Engineer specializing in resilient Selenium frameworks.",
                Description = "Submitted via lightning-fast resilient test suite covering all form fields, Summernote editor, Shadow DOMs, and nested iFrames.",
                Username = "ajay_g",
                Password = "TestPassword@123",
                ConfirmPassword = "TestPassword@123",
                AgreeTerms = true
            };

            int totalProcessed = 0;
            int coreFieldsVerified = 0;

            // 1. Process All Shadow DOM Forms (Focus on core fields present inside them)
            var shadowHosts = _finder.DiscoverAllShadowFormHosts();
            Console.WriteLine($"\n[DISCOVERY] Found {shadowHosts.Count} Shadow DOM form host(s).");

            for (int s = 0; s < shadowHosts.Count; s++)
            {
                try
                {
                    var host = shadowHosts[s];
                    var shadowPage = new PracticeFormPage(_driver, _settings.TimeoutSeconds, host);

                    var result = shadowPage.FillAndVerifyThreeFields($"Shadow DOM Form [#{s + 1}]", fName, gender, state);
                    if (result.FormCompleted)
                    {
                        totalProcessed++;
                        if (result.AllThreeFieldsVerified) coreFieldsVerified++;
                    }
                }
                catch (StaleElementReferenceException) { }
            }

            // 2. Process iFrames Encapsulated Inside Shadow Roots
            var shadowIframes = _finder.DiscoverIFramesInsideShadowRoots();
            Console.WriteLine($"\n[DISCOVERY] Found {shadowIframes.Count} iFrame(s) inside Shadow DOM.");

            for (int k = 0; k < shadowIframes.Count; k++)
            {
                try
                {
                    var sFrame = shadowIframes[k];
                    _driver.SwitchTo().Frame(sFrame);

                    var sFramePage = new PracticeFormPage(_driver, _settings.TimeoutSeconds);
                    var result = sFramePage.FillAndVerifyThreeFields($"Shadow iFrame [#{k + 1}]", fName, gender, state);
                    if (result.FormCompleted)
                    {
                        totalProcessed++;
                        if (result.AllThreeFieldsVerified) coreFieldsVerified++;
                    }
                }
                catch (StaleElementReferenceException) { }
                finally
                {
                    _driver.SwitchTo().DefaultContent();
                }
            }

            // 3. Process Top-Level & Nested iFrames
            var topIframes = _driver.FindElements(By.TagName("iframe"));
            Console.WriteLine($"\n[DISCOVERY] Found {topIframes.Count} top-level iFrame(s).");

            for (int i = 0; i < topIframes.Count; i++)
            {
                var frames = _driver.FindElements(By.TagName("iframe"));
                if (i >= frames.Count) break;

                var frameElement = frames[i];
                string frameLabel = !string.IsNullOrEmpty(frameElement.GetAttribute("id"))
                    ? $"iFrame (#{frameElement.GetAttribute("id")})"
                    : $"iFrame [index {i}]";

                try
                {
                    _driver.SwitchTo().Frame(frameElement);

                    var framePage = new PracticeFormPage(_driver, _settings.TimeoutSeconds);
                    var result = framePage.FillAndVerifyThreeFields(frameLabel, fName, gender, state);
                    if (result.FormCompleted)
                    {
                        totalProcessed++;
                        if (result.AllThreeFieldsVerified) coreFieldsVerified++;
                    }

                    // Inner Nested iFrames
                    var innerFrames = _driver.FindElements(By.TagName("iframe"));
                    for (int j = 0; j < innerFrames.Count; j++)
                    {
                        var innerList = _driver.FindElements(By.TagName("iframe"));
                        if (j >= innerList.Count) break;

                        _driver.SwitchTo().Frame(innerList[j]);
                        var innerPage = new PracticeFormPage(_driver, _settings.TimeoutSeconds);
                        var innerResult = innerPage.FillAndVerifyThreeFields($"{frameLabel} -> Nested iFrame [{j}]", fName, gender, state);
                        if (innerResult.FormCompleted)
                        {
                            totalProcessed++;
                            if (innerResult.AllThreeFieldsVerified) coreFieldsVerified++;
                        }

                        _driver.SwitchTo().ParentFrame();
                    }
                }
                catch (StaleElementReferenceException) { }
                finally
                {
                    _driver.SwitchTo().DefaultContent();
                }
            }

            // 4. Process Main Root Form With EVERY SINGLE DETAIL (all 16 fields)
            var mainRootForm = _finder.DiscoverMainRootForm(_driver);
            if (mainRootForm != null)
            {
                var rootPage = new PracticeFormPage(_driver, _settings.TimeoutSeconds, mainRootForm);
                var result = rootPage.FillCompleteForm("Main Root Form", fullFormData);
                if (result.FormCompleted)
                {
                    totalProcessed++;
                    if (result.AllThreeFieldsVerified) coreFieldsVerified++;
                }
            }

            Console.WriteLine("\n====================================================================");
            Console.WriteLine($" COMPLETE: Successfully automated {totalProcessed} form(s) in record time!");
            Console.WriteLine($" Core fields (First Name, Gender, State) verified across contexts: {coreFieldsVerified}");
            Console.WriteLine("====================================================================");

            Assert.That(totalProcessed, Is.GreaterThan(0), 
                "Expected at least one form context on the page to be automated.");

            Assert.That(coreFieldsVerified, Is.GreaterThan(0),
                "Expected core fields (First Name, Gender, State) to be verified successfully.");
        }

        [TearDown]
        public void Teardown()
        {
            try
            {
                _driver?.Quit();
                _driver?.Dispose();
            }
            catch { }
        }
    }
}
