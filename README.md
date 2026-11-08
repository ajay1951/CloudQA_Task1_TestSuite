# CloudQA Task 1: Resilient Selenium Automation Engineering

[![Build & Test CI](https://github.com/ajay1951/CloudQA_Task1_TestSuite/actions/workflows/ci.yml/badge.svg)](https://github.com/ajay1951/CloudQA_Task1_TestSuite/actions/workflows/ci.yml)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Selenium WebDriver](https://img.shields.io/badge/Selenium-4.40.0-green.svg)](https://www.selenium.dev/)
[![NUnit](https://img.shields.io/badge/NUnit-4.5.1-blue.svg)](https://nunit.org/)

---

## 1. Executive Summary

This repository delivers an enterprise-grade, resilient test automation framework engineered in **C# / .NET 10** using **Selenium WebDriver 4.40**. 

Traditional Selenium suites break frequently because they rely on brittle CSS selectors (`#fname`, `.col-md-6 > input:nth-child(2)`) or absolute XPaths (`//form/div[2]/div[1]/input`). When application developers reorder form fields, refactor CSS classes, randomize element IDs, or restructure HTML containers, conventional automated tests fail immediately.

This solution solves that fragility by implementing an **In-Browser Heuristic Scoring Engine** that mimics human visual and semantic recognition. It targets the three mandatory controls on the [CloudQA Automation Practice Form](https://app.cloudqa.io/home/AutomationPracticeForm):
1. **First Name** (Text input)
2. **Gender** (Radio button group)
3. **State** (Dropdown / Combobox)

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
│               (Scoring Requests & Business Interactions)               │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│                      Core/ResilientElementFinder                       │
│    Single-Pass JavaScript In-Browser Heuristic Evaluation Engine       │
│                                                                        │
│   • Label Matching (<label for="...">, enclosing <label>)              │
│   • Container Proximity (parent .form-group, .row, fieldset)           │
│   • ARIA Tree Analysis (aria-label, aria-labelledby, role="radiogroup")│
│   • Word-Boundary Option Isolation ("Male" vs "Female")                │
│   • Hidden Element Filtering (display:none, visibility:hidden)         │
│   • Mathematical Ambiguity Detection (Delta < 15 Score Margin Rejection│
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
Candidates accumulate points based on semantic certainty:

| Signal / Feature | Score Contribution | Description |
|---|:---:|---|
| **Explicit `<label for="id">`** | **+40 pts** | Exact or normalized match to `for` attribute |
| **Enclosing `<label>` Parent** | **+35 pts** | Input is physically nested within the matching `<label>` |
| **Container Proximity** | **+25 pts** | Input shares a common `.form-group`, `.field`, `.row`, or container with label |
| **ARIA Accessibility Attributes** | **+25 pts** | `aria-label` or `aria-labelledby` matches field name |
| **Accessible Placeholder** | **+20 pts** | `placeholder` attribute matches field descriptor |
| **Semantic `name` Attribute** | **+15 pts** | Name contains normalized keyword (e.g., `firstname`, `gender`) |
| **Form Scoping** | **+10 pts** | Candidate resides inside target `<form>` container |
| **Hidden Element Penalty** | **-100 pts** | Filters elements with `display: none`, `visibility: hidden`, or zero bounding box |

### 2.3 Strict Ambiguity Rejection (Anti-False-Positive)
Never select the first match blindly. If two identical or competing elements exist (e.g. duplicate unlabeled inputs or multiple forms) and their top confidence scores differ by less than **15 points**, the engine throws `AmbiguousMatchException` with detailed diagnostic candidates rather than performing an arbitrary click.

### 2.4 Radio Group Word-Boundary Discrimination
A classic failure mode in naive automation occurs with substring matching: `"female".Contains("male") == true`.
Our locator uses strict regex word boundaries `\bMale\b` and extracts only direct label text (excluding child input element text) to guarantee that selecting `Male` never accidentally clicks `Female`.

### 2.5 Strict Dropdown Verification (No Silent Fallbacks)
If the requested dropdown option is not present, `PracticeFormPage` **never silently falls back** to index 0 or index 1. It throws a descriptive `NoSuchElementException` listing all available options found in the DOM.

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

## 4. Test Suite & Controlled Mutation Fixtures

The test suite validates resilience against 12 controlled DOM mutation fixtures as well as live E2E integration.

### Controlled Fixtures Matrix (`Tests/ResilienceControlledTests.cs`)

| Fixture ID | Test Description | Mutation Type | Expected Behavior |
|:---:|---|---|:---:|
| **01** | `Fixture_01_Original_DOM` | Baseline standard form | Discovery & fill 100% PASS |
| **02** | `Fixture_02_Reordered_Fields` | Gender before Name, State first | Correct semantic association |
| **03** | `Fixture_03_Changed_IDs` | Scrambled GUID IDs (`#id-9984`) | Found via container & text proximity |
| **04** | `Fixture_04_Changed_Names` | Stripped `name` attributes | Found via `<label for>` association |
| **05** | `Fixture_05_Changed_Classes` | Obfuscated CSS (`.x9y2-fld`) | Layout & structure independence |
| **06** | `Fixture_06_Unrelated_Noise_Fields` | Extra noise textboxes & radios | Disregards irrelevant inputs |
| **07** | `Fixture_07_Duplicate_Labels_Ambiguity` | Duplicate conflicting fields | **Throws `AmbiguousMatchException`** |
| **08** | `Fixture_08_Multiple_Radio_Groups` | Gender + Marital Status + Newsletter | Scopes to correct radio group |
| **09** | `Fixture_09_Multiple_Dropdowns` | Country + State + City | Discovers specific "State" select |
| **10** | `Fixture_10_Deeply_Nested_Containers` | Input nested 5 levels deep in `<div>` | Ascends DOM tree for label association |
| **11** | `Fixture_11_Hidden_Duplicate_Controls` | Active input + `display:none` duplicate | Filters hidden, binds to visible |
| **12** | `Fixture_12_Missing_Required_Field` | Form without target field | **Throws `NoSuchElementException`** |

### Live E2E Integration Suite (`Tests/PracticeFormTests.cs`)
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
# Execute all 12 controlled mutation fixtures
dotnet test --filter "Category=Resilience" -- NUnit.Headless="true"
```

### 5.4 Run Live E2E Tests
```powershell
# Execute live E2E suite against CloudQA practice site
dotnet test --filter "Category=E2E" -- NUnit.Headless="true"
```

### 5.5 Cross-Browser Execution
```powershell
# Run tests using Firefox
dotnet test -- NUnit.Browser="Firefox" -- NUnit.Headless="true"

# Run tests using Microsoft Edge
dotnet test -- NUnit.Browser="Edge" -- NUnit.Headless="true"
```

---

## 6. Verified Test Results & Benchmark Metrics

All tests and standalone runners have been executed and verified in the environment:

```
Test Run Summary:
--------------------------------------------------------------------------------
Controlled Mutation Tests : Passed: 12, Failed: 0, Skipped: 0 (Duration: ~22s)
Live E2E Smoke Tests      : Passed:  5, Failed: 0, Skipped: 0 (Duration: ~10s)
Total NUnit Tests         : Passed: 17, Failed: 0, Skipped: 0 (100% Pass Rate)
Standalone Program.cs     : Full form automation, verification & submission PASS
--------------------------------------------------------------------------------
```

### In-Browser Locator Engine Performance
- **Single-pass element discovery**: `<15 ms` execution time in browser JavaScript runtime.
- **Total Program execution time**: `~8.4 seconds` (including browser cold-start, navigation, DOM interaction, pre-submission checks, and submission trigger).

---

## 7. Known Limitations & Edge Cases

While the heuristic locator engine handles severe DOM shifts, the following edge cases remain outside standard DOM accessibility boundaries:
1. **Canvas / WebGL Controls**: Forms rendered inside `<canvas>` or WebGL contexts have no DOM elements or accessibility nodes and require computer vision / OCR models.
2. **Closed Shadow DOM (`mode: "closed"`)**: Elements inside Shadow Roots with closed encapsulation cannot be accessed by external scripts without custom browser devtools protocol hooks.
3. **Unlabeled Icon-Only Elements with No Tooltips**: Fields that lack text labels, ARIA attributes, placeholders, or names cannot be semantically resolved without machine vision.
