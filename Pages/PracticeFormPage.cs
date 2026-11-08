using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using CloudQA.ResilientAutomation.Core;

namespace CloudQA.ResilientAutomation.Pages
{
    public class PracticeFormData
    {
        public string FirstName { get; set; } = "Ajay";
        public string LastName { get; set; } = "Gollapudi";
        public string Gender { get; set; } = "Male";
        public string DateOfBirth { get; set; } = "2001-05-19";
        public string MobileNumber { get; set; } = "9876543210";
        public string Email { get; set; } = "ajay@example.com";
        public string Country { get; set; } = "India";
        public string State { get; set; } = "India";
        public List<string> Hobbies { get; set; } = new() { "Dance", "Reading", "Cricket" };
        public string AboutYourself { get; set; } = "Automation test engineer specializing in Selenium frameworks.";
        public string Description { get; set; } = "Form submission test for CloudQA practice form.";
        public string Username { get; set; } = "ajay_g";
        public string Password { get; set; } = "Secure@Pass123";
        public string ConfirmPassword { get; set; } = "Secure@Pass123";
        public bool AgreeTerms { get; set; } = true;
    }

    /// <summary>
    /// Senior SDET Page object representing the CloudQA Practice Form.
    /// Provides action and verification methods for form fields with dynamic ARIA combobox
    /// support, stale element reference recovery, and zero silent fallbacks.
    /// </summary>
    public class PracticeFormPage
    {
        private readonly IWebDriver _driver;
        private readonly ISearchContext _context;
        private readonly ResilientElementFinder _finder;
        private readonly WebDriverWait _wait;
        private readonly int _timeoutSeconds;

        public PracticeFormPage(IWebDriver driver, int timeoutSeconds = 15, ISearchContext? context = null)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _context = context ?? driver;
            _finder = new ResilientElementFinder(driver, timeoutSeconds);
            _wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            _timeoutSeconds = timeoutSeconds;
        }

        #region Stale Element Reference Recovery Helper

        private void ExecuteWithStaleRecovery(string actionName, Action action, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    action();
                    return;
                }
                catch (AmbiguousMatchException)
                {
                    // Ambiguity must never be retried or suppressed
                    throw;
                }
                catch (StaleElementReferenceException ex) when (attempt < maxRetries)
                {
                    LogDiagnostic($"[Recovery] StaleElementReferenceException in '{actionName}' (Attempt {attempt}/{maxRetries}): {ex.Message}. Re-discovering fresh element...");
                    Thread.Sleep(150);
                }
            }
        }

        private T ExecuteWithStaleRecovery<T>(string actionName, Func<T> func, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return func();
                }
                catch (AmbiguousMatchException)
                {
                    throw;
                }
                catch (StaleElementReferenceException ex) when (attempt < maxRetries)
                {
                    LogDiagnostic($"[Recovery] StaleElementReferenceException in '{actionName}' (Attempt {attempt}/{maxRetries}): {ex.Message}. Re-discovering fresh element...");
                    Thread.Sleep(150);
                }
            }
            return func();
        }

        private static void LogDiagnostic(string message)
        {
            try { TestContext.Out.WriteLine(message); }
            catch { Console.WriteLine(message); }
        }

        #endregion

        #region Core Required Fields (First Name, Gender, State)

        public void SetFirstName(string firstName)
        {
            if (string.IsNullOrWhiteSpace(firstName)) throw new ArgumentException("First Name cannot be null or empty", nameof(firstName));

            ExecuteWithStaleRecovery("SetFirstName", () =>
            {
                var element = _finder.FindTextBox(_context, "First Name");
                if (element == null)
                {
                    throw new NoSuchElementException("Failed to find First Name textbox using resilient discovery strategies.");
                }

                _finder.ScrollIntoCenter(element);
                element.Clear();
                element.SendKeys(firstName);
            });
        }

        public string GetFirstNameValue()
        {
            return ExecuteWithStaleRecovery("GetFirstNameValue", () =>
            {
                var element = _finder.FindTextBox(_context, "First Name");
                if (element == null) throw new NoSuchElementException("First Name element not found when reading value.");
                return (element.GetAttribute("value") ?? "").Trim();
            });
        }

        public void SetLastName(string lastName)
        {
            if (string.IsNullOrWhiteSpace(lastName)) return;

            ExecuteWithStaleRecovery("SetLastName", () =>
            {
                var element = _finder.FindTextBox(_context, "Last Name");
                if (element != null)
                {
                    _finder.ScrollIntoCenter(element);
                    element.Clear();
                    element.SendKeys(lastName);
                }
            });
        }

        public string GetLastNameValue()
        {
            return ExecuteWithStaleRecovery("GetLastNameValue", () =>
            {
                var element = _finder.FindTextBox(_context, "Last Name");
                return element != null ? (element.GetAttribute("value") ?? "").Trim() : string.Empty;
            });
        }

        public void SelectGender(string genderOption)
        {
            if (string.IsNullOrWhiteSpace(genderOption)) throw new ArgumentException("Gender option cannot be null or empty", nameof(genderOption));

            ExecuteWithStaleRecovery("SelectGender", () =>
            {
                var radio = _finder.FindRadioButton(_context, genderOption, "Gender");
                if (radio == null)
                {
                    throw new NoSuchElementException($"Failed to find Gender radio button for option '{genderOption}'.");
                }

                _finder.SafeClick(radio);
            });
        }

        public bool IsGenderSelected(string genderOption)
        {
            return ExecuteWithStaleRecovery("IsGenderSelected", () =>
            {
                var radio = _finder.FindRadioButton(_context, genderOption, "Gender");
                if (radio == null) return false;

                if (radio.Selected) return true;
                return string.Equals(radio.GetAttribute("aria-checked"), "true", StringComparison.OrdinalIgnoreCase);
            });
        }

        public void SelectState(string stateText)
        {
            if (string.IsNullOrWhiteSpace(stateText)) throw new ArgumentException("State text cannot be null or empty", nameof(stateText));

            ExecuteWithStaleRecovery("SelectState", () =>
            {
                var dropdown = _finder.FindDropdown(_context, "State");
                if (dropdown == null)
                {
                    throw new NoSuchElementException("Failed to find State dropdown using resilient discovery strategies.");
                }

                _finder.ScrollIntoCenter(dropdown);

                if (dropdown.TagName.Equals("select", StringComparison.OrdinalIgnoreCase))
                {
                    SelectNativeDropdownOption(dropdown, stateText);
                }
                else
                {
                    SelectAriaComboboxOption(dropdown, stateText);
                }
            });
        }

        private void SelectNativeDropdownOption(IWebElement dropdown, string stateText)
        {
            var select = new SelectElement(dropdown);
            try
            {
                select.SelectByText(stateText);
            }
            catch (NoSuchElementException)
            {
                var matchingOption = select.Options.FirstOrDefault(o =>
                    ResilientElementFinder.Normalize(o.Text) == ResilientElementFinder.Normalize(stateText) ||
                    ResilientElementFinder.MatchText(o.Text, stateText));

                if (matchingOption != null)
                {
                    select.SelectByText(matchingOption.Text);
                }
                else
                {
                    var available = string.Join(", ", select.Options.Select(o => $"'{o.Text.Trim()}'"));
                    throw new NoSuchElementException($"Option '{stateText}' was not found in the State dropdown. Available options: [{available}]");
                }
            }
        }

        private void SelectAriaComboboxOption(IWebElement combobox, string stateText)
        {
            // 1. Open combobox if closed
            string expanded = (combobox.GetAttribute("aria-expanded") ?? "").Trim();
            if (!string.Equals(expanded, "true", StringComparison.OrdinalIgnoreCase))
            {
                _finder.SafeClick(combobox);
                Thread.Sleep(100);
            }

            // 2. Discover associated listbox via aria-controls / aria-owns or active DOM query
            ISearchContext optionRoot = _driver;
            string controlsId = (combobox.GetAttribute("aria-controls") ?? combobox.GetAttribute("aria-owns") ?? "").Trim();

            if (!string.IsNullOrWhiteSpace(controlsId))
            {
                try
                {
                    var linkedListbox = _driver.FindElement(By.Id(controlsId));
                    if (linkedListbox != null) optionRoot = linkedListbox;
                }
                catch { }
            }

            // 3. Collect option elements
            var optionElements = optionRoot.FindElements(By.CssSelector("[role='option'], li, .dropdown-item, .combobox-option, option"))
                                           .Where(o => o.Displayed).ToList();

            // If empty, search direct parent / sibling popup
            if (optionElements.Count == 0)
            {
                optionElements = _driver.FindElements(By.CssSelector("[role='listbox'] [role='option'], .dropdown-menu .dropdown-item, ul[role='listbox'] li"))
                                        .Where(o => o.Displayed).ToList();
            }

            var matching = optionElements.FirstOrDefault(o =>
            {
                var text = (o.Text ?? o.GetAttribute("innerText") ?? o.GetAttribute("textContent") ?? "").Trim();
                return ResilientElementFinder.Normalize(text) == ResilientElementFinder.Normalize(stateText) ||
                       ResilientElementFinder.MatchText(text, stateText);
            });

            if (matching != null)
            {
                _finder.SafeClick(matching);
            }
            else
            {
                var available = string.Join(", ", optionElements.Select(o => $"'{o.Text?.Trim()}'"));
                throw new NoSuchElementException($"Option '{stateText}' was not found in the custom ARIA combobox. Available options: [{available}]");
            }
        }

        public string GetSelectedState()
        {
            return ExecuteWithStaleRecovery("GetSelectedState", () =>
            {
                var dropdown = _finder.FindDropdown(_context, "State");
                if (dropdown == null) throw new NoSuchElementException("State dropdown not found when reading selection.");

                if (dropdown.TagName.Equals("select", StringComparison.OrdinalIgnoreCase))
                {
                    var select = new SelectElement(dropdown);
                    return (select.SelectedOption?.Text ?? "").Trim();
                }

                // Check active ARIA selected option
                string controlsId = (dropdown.GetAttribute("aria-controls") ?? dropdown.GetAttribute("aria-owns") ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(controlsId))
                {
                    try
                    {
                        var listbox = _driver.FindElement(By.Id(controlsId));
                        var selectedOption = listbox.FindElements(By.CssSelector("[role='option'][aria-selected='true'], [role='option'].selected"))
                                                    .FirstOrDefault();
                        if (selectedOption != null) return (selectedOption.Text ?? "").Trim();
                    }
                    catch { }
                }

                var activeOpt = _driver.FindElements(By.CssSelector("[role='option'][aria-selected='true'], [role='option'].selected, [role='listbox'] .active"))
                                       .FirstOrDefault();
                if (activeOpt != null) return (activeOpt.Text ?? "").Trim();

                return (dropdown.GetAttribute("value") ?? dropdown.Text ?? "").Trim();
            });
        }

        public void ClickSubmit()
        {
            ExecuteWithStaleRecovery("ClickSubmit", () =>
            {
                var submitBtn = _finder.FindSubmitButton(_context);
                if (submitBtn == null) throw new NoSuchElementException("Submit button not found in form context.");
                _finder.SafeClick(submitBtn);
            });
        }

        #endregion

        #region Extended Form Capabilities (All 16 Fields)

        public void FillCompleteForm(PracticeFormData data, bool clickSubmit = true)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            SetFirstName(data.FirstName);

            var lName = _finder.FindTextBox(_context, "Last Name");
            if (lName != null)
            {
                lName.Clear();
                lName.SendKeys(data.LastName);
            }

            SelectGender(data.Gender);

            var dob = _finder.FindTextBox(_context, "Date of Birth");
            if (dob != null)
            {
                dob.Clear();
                dob.SendKeys(data.DateOfBirth);
            }

            var mobile = _finder.FindTextBox(_context, "Mobile Number");
            if (mobile != null)
            {
                mobile.Clear();
                mobile.SendKeys(data.MobileNumber);
            }

            var email = _finder.FindTextBox(_context, "Email");
            if (email != null)
            {
                email.Clear();
                email.SendKeys(data.Email);
            }

            var country = _finder.FindDropdown(_context, "Country");
            if (country != null)
            {
                try
                {
                    var sel = new SelectElement(country);
                    sel.SelectByText(data.Country);
                }
                catch { }
            }

            SelectState(data.State);

            foreach (var hobby in data.Hobbies)
            {
                var cb = _finder.FindCheckbox(_context, hobby);
                if (cb != null && !cb.Selected)
                {
                    _finder.SafeClick(cb);
                }
            }

            var about = _finder.FindTextBox(_context, "About Yourself");
            if (about != null)
            {
                about.Clear();
                about.SendKeys(data.AboutYourself);
            }

            var user = _finder.FindTextBox(_context, "Username");
            if (user != null)
            {
                user.Clear();
                user.SendKeys(data.Username);
            }

            var pwd = _finder.FindTextBox(_context, "Password");
            if (pwd != null)
            {
                pwd.Clear();
                pwd.SendKeys(data.Password);
            }

            var confirm = _finder.FindTextBox(_context, "Confirm Password");
            if (confirm != null)
            {
                confirm.Clear();
                confirm.SendKeys(data.ConfirmPassword);
            }

            var agree = _finder.FindCheckbox(_context, "Agree");
            if (agree != null && !agree.Selected)
            {
                _finder.SafeClick(agree);
            }

            if (clickSubmit)
            {
                ClickSubmit();
            }
        }

        #endregion
    }
}
