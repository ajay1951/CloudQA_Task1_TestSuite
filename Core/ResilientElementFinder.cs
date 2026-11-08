using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace CloudQA.ResilientAutomation.Core
{
    /// <summary>
    /// Resilient locator engine that discovers web elements using semantic evidence,
    /// label associations, container proximity, and confidence scoring.
    /// Does not rely on fixed XPath or CSS selectors.
    /// </summary>
    public class ResilientElementFinder
    {
        private readonly IWebDriver _driver;
        private readonly WebDriverWait _wait;
        private readonly IJavaScriptExecutor _js;

        public ResilientElementFinder(IWebDriver driver, int timeoutSeconds = 15)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));
            _wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
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
            ScrollIntoCenter(element);
            try
            {
                element.Click();
            }
            catch (WebDriverException)
            {
                _js.ExecuteScript("arguments[0].click();", element);
            }
        }

        public IWebElement? FindTextBox(ISearchContext context, string semanticLabel)
        {
            var sw = Stopwatch.StartNew();
            var aliases = GetFieldAliases(semanticLabel);

            try
            {
                const string script = @"
                    const root = (arguments[0] && arguments[0].querySelectorAll) ? arguments[0] : document;
                    const aliases = arguments[1];

                    function norm(s) {
                        if (!s) return '';
                        return s.toString().trim().toLowerCase().replace(/[-_]/g, ' ').replace(/\s+/g, ' ');
                    }

                    function matchText(text, target) {
                        const nText = norm(text);
                        const nTarget = norm(target);
                        if (!nText || !nTarget) return false;
                        return nText === nTarget || nText.includes(nTarget) || nTarget.includes(nText);
                    }

                    const rawInputs = Array.from(root.querySelectorAll(""input:not([type='hidden']):not([type='radio']):not([type='checkbox']):not([type='submit']):not([type='button']), textarea""));
                    const inputs = rawInputs.filter(el => {
                        if (el.disabled) return false;
                        const style = window.getComputedStyle(el);
                        if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') return false;
                        return el.offsetWidth > 0 || el.offsetHeight > 0 || el.getClientRects().length > 0;
                    });

                    if (inputs.length === 0) return null;

                    const labels = Array.from(root.querySelectorAll('label')).filter(l => {
                        const style = window.getComputedStyle(l);
                        return style.display !== 'none' && style.visibility !== 'hidden';
                    });

                    const candidates = [];

                    for (const input of inputs) {
                        let score = 0;
                        const strategies = [];
                        const id = (input.id || '').trim();
                        const name = (input.name || '').trim();
                        const ph = (input.placeholder || '').trim();
                        const aria = (input.getAttribute('aria-label') || '').trim();
                        const auto = (input.autocomplete || '').trim();

                        if (id) {
                            const expl = labels.find(l => (l.htmlFor || '').toLowerCase() === id.toLowerCase() && aliases.some(a => matchText(l.textContent, a)));
                            if (expl) {
                                score += 100;
                                strategies.push('Label[for=\'' + id + '\']');
                            }
                        }

                        const enclosing = labels.find(l => aliases.some(a => matchText(l.textContent, a)) && l.contains(input));
                        if (enclosing) {
                            score += 90;
                            strategies.push('EnclosingLabel');
                        }

                        const matchingNearby = labels.find(l => aliases.some(a => matchText(l.textContent, a)));
                        if (matchingNearby) {
                            const container = matchingNearby.closest('.form-group, .form-row, .row, .field, [class*=""group""], div');
                            if (container && container.contains(input)) {
                                score += 75;
                                strategies.push('ContainerProximity');
                            }
                        }

                        if (aliases.some(a => matchText(aria, a))) {
                            score += 65;
                            strategies.push('aria-label');
                        }
                        if (aliases.some(a => matchText(auto, a))) {
                            score += 60;
                            strategies.push('autocomplete');
                        }

                        if (aliases.some(a => matchText(ph, a))) {
                            score += 45;
                            strategies.push('placeholder');
                        }
                        if (aliases.some(a => matchText(name, a))) {
                            score += 40;
                            strategies.push('name');
                        }
                        if (aliases.some(a => matchText(id, a))) {
                            score += 35;
                            strategies.push('id');
                        }

                        if (score > 0) {
                            candidates.push({ element: input, score: score, strategy: strategies.join(' + ') });
                        }
                    }

                    if (candidates.length === 0) return null;
                    candidates.sort((a, b) => b.score - a.score);

                    if (candidates.length > 1 && candidates[1].score === candidates[0].score && candidates[0].score >= 50) {
                        return { ambiguous: true, score: candidates[0].score };
                    }

                    return { element: candidates[0].element, score: candidates[0].score, strategy: candidates[0].strategy };
                ";

                var res = _js.ExecuteScript(script, context as IWebElement, aliases);
                if (res is IDictionary<string, object> dict)
                {
                    sw.Stop();
                    if (dict.ContainsKey("ambiguous") && Convert.ToBoolean(dict["ambiguous"]))
                    {
                        return null;
                    }

                    if (dict.TryGetValue("element", out var elObj) && elObj is IWebElement matchedInput)
                    {
                        return matchedInput;
                    }
                }
            }
            catch { }

            return null;
        }

        public IWebElement? FindRadioButton(ISearchContext context, string optionText, string groupLabel = "Gender")
        {
            try
            {
                const string script = @"
                    const root = (arguments[0] && arguments[0].querySelectorAll) ? arguments[0] : document;
                    const optionText = arguments[1];
                    const groupLabel = arguments[2];

                    function norm(s) {
                        if (!s) return '';
                        return s.toString().trim().toLowerCase().replace(/[-_]/g, ' ').replace(/\s+/g, ' ');
                    }

                    function matchText(text, target) {
                        const nText = norm(text);
                        const nTarget = norm(target);
                        if (!nText || !nTarget) return false;
                        return nText === nTarget || nText.includes(nTarget) || nTarget.includes(nText);
                    }

                    const rawRadios = Array.from(root.querySelectorAll(""input[type='radio']""));
                    const radios = rawRadios.filter(r => {
                        if (r.disabled) return false;
                        const style = window.getComputedStyle(r);
                        return style.display !== 'none' && style.visibility !== 'hidden';
                    });

                    if (radios.length === 0) return null;

                    let pool = radios.filter(r => matchText(r.name, groupLabel) || matchText(r.id, groupLabel));
                    if (pool.length === 0) pool = radios;

                    const labels = Array.from(root.querySelectorAll('label'));
                    const candidates = [];

                    for (const radio of pool) {
                        let score = 0;
                        const strategies = [];
                        const val = (radio.value || '').trim();
                        const id = (radio.id || '').trim();
                        const aria = (radio.getAttribute('aria-label') || '').trim();

                        if (matchText(val, optionText)) {
                            score += 80;
                            strategies.push('value');
                        }
                        if (matchText(aria, optionText)) {
                            score += 80;
                            strategies.push('aria-label');
                        }
                        if (matchText(id, optionText)) {
                            score += 60;
                            strategies.push('id');
                        }

                        let visibleText = '';
                        if (radio.nextSibling && radio.nextSibling.nodeType === 3) visibleText += ' ' + radio.nextSibling.textContent;
                        if (radio.nextElementSibling) visibleText += ' ' + radio.nextElementSibling.innerText;
                        if (radio.parentElement) visibleText += ' ' + radio.parentElement.innerText;
                        visibleText = visibleText.trim();

                        if (matchText(visibleText, optionText)) {
                            score += 100;
                            strategies.push('VisibleOptionText');
                        }

                        if (id) {
                            const lbl = labels.find(l => (l.htmlFor || '').toLowerCase() === id.toLowerCase() && matchText(l.textContent, optionText));
                            if (lbl) {
                                score += 90;
                                strategies.push('Label[for]');
                            }
                        }

                        if (score > 0) {
                            candidates.push({ element: radio, score: score, strategy: strategies.join(' + ') });
                        }
                    }

                    if (candidates.length === 0) return null;
                    candidates.sort((a, b) => b.score - a.score);
                    return { element: candidates[0].element, score: candidates[0].score };
                ";

                var res = _js.ExecuteScript(script, context as IWebElement, optionText, groupLabel);
                if (res is IDictionary<string, object> dict && dict.TryGetValue("element", out var elObj) && elObj is IWebElement matchedRadio)
                {
                    return matchedRadio;
                }
            }
            catch { }

            return null;
        }

        public IWebElement? FindDropdown(ISearchContext context, string semanticLabel)
        {
            var aliases = new[] { semanticLabel, "state", "province", "region" };

            try
            {
                const string script = @"
                    const root = (arguments[0] && arguments[0].querySelectorAll) ? arguments[0] : document;
                    const aliases = arguments[1];

                    function norm(s) {
                        if (!s) return '';
                        return s.toString().trim().toLowerCase().replace(/[-_]/g, ' ').replace(/\s+/g, ' ');
                    }

                    function matchText(text, target) {
                        const nText = norm(text);
                        const nTarget = norm(target);
                        if (!nText || !nTarget) return false;
                        return nText === nTarget || nText.includes(nTarget) || nTarget.includes(nText);
                    }

                    const rawSelects = Array.from(root.querySelectorAll('select, [role=""combobox""]'));
                    const selects = rawSelects.filter(s => {
                        if (s.disabled) return false;
                        const style = window.getComputedStyle(s);
                        return style.display !== 'none' && style.visibility !== 'hidden';
                    });

                    if (selects.length === 0) return null;

                    const labels = Array.from(root.querySelectorAll('label')).filter(l => {
                        const style = window.getComputedStyle(l);
                        return style.display !== 'none' && style.visibility !== 'hidden';
                    });

                    const candidates = [];

                    for (const select of selects) {
                        let score = 0;
                        const strategies = [];
                        const id = (select.id || '').trim();
                        const name = (select.name || '').trim();
                        const aria = (select.getAttribute('aria-label') || '').trim();

                        if (id) {
                            const lbl = labels.find(l => (l.htmlFor || '').toLowerCase() === id.toLowerCase() && aliases.some(a => matchText(l.textContent, a)));
                            if (lbl) {
                                score += 100;
                                strategies.push('Label[for]');
                            }
                        }

                        const stateLabel = labels.find(l => aliases.some(a => matchText(l.textContent, a)));
                        if (stateLabel) {
                            const container = stateLabel.closest('.form-group, .row, .field, div');
                            if (container && container.contains(select)) {
                                score += 85;
                                strategies.push('ContainerProximity');
                            }
                        }

                        if (aliases.some(a => matchText(aria, a))) score += 50;
                        if (aliases.some(a => matchText(name, a))) score += 45;
                        if (aliases.some(a => matchText(id, a))) score += 40;

                        if (score > 0) {
                            candidates.push({ element: select, score: score, strategy: strategies.join(' + ') });
                        }
                    }

                    if (candidates.length === 0) {
                        if (selects.length === 1) return { element: selects[0], score: 50 };
                        return null;
                    }

                    candidates.sort((a, b) => b.score - a.score);
                    return { element: candidates[0].element, score: candidates[0].score };
                ";

                var res = _js.ExecuteScript(script, context as IWebElement, aliases);
                if (res is IDictionary<string, object> dict && dict.TryGetValue("element", out var elObj) && elObj is IWebElement matchedSelect)
                {
                    return matchedSelect;
                }
            }
            catch { }

            return null;
        }

        public IWebElement? FindSubmitButton(ISearchContext context)
        {
            var buttons = context.FindElements(By.CssSelector("button, input[type='submit'], input[type='button'], a.btn"))
                                 .Where(b => b.Displayed && b.Enabled).ToList();
            var keywords = new[] { "submit", "save", "send", "register", "done" };

            var match = buttons.FirstOrDefault(b =>
            {
                var text = (b.Text ?? "").Trim();
                var val = (b.GetAttribute("value") ?? "").Trim();
                var aria = (b.GetAttribute("aria-label") ?? "").Trim();
                return keywords.Any(k => MatchText(text, k) || MatchText(val, k) || MatchText(aria, k));
            });

            return match ?? buttons.FirstOrDefault();
        }

        public IWebElement? FindCheckbox(ISearchContext context, string label)
        {
            var checkboxes = context.FindElements(By.CssSelector("input[type='checkbox']")).Where(c => c.Displayed).ToList();
            foreach (var cb in checkboxes)
            {
                var val = cb.GetAttribute("value") ?? "";
                var id = cb.GetAttribute("id") ?? "";
                if (MatchText(val, label) || MatchText(id, label)) return cb;

                try
                {
                    var parentText = cb.FindElement(By.XPath("..")).Text;
                    if (MatchText(parentText, label)) return cb;
                }
                catch { }
            }
            return null;
        }

        public void FillRichTextEditor(ISearchContext context, string semanticLabel, string content)
        {
            try
            {
                var editable = context.FindElements(By.CssSelector("div.note-editable, div[contenteditable='true']")).FirstOrDefault(e => e.Displayed);
                if (editable != null)
                {
                    _js.ExecuteScript("arguments[0].innerHTML = '<p>' + arguments[1] + '</p>'; arguments[0].dispatchEvent(new Event('input', { bubbles: true }));", editable, content);
                    return;
                }

                var textarea = FindTextBox(context, semanticLabel);
                if (textarea != null)
                {
                    textarea.Clear();
                    textarea.SendKeys(content);
                }
            }
            catch { }
        }

        public IWebElement? DiscoverMainRootForm(IWebDriver driver)
        {
            var forms = driver.FindElements(By.TagName("form")).Where(f => f.Displayed).ToList();
            if (!forms.Any()) return driver.FindElements(By.TagName("body")).FirstOrDefault();

            var primaryForm = forms.OrderByDescending(f =>
            {
                try { return f.FindElements(By.CssSelector("input, select, textarea")).Count; }
                catch { return 0; }
            }).FirstOrDefault();

            return primaryForm ?? forms.First();
        }

        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return input.Trim().ToLowerInvariant().Replace('_', ' ').Replace('-', ' ');
        }

        public static bool MatchText(string actual, string expected)
        {
            var normActual = Normalize(actual);
            var normExpected = Normalize(expected);
            if (string.IsNullOrEmpty(normActual) || string.IsNullOrEmpty(normExpected)) return false;
            return normActual.Contains(normExpected, StringComparison.OrdinalIgnoreCase);
        }

        private static string[] GetFieldAliases(string semanticLabel)
        {
            var clean = Normalize(semanticLabel);
            if (clean == "first name") return new[] { "first name", "firstname", "fname", "given name", "given-name" };
            if (clean == "last name") return new[] { "last name", "lastname", "lname", "surname", "family name" };
            if (clean == "gender") return new[] { "gender", "sex" };
            if (clean == "state") return new[] { "state", "province", "region" };
            return new[] { semanticLabel };
        }
    }
}
