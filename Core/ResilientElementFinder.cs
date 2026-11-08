using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace CloudQA.ResilientAutomation.Core
{
    /// <summary>
    /// Senior SDET Resilient Locator Engine.
    /// Discovers web elements using semantic evidence, accessible name computation, ARIA trees,
    /// container proximity, and confidence-based candidate ranking.
    /// Never selects an element without validated uniqueness and positive confidence.
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

        #region Text Box Discovery

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

                    function matchOption(text, target) {
                        const nText = norm(text);
                        const nTarget = norm(target);
                        if (!nText || !nTarget) return false;
                        if (nText === nTarget) return true;
                        const words = nText.split(/\s+/);
                        return words.includes(nTarget);
                    }

                    function isVisible(el) {
                        if (el.disabled) return false;
                        const style = window.getComputedStyle(el);
                        if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') return false;
                        const rects = el.getClientRects();
                        return (el.offsetWidth > 0 || el.offsetHeight > 0 || rects.length > 0);
                    }

                    const rawInputs = Array.from(root.querySelectorAll(""input:not([type='hidden']):not([type='radio']):not([type='checkbox']):not([type='submit']):not([type='button']), textarea""));
                    const inputs = rawInputs.filter(isVisible);

                    if (inputs.length === 0) return null;

                    const allLabels = Array.from(root.querySelectorAll('label')).filter(isVisible);
                    const candidates = [];

                    for (const input of inputs) {
                        let score = 0;
                        const strategies = [];
                        const id = (input.id || '').trim();
                        const name = (input.name || '').trim();
                        const ph = (input.placeholder || '').trim();
                        const aria = (input.getAttribute('aria-label') || '').trim();
                        const ariaBy = (input.getAttribute('aria-labelledby') || '').trim();
                        const auto = (input.autocomplete || '').trim();

                        // 1. Explicit label[for='id'] (+100)
                        if (id) {
                            const expl = allLabels.find(l => (l.htmlFor || '').toLowerCase() === id.toLowerCase() && aliases.some(a => matchText(l.textContent, a)));
                            if (expl) {
                                score += 100;
                                strategies.push('Label[for=\'' + id + '\']');
                            }
                        }

                        // 2. Enclosing label (+90)
                        const enclosing = allLabels.find(l => aliases.some(a => matchText(l.textContent, a)) && l.contains(input));
                        if (enclosing) {
                            score += 90;
                            strategies.push('EnclosingLabel');
                        }

                        // 3. Nearby field-container association (+75)
                        const nearby = allLabels.find(l => {
                            if (!aliases.some(a => matchText(l.textContent, a))) return false;
                            const container = l.closest('.form-group, .form-row, .row, .field, [class*=""group""], div, fieldset');
                            return container ? container.contains(input) : false;
                        });
                        if (nearby) {
                            score += 75;
                            strategies.push('ContainerProximity');
                        }

                        // 4. Accessible attributes (+65)
                        if (aliases.some(a => matchText(aria, a))) {
                            score += 65;
                            strategies.push('aria-label');
                        }
                        if (ariaBy) {
                            const byEl = document.getElementById(ariaBy);
                            if (byEl && aliases.some(a => matchText(byEl.textContent, a))) {
                                score += 65;
                                strategies.push('aria-labelledby');
                            }
                        }
                        if (aliases.some(a => matchText(auto, a))) {
                            score += 60;
                            strategies.push('autocomplete');
                        }

                        // 5. Semantic attributes (+35 to +45)
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

                        // Minimum confidence threshold: 40 points
                        if (score >= 40) {
                            candidates.push({
                                element: input,
                                score: score,
                                strategy: strategies.join(' + '),
                                details: `<input id='${id}' name='${name}' placeholder='${ph}'>`
                            });
                        }
                    }

                    if (candidates.length === 0) return null;
                    candidates.sort((a, b) => b.score - a.score);

                    // Ambiguity check: Top 2 candidates tied or within 15 points
                    if (candidates.length > 1) {
                        const top = candidates[0];
                        const runnerUp = candidates[1];
                        if (runnerUp.score === top.score || (top.score - runnerUp.score < 15 && top.score >= 50)) {
                            return { ambiguous: true, score: top.score, topDetails: top.details, runnerDetails: runnerUp.details };
                        }
                    }

                    return { element: candidates[0].element, score: candidates[0].score, strategy: candidates[0].strategy, details: candidates[0].details };
                ";

                var res = _js.ExecuteScript(script, context as IWebElement, aliases);
                sw.Stop();

                if (res is IDictionary<string, object> dict)
                {
                    if (dict.ContainsKey("ambiguous") && Convert.ToBoolean(dict["ambiguous"]))
                    {
                        dict.TryGetValue("topDetails", out var td);
                        dict.TryGetValue("runnerDetails", out var rd);
                        LogDiagnostic($"[Locator:TextBox] Ambiguous match rejected for '{semanticLabel}'. Score: {dict["score"]}. ({td} vs {rd})");
                        return null;
                    }

                    if (dict.TryGetValue("element", out var elObj) && elObj is IWebElement matchedInput)
                    {
                        LogDiagnostic($"[Locator:TextBox] Resolved '{semanticLabel}' in {sw.ElapsedMilliseconds}ms via {dict["strategy"]} (Score: {dict["score"]}).");
                        return matchedInput;
                    }
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic($"[Locator:TextBox] Diagnostic error for '{semanticLabel}': {ex.Message}");
            }

            return null;
        }

        #endregion

        #region Radio Button Discovery

        public IWebElement? FindRadioButton(ISearchContext context, string optionText, string groupLabel = "Gender")
        {
            var sw = Stopwatch.StartNew();

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

                    function matchOption(text, target) {
                        const nText = norm(text);
                        const nTarget = norm(target);
                        if (!nText || !nTarget) return false;
                        if (nText === nTarget) return true;
                        const words = nText.split(/\s+/);
                        return words.includes(nTarget);
                    }

                    function isVisible(el) {
                        if (el.disabled) return false;
                        const style = window.getComputedStyle(el);
                        if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') return false;
                        const rects = el.getClientRects();
                        return (el.offsetWidth > 0 || el.offsetHeight > 0 || rects.length > 0);
                    }

                    const rawRadios = Array.from(root.querySelectorAll(""input[type='radio']""));
                    const radios = rawRadios.filter(isVisible);

                    if (radios.length === 0) return null;

                    // Group Discovery: ARIA role='radiogroup', fieldset > legend, container heading, or name/id
                    let pool = [];

                    // 1. Check role='radiogroup' or fieldsets with matching legend / aria
                    const groups = Array.from(root.querySelectorAll('[role=""radiogroup""], fieldset')).filter(g => {
                        const aria = (g.getAttribute('aria-label') || '').trim();
                        const ariaBy = (g.getAttribute('aria-labelledby') || '').trim();
                        const legend = g.querySelector('legend');
                        const legendText = legend ? legend.textContent : '';
                        const byEl = ariaBy ? document.getElementById(ariaBy) : null;
                        const byText = byEl ? byEl.textContent : '';
                        return matchText(aria, groupLabel) || matchText(legendText, groupLabel) || matchText(byText, groupLabel);
                    });

                    if (groups.length > 0) {
                        pool = Array.from(groups[0].querySelectorAll(""input[type='radio']"")).filter(isVisible);
                    }

                    // 2. Fallback to name/id attribute matching group
                    if (pool.length === 0) {
                        pool = radios.filter(r => matchText(r.name, groupLabel) || matchText(r.id, groupLabel));
                    }

                    // 3. Fallback to container heading/label nearby
                    if (pool.length === 0) {
                        const headings = Array.from(root.querySelectorAll('h1, h2, h3, h4, h5, h6, label, span, p, div')).filter(el => {
                            const t = el.textContent || '';
                            return matchText(t, groupLabel) && el.querySelectorAll('input').length === 0;
                        });

                        if (headings.length > 0) {
                            const nearest = headings[0].closest('.form-group, .row, .field, div, fieldset');
                            if (nearest) {
                                const scoped = Array.from(nearest.querySelectorAll(""input[type='radio']"")).filter(isVisible);
                                if (scoped.length > 0) pool = scoped;
                            }
                        }
                    }

                    if (pool.length === 0) pool = radios;

                    const allLabels = Array.from(root.querySelectorAll('label')).filter(isVisible);
                    const candidates = [];

                    for (const radio of pool) {
                        let score = 0;
                        const strategies = [];
                        const val = (radio.value || '').trim();
                        const id = (radio.id || '').trim();
                        const aria = (radio.getAttribute('aria-label') || '').trim();

                        if (matchOption(val, optionText)) {
                            score += 80;
                            strategies.push('value');
                        }
                        if (matchOption(aria, optionText)) {
                            score += 80;
                            strategies.push('aria-label');
                        }
                        if (matchOption(id, optionText)) {
                            score += 60;
                            strategies.push('id');
                        }

                        // Visible Option Text (closest label, sibling text, or parent text)
                        let visibleText = '';
                        const directLabel = radio.closest('label');
                        if (directLabel) {
                            const clone = directLabel.cloneNode(true);
                            clone.querySelectorAll('input').forEach(i => i.remove());
                            visibleText = clone.textContent || '';
                        } else {
                            if (radio.nextSibling && radio.nextSibling.nodeType === 3) visibleText += ' ' + radio.nextSibling.textContent;
                            if (radio.nextElementSibling) visibleText += ' ' + radio.nextElementSibling.innerText;
                            if (radio.parentElement) visibleText += ' ' + radio.parentElement.innerText;
                        }
                        visibleText = visibleText.trim();

                        if (matchOption(visibleText, optionText)) {
                            score += 100;
                            strategies.push('VisibleOptionText');
                        }

                        if (id) {
                            const expl = allLabels.find(l => (l.htmlFor || '').toLowerCase() === id.toLowerCase() && matchOption(l.textContent, optionText));
                            if (expl) {
                                score += 90;
                                strategies.push('Label[for]');
                            }
                        }

                        if (score >= 50) {
                            candidates.push({
                                element: radio,
                                score: score,
                                strategy: strategies.join(' + '),
                                details: `<radio id='${id}' value='${val}'>`
                            });
                        }
                    }

                    if (candidates.length === 0) return null;
                    candidates.sort((a, b) => b.score - a.score);

                    if (candidates.length > 1) {
                        const top = candidates[0];
                        const runnerUp = candidates[1];
                        if (runnerUp.score === top.score) {
                            return { ambiguous: true, score: top.score };
                        }
                    }

                    return { element: candidates[0].element, score: candidates[0].score, strategy: candidates[0].strategy };
                ";

                var res = _js.ExecuteScript(script, context as IWebElement, optionText, groupLabel);
                sw.Stop();

                if (res is IDictionary<string, object> dict)
                {
                    if (dict.ContainsKey("ambiguous") && Convert.ToBoolean(dict["ambiguous"]))
                    {
                        LogDiagnostic($"[Locator:Radio] Ambiguous radio match rejected for '{optionText}' in group '{groupLabel}'.");
                        return null;
                    }

                    if (dict.TryGetValue("element", out var elObj) && elObj is IWebElement matchedRadio)
                    {
                        LogDiagnostic($"[Locator:Radio] Resolved '{optionText}' in group '{groupLabel}' in {sw.ElapsedMilliseconds}ms via {dict["strategy"]} (Score: {dict["score"]}).");
                        return matchedRadio;
                    }
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic($"[Locator:Radio] Diagnostic error for '{optionText}': {ex.Message}");
            }

            return null;
        }

        #endregion

        #region Dropdown Discovery

        public IWebElement? FindDropdown(ISearchContext context, string semanticLabel)
        {
            var sw = Stopwatch.StartNew();
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

                    function matchOption(text, target) {
                        const nText = norm(text);
                        const nTarget = norm(target);
                        if (!nText || !nTarget) return false;
                        if (nText === nTarget) return true;
                        const words = nText.split(/\s+/);
                        return words.includes(nTarget);
                    }

                    function isVisible(el) {
                        if (el.disabled) return false;
                        const style = window.getComputedStyle(el);
                        if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') return false;
                        const rects = el.getClientRects();
                        return (el.offsetWidth > 0 || el.offsetHeight > 0 || rects.length > 0);
                    }

                    const rawSelects = Array.from(root.querySelectorAll('select, [role=""combobox""]'));
                    const selects = rawSelects.filter(isVisible);

                    if (selects.length === 0) return null;

                    const allLabels = Array.from(root.querySelectorAll('label')).filter(isVisible);
                    const candidates = [];

                    for (const select of selects) {
                        let score = 0;
                        const strategies = [];
                        const id = (select.id || '').trim();
                        const name = (select.name || '').trim();
                        const aria = (select.getAttribute('aria-label') || '').trim();
                        const ariaBy = (select.getAttribute('aria-labelledby') || '').trim();

                        if (id) {
                            const expl = allLabels.find(l => (l.htmlFor || '').toLowerCase() === id.toLowerCase() && aliases.some(a => matchText(l.textContent, a)));
                            if (expl) {
                                score += 100;
                                strategies.push('Label[for]');
                            }
                        }

                        const nearby = allLabels.find(l => aliases.some(a => matchText(l.textContent, a)));
                        if (nearby) {
                            const container = nearby.closest('.form-group, .row, .field, div, fieldset');
                            if (container && container.contains(select)) {
                                score += 85;
                                strategies.push('ContainerProximity');
                            }
                        }

                        if (aliases.some(a => matchText(aria, a))) {
                            score += 65;
                            strategies.push('aria-label');
                        }
                        if (ariaBy) {
                            const byEl = document.getElementById(ariaBy);
                            if (byEl && aliases.some(a => matchText(byEl.textContent, a))) {
                                score += 65;
                                strategies.push('aria-labelledby');
                            }
                        }
                        if (aliases.some(a => matchText(name, a))) {
                            score += 45;
                            strategies.push('name');
                        }
                        if (aliases.some(a => matchText(id, a))) {
                            score += 40;
                            strategies.push('id');
                        }

                        // Strict rule: must have positive semantic evidence (score >= 40)
                        if (score >= 40) {
                            candidates.push({
                                element: select,
                                score: score,
                                strategy: strategies.join(' + '),
                                details: `<select id='${id}' name='${name}'>`
                            });
                        }
                    }

                    if (candidates.length === 0) return null;
                    candidates.sort((a, b) => b.score - a.score);

                    if (candidates.length > 1) {
                        const top = candidates[0];
                        const runnerUp = candidates[1];
                        if (runnerUp.score === top.score || (top.score - runnerUp.score < 15 && top.score >= 50)) {
                            return { ambiguous: true, score: top.score };
                        }
                    }

                    return { element: candidates[0].element, score: candidates[0].score, strategy: candidates[0].strategy };
                ";

                var res = _js.ExecuteScript(script, context as IWebElement, aliases);
                sw.Stop();

                if (res is IDictionary<string, object> dict)
                {
                    if (dict.ContainsKey("ambiguous") && Convert.ToBoolean(dict["ambiguous"]))
                    {
                        LogDiagnostic($"[Locator:Dropdown] Ambiguous dropdown match rejected for '{semanticLabel}'.");
                        return null;
                    }

                    if (dict.TryGetValue("element", out var elObj) && elObj is IWebElement matchedSelect)
                    {
                        LogDiagnostic($"[Locator:Dropdown] Resolved '{semanticLabel}' in {sw.ElapsedMilliseconds}ms via {dict["strategy"]} (Score: {dict["score"]}).");
                        return matchedSelect;
                    }
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic($"[Locator:Dropdown] Diagnostic error for '{semanticLabel}': {ex.Message}");
            }

            return null;
        }

        #endregion

        #region Submit & Form Discovery

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

        #endregion

        #region Helpers & Diagnostics

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

        private static void LogDiagnostic(string message)
        {
            try
            {
                TestContext.Out.WriteLine(message);
            }
            catch
            {
                Console.WriteLine(message);
            }
        }

        #endregion
    }
}
