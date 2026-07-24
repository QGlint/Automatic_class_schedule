using System;
using System.Collections.Generic;
using System.IO;

namespace Automatic_class_schedule.Infrastructure;

public static class RequirementValidator
{
    public static RequirementSpec LoadFromYaml(string path)
    {
        var lines = File.ReadAllLines(path);
        var spec = new RequirementSpec();
        string currentSection = "";
        string currentControl = "";

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            if (line.StartsWith("page:"))
            {
                spec.Page = line["page:".Length..].Trim();
                continue;
            }

            if (line.StartsWith("layout:") || line.StartsWith("controls:"))
            {
                currentSection = line.TrimEnd(':');
                currentControl = "";
                continue;
            }

            if (line.StartsWith("behavior:"))
            {
                currentSection = "behavior";
                continue;
            }

            if (currentSection == "layout" || currentSection == "controls")
            {
                if (line.EndsWith(':') && !line.StartsWith("  "))
                {
                    currentControl = line.TrimEnd(':').Trim();
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(currentControl))
                {
                    var parts = line.Split(':');
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var val = parts[1].Trim();

                        ControlRequirement req;
                        if (currentSection == "layout")
                            req = spec.Layout.TryGetValue(currentControl, out var lr) ? lr : new ControlRequirement();
                        else
                            req = spec.Controls.TryGetValue(currentControl, out var cr) ? cr : new ControlRequirement();

                        switch (key)
                        {
                            case "visible": req.Visible = bool.Parse(val); break;
                            case "enabled": req.Enabled = bool.Parse(val); break;
                            case "minWidth": req.MinWidth = double.Parse(val); break;
                            case "minHeight": req.MinHeight = double.Parse(val); break;
                        }

                        if (currentSection == "layout")
                            spec.Layout[currentControl] = req;
                        else
                            spec.Controls[currentControl] = req;
                    }
                }
            }
        }

        return spec;
    }

    public static VerificationResult Validate(RuntimeSnapshot snapshot, RequirementSpec spec)
    {
        var result = new VerificationResult();

        if (!string.IsNullOrWhiteSpace(spec.Page) && snapshot.Page != spec.Page)
        {
            result.LayoutWarnings.Add(new LayoutWarning
            {
                Target = "Page",
                Expected = spec.Page,
                Actual = snapshot.Page,
            });
        }

        var layoutResult = RuntimeInspector.VerifyLayout(snapshot, spec);
        result.BindingErrors.AddRange(layoutResult.BindingErrors);
        result.LayoutWarnings.AddRange(layoutResult.LayoutWarnings);
        result.AutomationErrors.AddRange(layoutResult.AutomationErrors);

        result.Status = result.BindingErrors.Count == 0
            && result.LayoutWarnings.Count == 0
            && result.AutomationErrors.Count == 0
            ? "PASS" : "FAIL";

        return result;
    }
}
