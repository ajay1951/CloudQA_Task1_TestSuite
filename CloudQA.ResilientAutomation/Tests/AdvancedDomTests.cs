using System;
using System.Diagnostics;
using NUnit.Framework;
using OpenQA.Selenium;
using CloudQA.ResilientAutomation.Configuration;
using CloudQA.ResilientAutomation.Core;
using CloudQA.ResilientAutomation.Pages;

namespace CloudQA.ResilientAutomation.Tests
{
    /// <summary>
    /// Isolated advanced DOM tests specifically testing Shadow DOM and iFrame handling
    /// without entangling or slowing down the primary form validation tests.
    /// </summary>
    [TestFixture]
    [Category("AdvancedDOM")]
    public class AdvancedDomTests
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
        [Description("Discovers and automates open Shadow DOM forms using recursive root discovery.")]
        public void Should_Discover_And_Automate_Open_Shadow_DOM_Forms()
        {
            var sw = Stopwatch.StartNew();
            var shadowHosts = _finder.DiscoverAllShadowFormHosts();
            TestContext.Out.WriteLine($"[Shadow DOM Test] Discovered {shadowHosts.Count} Shadow DOM host(s).");

            int processed = 0;
            for (int s = 0; s < shadowHosts.Count; s++)
            {
                var host = shadowHosts[s];
                var page = new PracticeFormPage(_driver, _settings.TimeoutSeconds, host);

                try
                {
                    page.SetFirstName("ShadowUser");
                    page.SelectGender("Male");
                    page.SelectState("India");

                    Assert.That(page.GetFirstNameValue(), Is.EqualTo("ShadowUser"));
                    Assert.That(page.IsGenderSelected("Male"), Is.True);
                    processed++;
                }
                catch (Exception ex)
                {
                    TestContext.Out.WriteLine($"[Shadow DOM #{s + 1}] Handled context: {ex.Message}");
                }
            }

            sw.Stop();
            TestContext.Out.WriteLine($"[Shadow DOM Test] Completed {processed} shadow forms in {sw.ElapsedMilliseconds}ms.");
            Assert.That(processed, Is.GreaterThan(0), "At least one Shadow DOM form should be successfully automated.");
        }

        [Test]
        [Description("Discovers and explicitly switches into iFrames, executes automation, and safely restores default content.")]
        public void Should_Explicitly_Switch_Automate_And_Restore_iFrames()
        {
            var sw = Stopwatch.StartNew();
            var topIframes = _driver.FindElements(By.TagName("iframe"));
            TestContext.Out.WriteLine($"[iFrame Test] Discovered {topIframes.Count} top-level iframe(s).");

            int processed = 0;
            for (int i = 0; i < topIframes.Count; i++)
            {
                var frames = _driver.FindElements(By.TagName("iframe"));
                if (i >= frames.Count) break;

                var frame = frames[i];
                try
                {
                    _driver.SwitchTo().Frame(frame);
                    var page = new PracticeFormPage(_driver, _settings.TimeoutSeconds);

                    page.SetFirstName("FrameUser");
                    page.SelectGender("Male");
                    page.SelectState("India");

                    Assert.That(page.GetFirstNameValue(), Is.EqualTo("FrameUser"));
                    processed++;
                }
                catch (Exception ex)
                {
                    TestContext.Out.WriteLine($"[iFrame #{i}] Context interaction handled: {ex.Message}");
                }
                finally
                {
                    _driver.SwitchTo().DefaultContent();
                }
            }

            sw.Stop();
            TestContext.Out.WriteLine($"[iFrame Test] Completed {processed} iFrames in {sw.ElapsedMilliseconds}ms.");
            Assert.That(processed, Is.GreaterThan(0), "At least one iFrame form should be successfully automated.");
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

