# CloudQA Task 1: Resilient Selenium Automation Engineering

[![Build & Test CI](https://github.com/ajay1951/CloudQA_Task1_TestSuite/actions/workflows/ci.yml/badge.svg)](https://github.com/ajay1951/CloudQA_Task1_TestSuite/actions/workflows/ci.yml)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Selenium WebDriver](https://img.shields.io/badge/Selenium-4.52.0-green.svg)](https://www.selenium.dev/)
[![NUnit](https://img.shields.io/badge/NUnit-5.0.0-blue.svg)](https://nunit.org/)

---

## 1. Executive Summary

This repository delivers an enterprise-grade, resilient test automation framework engineered in **C# / .NET 10** using **Selenium WebDriver 4.52** and **NUnit 5.0**. 

Traditional Selenium suites break frequently because they rely on brittle CSS selectors (`#fname`, `.col-md-6 > input:nth-child(2)`) or absolute XPaths (`//form/div[2]/div[1]/input`). When application developers reorder form fields, refactor CSS classes, randomize element IDs, or restructure HTML containers, conventional automated tests fail immediately.

This solution solves that fragility by implementing an **In-Browser Heuristic Scoring Engine** that mimics human visual and semantic recognition. It targets the three mandatory controls on the [CloudQA Automation Practice Form](https://app.cloudqa.io/home/AutomationPracticeForm):
1. **First Name** (Text input)
2. **Gender** (Radio button group)
3. **State** (Native `<select>` & Custom ARIA Combobox)

---

## 2. Architecture & Design Principles

```
┌────────────────────────────────────────────────────────────────────────┐
│                              Program.cs                                │
│                (Pre/Post Verification & Screenshots)                   │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        Pages/PracticeFormPage                          │
│     (Strict ARIA Comboboxes, Post-Selection Verification,             │
│      and Deterministic 3-Attempt Stale Recovery)                      │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│                      Core/ResilientElementFinder                       │
│    Single-Pass JavaScript In-Browser Heuristic Evaluation Engine       │
│                                                                        │
│   • Explicit Label Association (<label for="id">)           +100 pts   │
│   • Enclosing Label Hierarchy (<label><input ...></label>)   +90 pts   │
│   • Container Proximity (.form-group, .field, .row)          +80 pts   │
│   • ARIA Tree Analysis (aria-label, aria-labelledby)         +65 pts   │
│   • Token Normalization & Punctuation Stripping                        │
│   • Word-Boundary Option Matching (\bMale\b vs Female)                 │
│   • Hidden & Disabled Filtering (display:none, disabled=true)         │
│   • Strict Ambiguity Rejection (Score Delta < 15 points)               │
│   • Radio Group Scoping Priority & Fallback Rejection                 │
│   • Hardened SafeClick (No silent JS bypass of disabled or overlays)   │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│                         Core/DriverFactory                             │
│     (Selenium Manager, Linux Container Flags, Multi-Browser Support)   │
└────────────────────────────────────────────────────────────────────────┘
```

### 2.1 In-Browser Heuristic Evaluation
Standard Selenium locator iterations often result in dozens of synchronous JSON-wire/W3C round trips across localhost, causing high latency. 

To eliminate this overhead, `ResilientElementFinder` executes a **single-pass JavaScript evaluation directly inside the browser's JavaScript V8/SpiderMonkey engine**. All candidate interactive elements in the target scope are gathered, scored against semantic cues, and ranked in one pass before returning the best unique candidate to Selenium.

### 2.2 Confidence Scoring Rubric

| Signal / Feature | Exact Match | Partial / Heuristic | Description |
|---|:---:|:---:|---|
| **Explicit `<label for="id">`** | **+100 pts** | **+60 pts** | Exact or normalized match to `for` attribute |
| **Enclosing `<label>` Parent** | **+90 pts** | **+55 pts** | Input is physically nested within the matching `<label>` |
| **Container Proximity** | **+80 pts** | **+45 pts** | Input shares parent `.form-group`, `.field`, `.row` with label |
| **ARIA Accessibility Attributes** | **+65 pts** | **+40 pts** | `aria-label` or `aria-labelledby` matches field name |
| **Accessible Placeholder** | **+45 pts** | **+25 pts** | `placeholder` attribute matches field descriptor |
| **Semantic `name` Attribute** | **+40 pts** | **+20 pts** | Name contains normalized keyword (e.g., `firstname`, `gender`) |
| **Semantic `id` Attribute** | **+35 pts** | **+15 pts** | Element ID matches field name |
| **Hidden & Disabled Penalty** | **Filtered** | **Filtered** | Elements with `display: none`, `visibility: hidden`, or `disabled=true` are excluded |

### 2.3 Strict Ambiguity Rejection (Anti-False-Positive)
Never select the first match blindly. If two identical or competing elements exist (e.g. duplicate unlabeled inputs or multiple forms) and their top confidence scores differ by less than **15 points**, the engine throws `AmbiguousMatchException` with detailed candidate diagnostics rather than performing an arbitrary click.

### 2.4 Strict Radio Group Scoping & Priority
Radio group discovery follows a strict 5-tier priority:
1. `role="radiogroup"` with matching accessible name (`aria-label`, `aria-labelledby`, legend).
2. `fieldset` with matching `legend`.
3. Container with `aria-label` or `aria-labelledby` matching group label.
4. Local group heading/label container (`.closest('.form-group, fieldset, div')`).
5. Radio inputs with semantic `name` matching group label.

**Rejection Rule**: If no reliable group can be identified, the engine **rejects the operation** and never falls back to searching every radio button on the page, preventing cross-group contamination.

### 2.5 Word-Boundary Discrimination
To prevent naive substring collisions (`"female".Contains("male") == true` or `"unmarried".Contains("married") == true`), our engine uses whole-token regex word boundaries `\bMale\b` and extracts direct text nodes, stripping child inputs.

### 2.6 Strict Custom ARIA Combobox Support & Post-Selection Verification
Supports both native `<select>` and custom comboboxes:
- `<div role="combobox">` and `<input role="combobox">`
- `aria-controls` and `aria-owns` relationships
- Dynamic `<ul role="listbox">` and `<li role="option">`
- Replaces fixed delays with explicit `WebDriverWait` for dynamic options
- Normalized exact option matching (throws `AmbiguousMatchException` on duplicate matching options)
- Respects `disabled` and `aria-disabled="true"`
- **Post-Selection Verification**: Asserts that `aria-selected="true"`, combobox text, or input value matches the requested option. Throws `InvalidOperationException` if not verified; **zero arbitrary fallbacks**.

### 2.7 Deterministic Stale Element Reference Recovery
Wraps page object actions in a bounded retry helper with a strict policy:
- **Maximum total attempts**: Exactly 3 attempts.
- **Retry condition**: Retries only `StaleElementReferenceException`.
- **Immediate propagation**: Never retries `AmbiguousMatchException` or `NoSuchElementException`.
- **Preserves original failure**: If attempt 3 fails, the original `StaleElementReferenceException` is preserved and thrown.

### 2.8 Hardened SafeClick
Rejects clicks on disabled elements (`InvalidElementStateException`) or invisible elements (`ElementNotInteractableException`). Does not blindly bypass `ElementClickInterceptedException` with JavaScript clicks, ensuring modals, sticky headers, and overlay issues are caught honestly.

---

## 3. Configuration & Multi-Browser Support

Configuration follows a 3-tier hierarchy:
1. **CLI Arguments**: Highest priority (e.g., `-- "https://custom-url"` or `-- NUnit.TargetUrl="..."`)
2. **Environment Variables**: Overrides config file (e.g., `TARGET_URL`, `BROWSER`, `HEADLESS`)
3. **`appsettings.json`**: Default project settings

### Supported Browsers & Driver Management
Configurable in `appsettings.json` or CLI via `Browser`:
- **`Chrome`** (Google Chrome with modern `--headless=new`, `--no-sandbox`, and `--disable-dev-shm-usage`)
- **`Firefox`** (Mozilla Firefox via GeckoDriver with headless support)
- **`Edge`** (Microsoft Edge Chromium with headless support)

Driver binaries are managed automatically via **Selenium Manager** (built into Selenium 4.52), resolving exact platform-compatible drivers for local and CI environments without fixed conflicting NuGet driver packages.

---

## 4. Test Suite Coverage

The test suite consists of **42 automated tests** across six categories:

### 4.1 Controlled Mutation Fixtures Matrix (20 Tests, `Tests/ResilienceControlledTests.cs`)

| Fixture ID | Test Description | Mutation Type | Expected Behavior |
|:---:|---|---|:---:|
| **01** | `Fixture_01_Original_DOM` | Baseline standard form | Discovery & fill 100% PASS |
| **02** | `Fixture_02_Reordered_Fields` | Gender before Name, State first | Correct semantic association |
| **03** | `Fixture_03_Changed_IDs` | Scrambled GUID IDs (`#id-9984`) | Found via container & text proximity |
| **04** | `Fixture_04_Changed_Names` | Obfuscated nested array names | Found via `<label>` association |
| **05** | `Fixture_05_Changed_Classes` | Obfuscated CSS (`.x9y2-fld`) | Layout & structure independence |
| **06** | `Fixture_06_Unrelated_Noise_Fields` | Extra noise textboxes & radios | Disregards irrelevant inputs |
| **07** | `Fixture_07_Duplicate_Labels_Ambiguity` | Competing identical fields | **Throws `AmbiguousMatchException`** |
| **08** | `Fixture_08_Multiple_Radio_Groups` | Gender + Marital Status + Newsletter | Scopes to correct radio group |
| **09** | `Fixture_09_Multiple_Dropdowns` | Country + State + City | Discovers specific "State" select |
| **10** | `Fixture_10_Deeply_Nested_Containers` | Input nested 5 levels deep in `<div>` | Ascends DOM tree for label association |
| **11** | `Fixture_11_Hidden_Duplicate_Controls` | Active input + `display:none` duplicate | Filters hidden, binds to visible |
| **12** | `Fixture_12_Missing_Required_Field` | Form without target field | **Throws `NoSuchElementException`** |
| **13** | `Fixture_13_Near_Score_Ambiguity` | Candidates within 15 score margin | **Throws `AmbiguousMatchException`** |
| **14** | `Fixture_14_Duplicate_Radio_Options` | Male/Female in Gender and Marital Status | Only Gender group selected |
| **15** | `Fixture_15_Aria_Only_Radio_Group` | Pure ARIA markup (`role="radiogroup"`) | Correct radio option selection |
| **16** | `Fixture_16_Custom_Aria_Combobox` | `role="combobox"` + `role="listbox"` | Selects option & verifies state |
| **17** | `Fixture_17_Dynamic_DOM_Replacement` | Target input replaced during runtime | Stale recovery & rediscovery PASS |
| **18** | `Fixture_18_Disabled_Duplicate_Controls` | Disabled input alongside active input | Disabled ignored, active selected |
| **19** | `Fixture_19_Punctuation_Normalization` | Punctuation/hyphens & "Billing First Name" | Exact normalized winner selected |
| **20** | `Fixture_20_Multiple_Forms_Scoping` | Shipping vs Billing forms | Scoped form fills; unscoped rejects |

### 4.2 Stale Element Recovery Suite (5 Tests, `Tests/StaleElementRecoveryTests.cs`)
- `Retry_StaleOnFirstAttempt_SucceedsOnSecond`: Verifies success on attempt 2 after stale.
- `Retry_StaleOnFirstTwoAttempts_SucceedsOnThird`: Verifies success on attempt 3 after two stales.
- `Retry_StaleOnAllThreeAttempts_PreservesOriginalException`: Verifies exactly 3 attempts and preservation of original exception.
- `Retry_AmbiguousMatchException_ImmediatelyPropagatesWithoutRetry`: Verifies no retry loops on ambiguity.
- `Retry_NoSuchElementException_ImmediatelyPropagatesWithoutRetry`: Verifies no retry loops on missing elements.

### 4.3 SafeClick Reliability Suite (4 Tests, `Tests/SafeClickTests.cs`)
- `SafeClick_NormalElement_ClickSucceeds`: Standard click interactability.
- `SafeClick_HiddenElement_ThrowsElementNotInteractableException`: Rejects hidden elements.
- `SafeClick_DisabledElement_ThrowsInvalidElementStateException`: Rejects disabled elements.
- `SafeClick_InterceptedElement_ThrowsElementClickInterceptedException`: Asserts overlay interception cannot be silently bypassed without opt-in.

### 4.4 Custom ARIA Combobox Suite (5 Tests, `Tests/CustomComboboxTests.cs`)
- `CustomCombobox_ValidOption_SelectsAndVerifiesSuccessfully`: Option selection and state check.
- `CustomCombobox_MissingOption_ThrowsNoSuchElementException`: Asserts available options listing.
- `CustomCombobox_UnreflectedSelection_ThrowsInvalidOperationException`: Asserts post-selection verification failure.
- `CustomCombobox_DisabledOption_IsIgnored`: Filters out `aria-disabled="true"`.
- `CustomCombobox_DuplicateMatchingOptions_ThrowsAmbiguousMatchException`: Rejects duplicate option ambiguity.

### 4.5 Browser Verification Suite (3 Tests, `Tests/BrowserVerificationTests.cs`)
- `Chrome_HeadlessDriver_InitializesAndDisposesCleanly`: Chrome headless lifecycle and disposal.
- `DriverFactory_UnsupportedBrowser_ThrowsDescriptiveInvalidOperationException`: Guards against unsupported browsers.
- `TestSettings_InvalidTimeout_ThrowsInvalidOperationException`: Validates timeout bounds (1–300s).

### 4.6 Live E2E Integration Suite (5 Tests, `Tests/PracticeFormTests.cs`)
- `FullWorkflow_EndToEnd_AutomatesAndSubmitsPracticeForm`: Full live workflow execution.
- `AutomateFirstName_InputsValueCorrectly`: Live input verification.
- `SelectGender_SelectsCorrectRadioOption`: Live radio selection and attribute check.
- `SelectState_SelectsCorrectDropdownOption`: Live `<select>` option verification.
- `SubmitForm_TriggersSubmissionSuccessfully`: Pre/post-submission outcome verification.

---

## 5. Execution Guide

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/)
- Google Chrome, Mozilla Firefox, or Microsoft Edge

### 5.1 Build
```powershell
dotnet build -c Release
```

### 5.2 Run the Standalone Automation Runner
```powershell
# Run with default settings (Chrome, GUI mode)
dotnet run

# Run headlessly
dotnet run -- --headless true

# Run against a custom URL
dotnet run -- "https://app.cloudqa.io/home/AutomationPracticeForm"
```

### 5.3 Run Controlled Suites
```powershell
# Execute all controlled suites (Resilience, Stale Recovery, SafeClick, Combobox)
dotnet test --filter "Category=Resilience | Category=StaleRecovery | Category=SafeClick | Category=CustomCombobox" -- NUnit.Headless="true"
```

### 5.4 Run Browser Verification Tests
```powershell
dotnet test --filter "Category=BrowserVerification" -- NUnit.Headless="true"
```

### 5.5 Run Live E2E Smoke Tests
```powershell
dotnet test --filter "Category=E2E" -- NUnit.Headless="true"
```

### 5.6 Run Complete Test Suite (42 Tests)
```powershell
dotnet test -c Release -- NUnit.Headless="true"
```

---

## 6. Continuous Integration (GitHub Actions)

The pipeline [`.github/workflows/ci.yml`](.github/workflows/ci.yml) executes four isolated, non-masking jobs:
1. **`build`**: Compiles Release binary. Fails CI on compiler errors.
2. **`controlled-resilience-tests`**: Executes all 34 controlled tests. Fails CI on any test failure.
3. **`browser-verification-tests`**: Validates driver options, disposal, and configuration guards.
4. **`live-e2e-smoke-tests`**: Runs smoke tests against the live practice form. Errors are reported honestly without `continue-on-error: true`.

TRX test reports and failure screenshots are published as artifacts on every run (`if: always()`).

---

## 7. Known Limitations & Edge Cases

1. **Canvas / WebGL Controls**: Forms rendered inside `<canvas>` or WebGL contexts have no DOM elements or accessibility nodes and require computer vision / OCR models.
2. **Closed Shadow DOM (`mode: "closed"`)**: Elements inside Shadow Roots with closed encapsulation cannot be accessed by external scripts without custom browser devtools protocol hooks.
3. **Unlabeled Icon-Only Elements with No Tooltips**: Fields that lack text labels, ARIA attributes, placeholders, or names cannot be semantically resolved without machine vision.
