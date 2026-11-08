# CloudQA Task 1 - Resilient Selenium Automation

This project implements **Task 1: Automation that keeps working when the page changes** using C# (.NET 10) and Selenium WebDriver.

It automates and verifies the three required fields on `https://app.cloudqa.io/home/AutomationPracticeForm`:
1. **First Name** (text box)
2. **Gender** (radio button)
3. **State** (dropdown)

---

## How It Works (Resilience Strategy)

To ensure automation continues working even when page layouts or attributes change, the locator engine avoids fixed XPath or CSS selectors (such as `//div[2]/input` or `#fname`). Instead, it discovers elements dynamically using:

- **Label Association**: Matches `<label for="id">` and enclosing `<label>` elements by visible text.
- **Container Proximity**: Detects inputs sharing a common parent container (`.form-group`, `.field`, `.row`) with the matching label.
- **Accessibility Attributes**: Evaluates `aria-label`, `aria-labelledby`, and `autocomplete`.
- **Semantic Fallbacks**: Checks placeholder and name attributes as secondary evidence.
- **Disambiguation**: Evaluates candidates using confidence scoring and rejects ambiguous matches to prevent false-positive selections.
- **In-Browser Execution**: Scoring is performed directly in the browser engine in a single pass, keeping execution fast (~15–20 seconds).

---

## Getting Started

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/)
- Google Chrome installed

### 1. Build
```powershell
dotnet build
```

### 2. Run the Program
You can run the automation directly as a standalone console runner:
```powershell
# Run against the default practice form
dotnet run

# Run against a custom or modified test URL (as required by Point 4)
dotnet run -- "https://your-modified-page-url"
```

### 3. Run the Automated Tests
```powershell
# Run all tests headlessly
dotnet test

# Run tests with visible Chrome browser
dotnet test --filter "Category=E2E" -- NUnit.Headless="false"

# Run tests against a modified test URL
dotnet test -- NUnit.TargetUrl="https://your-modified-page-url"
```

---

## Configuration

Settings can be provided through three methods (in order of priority):
1. **Command Line**: `-- NUnit.TargetUrl="https://..."` or `dotnet run -- "https://..."`
2. **Environment Variables**: `TARGET_URL`, `HEADLESS`, `FIRST_NAME`, `GENDER`, `STATE`
3. **`appsettings.json`**: Default fallback values for local runs

```json
{
  "AutomationSettings": {
    "TargetUrl": "https://app.cloudqa.io/home/AutomationPracticeForm",
    "TimeoutSeconds": 15,
    "Headless": false,
    "Browser": "Chrome",
    "FirstName": "Ajay",
    "LastName": "Gollapudi",
    "Gender": "Male",
    "State": "India"
  }
}
```

---

## Project Structure

```
CloudQA_Task1_TestSuite/
├── CloudQA.ResilientAutomation.csproj   # Project file and NuGet dependencies
├── Program.cs                          # Standalone console entry point
├── appsettings.json                    # Default settings
├── README.md                           # Documentation
├── Configuration/
│   └── TestSettings.cs                 # Configuration loader
├── Core/
│   ├── DriverFactory.cs                # WebDriver initialization
│   └── ResilientElementFinder.cs       # Dynamic locator engine
├── Pages/
│   └── PracticeFormPage.cs             # Page object with form actions & assertions
└── Tests/
    ├── PracticeFormTests.cs            # Live E2E test suite
    └── ResilienceControlledTests.cs    # Tests for field reordering & scrambled IDs
```
