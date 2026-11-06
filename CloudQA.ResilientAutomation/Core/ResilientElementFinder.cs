using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace CloudQA.ResilientAutomation.Core
{
    public class ResilientElementFinder
    {
        private readonly IWebDriver _driver;
        private readonly WebDriverWait _wait;
        private readonly IJavaScriptExecutor _js;

        public ResilientElementFinder(IWebDriver driver, int timeoutSeconds = 15)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));
            _js = (IJavaScriptExecutor)_driver;
        }

        public void ScrollIntoCenter(IWebElement element)
        {
            try
            {
                _js.ExecuteScript("arguments[0].scrollIntoView({behavior: 'instant', block: 'center', inline: 'nearest'});", element);
            }
            catch { }
        }

        public void SafeClick(IWebElement element)
        {
            try
            {
                element.Click();
            }
            catch (WebDriverException)
            {
                try
                {
                    _js.ExecuteScript("arguments[0].click();", element);
                }
                catch
                {
                    try
                    {
                        _js.ExecuteScript("const label = arguments[0].closest('label') || arguments[0].parentElement; if(label) label.click(); else arguments[0].click();", element);
                    }
                    catch { }
                }
            }
        }

        #region Form & Embedded Context Discovery

        public IWebElement? DiscoverMainRootForm(IWebDriver driver)
        {
            try
            {
                const string script = @"
                    const forms = Array.from(document.querySelectorAll('form'));
                    if (forms.length === 0) return document.body;
                    let best = forms[0];
                    let maxControls = 0;
                    for (const f of forms) {
                        const count = f.querySelectorAll('input, select, textarea').length;
                        if (count > maxControls) {
                            maxControls = count;
                            best = f;
                        }
                    }
                    return best;
                ";
                return _js.ExecuteScript(script) as IWebElement ?? driver.FindElements(By.TagName("body")).FirstOrDefault();
            }
            catch
            {
                return driver.FindElements(By.TagName("body")).FirstOrDefault();
            }
        }

        public IReadOnlyList<IWebElement> DiscoverForms(ISearchContext context)
        {
            try
            {
                var forms = context.FindElements(By.TagName("form")).ToList();
                if (forms.Count > 0) return forms;

                var body = context.FindElements(By.TagName("body")).FirstOrDefault();
                if (body != null) return new List<IWebElement> { body };
            }
            catch { }

            return Array.Empty<IWebElement>();
        }

        public IReadOnlyList<IWebElement> DiscoverAllShadowFormHosts()
        {
            const string script = @"
                const hosts = [];
                function walk(root) {
                    const elements = root.querySelectorAll('*');
                    for (const el of elements) {
                        if (el.tagName.toLowerCase() === 'shadow-form') {
                            hosts.push(el);
                        }
                        if (el.shadowRoot) {
                            walk(el.shadowRoot);
                        }
                    }
                }
                walk(document);
                return hosts;
            ";

            try
            {
                var result = _js.ExecuteScript(script);
                if (result is ReadOnlyCollection<IWebElement> webElements)
                {
                    return webElements;
                }
            }
            catch { }
            return Array.Empty<IWebElement>();
        }

        public IReadOnlyList<IWebElement> DiscoverIFramesInsideShadowRoots()
        {
            const string script = @"
                const shadowIframes = [];
                function walk(root, insideShadow) {
                    const elements = root.querySelectorAll('*');
                    for (const el of elements) {
                        if (insideShadow && el.tagName.toLowerCase() === 'iframe') {
                            shadowIframes.push(el);
                        }
                        if (el.shadowRoot) {
                            walk(el.shadowRoot, true);
                        }
                    }
                }
                walk(document, false);
                return shadowIframes;
            ";

            try
            {
                var result = _js.ExecuteScript(script);
                if (result is ReadOnlyCollection<IWebElement> webElements)
                {
                    return webElements;
                }
            }
            catch { }
            return Array.Empty<IWebElement>();
        }

        #endregion

        #region Ultra-Fast Single-Roundtrip Resilient Finders

        /// <summary>
        /// Locates a text box in a single browser-side roundtrip using JavaScript.
        /// Safely handles both IWebElement contexts and IWebDriver contexts without illegal argument types.
        /// </summary>
        public IWebElement? FindTextBox(ISearchContext context, string semanticLabel)
        {
            try
            {
                var aliases = GetFieldAliases(semanticLabel).Select(a => a.ToLowerInvariant()).ToArray();
                var rootElement = context as IWebElement;

                const string script = @"
                    const root = arguments[0] || document;
                    const aliases = arguments[1];
                    const inputs = Array.from(root.querySelectorAll(""input:not([type='hidden']):not([type='radio']):not([type='checkbox']):not([type='submit']):not([type='button']), textarea""));
                    if (inputs.length === 0) return null;

                    // 1. Direct attribute match
                    for (const el of inputs) {
                        const id = (el.id || '').toLowerCase();
                        const name = (el.name || '').toLowerCase();
                        const ph = (el.placeholder || '').toLowerCase();
                        const aria = (el.getAttribute('aria-label') || '').toLowerCase();
                        for (const a of aliases) {
                            if (id.includes(a) || name.includes(a) || ph.includes(a) || aria.includes(a)) {
                                return el;
                            }
                        }
                    }

                    // 2. Correlate with <label>
                    const labels = root.querySelectorAll('label');
                    for (const l of labels) {
                        const text = (l.textContent || '').toLowerCase();
                        for (const a of aliases) {
                            if (text.includes(a)) {
                                if (l.htmlFor) {
                                    try {
                                        const target = root.querySelector('#' + CSS.escape(l.htmlFor));
                                        if (target) return target;
                                    } catch(e) {}
                                }
                                const nested = l.querySelector('input, textarea');
                                if (nested) return nested;
                                let next = l.nextElementSibling;
                                while (next) {
                                    if (next.matches('input, textarea')) return next;
                                    const inner = next.querySelector('input, textarea');
                                    if (inner) return inner;
                                    next = next.nextElementSibling;
                                }
                                if (l.parentElement) {
                                    const pInput = l.parentElement.querySelector('input, textarea');
                                    if (pInput) return pInput;
                                }
                            }
                        }
                    }

                    return inputs.length === 1 ? inputs[0] : null;
                ";

                return _js.ExecuteScript(script, (object?)rootElement, aliases) as IWebElement;
            }
            catch (StaleElementReferenceException)
            {
                return null;
            }
        }

        /// <summary>
        /// Locates a radio button in a single browser-side roundtrip.
        /// </summary>
        public IWebElement? FindRadioButton(ISearchContext context, string optionText, string? groupLabel = "Gender")
        {
            try
            {
                var rootElement = context as IWebElement;

                const string script = @"
                    const root = arguments[0] || document;
                    const opt = (arguments[1] || '').trim().toLowerCase();
                    const radios = Array.from(root.querySelectorAll(""input[type='radio']""));
                    if (radios.length === 0) return null;

                    // 1. Value or ID
                    for (const r of radios) {
                        const val = (r.value || '').trim().toLowerCase();
                        const id = (r.id || '').trim().toLowerCase();
                        if (val === opt || id === opt || id.includes(opt)) return r;
                    }

                    // 2. Sibling text & text nodes
                    for (const r of radios) {
                        const text = ((r.nextSibling ? r.nextSibling.textContent : '') + ' ' +
                                      (r.nextElementSibling ? r.nextElementSibling.innerText : '') + ' ' +
                                      (r.parentElement ? r.parentElement.innerText : '')).toLowerCase();
                        if (text.includes(opt)) return r;
                    }

                    // 3. Label correlation
                    const labels = root.querySelectorAll('label');
                    for (const l of labels) {
                        if ((l.textContent || '').toLowerCase().includes(opt)) {
                            if (l.htmlFor) {
                                try {
                                    const target = root.querySelector('#' + CSS.escape(l.htmlFor));
                                    if (target) return target;
                                } catch(e) {}
                            }
                            const nested = l.querySelector(""input[type='radio']"");
                            if (nested) return nested;
                        }
                    }

                    return radios[0];
                ";

                return _js.ExecuteScript(script, (object?)rootElement, optionText) as IWebElement;
            }
            catch (StaleElementReferenceException)
            {
                return null;
            }
        }

        /// <summary>
        /// Locates a dropdown in a single browser-side roundtrip.
        /// </summary>
        public IWebElement? FindDropdown(ISearchContext context, string semanticLabel)
        {
            try
            {
                var rootElement = context as IWebElement;

                const string script = @"
                    const root = arguments[0] || document;
                    const labelText = (arguments[1] || '').toLowerCase();
                    const selects = Array.from(root.querySelectorAll('select'));
                    if (selects.length === 0) return null;

                    const aliases = [labelText, 'state', 'region', 'province'];

                    // 1. Direct attributes
                    for (const s of selects) {
                        const id = (s.id || '').toLowerCase();
                        const name = (s.name || '').toLowerCase();
                        const aria = (s.getAttribute('aria-label') || '').toLowerCase();
                        for (const a of aliases) {
                            if (id.includes(a) || name.includes(a) || aria.includes(a)) return s;
                        }
                    }

                    // 2. Label correlation
                    const labels = root.querySelectorAll('label');
                    for (const l of labels) {
                        const text = (l.textContent || '').toLowerCase();
                        for (const a of aliases) {
                            if (text.includes(a)) {
                                if (l.htmlFor) {
                                    try {
                                        const target = root.querySelector('#' + CSS.escape(l.htmlFor));
                                        if (target) return target;
                                    } catch(e) {}
                                }
                                const nested = l.querySelector('select');
                                if (nested) return nested;
                                let next = l.nextElementSibling;
                                while (next) {
                                    if (next.matches('select')) return next;
                                    const inner = next.querySelector('select');
                                    if (inner) return inner;
                                    next = next.nextElementSibling;
                                }
                            }
                        }
                    }

                    return selects[0];
                ";

                return _js.ExecuteScript(script, (object?)rootElement, semanticLabel) as IWebElement;
            }
            catch (StaleElementReferenceException)
            {
                return null;
            }
        }

        /// <summary>
        /// Locates a checkbox in a single browser-side roundtrip.
        /// </summary>
        public IWebElement? FindCheckbox(ISearchContext context, string checkboxLabel)
        {
            try
            {
                var rootElement = context as IWebElement;

                const string script = @"
                    const root = arguments[0] || document;
                    const labelText = (arguments[1] || '').trim().toLowerCase();
                    const boxes = Array.from(root.querySelectorAll(""input[type='checkbox']""));
                    if (boxes.length === 0) return null;

                    // 1. Direct attributes
                    for (const b of boxes) {
                        const id = (b.id || '').toLowerCase();
                        const val = (b.value || '').toLowerCase();
                        const name = (b.name || '').toLowerCase();
                        if (id.includes(labelText) || val.includes(labelText) || name.includes(labelText)) return b;
                    }

                    // 2. Sibling text
                    for (const b of boxes) {
                        const text = ((b.nextSibling ? b.nextSibling.textContent : '') + ' ' +
                                      (b.nextElementSibling ? b.nextElementSibling.innerText : '') + ' ' +
                                      (b.parentElement ? b.parentElement.innerText : '')).toLowerCase();
                        if (text.includes(labelText)) return b;
                    }

                    // 3. Label correlation
                    const labels = root.querySelectorAll('label');
                    for (const l of labels) {
                        if ((l.textContent || '').toLowerCase().includes(labelText)) {
                            if (l.htmlFor) {
                                try {
                                    const target = root.querySelector('#' + CSS.escape(l.htmlFor));
                                    if (target) return target;
                                } catch(e) {}
                            }
                            const nested = l.querySelector(""input[type='checkbox']"");
                            if (nested) return nested;
                        }
                    }

                    return null;
                ";

                return _js.ExecuteScript(script, (object?)rootElement, checkboxLabel) as IWebElement;
            }
            catch (StaleElementReferenceException)
            {
                return null;
            }
        }

        public bool FillRichTextEditor(ISearchContext context, string semanticLabel, string content)
        {
            try
            {
                var rootElement = context as IWebElement;

                const string script = @"
                    const root = arguments[0] || document;
                    const content = arguments[1];
                    const editable = root.querySelector(""div.note-editable, div[contenteditable='true'], .wysiwyg-editor"");
                    if (editable) {
                        editable.innerHTML = '<p>' + content + '</p>';
                        editable.dispatchEvent(new Event('input', { bubbles: true }));
                        editable.dispatchEvent(new Event('change', { bubbles: true }));
                        return true;
                    }
                    return false;
                ";

                bool filled = Convert.ToBoolean(_js.ExecuteScript(script, (object?)rootElement, content));
                if (filled) return true;

                var textarea = FindTextBox(context, semanticLabel);
                if (textarea != null)
                {
                    textarea.Clear();
                    textarea.SendKeys(content);
                    return true;
                }
            }
            catch { }

            return false;
        }

        public IWebElement? FindSubmitButton(ISearchContext context)
        {
            try
            {
                var rootElement = context as IWebElement;

                const string script = @"
                    const root = arguments[0] || document;
                    const btns = Array.from(root.querySelectorAll(""button, input[type='submit'], input[type='button'], a.btn""));
                    const keywords = ['submit', 'save', 'send', 'register', 'done'];
                    for (const b of btns) {
                        const text = (b.innerText || b.value || b.getAttribute('aria-label') || '').toLowerCase();
                        for (const k of keywords) {
                            if (text.includes(k)) return b;
                        }
                    }
                    return btns[0] || null;
                ";

                return _js.ExecuteScript(script, (object?)rootElement) as IWebElement;
            }
            catch (StaleElementReferenceException)
            {
                return null;
            }
        }

        #endregion

        #region Field Verification Helpers

        public bool VerifyInputText(IWebElement input, string expected)
        {
            try
            {
                var actual = input.GetAttribute("value") ?? "";
                return string.Equals(actual.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        public bool VerifyRadioSelected(IWebElement radio)
        {
            try
            {
                if (radio.Selected) return true;
                var ariaChecked = radio.GetAttribute("aria-checked");
                return string.Equals(ariaChecked, "true", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        public bool VerifyDropdownSelected(IWebElement selectElement, string expected)
        {
            try
            {
                var select = new SelectElement(selectElement);
                var selectedText = select.SelectedOption?.Text?.Trim() ?? "";
                var selectedValue = select.SelectedOption?.GetAttribute("value")?.Trim() ?? "";

                return ContainsIgnoreCase(selectedText, expected) || ContainsIgnoreCase(selectedValue, expected);
            }
            catch { return false; }
        }

        #endregion

        #region Private Helpers

        private static string[] GetFieldAliases(string semanticLabel)
        {
            if (semanticLabel.Equals("First Name", StringComparison.OrdinalIgnoreCase))
                return new[] { "First Name", "firstname", "fname", "first_name", "first-name", "given-name", "name" };
            if (semanticLabel.Equals("Last Name", StringComparison.OrdinalIgnoreCase))
                return new[] { "Last Name", "lastname", "lname", "last_name", "last-name", "surname", "family-name" };
            if (semanticLabel.Equals("Date of Birth", StringComparison.OrdinalIgnoreCase))
                return new[] { "Date of Birth", "dob", "birthdate", "birthday", "yyyy-mm-dd" };
            if (semanticLabel.Equals("Mobile Number", StringComparison.OrdinalIgnoreCase) || 
                semanticLabel.Equals("Mobile #", StringComparison.OrdinalIgnoreCase) ||
                semanticLabel.Equals("Mobile", StringComparison.OrdinalIgnoreCase))
                return new[] { "Mobile #", "Mobile Number", "mobile", "phone", "contact", "cell" };
            if (semanticLabel.Equals("Email", StringComparison.OrdinalIgnoreCase))
                return new[] { "Email", "email", "e-mail" };
            if (semanticLabel.Equals("Country", StringComparison.OrdinalIgnoreCase))
                return new[] { "Country", "nation" };
            if (semanticLabel.Equals("About Yourself", StringComparison.OrdinalIgnoreCase))
                return new[] { "About Yourself", "about", "bio" };
            if (semanticLabel.Equals("Description", StringComparison.OrdinalIgnoreCase))
                return new[] { "Description", "desc", "details" };
            if (semanticLabel.Equals("Username", StringComparison.OrdinalIgnoreCase))
                return new[] { "Username", "user", "user_name" };
            if (semanticLabel.Equals("Password", StringComparison.OrdinalIgnoreCase))
                return new[] { "Password", "pwd", "pass" };
            if (semanticLabel.Equals("Confirm Password", StringComparison.OrdinalIgnoreCase))
                return new[] { "Confirm Password", "confirmpassword", "confirm_password" };

            return new[] { semanticLabel };
        }

        private static bool ContainsIgnoreCase(string? source, string target)
        {
            if (string.IsNullOrWhiteSpace(source)) return false;
            return source.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool EqualsIgnoreCase(string? source, string target)
        {
            if (string.IsNullOrWhiteSpace(source)) return false;
            return string.Equals(source.Trim(), target.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
