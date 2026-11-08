using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenQA.Selenium;
using CloudQA.ResilientAutomation.Core;
using CloudQA.ResilientAutomation.Pages;

namespace CloudQA.ResilientAutomation.Tests
{
    [TestFixture]
    [Category("StaleRecovery")]
    public class StaleElementRecoveryTests
    {
        [Test]
        [Description("Verifies that when attempt 1 throws StaleElementReferenceException, attempt 2 succeeds.")]
        public void Retry_StaleOnFirstAttempt_SucceedsOnSecond()
        {
            int callCount = 0;
            var logs = new List<string>();

            string result = PracticeFormPage.ExecuteWithStaleRecovery("TestStale1", () =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new StaleElementReferenceException("DOM element was detached.");
                }
                return "SuccessValue";
            }, maxRetries: 3, logger: logs.Add);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("SuccessValue"));
                Assert.That(callCount, Is.EqualTo(2), "Expected exactly 2 attempts.");
                Assert.That(logs, Has.Count.EqualTo(1));
                Assert.That(logs[0], Does.Contain("Attempt 1/3"));
            });
        }

        [Test]
        [Description("Verifies that when attempts 1 and 2 throw StaleElementReferenceException, attempt 3 succeeds.")]
        public void Retry_StaleOnFirstTwoAttempts_SucceedsOnThird()
        {
            int callCount = 0;
            var logs = new List<string>();

            int result = PracticeFormPage.ExecuteWithStaleRecovery("TestStale2", () =>
            {
                callCount++;
                if (callCount <= 2)
                {
                    throw new StaleElementReferenceException($"DOM element detached on attempt {callCount}.");
                }
                return 42;
            }, maxRetries: 3, logger: logs.Add);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(42));
                Assert.That(callCount, Is.EqualTo(3), "Expected exactly 3 attempts.");
                Assert.That(logs, Has.Count.EqualTo(2));
            });
        }

        [Test]
        [Description("Verifies that when all 3 attempts throw StaleElementReferenceException, the original exception is preserved.")]
        public void Retry_StaleOnAllThreeAttempts_PreservesOriginalException()
        {
            int callCount = 0;
            var logs = new List<string>();

            var ex = Assert.Throws<StaleElementReferenceException>(() =>
            {
                PracticeFormPage.ExecuteWithStaleRecovery("TestStale3", () =>
                {
                    callCount++;
                    throw new StaleElementReferenceException($"Detached persistently on attempt {callCount}.");
                }, maxRetries: 3, logger: logs.Add);
            });

            Assert.Multiple(() =>
            {
                Assert.That(callCount, Is.EqualTo(3), "Max total attempts must be exactly 3.");
                Assert.That(ex!.Message, Does.Contain("attempt 3"));
                Assert.That(logs, Has.Count.EqualTo(3));
            });
        }

        [Test]
        [Description("Verifies that AmbiguousMatchException is never retried and propagates immediately on attempt 1.")]
        public void Retry_AmbiguousMatchException_ImmediatelyPropagatesWithoutRetry()
        {
            int callCount = 0;

            Assert.Throws<AmbiguousMatchException>(() =>
            {
                PracticeFormPage.ExecuteWithStaleRecovery("TestAmbiguity", () =>
                {
                    callCount++;
                    throw new AmbiguousMatchException("Ambiguous candidate match detected.");
                }, maxRetries: 3);
            });

            Assert.That(callCount, Is.EqualTo(1), "Ambiguity must never trigger a retry loop.");
        }

        [Test]
        [Description("Verifies that NoSuchElementException is never retried and propagates immediately on attempt 1.")]
        public void Retry_NoSuchElementException_ImmediatelyPropagatesWithoutRetry()
        {
            int callCount = 0;

            Assert.Throws<NoSuchElementException>(() =>
            {
                PracticeFormPage.ExecuteWithStaleRecovery("TestMissing", () =>
                {
                    callCount++;
                    throw new NoSuchElementException("Element not present in DOM.");
                }, maxRetries: 3);
            });

            Assert.That(callCount, Is.EqualTo(1), "Missing element must never be treated as a stale reference.");
        }
    }
}

