using System;
using NUnit.Framework;
using OpenQA.Selenium;
using CloudQA.ResilientAutomation.Configuration;
using CloudQA.ResilientAutomation.Core;

namespace CloudQA.ResilientAutomation.Tests
{
    [TestFixture]
    [Category("BrowserVerification")]
    public class BrowserVerificationTests
    {
        [Test]
        [Description("Verifies Chrome driver initialization, headless mode, execution, and clean disposal.")]
        public void Chrome_HeadlessDriver_InitializesAndDisposesCleanly()
        {
            var settings = new TestSettings
            {
                Browser = "Chrome",
                Headless = true,
                TimeoutSeconds = 5
            };

            IWebDriver? driver = null;
            try
            {
                driver = DriverFactory.CreateDriver(settings);
                Assert.That(driver, Is.Not.Null);

                driver.Navigate().GoToUrl("about:blank");
                Assert.That(driver.Url, Does.Contain("about:blank"));
            }
            finally
            {
                if (driver != null)
                {
                    driver.Quit();
                    driver.Dispose();
                }
            }
        }

        [Test]
        [Description("Verifies that requesting an unsupported browser throws a descriptive InvalidOperationException.")]
        public void DriverFactory_UnsupportedBrowser_ThrowsDescriptiveInvalidOperationException()
        {
            var settings = new TestSettings
            {
                Browser = "Safari",
                Headless = true
            };

            var ex = Assert.Throws<InvalidOperationException>(() => DriverFactory.CreateDriver(settings));
            Assert.That(ex!.Message, Does.Contain("Unsupported browser 'Safari'"));
            Assert.That(ex.Message, Does.Contain("Chrome"));
            Assert.That(ex.Message, Does.Contain("Firefox"));
            Assert.That(ex.Message, Does.Contain("Edge"));
        }

        [Test]
        [Description("Verifies that invalid timeout values are rejected during validation.")]
        public void TestSettings_InvalidTimeout_ThrowsInvalidOperationException()
        {
            var invalidLow = new TestSettings { TimeoutSeconds = 0 };
            Assert.Throws<InvalidOperationException>(() => invalidLow.Validate());

            var invalidHigh = new TestSettings { TimeoutSeconds = 500 };
            Assert.Throws<InvalidOperationException>(() => invalidHigh.Validate());
        }
    }
}
