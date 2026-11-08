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
│        (Scoring Requests, ARIA Comboboxes & Stale Recovery)           │
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
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│                         Core/DriverFactory                             │
│     (Multi-Browser Lifecycle: Chrome, Firefox, Edge, Headless Mode)     │
└────────────────────────────────────────────────────────────────────────┘
```

### 2.1 The In-Browser Single-Pass Heuristic Engine
Standard Selenium locator iterations result in dozens of synchronous JSON-wire/W3C round trips across localhost, causing high latency (~1000ms+ per field). 

To solve this, `ResilientElementFinder` executes a **single JavaScript evaluation directly inside the browser's JavaScript V8/SpiderMonkey engine**. In a single DOM traversal (<15ms), all interactive candidate elements are scored against semantic cues and ranked mathematically.

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

### 2.6 Custom ARIA Combobox Support
Supports both native `<select>` and custom comboboxes:
- `<div role="combobox">` and `<input role="combobox">`
- `aria-controls` and `aria-owns` relationships
- Dynamic `<ul role="listbox">` and `<li role="option">`
- Post-selection state verification
- Descriptive `NoSuchElementException` with available options if missing; **zero arbitrary fallbacks**.

### 2.7 Dynamic DOM & Stale Element Recovery
Wraps page object actions in a bounded retry loop (up to 3 attempts) that intercepts `StaleElementReferenceException`, logs the event, and re-executes semantic discovery from scratch without reusing stale element references.

---

## 3. Configuration & Multi-Browser Support

Configuration follows a 3-tier hierarchy:
1. **CLI Arguments**: Highest priority (e.g., `-- "https://custom-url"` or `-- NUnit.TargetUrl="..."`)
2. **Environment Variables**: Overrides config file (e.g., `TARGET_URL`, `BROWSER`, `HEADLESS`)
3. **`appsettings.json`**: Default project settings

### Supported Browsers
Configurable in `appsettings.json` or CLI via `Browser`:
- **`Chrome`** (Google Chrome with W3C flags and headless support)
- **`Firefox`** (Mozilla Firefox via GeckoDriver with headless support)
- **`Edge`** (Microsoft Edge Chromium with headless support)

---

## 4. Test Suite Coverage

The test suite consists of **28 automated tests** across three categories:

### 4.1 Controlled Mutation Fixtures Matrix (20 Fixtures, `Tests/ResilienceControlledTests.cs`)

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

### 4.2 Browser Verification Suite (3 Tests, `Tests/BrowserVerificationTests.cs`)
- `Chrome_HeadlessDriver_InitializesAndDisposesCleanly`: Chrome driver lifecycle.
- `DriverFactory_UnsupportedBrowser_ThrowsDescriptiveInvalidOperationException`: Guards against unsupported browsers.
- `TestSettings_InvalidTimeout_ThrowsInvalidOperationException`: Validates timeout bounds (1–300s).

### 4.3 Live E2E Integration Suite (5 Tests, `Tests/PracticeFormTests.cs`)
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

### 5.3 Run Controlled Resilience Tests
```powershell
# Execute all 20 controlled mutation fixtures
dotnet test --filter "Category=Resilience" -- NUnit.Headless="true"
```

### 5.4 Run Browser Verification Tests
```powershell
dotnet test --filter "Category=BrowserVerification" -- NUnit.Headless="true"
```

### 5.5 Run Live E2E Tests
```powershell
dotnet test --filter "Category=E2E" -- NUnit.Headless="true"
```

### 5.6 Run Complete Suite (28 Tests)
```powershell
dotnet test -c Release -- NUnit.Headless="true"
```

---

## 6. Verified Test Results & Benchmark Metrics

All tests and standalone runners have been executed and verified in the environment:

```
Test Run Summary:
--------------------------------------------------------------------------------
Controlled Mutation Tests : Passed: 20, Failed: 0, Skipped: 0 (Duration: ~37s)
Live E2E Smoke Tests      : Passed:  5, Failed: 0, Skipped: 0 (Duration: ~6s)
Browser Verification Tests: Passed:  3, Failed: 0, Skipped: 0 (Duration: ~2s)
Total NUnit Tests         : Passed: 28, Failed: 0, Skipped: 0 (100% Pass Rate)
Standalone Program.cs     : Full form automation, verification & submission PASS
--------------------------------------------------------------------------------
```

---

## 7. Known Limitations & Edge Cases

1. **Canvas / WebGL Controls**: Forms rendered inside `<canvas>` or WebGL contexts have no DOM elements or accessibility nodes and require computer vision / OCR models.
2. **Closed Shadow DOM (`mode: "closed"`)**: Elements inside Shadow Roots with closed encapsulation cannot be accessed by external scripts without custom browser devtools protocol hooks.
3. **Unlabeled Icon-Only Elements with No Tooltips**: Fields that lack text labels, ARIA attributes, placeholders, or names cannot be semantically resolved without machine vision.
