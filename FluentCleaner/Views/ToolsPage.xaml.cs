// Imported and adapted from Winslopr app https://github.com/builtbybel/Winslopr/
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FluentCleaner.Views;

public enum ToolsCategoryType
{
    All, System, Privacy, Network, Apps, Debloat
}

public class ToolsCategory
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public ToolsCategoryType Type { get; set; }

    public ToolsCategory(string name, string icon, ToolsCategoryType type)
    {
        Name = name;
        Icon = icon;
        Type = type;
    }
}

public class ToolsDefinition
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string ScriptPath { get; set; } = string.Empty;
    public ScriptMeta Meta { get; set; } = new ScriptMeta();

    public string Description => Meta?.Description ?? string.Empty;
    public ToolsCategoryType Category => Meta?.Category ?? ToolsCategoryType.All;
    public List<string> Options => Meta?.Options ?? new List<string>();
    public bool SupportsInput => Meta?.SupportsInput ?? false;
    public string InputPlaceholder => Meta?.InputPlaceholder ?? string.Empty;
    public string PoweredByText => Meta?.PoweredByText ?? string.Empty;
    public string PoweredByUrl => Meta?.PoweredByUrl ?? string.Empty;
    public bool UseConsole => Meta?.UseConsole ?? false;
    public bool UseLog => Meta?.UseLog ?? false;

    public ToolsDefinition(string title, string icon, string scriptPath, ScriptMeta meta)
    {
        Title = title ?? string.Empty;
        Icon = icon ?? string.Empty;
        ScriptPath = scriptPath ?? string.Empty;
        Meta = meta ?? new ScriptMeta();
    }

    public ToolsDefinition()
    {
        Title = string.Empty;
        Icon = string.Empty;
        ScriptPath = string.Empty;
        Meta = new ScriptMeta();
    }
}

public record ScriptMeta
{
    public string Description { get; init; } = "";
    public List<string> Options { get; init; } = new();
    public ToolsCategoryType Category { get; init; } = ToolsCategoryType.All;
    public bool UseConsole { get; init; } = false;
    public bool UseLog { get; init; } = false;
    public bool SupportsInput { get; init; } = false;
    public string InputPlaceholder { get; init; } = "";
    public string PoweredByText { get; init; } = "";
    public string PoweredByUrl { get; init; } = "";

    public ScriptMeta() { }
}

public sealed partial class ToolsPage : Page, ISearchablePage
{
    private readonly List<ToolsDefinition> _allTools = new();
    private readonly ObservableCollection<ToolsCategory> _categories = new();
    private ToolsDefinition? _selectedTool;

    private ToolsCategoryType _category = ToolsCategoryType.All;
    private string _searchQuery = "";

    // Folder name next to the exe that holds .ps1 extension scripts
    private static readonly string ExtensionsDir =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions");

    public ToolsPage()
    {
        InitializeComponent();

        _categories.Add(new ToolsCategory("All", "\uE71D", ToolsCategoryType.All));
        _categories.Add(new ToolsCategory("System", "\uE770", ToolsCategoryType.System));
        _categories.Add(new ToolsCategory("Privacy", "\uE72E", ToolsCategoryType.Privacy));
        _categories.Add(new ToolsCategory("Network", "\uE839", ToolsCategoryType.Network));
        _categories.Add(new ToolsCategory("Apps", "\uE71D", ToolsCategoryType.Apps));
        _categories.Add(new ToolsCategory("Debloat", "\uE74D", ToolsCategoryType.Debloat));

        listCategories.ItemsSource = _categories;
        listCategories.SelectedIndex = 0;

        ClearDetails();
        LoadToolsAsync();
    }

    // ── ISearchablePage ───────────────────────────────────────────────────────
    public void OnSearch(string text) => ApplySearch(text);

    public void ApplySearch(string query)
    {
        _searchQuery = query ?? "";
        ApplyFilterAndSearch();
    }

    // ---------------- Loading ----------------

    private async void LoadToolsAsync()
    {
        _allTools.Clear();
        ClearDetails();

        if (!Directory.Exists(ExtensionsDir))
        {
            return;
        }

        string[] files = await Task.Run(() => Directory.GetFiles(ExtensionsDir, "*.ps1"));

        var loaded = await Task.Run(() =>
        {
            var list = new List<ToolsDefinition>();
            foreach (var path in files)
            {
                var title = Path.GetFileNameWithoutExtension(path);
                var meta = ReadMetadataFromScript(path);
                list.Add(new ToolsDefinition(title, PickIconForScript(title), path, meta));
            }
            return list;
        });

        _allTools.AddRange(loaded);
        ApplyFilterAndSearch();
    }

    // ---------------- Filter / Search ----------------

    private void listCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (listCategories.SelectedItem is ToolsCategory cat)
        {
            _category = cat.Type;
            ApplyFilterAndSearch();
        }
    }

    private void ApplyFilterAndSearch()
    {
        var q = (_searchQuery ?? "").Trim().ToLowerInvariant();

        var filtered = _allTools
            .Where(t =>
                (_category == ToolsCategoryType.All || t.Category == _category) &&
                (string.IsNullOrEmpty(q) ||
                 t.Title.ToLowerInvariant().Contains(q) ||
                 t.Description.ToLowerInvariant().Contains(q)))
            .OrderBy(t => t.Title)
            .ToList();

        if (filtered.Count > 0)
        {
            SetTool(filtered[0]);
        }
        else
        {
            ClearDetails();
        }
    }

    // ---------------- Details panel ----------------

    private void SetTool(ToolsDefinition? tool)
    {
        _selectedTool = tool;

        if (tool is null) { ClearDetails(); return; }

        lblIcon.Text = tool.Icon ?? "";
        lblTitle.Text = tool.Title ?? "";
        lblDescription.Text = tool.Description ?? "";
        progressRing.IsActive = false;

        // Options dropdown
        comboOptions.Items.Clear();
        if (tool.Options.Count > 0)
        {
            comboOptions.Visibility = Visibility.Visible;
            foreach (var opt in tool.Options)
                comboOptions.Items.Add(opt);
            comboOptions.SelectedIndex = 0;
        }
        else
        {
            comboOptions.Visibility = Visibility.Collapsed;
        }

        // Optional text input
        if (tool.SupportsInput)
        {
            textInput.Visibility = Visibility.Visible;
            textInput.PlaceholderText = string.IsNullOrWhiteSpace(tool.InputPlaceholder)
                ? "Enter input (e.g. IDs or raw arguments)"
                : tool.InputPlaceholder;
            textInput.Text = "";
        }
        else
        {
            textInput.Visibility = Visibility.Collapsed;
        }

        // Powered-by attribution
        if (!string.IsNullOrWhiteSpace(tool.PoweredByText) && !string.IsNullOrWhiteSpace(tool.PoweredByUrl))
        {
            linkPoweredBy.Content = tool.PoweredByText.Trim();
            linkPoweredBy.Tag = tool.PoweredByUrl.Trim();
            linkPoweredBy.Visibility = Visibility.Visible;
        }
        else
        {
            linkPoweredBy.Visibility = Visibility.Collapsed;
        }

        // Help info bar
        bool hasHelp = tool.Options.Any(o => o.Contains("help", StringComparison.OrdinalIgnoreCase));
        infoHelp.Title = "Help available";
        btnShowHelp.Content = "Show help";
        infoHelp.IsOpen = hasHelp;

        btnRun.Visibility = Visibility.Visible;
        btnUninstall.Visibility = Visibility.Visible;
    }

    private void ClearDetails()
    {
        _selectedTool = null;

        lblIcon.Text = "";
        lblTitle.Text = "";
        lblDescription.Text = "";

        comboOptions.Visibility = Visibility.Collapsed;
        comboOptions.Items.Clear();

        textInput.Visibility = Visibility.Collapsed;
        textInput.Text = "";

        linkPoweredBy.Visibility = Visibility.Collapsed;
        infoHelp.IsOpen = false;
        progressRing.IsActive = false;

        btnRun.Visibility = Visibility.Collapsed;
        btnUninstall.Visibility = Visibility.Collapsed;
    }

    // ---------------- Button handlers ----------------

    private async void btnRun_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTool is null) return;

        if (!File.Exists(_selectedTool.ScriptPath))
        {
            AppendLog("Script not found: " + _selectedTool.ScriptPath);
            return;
        }

        btnRun.IsEnabled = false;
        btnUninstall.IsEnabled = false;
        progressRing.IsActive = true;
        textLog.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, "");
        AppendLog($"── {_selectedTool.Title} ──");

        try
        {
            bool useConsole = _selectedTool.UseConsole;
            bool useLog = _selectedTool.UseLog;

            string? optionArg = null;
            if (comboOptions.Visibility == Visibility.Visible && comboOptions.SelectedItem is not null)
            {
                optionArg = comboOptions.SelectedItem.ToString()!;

                if (optionArg.EndsWith(" (console)", StringComparison.Ordinal))
                { useConsole = true; useLog = false; optionArg = optionArg[..^" (console)".Length].Trim(); }
                else if (optionArg.EndsWith(" (silent)", StringComparison.Ordinal))
                { useConsole = false; useLog = false; optionArg = optionArg[..^" (silent)".Length].Trim(); }
                else if (optionArg.EndsWith(" (log)", StringComparison.Ordinal))
                { useLog = true; useConsole = false; optionArg = optionArg[..^" (log)".Length].Trim(); }
            }

            string? inputArg = null;
            if (_selectedTool.SupportsInput && textInput.Visibility == Visibility.Visible)
            {
                var t = textInput.Text.Trim();
                if (!string.IsNullOrEmpty(t)) inputArg = t;
            }

            var extraArgs = new List<string>();
            if (!string.IsNullOrWhiteSpace(optionArg)) extraArgs.Add(optionArg);
            if (!string.IsNullOrWhiteSpace(inputArg)) extraArgs.Add(inputArg);

            await RunScriptAsync(_selectedTool.ScriptPath, extraArgs, useConsole, AppendLog);
        }
        catch (Exception ex)
        {
            AppendLog("Error: " + ex.Message);
        }
        finally
        {
            progressRing.IsActive = false;
            btnRun.IsEnabled = true;
            btnUninstall.IsEnabled = true;
        }
    }

    private async void btnUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTool is null) return;

        if (!File.Exists(_selectedTool.ScriptPath))
        {
            ClearDetails();
            LoadToolsAsync();
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Remove script",
            Content = $"Remove \"{_selectedTool.Title}\" from the Extensions folder?",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            File.Delete(_selectedTool.ScriptPath);
            ClearDetails();
            LoadToolsAsync();
        }
        catch (Exception ex)
        {
            AppendLog("Could not delete: " + ex.Message);
        }
    }

    private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var target = Directory.Exists(ExtensionsDir)
            ? ExtensionsDir
            : AppDomain.CurrentDomain.BaseDirectory;
        try { Process.Start("explorer.exe", target); }
        catch (Exception ex) { Debug.WriteLine($"Failed to open folder: {ex.Message}"); }
    }

    private async void btnShowHelp_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTool is null) return;
        var helpOpt = _selectedTool.Options
            .FirstOrDefault(o => o.Contains("help", StringComparison.OrdinalIgnoreCase));
        if (helpOpt is null) return;

        textLog.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, "");
        AppendLog($"── {_selectedTool.Title} — Help ──");
        await RunScriptAsync(_selectedTool.ScriptPath, new[] { helpOpt }, false, AppendLog);
    }

    private void btnClearLog_Click(object sender, RoutedEventArgs e) =>
        textLog.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, "");

    private void linkPoweredBy_Click(object sender, RoutedEventArgs e)
    {
        var url = linkPoweredBy.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Debug.WriteLine($"Failed to open powered by link: {ex.Message}"); }
    }

    // ---------------- Log output ----------------

    private void AppendLog(string line) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            textLog.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out string currentText);
            textLog.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, currentText + line + "\n");
        });

    // ---------------- Script execution ----------------

    private static Task RunScriptAsync(string scriptPath, IEnumerable<string> extraArgs, bool useConsole,
                                       Action<string>? onOutput = null) =>
        Task.Run(() =>
        {
            var psi = new ProcessStartInfo("powershell.exe");
            if (useConsole)
            {
                psi.ArgumentList.Add("-NoExit");
                psi.UseShellExecute = true;
            }
            else
            {
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
            }

            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-File");
            psi.ArgumentList.Add(scriptPath);

            foreach (var arg in extraArgs)
            {
                psi.ArgumentList.Add(arg);
            }

            if (useConsole)
            {
                Process.Start(psi);
                return;
            }

            using var p = new Process { StartInfo = psi };
            p.OutputDataReceived += (_, ev) => { if (!string.IsNullOrEmpty(ev.Data)) onOutput?.Invoke(ev.Data); };
            p.ErrorDataReceived += (_, ev) => { if (!string.IsNullOrEmpty(ev.Data)) onOutput?.Invoke("ERR: " + ev.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();
        });

    // ---------------- Metadata parsing ----------------

    private static ScriptMeta ReadMetadataFromScript(string scriptPath)
    {
        string description = "No description available.";
        var options = new List<string>();
        var category = ToolsCategoryType.All;
        bool useConsole = false, useLog = false, inputEnabled = false;
        string inputPh = "", poweredByText = "", poweredByUrl = "";

        try
        {
            foreach (var line in File.ReadLines(scriptPath).Take(15))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith("# Description:", StringComparison.OrdinalIgnoreCase))
                    description = line[14..].Trim();
                else if (line.StartsWith("# Category:", StringComparison.OrdinalIgnoreCase))
                    category = line[11..].Trim().ToLowerInvariant() switch
                    {
                        "system" => ToolsCategoryType.System,
                        "privacy" => ToolsCategoryType.Privacy,
                        "network" => ToolsCategoryType.Network,
                        "apps" => ToolsCategoryType.Apps,
                        "debloat" => ToolsCategoryType.Debloat,
                        _ => ToolsCategoryType.All
                    };
                else if (line.StartsWith("# Options:", StringComparison.OrdinalIgnoreCase))
                    options = line[10..].Split(';').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                else if (line.StartsWith("# Host:", StringComparison.OrdinalIgnoreCase))
                {
                    var raw = line[7..].Trim().ToLowerInvariant();
                    useConsole = raw == "console";
                    useLog = raw == "log";
                }
                else if (line.StartsWith("# Input:", StringComparison.OrdinalIgnoreCase))
                    inputEnabled = line[8..].Trim().ToLowerInvariant() is "true" or "yes" or "1";
                else if (line.StartsWith("# InputPlaceholder:", StringComparison.OrdinalIgnoreCase))
                    inputPh = line[19..].Trim();
                else if (line.StartsWith("# PoweredBy:", StringComparison.OrdinalIgnoreCase))
                    poweredByText = line[12..].Trim();
                else if (line.StartsWith("# PoweredUrl:", StringComparison.OrdinalIgnoreCase))
                    poweredByUrl = line[13..].Trim();
                else if (line.StartsWith('#') && description == "No description available.")
                    description = line.TrimStart('#').Trim();
            }
        }
        catch (Exception ex) { Debug.WriteLine($"Failed to read metadata from script '{scriptPath}': {ex.Message}"); }

        return new ScriptMeta
        {
            Description = description,
            Options = options,
            Category = category,
            UseConsole = useConsole,
            UseLog = useLog,
            SupportsInput = inputEnabled,
            InputPlaceholder = inputPh,
            PoweredByText = poweredByText,
            PoweredByUrl = poweredByUrl
        };
    }

    private static readonly Dictionary<string, string> _iconMap = new()
    {
        ["debloat"] = "🧹",
        ["network"] = "🌐",
        ["explorer"] = "📂",
        ["update"] = "🔄",
        ["context"] = "📋",
        ["backup"] = "💾",
        ["security"] = "🛡️",
        ["performance"] = "⚡",
        ["privacy"] = "🔒",
        ["app"] = "📦",
        ["setup"] = "⚙️",
        ["restore"] = "♻️",
        ["cache"] = "🗑️",
        ["defender"] = "🛡️",
        ["power"] = "🔌",
        ["install"] = "📥",
        ["boot"] = "🚀",
        ["clean"] = "🧼"
    };

    private static string PickIconForScript(string name)
    {
        name = (name ?? "").ToLowerInvariant();
        foreach (var kv in _iconMap)
            if (name.Contains(kv.Key)) return kv.Value;
        return "🔧";
    }
}
