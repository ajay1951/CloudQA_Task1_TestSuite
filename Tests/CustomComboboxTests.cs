using System;
using System.Text;
using NUnit.Framework;
using OpenQA.Selenium;
using CloudQA.ResilientAutomation.Configuration;
using CloudQA.ResilientAutomation.Core;
using CloudQA.ResilientAutomation.Pages;

namespace CloudQA.ResilientAutomation.Tests
{
    [TestFixture]
    [Category("CustomCombobox")]
    public class CustomComboboxTests
    {
        private IWebDriver _driver = null!;

        [SetUp]
        public void Setup()
        {
            var settings = new TestSettings { Headless = true, TimeoutSeconds = 5 };
            _driver = DriverFactory.CreateDriver(settings);
        }

        [Test]
        [Description("Verifies successful selection and post-selection verification on a custom ARIA combobox.")]
        public void CustomCombobox_ValidOption_SelectsAndVerifiesSuccessfully()
        {
            const string html = @"
                <html><body>
                    <form>
                        <label id='lbl-state'>State</label>
                        <div role='combobox' id='state-cb' aria-labelledby='lbl-state' aria-controls='listbox-state' aria-expanded='false'
                             onclick=""document.getElementById('listbox-state').style.display='block'; this.setAttribute('aria-expanded','true');"">
                            Choose State
                        </div>
                        <ul role='listbox' id='listbox-state' style='display:none;'>
                            <li role='option' id='opt-tx' onclick=""document.getElementById('state-cb').innerText='Texas'; this.setAttribute('aria-selected','true');"">Texas</li>
                            <li role='option' id='opt-in' onclick=""document.getElementById('state-cb').innerText='India'; this.setAttribute('aria-selected','true');"">India</li>
                        </ul>
                    </form>
                </body></html>";

            LoadHtml(html);
            var page = new PracticeFormPage(_driver, 5);

            page.SelectState("India");

            Assert.That(page.GetSelectedState(), Is.EqualTo("India"));
        }

        [Test]
        [Description("Verifies that requesting a missing option throws NoSuchElementException listing all available options.")]
        public void CustomCombobox_MissingOption_ThrowsNoSuchElementException()
        {
            const string html = @"
                <html><body>
                    <form>
                        <label id='lbl-state'>State</label>
                        <div role='combobox' id='state-cb' aria-labelledby='lbl-state' aria-controls='listbox-state' aria-expanded='false'
                             onclick=""document.getElementById('listbox-state').style.display='block'; this.setAttribute('aria-expanded','true');"">
                            Choose State
                        </div>
                        <ul role='listbox' id='listbox-state' style='display:none;'>
                            <li role='option' id='opt-tx'>Texas</li>
                            <li role='option' id='opt-ca'>California</li>
                        </ul>
                    </form>
                </body></html>";

            LoadHtml(html);
            var page = new PracticeFormPage(_driver, 5);

            var ex = Assert.Throws<NoSuchElementException>(() => page.SelectState("India"));
            Assert.Multiple(() =>
            {
                Assert.That(ex!.Message, Does.Contain("India"));
                Assert.That(ex.Message, Does.Contain("Texas"));
                Assert.That(ex.Message, Does.Contain("California"));
            });
        }

        [Test]
        [Description("Verifies that if the custom dropdown accepts a click but fails to update its DOM state, an InvalidOperationException is thrown.")]
        public void CustomCombobox_UnreflectedSelection_ThrowsInvalidOperationException()
        {
            const string html = @"
                <html><body>
                    <form>
                        <label id='lbl-state'>State</label>
                        <div role='combobox' id='state-cb' aria-labelledby='lbl-state' aria-controls='listbox-state' aria-expanded='false'
                             onclick=""document.getElementById('listbox-state').style.display='block'; this.setAttribute('aria-expanded','true');"">
                            Choose State
                        </div>
                        <!-- Broken dropdown that does not update combobox text or aria-selected on click -->
                        <ul role='listbox' id='listbox-state' style='display:none;'>
                            <li role='option' id='opt-in' onclick=""console.log('clicked but nothing updated');"">India</li>
                        </ul>
                    </form>
                </body></html>";

            LoadHtml(html);
            var page = new PracticeFormPage(_driver, 2);

            var ex = Assert.Throws<InvalidOperationException>(() => page.SelectState("India"));
            Assert.That(ex!.Message, Does.Contain("Post-selection verification failed"));
        }

        [Test]
        [Description("Verifies that disabled ARIA options (aria-disabled='true') are not selectable.")]
        public void CustomCombobox_DisabledOption_IsIgnored()
        {
            const string html = @"
                <html><body>
                    <form>
                        <label id='lbl-state'>State</label>
                        <div role='combobox' id='state-cb' aria-labelledby='lbl-state' aria-controls='listbox-state' aria-expanded='false'
                             onclick=""document.getElementById('listbox-state').style.display='block'; this.setAttribute('aria-expanded','true');"">
                            Choose State
                        </div>
                        <ul role='listbox' id='listbox-state' style='display:none;'>
                            <li role='option' id='opt-in' aria-disabled='true'>India</li>
                            <li role='option' id='opt-us'>United States</li>
                        </ul>
                    </form>
                </body></html>";

            LoadHtml(html);
            var page = new PracticeFormPage(_driver, 2);

            // India is disabled, so searching for India should fail with NoSuchElementException
            var ex = Assert.Throws<NoSuchElementException>(() => page.SelectState("India"));
            Assert.That(ex!.Message, Does.Contain("Option 'India' was not found"));
        }

        [Test]
        [Description("Verifies that duplicate matching options in a custom combobox throw AmbiguousMatchException.")]
        public void CustomCombobox_DuplicateMatchingOptions_ThrowsAmbiguousMatchException()
        {
            const string html = @"
                <html><body>
                    <form>
                        <label id='lbl-state'>State</label>
                        <div role='combobox' id='state-cb' aria-labelledby='lbl-state' aria-controls='listbox-state' aria-expanded='false'
                             onclick=""document.getElementById('listbox-state').style.display='block'; this.setAttribute('aria-expanded','true');"">
                            Choose State
                        </div>
                        <!-- Two identical options for India -->
                        <ul role='listbox' id='listbox-state' style='display:none;'>
                            <li role='option' id='opt-in-1'>India</li>
                            <li role='option' id='opt-in-2'>India</li>
                        </ul>
                    </form>
                </body></html>";

            LoadHtml(html);
            var page = new PracticeFormPage(_driver, 2);

            Assert.Throws<AmbiguousMatchException>(() => page.SelectState("India"));
        }

        private void LoadHtml(string html)
        {
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(html));
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

