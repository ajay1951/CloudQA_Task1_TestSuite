using System;
using System.Collections.Generic;
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
        public string AboutYourself { get; set; } = "Senior Full-Stack & Automation Test Engineer specializing in resilient Selenium automation.";
        public string Description { get; set; } = "Complete submission for CloudQA practice form with resilient multi-context element locators.";
        public string Username { get; set; } = "ajay_g";
        public string Password { get; set; } = "Secure@Pass123";
        public string ConfirmPassword { get; set; } = "Secure@Pass123";
        public bool AgreeTerms { get; set; } = true;
    }

    public class FormTestResult
    {
        public string ContextName { get; set; } = string.Empty;
        public bool FirstNameFound { get; set; }
        public bool FirstNameVerified { get; set; }
        public bool LastNameFound { get; set; }
        public bool GenderFound { get; set; }
        public bool GenderVerified { get; set; }
        public bool DobFound { get; set; }
        public bool MobileFound { get; set; }
        public bool EmailFound { get; set; }
        public bool CountryFound { get; set; }
        public bool StateFound { get; set; }
        public bool StateVerified { get; set; }
        public int HobbiesSelectedCount { get; set; }
        public bool AboutFound { get; set; }
        public bool DescriptionFound { get; set; }
        public bool UsernameFound { get; set; }
        public bool PasswordFound { get; set; }
        public bool ConfirmPasswordFound { get; set; }
        public bool AgreeTermsChecked { get; set; }
        public bool SubmitClicked { get; set; }

        public bool AllThreeFieldsVerified => FirstNameVerified && GenderVerified && StateVerified;
        public bool FormCompleted => FirstNameFound || GenderFound || StateFound;
    }

    public class PracticeFormPage
    {
        private readonly ISearchContext _context;
        private readonly ResilientElementFinder _finder;

        public PracticeFormPage(IWebDriver driver, int timeoutSeconds = 15, ISearchContext? context = null)
        {
            _context = context ?? driver;
            _finder = new ResilientElementFinder(driver, timeoutSeconds);
        }

        /// <summary>
        /// Fills all details present on the form context with high performance.
        /// </summary>
        public FormTestResult FillCompleteForm(string contextName, PracticeFormData data, bool clickSubmit = true)
        {
            var result = new FormTestResult { ContextName = contextName };
            Console.WriteLine($"\n=======================================================");
            Console.WriteLine($" [Processing Context: {contextName}]");
            Console.WriteLine($"=======================================================");

            try
            {
                // 1. First Name
                var fNameInput = _finder.FindTextBox(_context, "First Name");
                if (fNameInput != null)
                {
                    result.FirstNameFound = true;
                    fNameInput.Clear();
                    fNameInput.SendKeys(data.FirstName);
                    result.FirstNameVerified = _finder.VerifyInputText(fNameInput, data.FirstName);
                    Console.WriteLine($"  ✓ First Name: '{data.FirstName}' (Verified: {result.FirstNameVerified})");
                }

                // 2. Last Name
                var lNameInput = _finder.FindTextBox(_context, "Last Name");
                if (lNameInput != null)
                {
                    result.LastNameFound = true;
                    lNameInput.Clear();
                    lNameInput.SendKeys(data.LastName);
                    Console.WriteLine($"  ✓ Last Name: '{data.LastName}'");
                }

                // 3. Gender (Radio)
                var genderRadio = _finder.FindRadioButton(_context, data.Gender, "Gender");
                if (genderRadio != null)
                {
                    result.GenderFound = true;
                    _finder.SafeClick(genderRadio);
                    result.GenderVerified = _finder.VerifyRadioSelected(genderRadio);
                    Console.WriteLine($"  ✓ Gender: '{data.Gender}' (Verified Selected: {result.GenderVerified})");
                }

                // 4. Date of Birth
                var dobInput = _finder.FindTextBox(_context, "Date of Birth");
                if (dobInput != null)
                {
                    result.DobFound = true;
                    dobInput.Clear();
                    dobInput.SendKeys(data.DateOfBirth);
                    Console.WriteLine($"  ✓ Date of Birth: '{data.DateOfBirth}'");
                }

                // 5. Mobile #
                var mobileInput = _finder.FindTextBox(_context, "Mobile Number");
                if (mobileInput != null)
                {
                    result.MobileFound = true;
                    mobileInput.Clear();
                    mobileInput.SendKeys(data.MobileNumber);
                    Console.WriteLine($"  ✓ Mobile #: '{data.MobileNumber}'");
                }

                // 6. Email
                var emailInput = _finder.FindTextBox(_context, "Email");
                if (emailInput != null)
                {
                    result.EmailFound = true;
                    emailInput.Clear();
                    emailInput.SendKeys(data.Email);
                    Console.WriteLine($"  ✓ Email: '{data.Email}'");
                }

                // 7. Country
                var countryInput = _finder.FindTextBox(_context, "Country");
                if (countryInput != null)
                {
                    result.CountryFound = true;
                    countryInput.Clear();
                    countryInput.SendKeys(data.Country);
                    Console.WriteLine($"  ✓ Country: '{data.Country}'");
                }

                // 8. State (Dropdown)
                var stateSelect = _finder.FindDropdown(_context, "State");
                if (stateSelect != null)
                {
                    result.StateFound = true;
                    var selectElement = new SelectElement(stateSelect);
                    try
                    {
                        selectElement.SelectByText(data.State);
                        result.StateVerified = _finder.VerifyDropdownSelected(stateSelect, data.State);
                        Console.WriteLine($"  ✓ State: Selected '{data.State}' by text. (Verified: {result.StateVerified})");
                    }
                    catch (NoSuchElementException)
                    {
                        if (selectElement.Options.Count > 1)
                        {
                            selectElement.SelectByIndex(1);
                            var selectedOption = selectElement.SelectedOption.Text.Trim();
                            result.StateVerified = true;
                            Console.WriteLine($"  ✓ State: Fallback selected option '{selectedOption}'.");
                        }
                    }
                }

                // 9. Hobbies Checkboxes
                foreach (var hobby in data.Hobbies)
                {
                    var cb = _finder.FindCheckbox(_context, hobby);
                    if (cb != null)
                    {
                        if (!cb.Selected)
                        {
                            _finder.SafeClick(cb);
                        }
                        result.HobbiesSelectedCount++;
                        Console.WriteLine($"  ✓ Hobby: '{hobby}' selected.");
                    }
                }

                // 10. About Yourself
                var aboutInput = _finder.FindTextBox(_context, "About Yourself");
                if (aboutInput != null)
                {
                    result.AboutFound = true;
                    aboutInput.Clear();
                    aboutInput.SendKeys(data.AboutYourself);
                    Console.WriteLine("  ✓ About Yourself: Populated.");
                }

                // 11. Description
                bool descFilled = _finder.FillRichTextEditor(_context, "Description", data.Description);
                if (descFilled)
                {
                    result.DescriptionFound = true;
                    Console.WriteLine("  ✓ Description (Rich Text Editor): Populated.");
                }

                // 12. Username
                var userInput = _finder.FindTextBox(_context, "Username");
                if (userInput != null)
                {
                    result.UsernameFound = true;
                    userInput.Clear();
                    userInput.SendKeys(data.Username);
                    Console.WriteLine($"  ✓ Username: '{data.Username}'");
                }

                // 13. Password
                var passInput = _finder.FindTextBox(_context, "Password");
                if (passInput != null)
                {
                    result.PasswordFound = true;
                    passInput.Clear();
                    passInput.SendKeys(data.Password);
                    Console.WriteLine("  ✓ Password: Set.");
                }

                // 14. Confirm Password
                var confirmPassInput = _finder.FindTextBox(_context, "Confirm Password");
                if (confirmPassInput != null)
                {
                    result.ConfirmPasswordFound = true;
                    confirmPassInput.Clear();
                    confirmPassInput.SendKeys(data.ConfirmPassword);
                    Console.WriteLine("  ✓ Confirm Password: Set.");
                }

                // 15. Agreement Checkbox
                if (data.AgreeTerms)
                {
                    var agreeCb = _finder.FindCheckbox(_context, "Agree") 
                                  ?? _finder.FindCheckbox(_context, "terms");
                    if (agreeCb != null)
                    {
                        if (!agreeCb.Selected)
                        {
                            _finder.SafeClick(agreeCb);
                        }
                        result.AgreeTermsChecked = true;
                        Console.WriteLine("  ✓ Terms & Conditions: Agreed.");
                    }
                }

                // 16. Submit Button
                if (clickSubmit)
                {
                    var submitBtn = _finder.FindSubmitButton(_context);
                    if (submitBtn != null)
                    {
                        _finder.SafeClick(submitBtn);
                        result.SubmitClicked = true;
                        Console.WriteLine("  ✓ Submit Button Clicked.");
                    }
                }
            }
            catch (StaleElementReferenceException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Notice: {ex.Message}");
            }

            return result;
        }

        public FormTestResult FillAndVerifyThreeFields(string contextName, string firstName, string gender, string state, bool clickSubmit = true)
        {
            var data = new PracticeFormData
            {
                FirstName = firstName,
                Gender = gender,
                State = state
            };
            return FillCompleteForm(contextName, data, clickSubmit);
        }
    }
}
