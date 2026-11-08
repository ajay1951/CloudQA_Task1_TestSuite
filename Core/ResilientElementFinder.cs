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

        public void SafeClick(IWebElement element, bool allowJsFallback = false)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));

            // 1. Verify Enabled state (never click disabled elements)
            if (!element.Enabled || string.Equals(element.GetAttribute("disabled"), "true", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidElementStateException($"Cannot click element <{element.TagName}>: element is disabled.");
            }

            // 2. Scroll into view and verify Displayed state
            ScrollIntoCenter(element);
            if (!element.Displayed)
            {
                throw new ElementNotInteractableException($"Cannot click element <{element.TagName}>: element is not displayed/visible.");
            }

            try
            {
                element.Click();
            }
            catch (ElementClickInterceptedException ex)
            {
                LogDiagnostic($"[SafeClick] ElementClickInterceptedException on <{element.TagName}>: {ex.Message}");
                if (allowJsFallback)
                {
                    LogDiagnostic($"[SafeClick] Applying explicitly controlled JavaScript click fallback on <{element.TagName}>.");
                    _js.ExecuteScript("arguments[0].click();", element);
                }
                else
                {
                    throw new ElementClickInterceptedException($"Click on <{element.TagName}> was intercepted by another element. Disabling JS fallback prevents bypassing UI overlays.", ex);
                }
            }
            catch (ElementNotInteractableException ex)
            {
                LogDiagnostic($"[SafeClick] ElementNotInteractableException on <{element.TagName}>: {ex.Message}");
                if (allowJsFallback)
                {
                    _js.ExecuteScript("arguments[0].click();", element);
                }
                else
                {
                    throw;
                }
            }
        }

        #region Common JavaScript Engine Core

        private const string CommonJsFunctions = @"
            function norm(s) {
                if (!s) return '';
                return s.toString().toLowerCase()
                        .replace(/[^a-z0-9\s]/g, ' ')
                        .replace(/\b(required|optional|mandatory|enter|please|select|choose)\b/g, ' ')
                        .replace(/\s+/g, ' ')
                        .trim();
            }

            function matchText(actual, target) {
                const nActual = norm(actual);
                const nTarget = norm(target);
                if (!nActual || !nTarget) return { match: false, score: 0, exact: false };
                if (nActual === nTarget) return { match: true, score: 100, exact: true };

                const actualTokens = nActual.split(/\s+/);
                const targetTokens = nTarget.split(/\s+/);
                const targetPhrase = targetTokens.join(' ');
                const actualPhrase = actualTokens.join(' ');

                if (actualPhrase === targetPhrase) {
                    return { match: true, score: 100, exact: true };
                }

                // Whole-phrase boundary matching
                const phraseRegex = new RegExp('(^|\\s)' + targetPhrase.replace(/[.*+?^${}()|[\\]\\]/g, '\\$&') + '(\\s|$)', 'i');
                if (phraseRegex.test(actualPhrase)) {
                    const extraTokens = actualTokens.length - targetTokens.length;
                    const score = Math.max(30, 80 - (extraTokens * 30));
                    return { match: true, score: score, exact: false };
                }

                // Token subset containment
                const allContained = targetTokens.every(t => actualTokens.includes(t));
                if (allContained) {
                    const extraTokens = actualTokens.length - targetTokens.length;
                    return { match: true, score: Math.max(25, 70 - (extraTokens * 25)), exact: false };
                }

                return { match: false, score: 0, exact: false };
            }

            function matchOption(text, target) {
                const nText = norm(text);
                const nTarget = norm(target);
                if (!nText || !nTarget) return false;
                if (nText === nTarget) return true;
                const tokens = nText.split(/\s+/);
                return tokens.includes(nTarget);
            }

            function isVisible(el) {
                if (!el || el.disabled) return false;
                const style = window.getComputedStyle(el);
                if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') return false;
                const rects = el.getClientRects();
                return (el.offsetWidth > 0 || el.offsetHeight > 0 || rects.length > 0);
            }

            function evaluateCandidates(candidates, minConfidence = 40) {
                if (!candidates || candidates.length === 0) return null;
                candidates.sort((a, b) => b.score - a.score);
                const top = candidates[0];
                if (top.score < minConfidence) return null;

                if (candidates.length > 1) {
                    const runnerUp = candidates[1];
                    const delta = top.score - runnerUp.score;
                    if (delta < 15) {
                        return {
                            ambiguous: true,
                            topScore: top.score,
                            runnerScore: runnerUp.score,
                            margin: delta,
                            topDetails: top.details,
                            runnerDetails: runnerUp.details
                        };
                    }
                }

                return {
                    element: top.element,
                    score: top.score,
                    strategy: top.strategy,
                    details: top.details
                };
            }
        ";

        private IWebElement? ProcessCandidateResult(object? res, string controlType, string label, Stopwatch sw)
        {
            if (res is IDictionary<string, object> dict)
            {
                if (dict.ContainsKey("ambiguous") && Convert.ToBoolean(dict["ambiguous"]))
                {
                    int topScore = dict.ContainsKey("topScore") ? Convert.ToInt32(dict["topScore"]) : 0;
                    int runnerScore = dict.ContainsKey("runnerScore") ? Convert.ToInt32(dict["runnerScore"]) : 0;
                    int margin = dict.ContainsKey("margin") ? Convert.ToInt32(dict["margin"]) : 0;
                    string td = dict.TryGetValue("topDetails", out var t) ? t?.ToString() ?? "" : "";
                    string rd = dict.TryGetValue("runnerDetails", out var r) ? r?.ToString() ?? "" : "";

                    string errorMsg = $"[Locator:{controlType}] Ambiguous candidate match for '{label}'. Top Score: {topScore}, Runner-up Score: {runnerScore} (Margin: {margin} < 15). Candidate 1: {td} vs Candidate 2: {rd}";
                    LogDiagnostic(errorMsg);
                    throw new AmbiguousMatchException(errorMsg, controlType, topScore, runnerScore, margin, td, rd);
                }

                if (dict.ContainsKey("error") && dict["error"]?.ToString() == "NoReliableGroup")
                {
                    string group = dict.TryGetValue("groupLabel", out var g) ? g?.ToString() ?? label : label;
                    string err = $"Could not reliably scope radio group '{group}'. Fallback to global radio pool rejected to prevent cross-group contamination.";
                    LogDiagnostic($"[Locator:Radio] {err}");
                    throw new NoSuchElementException(err);
                }

                if (dict.TryGetValue("element", out var elObj) && elObj is IWebElement matchedElement)
                {
                    LogDiagnostic($"[Locator:{controlType}] Resolved '{label}' in {sw.ElapsedMilliseconds}ms via {dict["strategy"]} (Score: {dict["score"]}).");
                    return matchedElement;
                }
            }

            return null;
        }

        #endregion

        #region Text Box Discovery

        public IWebElement? FindTextBox(ISearchContext context, string semanticLabel)
        {
            var sw = Stopwatch.StartNew();
            var aliases = GetFieldAliases(semanticLabel);

            try
            {
                string script = CommonJsFunctions + @"
                    const root = (arguments[0] && arguments[0].querySelectorAll) ? arguments[0] : document;
                    const aliases = arguments[1];

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

                        // 1. Explicit label[for='id']
                        if (id) {
                            const expl = allLabels.find(l => (l.htmlFor || '').toLowerCase() === id.toLowerCase() && aliases.some(a => matchText(l.textContent, a).match));
                            if (expl) {
                                const m = aliases.map(a => matchText(expl.textContent, a)).find(r => r.match);
                                score += (m && m.exact) ? 100 : 60;
                                strategies.push('Label[for=\'' + id + '\']');
                            }
                        }

                        // 2. Enclosing label
                        const enclosing = allLabels.find(l => aliases.some(a => matchText(l.textContent, a).match) && l.contains(input));
                        if (enclosing) {
                            const m = aliases.map(a => matchText(enclosing.textContent, a)).find(r => r.match);
                            score += (m && m.exact) ? 90 : 55;
                            strategies.push('EnclosingLabel');
                        }

                        // 3. Nearby field-container association
                        const container = input.closest('.form-group, .form-row, .row, .field, [class*=""group""], div, fieldset');
                        if (container) {
                            const nearbyLabel = Array.from(container.querySelectorAll('label')).find(l => aliases.some(a => matchText(l.textContent, a).match));
                            if (nearbyLabel) {
                                const m = aliases.map(a => matchText(nearbyLabel.textContent, a)).find(r => r.match);
                                score += (m && m.exact) ? 80 : 45;
                                strategies.push('ContainerProximity');
                            }
                        }

                        // 4. Accessible attributes
                        for (const a of aliases) {
                            const m = matchText(aria, a);
                            if (m.match) {
                                score += m.exact ? 65 : 40;
                                strategies.push('aria-label');
                                break;
                            }
                        }

                        if (ariaBy) {
                            const byEl = document.getElementById(ariaBy);
                            if (byEl) {
                                for (const a of aliases) {
                                    const m = matchText(byEl.textContent, a);
                                    if (m.match) {
                                        score += m.exact ? 65 : 40;
                                        strategies.push('aria-labelledby');
                                        break;
                                    }
                                }
                            }
                        }

                        for (const a of aliases) {
                            const m = matchText(auto, a);
                            if (m.match) {
                                score += 50;
                                strategies.push('autocomplete');
                                break;
                            }
                        }

                        // 5. Semantic attributes
                        for (const a of aliases) {
                            const m = matchText(ph, a);
                            if (m.match) {
                                score += m.exact ? 45 : 25;
                                strategies.push('placeholder');
                                break;
                            }
                        }
                        for (const a of aliases) {
                            const m = matchText(name, a);
                            if (m.match) {
                                score += m.exact ? 40 : 20;
                                strategies.push('name');
                                break;
                            }
                        }
                        for (const a of aliases) {
                            const m = matchText(id, a);
                            if (m.match) {
                                score += m.exact ? 35 : 15;
                                strategies.push('id');
                                break;
                            }
                        }

                        if (score >= 40) {
                            candidates.push({
                                element: input,
                                score: score,
                                strategy: strategies.join(' + '),
                                details: `<input id='${id}' name='${name}' placeholder='${ph}'>`
                            });
                        }
                    }

                    return evaluateCandidates(candidates, 40);
                ";

                var res = _js.ExecuteScript(script, context as IWebElement, aliases);
                sw.Stop();
                return ProcessCandidateResult(res, "TextBox", semanticLabel, sw);
            }
            catch (AmbiguousMatchException)
            {
                throw;
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
                string script = CommonJsFunctions + @"
                    const root = (arguments[0] && arguments[0].querySelectorAll) ? arguments[0] : document;
                    const optionText = arguments[1];
                    const groupLabel = arguments[2];

                    const rawRadios = Array.from(root.querySelectorAll(""input[type='radio']""));
                    const radios = rawRadios.filter(isVisible);
                    if (radios.length === 0) return null;

                    let pool = [];

                    // 1. role='radiogroup'
                    const ariaGroups = Array.from(root.querySelectorAll('[role=""radiogroup""]')).filter(g => {
                        const aria = (g.getAttribute('aria-label') || '').trim();
                        const ariaBy = (g.getAttribute('aria-labelledby') || '').trim();
                        const byEl = ariaBy ? document.getElementById(ariaBy) : null;
                        const byText = byEl ? byEl.textContent : '';
                        return matchText(aria, groupLabel).match || matchText(byText, groupLabel).match;
                    });
                    if (ariaGroups.length > 0) {
                        pool = Array.from(ariaGroups[0].querySelectorAll(""input[type='radio']"")).filter(isVisible);
                    }

                    // 2. fieldset with legend
                    if (pool.length === 0) {
                        const fieldsets = Array.from(root.querySelectorAll('fieldset')).filter(f => {
                            const legend = f.querySelector('legend');
                            return legend ? matchText(legend.textContent, groupLabel).match : false;
                        });
                        if (fieldsets.length > 0) {
                            pool = Array.from(fieldsets[0].querySelectorAll(""input[type='radio']"")).filter(isVisible);
                        }
                    }

                    // 3. Container with aria-label or aria-labelledby
                    if (pool.length === 0) {
                        const ariaContainers = Array.from(root.querySelectorAll('[aria-label], [aria-labelledby]')).filter(c => {
                            if (c.getAttribute('role') === 'radiogroup') return false;
                            const aria = (c.getAttribute('aria-label') || '').trim();
                            const ariaBy = (c.getAttribute('aria-labelledby') || '').trim();
                            const byEl = ariaBy ? document.getElementById(ariaBy) : null;
                            const byText = byEl ? byEl.textContent : '';
                            return matchText(aria, groupLabel).match || matchText(byText, groupLabel).match;
                        });
                        if (ariaContainers.length > 0) {
                            pool = Array.from(ariaContainers[0].querySelectorAll(""input[type='radio']"")).filter(isVisible);
                        }
                    }

                    // 4. Group heading / label + local container
                    if (pool.length === 0) {
                        const headings = Array.from(root.querySelectorAll('h1, h2, h3, h4, h5, h6, label, span, p, div')).filter(el => {
                            const t = el.textContent || '';
                            return matchText(t, groupLabel).match && el.querySelectorAll('input').length === 0;
                        });

                        if (headings.length > 0) {
                            const nearest = headings[0].closest('.form-group, .row, .field, div, fieldset');
                            if (nearest) {
                                const scoped = Array.from(nearest.querySelectorAll(""input[type='radio']"")).filter(isVisible);
                                if (scoped.length > 0) pool = scoped;
                            }
                        }
                    }

                    // 5. Radio semantic name
                    if (pool.length === 0) {
                        const namedRadios = radios.filter(r => matchText(r.name, groupLabel).match);
                        if (namedRadios.length > 0) pool = namedRadios;
                    }

                    // Rejection rule: If no reliable group identified, never search all radios globally
                    if (pool.length === 0) {
                        return { error: 'NoReliableGroup', groupLabel: groupLabel };
                    }

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

                        // Visible Option Text
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

                    return evaluateCandidates(candidates, 50);
                ";

                var res = _js.ExecuteScript(script, context as IWebElement, optionText, groupLabel);
                sw.Stop();
                return ProcessCandidateResult(res, "Radio", $"{groupLabel}:{optionText}", sw);
            }
            catch (AmbiguousMatchException)
            {
                throw;
            }
            catch (NoSuchElementException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogDiagnostic($"[Locator:Radio] Diagnostic error for '{optionText}': {ex.Message}");
            }

            return null;
        }

        #endregion

        #region Dropdown & Combobox Discovery

        public IWebElement? FindDropdown(ISearchContext context, string semanticLabel)
        {
            var sw = Stopwatch.StartNew();
            var aliases = GetFieldAliases(semanticLabel);

            try
            {
                string script = CommonJsFunctions + @"
                    const root = (arguments[0] && arguments[0].querySelectorAll) ? arguments[0] : document;
                    const aliases = arguments[1];

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
                        const isCombobox = select.getAttribute('role') === 'combobox';

                        // 1. Explicit label[for]
                        if (id) {
                            const expl = allLabels.find(l => (l.htmlFor || '').toLowerCase() === id.toLowerCase() && aliases.some(a => matchText(l.textContent, a).match));
                            if (expl) {
                                const m = aliases.map(a => matchText(expl.textContent, a)).find(r => r.match);
                                score += (m && m.exact) ? 100 : 70;
                                strategies.push('Label[for]');
                            }
                        }

                        // 2. Enclosing label
                        const enclosing = allLabels.find(l => aliases.some(a => matchText(l.textContent, a).match) && l.contains(select));
                        if (enclosing) {
                            const m = aliases.map(a => matchText(enclosing.textContent, a)).find(r => r.match);
                            score += (m && m.exact) ? 95 : 70;
                            strategies.push('EnclosingLabel');
                        }

                        // 3. Container proximity
                        const container = select.closest('.form-group, .row, .field, div, fieldset');
                        if (container) {
                            const nearbyLabel = Array.from(container.querySelectorAll('label')).find(l => aliases.some(a => matchText(l.textContent, a).match));
                            if (nearbyLabel) {
                                const m = aliases.map(a => matchText(nearbyLabel.textContent, a)).find(r => r.match);
                                score += (m && m.exact) ? 85 : 55;
                                strategies.push('ContainerProximity');
                            }
                        }

                        // 4. Accessible attributes
                        for (const a of aliases) {
                            const m = matchText(aria, a);
                            if (m.match) {
                                score += m.exact ? 65 : 40;
                                strategies.push('aria-label');
                                break;
                            }
                        }

                        if (ariaBy) {
                            const byEl = document.getElementById(ariaBy);
                            if (byEl) {
                                for (const a of aliases) {
                                    const m = matchText(byEl.textContent, a);
                                    if (m.match) {
                                        score += m.exact ? 65 : 40;
                                        strategies.push('aria-labelledby');
                                        break;
                                    }
                                }
                            }
                        }

                        // 5. Semantic attributes
                        for (const a of aliases) {
                            const m = matchText(name, a);
                            if (m.match) {
                                score += m.exact ? 45 : 25;
                                strategies.push('name');
                                break;
                            }
                        }

                        for (const a of aliases) {
                            const m = matchText(id, a);
                            if (m.match) {
                                score += m.exact ? 40 : 20;
                                strategies.push('id');
                                break;
                            }
                        }

                        if (isCombobox) {
                            score += 15;
                            strategies.push('role=combobox');
                        }

                        if (score >= 40) {
                            candidates.push({
                                element: select,
                                score: score,
                                strategy: strategies.join(' + '),
                                details: `<${select.tagName.toLowerCase()} id='${id}' name='${name}' role='${select.getAttribute('role') || ''}'>`
                            });
                        }
                    }

                    return evaluateCandidates(candidates, 40);
                ";

                var res = _js.ExecuteScript(script, context as IWebElement, aliases);
                sw.Stop();
                return ProcessCandidateResult(res, "Dropdown", semanticLabel, sw);
            }
            catch (AmbiguousMatchException)
            {
                throw;
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
            var sw = Stopwatch.StartNew();

            try
            {
                string script = CommonJsFunctions + @"
                    const root = (arguments[0] && arguments[0].querySelectorAll) ? arguments[0] : document;
                    const rawButtons = Array.from(root.querySelectorAll(""button, input[type='submit'], input[type='button'], a.btn, [role='button']""));
                    const buttons = rawButtons.filter(isVisible);
                    if (buttons.length === 0) return null;

                    const keywords = ['submit', 'save', 'send', 'register', 'done'];
                    const candidates = [];

                    for (const btn of buttons) {
                        let score = 0;
                        const strategies = [];
                        const text = (btn.innerText || btn.textContent || '').trim();
                        const val = (btn.value || '').trim();
                        const aria = (btn.getAttribute('aria-label') || '').trim();
                        const type = (btn.getAttribute('type') || '').trim();

                        if (type.toLowerCase() === 'submit') {
                            score += 40;
                            strategies.push('type=submit');
                        }

                        for (const kw of keywords) {
                            const mt = matchText(text, kw);
                            if (mt.match) {
                                score += mt.exact ? 60 : 35;
                                strategies.push('text=' + kw);
                                break;
                            }
                            const mv = matchText(val, kw);
                            if (mv.match) {
                                score += mv.exact ? 50 : 30;
                                strategies.push('value=' + kw);
                                break;
                            }
                            const ma = matchText(aria, kw);
                            if (ma.match) {
                                score += ma.exact ? 50 : 30;
                                strategies.push('aria=' + kw);
                                break;
                            }
                        }

                        if (score >= 40) {
                            candidates.push({
                                element: btn,
                                score: score,
                                strategy: strategies.join(' + '),
                                details: `<${btn.tagName.toLowerCase()} type='${type}' text='${text}'>`
                            });
                        }
                    }

                    return evaluateCandidates(candidates, 40);
                ";

                var res = _js.ExecuteScript(script, context as IWebElement);
                sw.Stop();
                return ProcessCandidateResult(res, "SubmitButton", "Submit", sw);
            }
            catch (AmbiguousMatchException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogDiagnostic($"[Locator:SubmitButton] Diagnostic error: {ex.Message}");
            }

            return null;
        }

        public IWebElement? FindCheckbox(ISearchContext context, string label)
        {
            var checkboxes = context.FindElements(By.CssSelector("input[type='checkbox']")).Where(c => c.Displayed && c.Enabled).ToList();
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
            if (clean == "state") return new[] { "state", "province", "region", "province state", "state province" };
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
