using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace CloudQA.ResilientAutomation.Core
{
    public class CandidateMatch<T>
    {
        public T Element { get; set; } = default!;
        public int Score { get; set; }
        public string StrategyUsed { get; set; } = string.Empty;
        public string ElementDetails { get; set; } = string.Empty;
    }

    /// <summary>
    /// Senior SDET-grade Resilient Element Finder.
    /// Uses prioritized confidence scoring, accessibility standards, explicit waits,
    /// rejection of ambiguous matches, and comprehensive diagnostic logging.
    /// Does not depend on arbitrary position or hardcoded IDs/XPath.
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

        #region First Name (Input / Textbox Discovery)

        /// <summary>
        /// Finds an input textbox using prioritized confidence scoring:
        /// 1. Explicit <label for="id"> (+100)
        /// 2. Enclosing <label> (+90)
        /// 3. Nearby field-container association (form-group/row) (+75)
        /// 4. Accessible attributes (aria-label, aria-labelledby, autocomplete) (+60)
        /// 5. Semantic attributes (placeholder, name, id) (+40)
        /// Rejects ambiguous candidates when top scores are tied without confidence separation.
        /// </summary>
        public IWebElement? FindTextBox(ISearchContext context, string semanticLabel)
        {
            var sw = Stopwatch.StartNew();
            var aliases = GetFieldAliases(semanticLabel);

            // Fast Single-Pass DOM Evaluation via In-Browser Engine (<15ms instead of hundreds of HTTP roundtrips)
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

                        // 1. Explicit label[for='id']
                        if (id) {
                            const expl = labels.find(l => (l.htmlFor || '').toLowerCase() === id.toLowerCase() && aliases.some(a => matchText(l.textContent, a)));
                            if (expl) {
                                score += 100;
                                strategies.push('Label[for=\'' + id + '\'] (\'' + expl.textContent.trim() + '\')');
                            }
                        }

                        // 2. Enclosing label
                        const enclosing = labels.find(l => aliases.some(a => matchText(l.textContent, a)) && l.contains(input));
                        if (enclosing) {
                            score += 90;
                            strategies.push('EnclosingLabel (\'' + enclosing.textContent.trim() + '\')');
                        }

                        // 3. Container proximity (.form-group, .row, .field)
                        const matchingNearby = labels.find(l => aliases.some(a => matchText(l.textContent, a)));
                        if (matchingNearby) {
                            const container = matchingNearby.closest('.form-group, .form-row, .row, .field, [class*=""group""], div');
                            if (container && container.contains(input)) {
                                score += 75;
                                strategies.push('ContainerProximity (\'' + matchingNearby.textContent.trim() + '\')');
                            }
                        }

                        // 4. Accessible attributes
                        if (aliases.some(a => matchText(aria, a))) {
                            score += 65;
                            strategies.push('aria-label=\'' + aria + '\'');
                        }
                        if (aliases.some(a => matchText(auto, a))) {
                            score += 60;
                            strategies.push('autocomplete=\'' + auto + '\'');
                        }

                        // 5. Semantic attributes
                        if (aliases.some(a => matchText(ph, a))) {
                            score += 45;
                            strategies.push('placeholder=\'' + ph + '\'');
                        }
                        if (aliases.some(a => matchText(name, a))) {
                            score += 40;
                            strategies.push('name=\'' + name + '\'');
                        }
                        if (aliases.some(a => matchText(id, a))) {
                            score += 35;
                            strategies.push('id=\'' + id + '\'');
                        }

                        if (score > 0) {
                            candidates.push({
                                element: input,
                                score: score,
                                strategy: strategies.join(' + '),
                                details: '<input tag=\'' + input.tagName.toLowerCase() + '\' id=\'' + id + '\' name=\'' + name + '\' ph=\'' + ph + '\'>'
                            });
                        }
                    }

                    if (candidates.length === 0) return null;
                    candidates.sort((a, b) => b.score - a.score);

                    if (candidates.length > 1 && candidates[1].score === candidates[0].score && candidates[0].score >= 50) {
                        return {
                            ambiguous: true,
                            score: candidates[0].score,
                            details: candidates[0].details + ' vs ' + candidates[1].details
                        };
                    }

                    return {
                        element: candidates[0].element,
                        score: candidates[0].score,
                        strategy: candidates[0].strategy,
                        details: candidates[0].details
                    };
                ";

                var res = _js.ExecuteScript(script, context as IWebElement, aliases);
                if (res is IDictionary<string, object> dict)
                {
                    sw.Stop();
                    if (dict.ContainsKey("ambiguous") && Convert.ToBoolean(dict["ambiguous"]))
                    {
                        LogDiagnostic($"[FindTextBox: '{semanticLabel}'] Rejected ambiguous match: 2 candidates tied with score {dict["score"]}. ({dict["details"]})");
                        return null;
                    }

                    if (dict.TryGetValue("element", out var elObj) && elObj is IWebElement matchedInput)
                    {
                        LogDiagnostic($"[FindTextBox: '{semanticLabel}'] Selected candidate with score {dict["score"]} via {dict["strategy"]}. Time: {sw.ElapsedMilliseconds}ms. Details: {dict["details"]}");
                        return matchedInput;
                    }
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic($"[FindTextBox: '{semanticLabel}'] Fast evaluation fallback triggered: {ex.Message}");
            }

            var candidates = new List<CandidateMatch<IWebElement>>();

            var allInputs = context.FindElements(By.CssSelector("input:not([type='hidden']):not([type='radio']):not([type='checkbox']):not([type='submit']):not([type='button']), textarea"))
                                   .Where(el => el.Displayed && el.Enabled)
                                   .ToList();

            if (!allInputs.Any())
            {
                LogDiagnostic($"[FindTextBox: '{semanticLabel}'] No enabled, displayed text inputs found in context.");
                return null;
            }

            var allLabels = context.FindElements(By.TagName("label")).Where(l => l.Displayed).ToList();

            foreach (var input in allInputs)
            {
                int score = 0;
                var strategies = new List<string>();

                var id = (input.GetAttribute("id") ?? "").Trim();
                var name = (input.GetAttribute("name") ?? "").Trim();
                var placeholder = (input.GetAttribute("placeholder") ?? "").Trim();
                var ariaLabel = (input.GetAttribute("aria-label") ?? "").Trim();
                var ariaLabelledBy = (input.GetAttribute("aria-labelledby") ?? "").Trim();
                var autocomplete = (input.GetAttribute("autocomplete") ?? "").Trim();

                // 1. Explicit label association (label[for='id'])
                if (!string.IsNullOrEmpty(id))
                {
                    var explicitLabel = allLabels.FirstOrDefault(l => 
                        string.Equals(l.GetAttribute("for"), id, StringComparison.OrdinalIgnoreCase) &&
                        aliases.Any(a => MatchText(l.Text, a)));

                    if (explicitLabel != null)
                    {
                        score += 100;
                        strategies.Add($"Label[for='{id}'] ('{explicitLabel.Text.Trim()}')");
                    }
                }

                // 2. Enclosing label association (<label>First Name <input/></label>)
                var enclosingLabel = allLabels.FirstOrDefault(l =>
                {
                    try
                    {
                        return aliases.Any(a => MatchText(l.Text, a)) && l.FindElements(By.XPath(".//input | .//textarea")).Any(nested => nested.Equals(input));
                    }
                    catch { return false; }
                });

                if (enclosingLabel != null)
                {
                    score += 90;
                    strategies.Add($"EnclosingLabel ('{enclosingLabel.Text.Trim()}')");
                }

                // 3. Nearby field container / form-group association
                try
                {
                    var matchingNearbyLabel = allLabels.FirstOrDefault(l => aliases.Any(a => MatchText(l.Text, a)));
                    if (matchingNearbyLabel != null)
                    {
                        var isParentRelated = Convert.ToBoolean(_js.ExecuteScript(@"
                            let label = arguments[0];
                            let input = arguments[1];
                            let container = label.closest('.form-group, .form-row, .row, .field, [class*=""group""], div');
                            return container ? container.contains(input) : false;
                        ", matchingNearbyLabel, input));

                        if (isParentRelated)
                        {
                            score += 75;
                            strategies.Add($"ContainerProximity ('{matchingNearbyLabel.Text.Trim()}')");
                        }
                    }
                }
                catch { }

                // 4. Accessible attributes (aria-label, autocomplete, aria-labelledby)
                if (aliases.Any(a => MatchText(ariaLabel, a)))
                {
                    score += 65;
                    strategies.Add($"aria-label='{ariaLabel}'");
                }

                if (!string.IsNullOrEmpty(ariaLabelledBy))
                {
                    var labelRef = allLabels.FirstOrDefault(l => string.Equals(l.GetAttribute("id"), ariaLabelledBy, StringComparison.OrdinalIgnoreCase));
                    if (labelRef != null && aliases.Any(a => MatchText(labelRef.Text, a)))
                    {
                        score += 65;
                        strategies.Add($"aria-labelledby='{ariaLabelledBy}'");
                    }
                }

                if (semanticLabel.Equals("First Name", StringComparison.OrdinalIgnoreCase) && 
                    (MatchText(autocomplete, "given-name") || MatchText(autocomplete, "fname")))
                {
                    score += 60;
                    strategies.Add($"autocomplete='{autocomplete}'");
                }

                // 5. Semantic attributes (placeholder, name, id)
                if (aliases.Any(a => MatchText(placeholder, a)))
                {
                    score += 45;
                    strategies.Add($"placeholder='{placeholder}'");
                }

                if (aliases.Any(a => MatchText(name, a)))
                {
                    score += 40;
                    strategies.Add($"name='{name}'");
                }

                if (aliases.Any(a => MatchText(id, a)))
                {
                    score += 35;
                    strategies.Add($"id='{id}'");
                }

                if (score > 0)
                {
                    candidates.Add(new CandidateMatch<IWebElement>
                    {
                        Element = input,
                        Score = score,
                        StrategyUsed = string.Join(" + ", strategies),
                        ElementDetails = $"<input tag='{input.TagName}' id='{id}' name='{name}' ph='{placeholder}'>"
                    });
                }
            }

            sw.Stop();

            if (!candidates.Any())
            {
                LogDiagnostic($"[FindTextBox: '{semanticLabel}'] No candidate met confidence threshold. Search time: {sw.ElapsedMilliseconds}ms");
                return null;
            }

            var ordered = candidates.OrderByDescending(c => c.Score).ToList();
            var top = ordered.First();

            // Ambiguity rejection check: If top 2 candidates have identical high scores, reject to prevent false actions
            if (ordered.Count > 1 && ordered[1].Score == top.Score && top.Score >= 50)
            {
                LogDiagnostic($"[FindTextBox: '{semanticLabel}'] Rejected ambiguous match: 2 candidates tied with score {top.Score}. ({ordered[0].ElementDetails} vs {ordered[1].ElementDetails})");
                return null;
            }

            LogDiagnostic($"[FindTextBox: '{semanticLabel}'] Selected candidate with score {top.Score} via {top.StrategyUsed}. Time: {sw.ElapsedMilliseconds}ms. Details: {top.ElementDetails}");
            return top.Element;
        }

        #endregion

        #region Gender (Radio Button Discovery)

        /// <summary>
        /// Discovers Gender radio button:
        /// 1. Finds the Gender field group (via visible label/legend/heading text 'Gender').
        /// 2. Discovers radio buttons inside or associated with that group.
        /// 3. Associates each radio input with its visible option text (labels, spans, text nodes).
        /// 4. Does not assume radio buttons have a proper HTML label element.
        /// </summary>
        public IWebElement? FindRadioButton(ISearchContext context, string optionText, string groupLabel = "Gender")
        {
            var sw = Stopwatch.StartNew();

            // Fast Single-Pass DOM Evaluation via In-Browser Engine
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
                            strategies.push('value=\'' + val + '\'');
                        }
                        if (matchText(aria, optionText)) {
                            score += 80;
                            strategies.push('aria-label=\'' + aria + '\'');
                        }
                        if (matchText(id, optionText)) {
                            score += 60;
                            strategies.push('id=\'' + id + '\'');
                        }

                        let visibleText = '';
                        if (radio.nextSibling && radio.nextSibling.nodeType === 3) visibleText += ' ' + radio.nextSibling.textContent;
                        if (radio.nextElementSibling) visibleText += ' ' + radio.nextElementSibling.innerText;
                        if (radio.parentElement) visibleText += ' ' + radio.parentElement.innerText;
                        visibleText = visibleText.trim();

                        if (matchText(visibleText, optionText)) {
                            score += 100;
                            strategies.push('VisibleOptionText (\'' + visibleText + '\')');
                        }

                        if (id) {
                            const lbl = labels.find(l => (l.htmlFor || '').toLowerCase() === id.toLowerCase() && matchText(l.textContent, optionText));
                            if (lbl) {
                                score += 90;
                                strategies.push('Label[for=\'' + id + '\'] (\'' + lbl.textContent.trim() + '\')');
                            }
                        }

                        if (score > 0) {
                            candidates.push({
                                element: radio,
                                score: score,
                                strategy: strategies.join(' + ')
                            });
                        }
                    }

                    if (candidates.length === 0) return null;
                    candidates.sort((a, b) => b.score - a.score);
                    return {
                        element: candidates[0].element,
                        score: candidates[0].score,
                        strategy: candidates[0].strategy
                    };
                ";

                var res = _js.ExecuteScript(script, context as IWebElement, optionText, groupLabel);
                if (res is IDictionary<string, object> dict && dict.TryGetValue("element", out var elObj) && elObj is IWebElement matchedRadio)
                {
                    sw.Stop();
                    LogDiagnostic($"[FindRadioButton: '{optionText}'] Selected radio with score {dict["score"]} via {dict["strategy"]}. Time: {sw.ElapsedMilliseconds}ms");
                    return matchedRadio;
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic($"[FindRadioButton: '{optionText}'] Fast evaluation fallback triggered: {ex.Message}");
            }

            // 1. Locate group radios: radios under a container with 'Gender' or having name='gender'
            var allRadios = context.FindElements(By.CssSelector("input[type='radio']")).Where(r => r.Displayed).ToList();
            if (!allRadios.Any())
            {
                LogDiagnostic($"[FindRadioButton: '{optionText}'] No radio buttons found in context.");
                return null;
            }

            // Group discovery: prioritize radios sharing group name or under gender container
            var groupRadios = allRadios.Where(r => 
                MatchText(r.GetAttribute("name") ?? "", groupLabel) ||
                MatchText(r.GetAttribute("id") ?? "", groupLabel)).ToList();

            if (!groupRadios.Any())
            {
                // Fallback to searching nearby heading/label for group
                try
                {
                    var groupHeader = context.FindElements(By.XPath(".//*[self::label or self::legend or self::span or self::div or self::h4 or self::h5][contains(translate(text(), 'GENDER', 'gender'), 'gender')]")).FirstOrDefault();
                    if (groupHeader != null)
                    {
                        var containerRadios = groupHeader.FindElements(By.XPath("following::input[@type='radio'][position() <= 4]")).ToList();
                        if (containerRadios.Any()) groupRadios = containerRadios;
                    }
                }
                catch { }
            }

            var pool = groupRadios.Any() ? groupRadios : allRadios;
            var candidates = new List<CandidateMatch<IWebElement>>();

            foreach (var radio in pool)
            {
                int score = 0;
                var strategies = new List<string>();

                var val = (radio.GetAttribute("value") ?? "").Trim();
                var id = (radio.GetAttribute("id") ?? "").Trim();
                var ariaLabel = (radio.GetAttribute("aria-label") ?? "").Trim();

                // A. Direct value or aria-label match (+80)
                if (MatchText(val, optionText))
                {
                    score += 80;
                    strategies.Add($"value='{val}'");
                }

                if (MatchText(ariaLabel, optionText))
                {
                    score += 80;
                    strategies.Add($"aria-label='{ariaLabel}'");
                }

                if (MatchText(id, optionText))
                {
                    score += 60;
                    strategies.Add($"id='{id}'");
                }

                // B. Sibling text & text nodes via JavaScript (handles bare text nodes without <label>) (+100)
                try
                {
                    var visibleText = _js.ExecuteScript(@"
                        let r = arguments[0];
                        let t = '';
                        if (r.nextSibling && r.nextSibling.nodeType === 3) t += ' ' + r.nextSibling.textContent;
                        if (r.nextElementSibling) t += ' ' + r.nextElementSibling.innerText;
                        if (r.parentElement) t += ' ' + r.parentElement.innerText;
                        return t.trim();
                    ", radio)?.ToString() ?? "";

                    if (MatchOptionText(visibleText, optionText))
                    {
                        score += 100;
                        strategies.Add($"VisibleOptionText ('{visibleText}')");
                    }
                }
                catch { }

                // C. Explicit <label for="id"> (+90)
                if (!string.IsNullOrEmpty(id))
                {
                    try
                    {
                        var label = context.FindElements(By.CssSelector($"label[for='{id}']")).FirstOrDefault();
                        if (label != null && MatchOptionText(label.Text, optionText))
                        {
                            score += 90;
                            strategies.Add($"Label[for='{id}'] ('{label.Text.Trim()}')");
                        }
                    }
                    catch { }
                }

                if (score > 0)
                {
                    candidates.Add(new CandidateMatch<IWebElement>
                    {
                        Element = radio,
                        Score = score,
                        StrategyUsed = string.Join(" + ", strategies),
                        ElementDetails = $"<input type='radio' id='{id}' value='{val}'>"
                    });
                }
            }

            sw.Stop();

            if (!candidates.Any())
            {
                LogDiagnostic($"[FindRadioButton: '{optionText}'] No radio candidate matched criteria. Time: {sw.ElapsedMilliseconds}ms");
                return null;
            }

            var best = candidates.OrderByDescending(c => c.Score).First();
            LogDiagnostic($"[FindRadioButton: '{optionText}'] Selected radio with score {best.Score} via {best.StrategyUsed}. Time: {sw.ElapsedMilliseconds}ms");
            return best.Element;
        }

        #endregion

        #region State (Dropdown Discovery & Interaction)

        /// <summary>
        /// Locates the State dropdown (<select>) via semantic label association, enclosing containers, or attributes.
        /// Interacts using Selenium SelectElement.
        /// </summary>
        public IWebElement? FindDropdown(ISearchContext context, string semanticLabel)
        {
            var sw = Stopwatch.StartNew();
            var aliases = new[] { semanticLabel, "state", "province", "region" };

            // Fast Single-Pass DOM Evaluation via In-Browser Engine
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

                    const rawSelects = Array.from(root.querySelectorAll('select'));
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
                                strategies.push('Label[for=\'' + id + '\'] (\'' + lbl.textContent.trim() + '\')');
                            }
                        }

                        const stateLabel = labels.find(l => aliases.some(a => matchText(l.textContent, a)));
                        if (stateLabel) {
                            const container = stateLabel.closest('.form-group, .row, .field, div');
                            if (container && container.contains(select)) {
                                score += 85;
                                strategies.push('ContainerProximity (\'' + stateLabel.textContent.trim() + '\')');
                            }
                        }

                        if (aliases.some(a => matchText(aria, a))) {
                            score += 50;
                            strategies.push('aria-label=\'' + aria + '\'');
                        }
                        if (aliases.some(a => matchText(name, a))) {
                            score += 45;
                            strategies.push('name=\'' + name + '\'');
                        }
                        if (aliases.some(a => matchText(id, a))) {
                            score += 40;
                            strategies.push('id=\'' + id + '\'');
                        }

                        if (score > 0) {
                            candidates.push({
                                element: select,
                                score: score,
                                strategy: strategies.join(' + ')
                            });
                        }
                    }

                    if (candidates.length === 0) {
                        if (selects.length === 1) {
                            return { element: selects[0], score: 50, strategy: 'SingleAvailableDropdown' };
                        }
                        return null;
                    }

                    candidates.sort((a, b) => b.score - a.score);
                    return {
                        element: candidates[0].element,
                        score: candidates[0].score,
                        strategy: candidates[0].strategy
                    };
                ";

                var res = _js.ExecuteScript(script, context as IWebElement, aliases);
                if (res is IDictionary<string, object> dict && dict.TryGetValue("element", out var elObj) && elObj is IWebElement matchedSelect)
                {
                    sw.Stop();
                    LogDiagnostic($"[FindDropdown: '{semanticLabel}'] Selected dropdown with score {dict["score"]} via {dict["strategy"]}. Time: {sw.ElapsedMilliseconds}ms");
                    return matchedSelect;
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic($"[FindDropdown: '{semanticLabel}'] Fast evaluation fallback triggered: {ex.Message}");
            }

            var selects = context.FindElements(By.CssSelector("select")).Where(s => s.Displayed && s.Enabled).ToList();

            if (!selects.Any())
            {
                LogDiagnostic($"[FindDropdown: '{semanticLabel}'] No displayed, enabled <select> elements in context.");
                return null;
            }

            var candidates = new List<CandidateMatch<IWebElement>>();
            var allLabels = context.FindElements(By.TagName("label")).Where(l => l.Displayed).ToList();

            foreach (var select in selects)
            {
                int score = 0;
                var strategies = new List<string>();

                var id = (select.GetAttribute("id") ?? "").Trim();
                var name = (select.GetAttribute("name") ?? "").Trim();
                var ariaLabel = (select.GetAttribute("aria-label") ?? "").Trim();

                // 1. Explicit <label for="id"> (+100)
                if (!string.IsNullOrEmpty(id))
                {
                    var label = allLabels.FirstOrDefault(l => 
                        string.Equals(l.GetAttribute("for"), id, StringComparison.OrdinalIgnoreCase) &&
                        aliases.Any(a => MatchText(l.Text, a)));

                    if (label != null)
                    {
                        score += 100;
                        strategies.Add($"Label[for='{id}'] ('{label.Text.Trim()}')");
                    }
                }

                // 2. Enclosing container proximity (label + select in same row/form-group) (+85)
                var stateLabel = allLabels.FirstOrDefault(l => aliases.Any(a => MatchText(l.Text, a)));
                if (stateLabel != null)
                {
                    try
                    {
                        var isRelated = Convert.ToBoolean(_js.ExecuteScript(@"
                            let l = arguments[0];
                            let s = arguments[1];
                            let c = l.closest('.form-group, .row, .field, div');
                            return c ? c.contains(s) : false;
                        ", stateLabel, select));

                        if (isRelated)
                        {
                            score += 85;
                            strategies.Add($"ContainerProximity ('{stateLabel.Text.Trim()}')");
                        }
                    }
                    catch { }
                }

                // 3. Direct attributes (+50)
                if (aliases.Any(a => MatchText(ariaLabel, a)))
                {
                    score += 50;
                    strategies.Add($"aria-label='{ariaLabel}'");
                }

                if (aliases.Any(a => MatchText(name, a)))
                {
                    score += 45;
                    strategies.Add($"name='{name}'");
                }

                if (aliases.Any(a => MatchText(id, a)))
                {
                    score += 40;
                    strategies.Add($"id='{id}'");
                }

                if (score > 0)
                {
                    candidates.Add(new CandidateMatch<IWebElement>
                    {
                        Element = select,
                        Score = score,
                        StrategyUsed = string.Join(" + ", strategies),
                        ElementDetails = $"<select id='{id}' name='{name}'>"
                    });
                }
            }

            sw.Stop();

            if (!candidates.Any())
            {
                // If only 1 select exists in this form, select it as a safe single fallback
                if (selects.Count == 1)
                {
                    LogDiagnostic($"[FindDropdown: '{semanticLabel}'] Selected single available dropdown in context. Time: {sw.ElapsedMilliseconds}ms");
                    return selects[0];
                }

                LogDiagnostic($"[FindDropdown: '{semanticLabel}'] No candidate matched dropdown criteria. Time: {sw.ElapsedMilliseconds}ms");
                return null;
            }

            var best = candidates.OrderByDescending(c => c.Score).First();
            LogDiagnostic($"[FindDropdown: '{semanticLabel}'] Selected dropdown with score {best.Score} via {best.StrategyUsed}. Time: {sw.ElapsedMilliseconds}ms");
            return best.Element;
        }

        #endregion

        #region Other Form Elements (Checkboxes, WYSIWYG, Submit, Root Form Discovery)

        public IWebElement? FindCheckbox(ISearchContext context, string checkboxLabel)
        {
            var checkboxes = context.FindElements(By.CssSelector("input[type='checkbox']")).Where(c => c.Displayed).ToList();
            if (!checkboxes.Any()) return null;

            foreach (var cb in checkboxes)
            {
                var val = cb.GetAttribute("value") ?? "";
                var id = cb.GetAttribute("id") ?? "";
                var name = cb.GetAttribute("name") ?? "";

                if (MatchText(val, checkboxLabel) || MatchText(id, checkboxLabel) || MatchText(name, checkboxLabel))
                    return cb;

                try
                {
                    var text = _js.ExecuteScript(@"
                        let c = arguments[0];
                        return ((c.nextSibling ? c.nextSibling.textContent : '') + ' ' +
                                (c.nextElementSibling ? c.nextElementSibling.innerText : '') + ' ' +
                                (c.parentElement ? c.parentElement.innerText : '')).trim();
                    ", cb)?.ToString() ?? "";

                    if (MatchText(text, checkboxLabel)) return cb;
                }
                catch { }
            }

            return null;
        }

        public bool FillRichTextEditor(ISearchContext context, string semanticLabel, string content)
        {
            try
            {
                var editables = context.FindElements(By.CssSelector("div.note-editable, div[contenteditable='true'], .wysiwyg-editor")).Where(e => e.Displayed).ToList();
                if (editables.Any())
                {
                    var editable = editables.First();
                    _js.ExecuteScript(@"
                        arguments[0].innerHTML = '<p>' + arguments[1] + '</p>';
                        arguments[0].dispatchEvent(new Event('input', { bubbles: true }));
                        arguments[0].dispatchEvent(new Event('change', { bubbles: true }));
                    ", editable, content);
                    return true;
                }

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
            var buttons = context.FindElements(By.CssSelector("button, input[type='submit'], input[type='button'], a.btn")).Where(b => b.Displayed && b.Enabled).ToList();
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

        #region Shadow DOM & iFrame Discovery (Isolated Support)

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
                if (result is System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> webElements)
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
                if (result is System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> webElements)
                {
                    return webElements;
                }
            }
            catch { }
            return Array.Empty<IWebElement>();
        }

        #endregion

        #region Normalization & Matching Helpers

        private static string[] GetFieldAliases(string semanticLabel)
        {
            var clean = Normalize(semanticLabel);
            if (clean == "first name")
                return new[] { "first name", "firstname", "first_name", "first-name", "given name", "given-name", "fname" };
            if (clean == "last name")
                return new[] { "last name", "lastname", "last_name", "last-name", "family name", "family-name", "surname", "lname" };
            if (clean == "date of birth")
                return new[] { "date of birth", "dob", "birthdate", "birthday", "yyyy-mm-dd" };
            if (clean == "mobile number" || clean == "mobile #" || clean == "mobile")
                return new[] { "mobile #", "mobile number", "mobile", "phone", "cell", "contact" };
            if (clean == "email")
                return new[] { "email", "e-mail" };
            if (clean == "country")
                return new[] { "country", "nation" };
            if (clean == "state")
                return new[] { "state", "province", "region" };
            if (clean == "about yourself")
                return new[] { "about yourself", "about", "bio" };
            if (clean == "description")
                return new[] { "description", "desc" };
            if (clean == "username")
                return new[] { "username", "user", "user_name" };
            if (clean == "password")
                return new[] { "password", "pwd" };
            if (clean == "confirm password")
                return new[] { "confirm password", "confirmpassword" };

            return new[] { clean };
        }

        public static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            // Trim, lowercase, replace underscores and dashes with spaces, collapse multiple whitespaces
            var replaced = input.Trim().ToLowerInvariant().Replace('_', ' ').Replace('-', ' ');
            return Regex.Replace(replaced, @"\s+", " ");
        }

        private static bool MatchText(string? source, string target)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) return false;
            return Normalize(source).Contains(Normalize(target));
        }

        private static bool MatchOptionText(string? source, string option)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(option)) return false;
            var normSource = Normalize(source);
            var normOption = Normalize(option);
            // Word boundary match or exact match to prevent 'Male' falsely matching 'Female'
            return Regex.IsMatch(normSource, $@"\b{Regex.Escape(normOption)}\b");
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
