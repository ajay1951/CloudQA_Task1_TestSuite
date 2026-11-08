using System;
using System.Collections.Generic;
using System.Linq;
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
        public string AboutYourself { get; set; } = "Senior Full-Stack & Automation Test Engineer specializing in resilient Selenium frameworks.";
        public string Description { get; set; } = "Complete submission for CloudQA practice form with resilient locator architecture.";
        public string Username { get; set; } = "ajay_g";
        public string Password { get; set; } = "Secure@Pass123";
        public string ConfirmPassword { get; set; } = "Secure@Pass123";
        public bool AgreeTerms { get; set; } = true;
    }

    /// <summary>
    /// Senior SDET Page Object Model representing the CloudQA Practice Form.
    /// Strictly separates browser action and state retrieval from test assertion logic.
    /// </summary>
    public class PracticeFormPage
    {
        private readonly ISearchContext _context;
        private readonly ResilientElementFinder _finder;
        private readonly WebDriverWait _wait;

        public PracticeFormPage(IWebDriver driver, int timeoutSeconds = 15, ISearchContext? context = null)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            _context = context ?? driver;
            _finder = new ResilientElementFinder(driver, timeoutSeconds);
            _wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        }

        #region Core Required Fields (First Name, Gender, State)

        public void SetFirstName(string firstName)
        {
            if (string.IsNullOrWhiteSpace(firstName)) throw new ArgumentException("First Name cannot be null or empty", nameof(firstName));
            
            var element = _finder.FindTextBox(_context, "First Name");
            if (element == null)
            {
                throw new NoSuchElementException("Failed to find First Name textbox using resilient discovery strategies.");
            }

            _finder.ScrollIntoCenter(element);
            element.Clear();
            element.SendKeys(firstName);
        }

        public string GetFirstNameValue()
        {
            var element = _finder.FindTextBox(_context, "First Name");
            if (element == null) throw new NoSuchElementException("First Name element not found when reading value.");
            return (element.GetAttribute("value") ?? "").Trim();
        }

        public void SetLastName(string lastName)
        {
            if (string.IsNullOrWhiteSpace(lastName)) return;

            var element = _finder.FindTextBox(_context, "Last Name");
            if (element != null)
            {
                _finder.ScrollIntoCenter(element);
                element.Clear();
                element.SendKeys(lastName);
            }
        }

        public string GetLastNameValue()
        {
            var element = _finder.FindTextBox(_context, "Last Name");
            return element != null ? (element.GetAttribute("value") ?? "").Trim() : string.Empty;
        }

        public void SelectGender(string genderOption)
        {
            if (string.IsNullOrWhiteSpace(genderOption)) throw new ArgumentException("Gender option cannot be null or empty", nameof(genderOption));

            var radio = _finder.FindRadioButton(_context, genderOption, "Gender");
            if (radio == null)
            {
                throw new NoSuchElementException($"Failed to find Gender radio button for option '{genderOption}'.");
            }

            _finder.SafeClick(radio);
        }

        public bool IsGenderSelected(string genderOption)
        {
            var radio = _finder.FindRadioButton(_context, genderOption, "Gender");
            if (radio == null) return false;
            
            if (radio.Selected) return true;
            return string.Equals(radio.GetAttribute("aria-checked"), "true", StringComparison.OrdinalIgnoreCase);
        }

        public void SelectState(string stateText)
        {
            if (string.IsNullOrWhiteSpace(stateText)) throw new ArgumentException("State text cannot be null or empty", nameof(stateText));

            var dropdown = _finder.FindDropdown(_context, "State");
            if (dropdown == null)
            {
                throw new NoSuchElementException("Failed to find State dropdown using resilient discovery strategies.");
            }

            _finder.ScrollIntoCenter(dropdown);

            if (dropdown.TagName.Equals("select", StringComparison.OrdinalIgnoreCase))
            {
                var select = new SelectElement(dropdown);
                try
                {
                    select.SelectByText(stateText);
                }
                catch (NoSuchElementException)
                {
                    var matchingOption = select.Options.FirstOrDefault(o => 
                        ResilientElementFinder.Normalize(o.Text).Contains(ResilientElementFinder.Normalize(stateText)));
                    
                    if (matchingOption != null)
                    {
                        select.SelectByText(matchingOption.Text);
                    }
                    else if (select.Options.Count > 1)
                    {
                        select.SelectByIndex(1);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            else
            {
                // Fallback for custom div/button-based dropdowns on modified pages
                _finder.SafeClick(dropdown);
                var option = _context.FindElements(By.XPath($".//*[contains(translate(text(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '{stateText.ToLower()}')]"))
                                     .FirstOrDefault(o => o.Displayed);
                if (option != null) _finder.SafeClick(option);
            }
        }

        public string GetSelectedState()
        {
            var dropdown = _finder.FindDropdown(_context, "State");
            if (dropdown == null) throw new NoSuchElementException("State dropdown not found when reading selection.");

            if (dropdown.TagName.Equals("select", StringComparison.OrdinalIgnoreCase))
            {
                var select = new SelectElement(dropdown);
                return (select.SelectedOption?.Text ?? "").Trim();
            }

            return (dropdown.GetAttribute("value") ?? dropdown.Text ?? "").Trim();
        }

        public void ClickSubmit()
        {
            var submitBtn = _finder.FindSubmitButton(_context);
            if (submitBtn == null) throw new NoSuchElementException("Submit button not found in form context.");
            _finder.SafeClick(submitBtn);
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

            var country = _finder.FindTextBox(_context, "Country");
            if (country != null)
            {
                country.Clear();
                country.SendKeys(data.Country);
            }

            SelectState(data.State);

            foreach (var hobby in data.Hobbies)
            {
                var cb = _finder.FindCheckbox(_context, hobby);
                if (cb != null && !cb.Selected) _finder.SafeClick(cb);
            }

            var about = _finder.FindTextBox(_context, "About Yourself");
            if (about != null)
            {
                about.Clear();
                about.SendKeys(data.AboutYourself);
            }

            _finder.FillRichTextEditor(_context, "Description", data.Description);

            var user = _finder.FindTextBox(_context, "Username");
            if (user != null)
            {
                user.Clear();
                user.SendKeys(data.Username);
            }

            var pass = _finder.FindTextBox(_context, "Password");
            if (pass != null)
            {
                pass.Clear();
                pass.SendKeys(data.Password);
            }

            var confirmPass = _finder.FindTextBox(_context, "Confirm Password");
            if (confirmPass != null)
            {
                confirmPass.Clear();
                confirmPass.SendKeys(data.ConfirmPassword);
            }

            if (data.AgreeTerms)
            {
                var agree = _finder.FindCheckbox(_context, "Agree") ?? _finder.FindCheckbox(_context, "terms");
                if (agree != null && !agree.Selected) _finder.SafeClick(agree);
            }

            if (clickSubmit)
            {
                ClickSubmit();
            }
        }

        #endregion
    }
}
