# CloudQA Resilient Automation Test Suite (C# & Selenium)

This repository contains a resilient end-to-end test automation solution in **C# (.NET 10)** using **Selenium WebDriver** and **NUnit** for `app.cloudqa.io/home/AutomationPracticeForm`.

It automates **every single detail** on the form while rigorously verifying the core required fields:
* **First Name** (text box) — *Core Required Field*
* **Last Name** (text box)
* **Gender** (radio button: Male / Female / Transgender) — *Core Required Field*
* **Date of Birth** (date format: `YYYY-MM-DD`)
* **Mobile #** (phone number)
* **Email** (email address)
* **Country** (text box)
* **State** (dropdown) — *Core Required Field*
* **Hobbies** (multi-select checkboxes: Dance, Reading, Cricket)
* **About Yourself** (textarea)
* **Description** (Summernote WYSIWYG Rich Text Editor / `contenteditable`)
* **Username** (text box)
* **Password & Confirm Password** (password inputs)
* **Terms & Conditions Agreement** (checkbox)
* **Submit Button** (resilient trigger)

---

## 🛠️ Resilient Architecture (Survives Layout & Structure Changes)

1. **No Fixed Selectors / Dynamic Heuristic Locators**:
   * Uses semantic attribute aliases (`fname`, `firstname`, `mobile#`, `dob`, `country`, `state`, etc.).
   * Checks explicit `label[for="id"]`, enclosing `<label><input ... /></label>`, and proximity/sibling lookups.
   * Handles text-node siblings for radio buttons and checkboxes using JavaScript evaluation.
   * Interacts with Summernote rich text editor via DOM `contenteditable` and event dispatching.
2. **Multi-Context Traversal**:
   * Discovers and automates **Shadow DOM custom elements** (`<shadow-form>`).
   * Discovers and automates **iFrames embedded inside Shadow Roots**.
   * Discovers and automates **Top-level and nested iFrames**.
   * Discovers and automates the **Root Form** using adaptive container detection (no fixed form ID).
3. **Adaptive Assertions**:
   * Asserts and verifies field values dynamically (`value == expected`, `radio.Selected`, `select.SelectedOption.Text == state`).
   * Avoids hardcoded form counts, ensuring compatibility with modified test pages.

---

## ⚙️ Configuration & URL Overrides

The target page URL is fully configurable:

### 1. NUnit Command Line Parameters
```bash
dotnet test -- NUnit.TargetUrl="https://app.cloudqa.io/home/AutomationPracticeForm"
```

### 2. Environment Variables
```bash
# Windows PowerShell
$env:TARGET_URL = "https://app.cloudqa.io/home/AutomationPracticeForm"
dotnet test

# Linux / macOS
export TARGET_URL="https://app.cloudqa.io/home/AutomationPracticeForm"
dotnet test
```

### 3. `appsettings.json`
Located in `CloudQA.ResilientAutomation/appsettings.json`:
```json
{
  "AutomationSettings": {
    "TargetUrl": "https://app.cloudqa.io/home/AutomationPracticeForm",
    "TimeoutSeconds": 15,
    "Headless": false
  }
}
```

---

## 🚀 How to Build & Run Tests

### Prerequisites
* [.NET 10.0 SDK](https://dotnet.microsoft.com/)
* Google Chrome installed

### Build
From the repository root:
```bash
dotnet build
```

### Run All Tests
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run in Headless Mode
```bash
dotnet test -- NUnit.Headless=true
```
Or set `"Headless": true` in `appsettings.json`.
