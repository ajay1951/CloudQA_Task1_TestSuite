using System;
using System.Text;
using NUnit.Framework;
using OpenQA.Selenium;
using CloudQA.ResilientAutomation.Configuration;
using CloudQA.ResilientAutomation.Core;

namespace CloudQA.ResilientAutomation.Tests
{
    [TestFixture]
    [Category("SafeClick")]
    public class SafeClickTests
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
        [Description("Verifies that SafeClick normally executes a standard Selenium click on an enabled, visible element.")]
        public void SafeClick_NormalElement_ClickSucceeds()
        {
            const string html = @"
                <html><body>
                    <button id='btn' onclick=""this.innerText='Clicked';"">ClickMe</button>
                </body></html>";

            LoadHtml(html);
            var btn = _driver.FindElement(By.Id("btn"));

            _finder.SafeClick(btn);

            Assert.That(btn.Text, Is.EqualTo("Clicked"));
        }

        [Test]
        [Description("Verifies that SafeClick throws ElementNotInteractableException on elements with display:none.")]
        public void SafeClick_HiddenElement_ThrowsElementNotInteractableException()
        {
            const string html = @"
                <html><body>
                    <button id='hidden_btn' style='display:none;' onclick=""this.innerText='Ghost';"">Hidden</button>
                </body></html>";

            LoadHtml(html);
            var btn = _driver.FindElement(By.Id("hidden_btn"));

            Assert.Throws<ElementNotInteractableException>(() => _finder.SafeClick(btn));
        }

        [Test]
        [Description("Verifies that SafeClick rejects disabled elements with InvalidElementStateException.")]
        public void SafeClick_DisabledElement_ThrowsInvalidElementStateException()
        {
            const string html = @"
                <html><body>
                    <button id='dis_btn' disabled onclick=""this.innerText='Illegal';"">Disabled</button>
                </body></html>";

            LoadHtml(html);
            var btn = _driver.FindElement(By.Id("dis_btn"));

            var ex = Assert.Throws<InvalidElementStateException>(() => _finder.SafeClick(btn));
            Assert.That(ex!.Message, Does.Contain("disabled"));
            Assert.That(btn.Text, Is.EqualTo("Disabled"), "Disabled element must not have been modified.");
        }

        [Test]
        [Description("Verifies that when a click is intercepted by a modal/overlay, ElementClickInterceptedException is thrown without silent JS bypass.")]
        public void SafeClick_InterceptedElement_ThrowsElementClickInterceptedException()
        {
            const string html = @"
                <html><body>
                    <button id='target_btn' style='position:absolute; top:20px; left:20px;' onclick=""this.innerText='Bypassed';"">Target</button>
                    <!-- Modal overlay completely covering target_btn -->
                    <div id='overlay' style='position:absolute; top:0; left:0; width:200px; height:100px; background:rgba(0,0,0,0.5); z-index:9999;'>Modal Overlay</div>
                </body></html>";

            LoadHtml(html);
            var targetBtn = _driver.FindElement(By.Id("target_btn"));

            Assert.Throws<ElementClickInterceptedException>(() => _finder.SafeClick(targetBtn, allowJsFallback: false));
            Assert.That(targetBtn.Text, Is.EqualTo("Target"), "Click should have been blocked by overlay.");
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

