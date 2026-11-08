using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using OpenQA.Selenium;
using CloudQA.ResilientAutomation.Configuration;
using CloudQA.ResilientAutomation.Core;
using CloudQA.ResilientAutomation.Pages;

namespace CloudQA.ResilientAutomation.Tests
{
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

        #region 1. Original DOM Structure

        [Test]
        [Description("Fixture 1: Validates automation against standard, conventional HTML form structure.")]
        public void Fixture_01_Original_DOM_Structure()
        {
            const string html = @"
                <html><body>
                    <form id='practice-form'>
                        <div class='form-group'>
                            <label for='fname'>First Name</label>
                            <input type='text' id='fname' name='firstname' />
                        </div>
                        <div class='form-group'>
                            <label>Gender</label>
                            <div>
                                <label><input type='radio' name='gender' value='Male' id='male'> Male</label>
                                <label><input type='radio' name='gender' value='Female' id='female'> Female</label>
                            </div>
                        </div>
                        <div class='form-group'>
                            <label for='state'>State</label>
                            <select id='state' name='state'>
                                <option value=''>Choose State</option>
                                <option value='IN'>India</option>
                                <option value='US'>United States</option>
                            </select>
                        </div>
                        <button type='submit'>Submit</button>
                    </form>
                </body></html>";

            LoadHtml(html);
            var page = new PracticeFormPage(_driver, 5);

            page.SetFirstName("John");
            page.SelectGender("Male");
            page.SelectState("India");

            Assert.Multiple(() =>
            {
                Assert.That(page.GetFirstNameValue(), Is.EqualTo("John"));
                Assert.That(page.IsGenderSelected("Male"), Is.True);
                Assert.That(page.GetSelectedState(), Does.Contain("India"));
            });
        }

        #endregion

        #region 2. Reordered Fields

        [Test]
        [Description("Fixture 2: Proves resilience when fields appear in reverse visual and DOM order.")]
        public void Fixture_02_Reordered_Fields()
        {
            const string html = @"
                <html><body>
                    <form id='reordered-form'>
                        <!-- State dropdown placed first -->
                        <div class='row'>
                            <label for='state-dropdown'>State</label>
                            <select id='state-dropdown'>
                                <option value='TX'>Texas</option>
                                <option value='CA'>California</option>
                            </select>
                        </div>
                        <!-- Gender radio group placed second -->
                        <div class='row'>
                            <fieldset>
                                <legend>Gender</legend>
                                <input type='radio' name='g_option' id='g_m' value='Male'><label for='g_m'>Male</label>
                                <input type='radio' name='g_option' id='g_f' value='Female'><label for='g_f'>Female</label>
                            </fieldset>
                        </div>
                        <!-- First Name placed last -->
                        <div class='row'>
                            <label for='user-first'>First Name</label>
                            <input type='text' id='user-first' />
                        </div>
                    </form>
                </body></html>";

            LoadHtml(html);
            var page = new PracticeFormPage(_driver, 5);

            page.SetFirstName("Alice");
            page.SelectGender("Female");
            page.SelectState("California");

            Assert.Multiple(() =>
            {
                Assert.That(page.GetFirstNameValue(), Is.EqualTo("Alice"));
                Assert.That(page.IsGenderSelected("Female"), Is.True);
                Assert.That(page.GetSelectedState(), Does.Contain("California"));
            });
        }

        #endregion

        #region 3. Changed IDs

        [Test]
        [Description("Fixture 3: Proves resilience when element IDs are completely randomized or dynamically generated.")]
        public void Fixture_03_Changed_IDs()
        {
            const string html = @"
                <html><body>
                    <form>
                        <div class='field'>
                            <label for='rnd_id_99482'>First Name</label>
                            <input type='text' id='rnd_id_99482' />
                        </div>
                        <div class='field'>
                            <label>Gender</label>
                            <input type='radio' name='grp_gen' id='gen_opt_882' value='Male'> Male
                            <input type='radio' name='grp_gen' id='gen_opt_883' value='Female'> Female
                        </div>
                        <div class='field'>
                            <label for='dropdown_hash_774'>State</label>
                            <select id='dropdown_hash_774'>
                                <option value='1'>Delhi</option>
                                <option value='2'>Maharashtra</option>
                            </select>
                        </div>
                    </form>
                </body></html>";

            LoadHtml(html);
            var page = new PracticeFormPage(_driver, 5);

            page.SetFirstName("Dev");
            page.SelectGender("Male");
            page.SelectState("Maharashtra");

            Assert.Multiple(() =>
            {
                Assert.That(page.GetFirstNameValue(), Is.EqualTo("Dev"));
                Assert.That(page.IsGenderSelected("Male"), Is.True);
                Assert.That(page.GetSelectedState(), Does.Contain("Maharashtra"));
            });
        }

        #endregion

        #region 4. Changed Names

        [Test]
        [Description("Fixture 4: Proves resilience when name attributes are altered or obfuscated.")]
        public void Fixture_04_Changed_Names()
        {
            const string html = @"
                <html><body>
                    <form>
                        <label>Given Name
                            <input type='text' name='data[user][attributes][fname_field]' />
                        </label>
                        <div>
                            <span>Select Gender:</span>
                            <label><input type='radio' name='attr_gender_radio' value='Male'> Male</label>
                            <label><input type='radio' name='attr_gender_radio' value='Female'> Female</label>
                        </div>
                        <label>Province / State:
                            <select name='data[user][region_select]'>
                                <option value='ON'>Ontario</option>
                                <option value='BC'>British Columbia</option>
                            </select>
                        </label>
                    </form>
                </body></html>";

            LoadHtml(html);
            var page = new PracticeFormPage(_driver, 5);

            page.SetFirstName("Priya");
            page.SelectGender("Female");
            page.SelectState("Ontario");

            Assert.Multiple(() =>
            {
                Assert.That(page.GetFirstNameValue(), Is.EqualTo("Priya"));
                Assert.That(page.IsGenderSelected("Female"), Is.True);
                Assert.That(page.GetSelectedState(), Does.Contain("Ontario"));
            });
        }

        #endregion

        #region 5. Changed Classes

        [Test]
        [Description("Fixture 5: Proves resilience when CSS utility or framework classes are altered.")]
        public void Fixture_05_Changed_Classes()
        {
            const string html = @"
                <html><body>
                    <div class='custom-tailwind-wrapper p-4 flex-col'>
                        <div class='tw-input-wrapper'>
                            <label for='tf-input' class='text-sm font-bold'>First Name</label>
                            <input id='tf-input' class='border rounded py-2 px-3' type='text' />
                        </div>
                        <div class='tw-radio-group my-2'>
                            <p class='font-medium'>Gender</p>
                            <label class='inline-flex items-center'><input type='radio' name='r_gen' value='Male'> Male</label>
                            <label class='inline-flex items-center'><input type='radio' name='r_gen' value='Female'> Female</label>
                        </div>
                        <div class='tw-select-box'>
                            <label for='tf-select' class='block'>State</label>
                            <select id='tf-select' class='bg-gray-50 border'>
                                <option value='Karnataka'>Karnataka</option>
                                <option value='Kerala'>Kerala</option>
                            </select>
                        </div>
                    </div>
                </body></html>";

            LoadHtml(html);
            var page = new PracticeFormPage(_driver, 5);

            page.SetFirstName("Rohan");
            page.SelectGender("Male");
            page.SelectState("Karnataka");

            Assert.Multiple(() =>
            {
                Assert.That(page.GetFirstNameValue(), Is.EqualTo("Rohan"));
                Assert.That(page.IsGenderSelected("Male"), Is.True);
                Assert.That(page.GetSelectedState(), Does.Contain("Karnataka"));
            });
        }

        #endregion

        #region 6. Additional Unrelated Fields

        [Test]
        [Description("Fixture 6: Proves that unrelated inputs (billing names, country, phone) do not confuse discovery.")]
        public void Fixture_06_Additional_Unrelated_Fields()
        {
            const string html = @"
                <html><body>
                    <form>
                        <div>
                            <label for='bill_name'>Billing Name</label>
                            <input id='bill_name' type='text' />
                        </div>
                        <div>
                            <label for='cust_fname'>First Name</label>
                            <input id='cust_fname' type='text' />
                        </div>
                        <div>
                            <label for='phone'>Phone Number</label>
                            <input id='phone' type='tel' />
                        </div>
                        <div>
                            <label>Gender</label>
                            <input type='radio' name='gen_rad' value='Male'> Male
                            <input type='radio' name='gen_rad' value='Female'> Female
                        </div>
                        <div>
                            <label for='country_sel'>Country</label>
                            <select id='country_sel'><option>Canada</option><option>India</option></select>
                        </div>
                        <div>
                            <label for='state_sel'>State</label>
                            <select id='state_sel'><option>Punjab</option><option>Gujarat</option></select>
                        </div>
                    </form>
                </body></html>";

            LoadHtml(html);
            var page = new PracticeFormPage(_driver, 5);

            page.SetFirstName("Ananya");
            page.SelectGender("Female");
            page.SelectState("Gujarat");

            Assert.Multiple(() =>
            {
                Assert.That(page.GetFirstNameValue(), Is.EqualTo("Ananya"));
                Assert.That(page.IsGenderSelected("Female"), Is.True);
                Assert.That(page.GetSelectedState(), Does.Contain("Gujarat"));
            });
        }

        #endregion

        #region 7. Duplicate Labels (Ambiguity Detection)

        [Test]
        [Description("Fixture 7: Validates that when two identical First Name inputs exist without clear distinction, ambiguity is rejected.")]
        public void Fixture_07_Duplicate_Labels_Ambiguity_Rejection()
        {
            const string html = @"
                <html><body>
                    <form>
                        <div class='form-group'>
                            <label>First Name</label>
                            <input type='text' class='form-control' />
                        </div>
                        <div class='form-group'>
                            <label>First Name</label>
                            <input type='text' class='form-control' />
                        </div>
                    </form>
                </body></html>";

            LoadHtml(html);
            var match = _finder.FindTextBox(_driver, "First Name");

            Assert.That(match, Is.Null, "Expected null due to ambiguity rejection between two identically scored inputs.");
        }

        #endregion

        #region 8. Multiple Radio Groups

        [Test]
        [Description("Fixture 8: Proves that multiple radio groups (Marital Status vs Gender) are accurately differentiated.")]
        public void Fixture_08_Multiple_Radio_Groups()
        {
            const string html = @"
                <html><body>
                    <form>
                        <fieldset>
                            <legend>Marital Status</legend>
                            <label><input type='radio' name='marital' value='Single'> Single</label>
                            <label><input type='radio' name='marital' value='Married'> Married</label>
                        </fieldset>

                        <div role='radiogroup' aria-label='Gender'>
                            <span class='title'>Gender</span>
                            <label><input type='radio' name='gender_opt' value='Male'> Male</label>
                            <label><input type='radio' name='gender_opt' value='Female'> Female</label>
                        </div>

                        <fieldset>
                            <legend>Preferred Contact Method</legend>
                            <label><input type='radio' name='contact' value='Email'> Email</label>
                            <label><input type='radio' name='contact' value='Phone'> Phone</label>
                        </fieldset>
                    </form>
                </body></html>";

            LoadHtml(html);
            var page = new PracticeFormPage(_driver, 5);

            page.SelectGender("Male");

            var maleRadio = _finder.FindRadioButton(_driver, "Male", "Gender");
            var singleRadio = _driver.FindElement(By.CssSelector("input[value='Single']"));

            Assert.Multiple(() =>
            {
                Assert.That(maleRadio, Is.Not.Null);
                Assert.That(maleRadio!.Selected, Is.True);
                Assert.That(singleRadio.Selected, Is.False, "Marital status radio should not have been selected.");
            });
        }

        #endregion

        #region 9. Multiple Dropdowns

        [Test]
        [Description("Fixture 9: Proves accurate selection when Country, State, and City dropdowns coexist on the same form.")]
        public void Fixture_09_Multiple_Dropdowns()
        {
            const string html = @"
                <html><body>
                    <form>
                        <div>
                            <label for='sel_country'>Country</label>
                            <select id='sel_country'><option>India</option><option>USA</option></select>
                        </div>
                        <div>
                            <label for='sel_state'>State</label>
                            <select id='sel_state'><option>Goa</option><option>Kerala</option></select>
                        </div>
                        <div>
                            <label for='sel_city'>City</label>
                            <select id='sel_city'><option>Panaji</option><option>Kochi</option></select>
                        </div>
                    </form>
                </body></html>";

            LoadHtml(html);
            var page = new PracticeFormPage(_driver, 5);

            page.SelectState("Kerala");

            var stateSelect = new OpenQA.Selenium.Support.UI.SelectElement(_driver.FindElement(By.Id("sel_state")));
            var countrySelect = new OpenQA.Selenium.Support.UI.SelectElement(_driver.FindElement(By.Id("sel_country")));

            Assert.Multiple(() =>
            {
                Assert.That(stateSelect.SelectedOption.Text, Is.EqualTo("Kerala"));
                Assert.That(countrySelect.SelectedOption.Text, Is.EqualTo("India"));
            });
        }

        #endregion

        #region 10. Nested Containers

        [Test]
        [Description("Fixture 10: Proves discovery within complex, deeply nested container structures.")]
        public void Fixture_10_Nested_Containers()
        {
            const string html = @"
                <html><body>
                    <section class='wizard-step active'>
                        <div class='card shadow-lg'>
                            <div class='card-body'>
                                <div class='row g-3'>
                                    <div class='col-md-6'>
                                        <div class='form-floating mb-3'>
                                            <input type='text' class='form-control' id='floatingFirstName' placeholder='First Name'>
                                            <label for='floatingFirstName'>First Name</label>
                                        </div>
                                    </div>
                                    <div class='col-md-6'>
                                        <div class='p-3 border rounded'>
                                            <label class='form-label d-block'>Gender</label>
                                            <div class='form-check form-check-inline'>
                                                <input class='form-check-input' type='radio' name='nested_gen' id='genM' value='Male'>
                                                <label class='form-check-label' for='genM'>Male</label>
                                            </div>
                                        </div>
                                    </div>
                                    <div class='col-12'>
                                        <div class='input-group'>
                                            <label class='input-group-text' for='nestedState'>State</label>
                                            <select class='form-select' id='nestedState'>
                                                <option value='Assam'>Assam</option>
                                                <option value='Bihar'>Bihar</option>
                                            </select>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </section>
                </body></html>";

            LoadHtml(html);
            var page = new PracticeFormPage(_driver, 5);

            page.SetFirstName("Suresh");
            page.SelectGender("Male");
            page.SelectState("Assam");

            Assert.Multiple(() =>
            {
                Assert.That(page.GetFirstNameValue(), Is.EqualTo("Suresh"));
                Assert.That(page.IsGenderSelected("Male"), Is.True);
                Assert.That(page.GetSelectedState(), Does.Contain("Assam"));
            });
        }

        #endregion

        #region 11. Hidden Duplicate Controls

        [Test]
        [Description("Fixture 11: Validates that hidden duplicate inputs are filtered out and visible inputs are selected.")]
        public void Fixture_11_Hidden_Duplicate_Controls()
        {
            const string html = @"
                <html><body>
                    <form>
                        <!-- Hidden duplicate element -->
                        <div style='display: none;'>
                            <label for='hidden_fn'>First Name</label>
                            <input id='hidden_fn' type='text' value='HiddenGhost' />
                        </div>

                        <!-- Active visible element -->
                        <div>
                            <label for='visible_fn'>First Name</label>
                            <input id='visible_fn' type='text' />
                        </div>
                    </form>
                </body></html>";

            LoadHtml(html);
            var page = new PracticeFormPage(_driver, 5);

            page.SetFirstName("RealUser");

            var visibleInput = _driver.FindElement(By.Id("visible_fn"));
            var hiddenInput = _driver.FindElement(By.Id("hidden_fn"));

            Assert.Multiple(() =>
            {
                Assert.That(visibleInput.GetAttribute("value"), Is.EqualTo("RealUser"));
                Assert.That(hiddenInput.GetAttribute("value"), Is.EqualTo("HiddenGhost"), "Hidden input should not have been modified.");
            });
        }

        #endregion

        #region 12. Missing Required Fields (Expected Failure Assertions)

        [Test]
        [Description("Fixture 12: Validates that missing fields and invalid options trigger explicit, descriptive exceptions.")]
        public void Fixture_12_Missing_Required_Fields_Expected_Failures()
        {
            const string html = @"
                <html><body>
                    <form id='sparse-form'>
                        <label for='email'>Email Address</label>
                        <input id='email' type='email' />

                        <label for='state_sel'>State</label>
                        <select id='state_sel'>
                            <option value='NY'>New York</option>
                        </select>
                    </form>
                </body></html>";

            LoadHtml(html);
            var page = new PracticeFormPage(_driver, 5);

            // 1. Missing First Name must throw NoSuchElementException
            var fnEx = Assert.Throws<NoSuchElementException>(() => page.SetFirstName("Test"));
            Assert.That(fnEx!.Message, Does.Contain("First Name"));

            // 2. Missing Gender radio group must throw NoSuchElementException
            var genEx = Assert.Throws<NoSuchElementException>(() => page.SelectGender("Male"));
            Assert.That(genEx!.Message, Does.Contain("Gender"));

            // 3. Requesting a non-existent State option must throw NoSuchElementException (no silent fallback!)
            var stateEx = Assert.Throws<NoSuchElementException>(() => page.SelectState("California"));
            Assert.That(stateEx!.Message, Does.Contain("California"));
        }

        #endregion

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
