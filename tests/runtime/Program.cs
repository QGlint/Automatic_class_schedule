using WinUIVerify.Models;
using WinUIVerify.Services;

string requirementsDir = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "requirements");
string reportsDir = args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "reports");
string appName = "Automatic_class_schedule";
string goldenDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "visual");

Directory.CreateDirectory(reportsDir);

Console.WriteLine("========================================");
Console.WriteLine(" WinUI Agent Runtime Verification");
Console.WriteLine("========================================");

// 1. Load requirements
Console.WriteLine("\n[1/5] Loading requirements...");
var spec = RequirementParser.LoadFromYaml(Path.Combine(requirementsDir, "ui.yaml"));
Console.WriteLine($"  Loaded {spec.Pages.Count} page spec(s)");

if (spec.Pages.Count == 0)
{
    Console.Error.WriteLine("  ERROR: No requirements found");
    return 1;
}

// 2. Launch app
Console.WriteLine("\n[2/5] Launching application...");
var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
{
    FileName = appName,
    UseShellExecute = true,
});
if (proc == null)
{
    Console.Error.WriteLine("  ERROR: Failed to launch application");
    return 1;
}
Console.WriteLine($"  PID: {proc.Id}");

// 3. Connect and inspect
Console.WriteLine("\n[3/5] Inspecting runtime...");
var inspector = new AppInspector();
if (!inspector.ConnectToProcess(appName))
{
    Console.Error.WriteLine("  ERROR: Could not connect to application");
    proc.Kill();
    return 1;
}
Console.WriteLine("  Connected");

var snapshot = inspector.CaptureSnapshot();
Console.WriteLine($"  Captured {snapshot.Controls.Count} controls");

// 4. Validate
Console.WriteLine("\n[4/5] Validating against requirements...");
var allWarnings = new List<LayoutWarning>();
var allBindingErrors = new List<BindingErrorInfo>();
var allAutomationErrors = new List<string>();

foreach (var (pageKey, pageReq) in spec.Pages)
{
    int layoutIssues = 0, ctrlIssues = 0;

    foreach (var (name, req) in pageReq.Layout)
    {
        var ctl = snapshot.Controls.Find(c =>
            c.Name == name || c.AutomationId == name);

        if (req.Visible == true && (ctl == null || ctl.Visibility != "Visible"))
        {
            allWarnings.Add(new LayoutWarning
            {
                Target = name,
                Expected = "visible:true",
                Actual = ctl?.Visibility ?? "not found",
            });
            layoutIssues++;
        }

        if (req.MinWidth.HasValue && ctl != null && ctl.Width < req.MinWidth.Value)
        {
            allWarnings.Add(new LayoutWarning
            {
                Target = name,
                Expected = $"width>={req.MinWidth.Value}",
                Actual = ctl.Width.ToString("F0"),
            });
            layoutIssues++;
        }

        if (req.MinHeight.HasValue && ctl != null && ctl.Height < req.MinHeight.Value)
        {
            allWarnings.Add(new LayoutWarning
            {
                Target = name,
                Expected = $"height>={req.MinHeight.Value}",
                Actual = ctl.Height.ToString("F0"),
            });
            layoutIssues++;
        }
    }

    foreach (var (name, req) in pageReq.Controls)
    {
        var ctl = snapshot.Controls.Find(c =>
            c.Name == name || c.AutomationId == name);

        if (req.Visible == true && (ctl == null || ctl.Visibility != "Visible"))
        {
            allWarnings.Add(new LayoutWarning
            {
                Target = name,
                Expected = "visible:true",
                Actual = ctl?.Visibility ?? "not found",
            });
            ctrlIssues++;
        }

        if (req.Enabled == true && ctl != null && !ctl.IsEnabled)
        {
            allWarnings.Add(new LayoutWarning
            {
                Target = name,
                Expected = "enabled:true",
                Actual = "false",
            });
            ctrlIssues++;
        }
    }

    Console.WriteLine($"  [{pageKey}] layout:{layoutIssues} controls:{ctrlIssues}");
}

// 5. Visual regression
Console.WriteLine("\n[5/5] Visual regression...");
int visualPass = 0, visualFail = 0;
if (Directory.Exists(goldenDir))
{
    var goldenFiles = Directory.GetFiles(goldenDir, "*.png", SearchOption.AllDirectories);
    foreach (var golden in goldenFiles)
    {
        var relPath = Path.GetRelativePath(goldenDir, golden);
        var actual = Path.Combine(reportsDir, relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(actual)!);

        inspector.CaptureScreenshot(actual);

        var vr = VisualRegression.Compare(golden, actual);
        Console.WriteLine($"  {relPath}: {vr.Difference}% diff ({vr.Status})");

        if (vr.Status == "PASS") visualPass++;
        else visualFail++;
    }
}
else
{
    Console.WriteLine("  No golden screenshots found, taking baseline...");
    var baseline = Path.Combine(reportsDir, "baseline.png");
    inspector.CaptureScreenshot(baseline);
    Console.WriteLine($"  Saved baseline: {baseline}");
}

inspector.Dispose();
proc.Kill();

// Generate report
var result = new VerificationResult
{
    BindingErrors = allBindingErrors,
    LayoutWarnings = allWarnings,
    AutomationErrors = allAutomationErrors,
};

result.Status = result.BindingErrors.Count == 0
    && result.LayoutWarnings.Count == 0
    && result.AutomationErrors.Count == 0
    && visualFail == 0
    ? "PASS" : "FAIL";

var reportPath = Path.Combine(reportsDir, "verify-report.json");
var reportJson = System.Text.Json.JsonSerializer.Serialize(new
{
    status = result.Status,
    timestamp = DateTime.UtcNow.ToString("O"),
    layout = new { total = result.LayoutWarnings.Count, items = result.LayoutWarnings.Select(w => new { w.Target, w.Expected, w.Actual }) },
    binding = new { total = result.BindingErrors.Count, items = result.BindingErrors },
    automation = new { total = result.AutomationErrors.Count, items = result.AutomationErrors },
    visual = new { pass = visualPass, fail = visualFail },
}, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

File.WriteAllText(reportPath, reportJson);
Console.WriteLine($"\nReport: {reportPath}");

Console.WriteLine($"\n========================================");
Console.WriteLine($" Result: {result.Status}");
Console.WriteLine($"   Layout:   {result.LayoutWarnings.Count}");
Console.WriteLine($"   Binding:  {result.BindingErrors.Count}");
Console.WriteLine($"   Automation: {result.AutomationErrors.Count}");
Console.WriteLine($"   Visual:   {visualPass} pass, {visualFail} fail");
Console.WriteLine($"========================================");

return result.Status == "PASS" ? 0 : 1;
