using System;
using System.IO;
using NUnit.Framework;
using OpenQA.Selenium;
using CloudQA.ResilientAutomation.Configuration;
using CloudQA.ResilientAutomation.Core;
using CloudQA.ResilientAutomation.Pages;

namespace CloudQA.ResilientAutomation.Tests
{
    /// <summary>
    /// Controlled test fixtures testing resilience against synthetic DOM mutations:
    /// - Reordered field layout
    /// - Renamed IDs, names, and CSS classes
    /// - Ambiguous element matches
    /// - Missing field detection
    /// Proves resilience with reproducible, measurable assertions.
    /// </summary>
    [TestFixture]
    [Category("Resilience")]
    public class ResilienceControlledTests
    {
        private IWebDriver _driver = null!;
        private ResilientElementFinder _finder = null!;

        [SetUp]
        public void Setup()
        {
            var settings = new TestSettings { Headless = true, TimeoutSeconds = 5 };
            _driver = DriverFactory.CreateDriver(settings);
            _finder = new ResilientElementFinder(_driver, 5);
        }

        [Test]
        [Description("Proves resilience when fields are reordered: State dropdown appears first, followed by Gender, then First Name.")]
        public void Should_Find_Fields_When_Order_Is_Reversed()
        {
            const string html = @"
                <html>
                <body>
                    <form id='mutated-form'>
                        <div class='field-container'>
                            <label for='state-select'>Select State</label>
                            <select id='state-select'>
                                <option value=''>-- Select --</option>
                                <option value='TX'>Texas</option>
                                <option value='CA'>California</option>
                            </select>
                        </div>
                        <div class='field-container'>
                            <span>Gender</span>
                            <label><input type='radio' name='rad_gender' value='Male'> Male</label>
                            <label><input type='radio' name='rad_gender' value='Female'> Female</label>
                        </div>
                        <div class='field-container'>
                            <label for='user-given-name'>First Name</label>
                            <input id='user-given-name' type='text' />
                        </div>
                    </form>
                </body>
                </html>";

            LoadHtml(html);

            var page = new PracticeFormPage(_driver, 5);
            page.SetFirstName("TestUser");
            page.SelectGender("Female");
            page.SelectState("California");

            Assert.Multiple(() =>
            {
                Assert.That(page.GetFirstNameValue(), Is.EqualTo("TestUser"));
                Assert.That(page.IsGenderSelected("Female"), Is.True);
                Assert.That(page.GetSelectedState(), Does.Contain("California"));
            });
        }

        [Test]
        [Description("Proves resilience when element IDs, names, and classes are completely randomized/changed.")]
        public void Should_Find_Fields_When_Ids_Names_And_Classes_Are_Altered()
        {
            const string html = @"
                <html>
                <body>
                    <div id='custom-wrapper-992'>
                        <!-- No standard IDs or classes -->
                        <div class='col-random-xyz'>
                            <label>Given Name
                                <input type='text' name='random_attr_872' placeholder='Enter given name' />
                            </label>
                        </div>
                        <div class='col-random-abc'>
                            <h4>Your Gender</h4>
                            <input type='radio' name='radio_group_43' value='M'> <span class='lbl'>Male</span>
                            <input type='radio' name='radio_group_43' value='F'> <span class='lbl'>Female</span>
                        </div>
                        <div class='col-random-def'>
                            <label>Province / Region</label>
                            <select name='sel_reg_11'>
                                <option value='1'>Ontario</option>
                                <option value='2'>Quebec</option>
                            </select>
                        </div>
                    </div>
                </body>
                </html>";

            LoadHtml(html);

            var page = new PracticeFormPage(_driver, 5);
            page.SetFirstName("RandomizedName");
            page.SelectGender("Male");
            page.SelectState("Ontario");

            Assert.Multiple(() =>
            {
                Assert.That(page.GetFirstNameValue(), Is.EqualTo("RandomizedName"));
                Assert.That(page.IsGenderSelected("Male"), Is.True);
                Assert.That(page.GetSelectedState(), Does.Contain("Ontario"));
            });
        }

        [Test]
        [Description("Verifies that the scoring engine rejects ambiguous matches when two elements tie with identical confidence.")]
        public void Should_Reject_Ambiguous_Candidates_When_Tied()
        {
            const string html = @"
                <html>
                <body>
                    <form>
                        <!-- Two identical unlabeled textboxes with equal low confidence -->
                        <input type='text' class='text-input' />
                        <input type='text' class='text-input' />
                    </form>
                </body>
                </html>";

            LoadHtml(html);

            // Searching for 'First Name' when two generic inputs exist without label/placeholder evidence must reject ambiguity
            var match = _finder.FindTextBox(_driver, "First Name");
            Assert.That(match, Is.Null, "Finder should return null when candidates are tied without sufficient confidence margin.");
        }

        [Test]
        [Description("Verifies that missing fields are reported gracefully rather than throwing uncaught driver crashes.")]
        public void Should_Return_Null_When_Field_Is_Missing()
        {
            const string html = @"
                <html>
                <body>
                    <form>
                        <label for='phone'>Phone Number</label>
                        <input id='phone' type='tel' />
                    </form>
                </body>
                </html>";

            LoadHtml(html);

            var match = _finder.FindTextBox(_driver, "First Name");
            Assert.That(match, Is.Null, "Expected null when requested semantic label is absent from DOM.");

            var radio = _finder.FindRadioButton(_driver, "Male");
            Assert.That(radio, Is.Null, "Expected null when radio buttons are absent from DOM.");

            var dropdown = _finder.FindDropdown(_driver, "State");
            Assert.That(dropdown, Is.Null, "Expected null when dropdown is absent from DOM.");
        }

        private void LoadHtml(string html)
        {
            var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(html));
            _driver.Navigate().GoToUrl($"data:text/html;base64,{base64}");
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

