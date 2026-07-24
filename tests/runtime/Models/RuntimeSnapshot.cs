namespace WinUIVerify.Models;

public sealed class RuntimeSnapshot
{
    public string Page { get; set; } = string.Empty;
    public bool Loaded { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public List<ControlSnapshot> Controls { get; set; } = new();
    public List<BindingErrorInfo> BindingErrors { get; set; } = new();
    public List<LayoutWarning> LayoutWarnings { get; set; } = new();
}

public sealed class ControlSnapshot
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AutomationId { get; set; } = string.Empty;
    public string Visibility { get; set; } = "Visible";
    public bool IsEnabled { get; set; } = true;
    public double Width { get; set; }
    public double Height { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class BindingErrorInfo
{
    public string Type => "BindingError";
    public string Path { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class LayoutWarning
{
    public string Type { get; set; } = "Layout";
    public string Target { get; set; } = string.Empty;
    public string Expected { get; set; } = string.Empty;
    public string Actual { get; set; } = string.Empty;
}

public sealed class VerificationResult
{
    public string Status { get; set; } = "PASS";
    public List<BindingErrorInfo> BindingErrors { get; set; } = new();
    public List<LayoutWarning> LayoutWarnings { get; set; } = new();
    public List<string> AutomationErrors { get; set; } = new();
    public double VisualDifference { get; set; }
}

public sealed class RequirementSpec
{
    public Dictionary<string, PageRequirement> Pages { get; set; } = new();
}

public sealed class PageRequirement
{
    public string Page { get; set; } = string.Empty;
    public Dictionary<string, ControlRequirement> Layout { get; set; } = new();
    public Dictionary<string, ControlRequirement> Controls { get; set; } = new();
}

public sealed class ControlRequirement
{
    public bool? Visible { get; set; }
    public bool? Enabled { get; set; }
    public double? MinWidth { get; set; }
    public double? MinHeight { get; set; }
}
