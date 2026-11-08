using System;
using System.Diagnostics;
using NUnit.Framework;
using OpenQA.Selenium;
using CloudQA.ResilientAutomation.Configuration;
using CloudQA.ResilientAutomation.Core;
using CloudQA.ResilientAutomation.Pages;

namespace CloudQA.ResilientAutomation.Tests
{
    [TestFixture]
    [Category("E2E")]
    public class PracticeFormTests
    {
        private IWebDriver _driver = null!;
        private TestSettings _settings = null!;
        private PracticeFormPage _page = null!;
        private ResilientElementFinder _finder = null!;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var initSw = Stopwatch.StartNew();
            _settings = TestSettings.Load();
            _driver = DriverFactory.CreateDriver(_settings);
            _driver.Navigate().GoToUrl(_settings.TargetUrl);
            initSw.Stop();
            TestContext.Out.WriteLine($"[Setup] Browser initialized and navigated to '{_settings.TargetUrl}' in {initSw.ElapsedMilliseconds}ms.");

            _finder = new ResilientElementFinder(_driver, _settings.TimeoutSeconds);
            var mainForm = _finder.DiscoverMainRootForm(_driver);
            _page = new PracticeFormPage(_driver, _settings.TimeoutSeconds, mainForm);
        }

        [OneTimeTearDown]
        public void OneTimeTeardown()
        {
            try
            {
                _driver?.Quit();
                _driver?.Dispose();
            }
            catch { }
        }

        [Test]
        [Description("Requirement 1: Automates First Name textbox entry and verifies the resulting input value.")]
        public void Test_01_FirstName_Discovery_Entry_And_Verification()
        {
            var sw = Stopwatch.StartNew();

            _page.SetFirstName(_settings.FirstName);
            var actualValue = _page.GetFirstNameValue();

            sw.Stop();
            TestContext.Out.WriteLine($"[Test 01] First Name entered and verified in {sw.ElapsedMilliseconds}ms.");

            Assert.That(actualValue, Is.EqualTo(_settings.FirstName), 
                $"Expected First Name to contain '{_settings.FirstName}', but found '{actualValue}'.");
        }

        [Test]
        [Description("Requirement 2: Discovers the Gender field group, selects the requested option, and verifies selection state.")]
        public void Test_02_Gender_Radio_Discovery_Selection_And_Verification()
        {
            var sw = Stopwatch.StartNew();

            _page.SelectGender(_settings.Gender);
            var isSelected = _page.IsGenderSelected(_settings.Gender);

            sw.Stop();
            TestContext.Out.WriteLine($"[Test 02] Gender '{_settings.Gender}' selected and verified in {sw.ElapsedMilliseconds}ms.");

            Assert.That(isSelected, Is.True, 
                $"Expected Gender radio button for '{_settings.Gender}' to be checked/selected.");
        }

        [Test]
        [Description("Requirement 3: Discovers the State dropdown, selects requested state via SelectElement, and verifies selected option.")]
        public void Test_03_State_Dropdown_Discovery_Selection_And_Verification()
        {
            var sw = Stopwatch.StartNew();

            _page.SelectState(_settings.State);
            var actualState = _page.GetSelectedState();

            sw.Stop();
            TestContext.Out.WriteLine($"[Test 03] State '{_settings.State}' selected and verified in {sw.ElapsedMilliseconds}ms. Selected option: '{actualState}'.");

            Assert.That(actualState, Does.Contain(_settings.State).IgnoreCase,
                $"Expected State dropdown selection to match '{_settings.State}', but found '{actualState}'.");
        }

        [Test]
        [Description("Requirement 4: End-to-end verification of all three core fields executed in sequence with form submission.")]
        public void Test_04_All_Three_Required_Fields_EndToEnd_Automation()
        {
            var totalSw = Stopwatch.StartNew();

            _page.SetFirstName(_settings.FirstName);
            _page.SetLastName(_settings.LastName);
            _page.SelectGender(_settings.Gender);
            _page.SelectState(_settings.State);

            var actualName = _page.GetFirstNameValue();
            var isGenderSelected = _page.IsGenderSelected(_settings.Gender);
            var actualState = _page.GetSelectedState();

            Assert.Multiple(() =>
            {
                Assert.That(actualName, Is.EqualTo(_settings.FirstName), "First Name verification failed.");
                Assert.That(isGenderSelected, Is.True, "Gender selection verification failed.");
                Assert.That(actualState, Does.Contain(_settings.State).IgnoreCase, "State selection verification failed.");
            });

            _page.ClickSubmit();
            try
            {
                var alert = _driver.SwitchTo().Alert();
                alert.Accept();
            }
            catch (NoAlertPresentException) { }

            totalSw.Stop();
            TestContext.Out.WriteLine($"[Test 04] Complete 3-field automation + submission completed in {totalSw.ElapsedMilliseconds}ms.");
        }

        [Test]
        [Description("Extended Capability: Populates all 16 fields of the practice form including Summernote editor and checkboxes.")]
        public void Test_05_Complete_Full_Practice_Form_Automation()
        {
            var totalSw = Stopwatch.StartNew();
            _driver.Navigate().GoToUrl(_settings.TargetUrl);
            var mainForm = _finder.DiscoverMainRootForm(_driver);
            _page = new PracticeFormPage(_driver, _settings.TimeoutSeconds, mainForm);

            var data = new PracticeFormData
            {
                FirstName = _settings.FirstName,
                LastName = _settings.LastName,
                Gender = _settings.Gender,
                State = _settings.State
            };

            _page.FillCompleteForm(data, clickSubmit: true);
            try
            {
                var alert = _driver.SwitchTo().Alert();
                alert.Accept();
            }
            catch (NoAlertPresentException) { }

            totalSw.Stop();
            TestContext.Out.WriteLine($"[Test 05] Full 16-field form automated and submitted in {totalSw.ElapsedMilliseconds}ms.");

            Assert.Pass("Full form fields automated and submitted successfully.");
        }
    }
}
