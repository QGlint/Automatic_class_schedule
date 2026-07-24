using WinUIVerify.Models;

namespace WinUIVerify.Services;

public static class RequirementParser
{
    public static RequirementSpec LoadFromYaml(string path)
    {
        var spec = new RequirementSpec();

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Requirement file not found: {path}");
            return spec;
        }

        var lines = File.ReadAllLines(path);
        string currentPage = "";
        string currentSection = "";
        string currentControl = "";

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            if (!line.StartsWith("  ") && line.EndsWith(':') && char.IsLetter(line[0]))
            {
                currentPage = line.TrimEnd(':').Trim();
                currentSection = "";
                currentControl = "";

                if (!spec.Pages.ContainsKey(currentPage))
                    spec.Pages[currentPage] = new PageRequirement { Page = currentPage };

                continue;
            }

            if (string.IsNullOrWhiteSpace(currentPage))
                continue;

            var trimmed = raw.TrimStart();
            var indent = raw.Length - trimmed.Length;

            if (indent == 2 && (trimmed == "layout:" || trimmed == "controls:"))
            {
                currentSection = trimmed.TrimEnd(':');
                currentControl = "";
                continue;
            }

            if (indent == 4 && trimmed.EndsWith(':'))
            {
                currentControl = trimmed.TrimEnd(':').Trim();
                continue;
            }

            if (indent == 6 && !string.IsNullOrWhiteSpace(currentControl) && trimmed.Contains(':'))
            {
                var parts = trimmed.Split(':', 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var val = parts[1].Trim();

                    var page = spec.Pages[currentPage];
                    ControlRequirement req;
                    if (currentSection == "layout")
                    {
                        req = page.Layout.TryGetValue(currentControl, out var lr) ? lr : new ControlRequirement();
                    }
                    else
                    {
                        req = page.Controls.TryGetValue(currentControl, out var cr) ? cr : new ControlRequirement();
                    }

                    switch (key)
                    {
                        case "visible": req.Visible = bool.Parse(val); break;
                        case "enabled": req.Enabled = bool.Parse(val); break;
                        case "minWidth": req.MinWidth = double.Parse(val); break;
                        case "minHeight": req.MinHeight = double.Parse(val); break;
                    }

                    if (currentSection == "layout")
                        page.Layout[currentControl] = req;
                    else
                        page.Controls[currentControl] = req;
                }
            }
        }

        return spec;
    }
}
