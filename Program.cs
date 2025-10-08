// 'Code Prompt Builder' - A .NET 9 WinForms Application
// Redesigned for a more professional and responsive UI.
// Author: Gemini
// Date: August 12, 2025
// Revision: Added persistence for TreeView checkbox selections.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

public class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

/// <summary>
/// Defines the structure for application settings to be persisted.
/// </summary>
public class AppSettings
{
    public string SourceDirectory { get; set; } = "";
    public string FileExtensions { get; set; } = "*.cs; *.csproj; *.sln; *.json; *.xml; *.xslt; *.sql; *.txt; *.md;";
    public string UserPrompt { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public bool IsSystemPromptEnabled { get; set; } = true;
    public bool IsUserPromptEnabled { get; set; } = true;
    public List<string> CheckedPaths { get; set; } = new List<string>();
    public List<string> CollapsedPaths { get; set; } = new List<string>();
}

/// <summary>
/// Handles saving and loading of application settings to a JSON file.
/// </summary>
public static class SettingsManager
{
    internal const string SettingsFileName = ".CodePromptBuilder";
    private static readonly string ExecutableDirectory = InitializeExecutableDirectory();

    public static AppSettings Load(string? preferredDirectory = null)
    {
        foreach (var directory in EnumerateCandidateDirectories(preferredDirectory))
        {
            string settingsPath = Path.Combine(directory, SettingsFileName);
            if (!File.Exists(settingsPath))
            {
                continue;
            }

            try
            {
                string jsonString = File.ReadAllText(settingsPath);
                var loadedSettings = JsonSerializer.Deserialize<AppSettings>(jsonString);
                if (loadedSettings != null)
                {
                    loadedSettings.CheckedPaths ??= new List<string>();
                    loadedSettings.CollapsedPaths ??= new List<string>();
                    loadedSettings.SourceDirectory = ResolveSourceDirectory(loadedSettings.SourceDirectory, preferredDirectory, directory);
                    return loadedSettings;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load settings from {settingsPath}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        string fallbackDirectory = ResolveSourceDirectory(null, preferredDirectory, Directory.GetCurrentDirectory());
        return new AppSettings
        {
            SourceDirectory = fallbackDirectory,
            CheckedPaths = new List<string>(),
            CollapsedPaths = new List<string>()
        };
    }

    public static void Save(AppSettings settings)
    {
        if (settings is null)
        {
            return;
        }

        var directories = EnumerateSaveDirectories();
        settings.CheckedPaths ??= new List<string>();
        settings.CollapsedPaths ??= new List<string>();

        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(settings, options);

        foreach (var directory in directories)
        {
            string settingsPath = Path.Combine(directory, SettingsFileName);
            try
            {
                File.WriteAllText(settingsPath, jsonString);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings to {settingsPath}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private static string InitializeExecutableDirectory()
    {
        try
        {
            string? baseDirectory = AppContext.BaseDirectory;
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return string.Empty;
            }

            return Path.GetFullPath(baseDirectory);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IEnumerable<string> EnumerateCandidateDirectories(string? preferredDirectory)
    {
        var directories = new List<string>();

        AddDirectoryIfValid(directories, preferredDirectory);
        AddDirectoryIfValid(directories, Directory.GetCurrentDirectory());
        AddDirectoryIfValid(directories, ExecutableDirectory);

        return directories;
    }

    private static IEnumerable<string> EnumerateSaveDirectories()
    {
        var directories = new List<string>();

        AddDirectoryIfValid(directories, Directory.GetCurrentDirectory());
        AddDirectoryIfValid(directories, ExecutableDirectory);

        return directories;
    }

    private static void AddDirectoryIfValid(List<string> directories, string? directory)
    {
        string? normalized = NormalizeExistingDirectory(directory);
        if (normalized is null)
        {
            return;
        }

        if (!directories.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            directories.Add(normalized);
        }
    }

    private static string ResolveSourceDirectory(string? storedDirectory, string? preferredDirectory, string fallbackDirectory)
    {
        // Favor the caller-provided directory (e.g. launch directory or user selection) over any persisted value.
        string? normalizedPreferred = NormalizeExistingDirectory(preferredDirectory);
        if (normalizedPreferred != null)
        {
            return normalizedPreferred;
        }

        string? normalizedStored = NormalizeExistingDirectory(storedDirectory);
        if (normalizedStored != null)
        {
            return normalizedStored;
        }

        string? normalizedFallback = NormalizeExistingDirectory(fallbackDirectory);
        if (normalizedFallback != null)
        {
            return normalizedFallback;
        }

        return string.Empty;
    }

    private static string? NormalizeExistingDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        try
        {
            string fullPath = Path.GetFullPath(directory);
            return Directory.Exists(fullPath) ? fullPath : null;
        }
        catch
        {
            return null;
        }
    }
}

internal sealed class GitIgnoreEvaluator
{
    private readonly string rootDirectory;
    private readonly List<GitIgnoreRule> rules;

    private GitIgnoreEvaluator(string rootDirectory, List<GitIgnoreRule> rules)
    {
        this.rootDirectory = rootDirectory;
        this.rules = rules;
    }

    public static GitIgnoreEvaluator? Load(string? rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return null;
        }

        try
        {
            string rootFullPath = Path.GetFullPath(rootDirectory);
            if (!Directory.Exists(rootFullPath))
            {
                return null;
            }

            var ruleList = new List<GitIgnoreRule>();
            IEnumerable<string> ignoreFiles;

            try
            {
                ignoreFiles = Directory.GetFiles(rootFullPath, ".gitignore", SearchOption.AllDirectories)
                    .OrderBy(path => GetDepth(Path.GetRelativePath(rootFullPath, Path.GetDirectoryName(path)!)))
                    .ThenBy(path => path, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                ignoreFiles = Array.Empty<string>();
            }

            foreach (var file in ignoreFiles)
            {
                try
                {
                    string baseDirectory = Path.GetDirectoryName(file)!;
                    string relativeBase = NormalizeRelative(Path.GetRelativePath(rootFullPath, baseDirectory));
                    foreach (string line in File.ReadAllLines(file))
                    {
                        var rule = GitIgnoreRule.Parse(line, relativeBase);
                        if (rule != null)
                        {
                            ruleList.Add(rule);
                        }
                    }
                }
                catch
                {
                    // Skip files we cannot read.
                }
            }

            if (ruleList.Count == 0)
            {
                return new GitIgnoreEvaluator(rootFullPath, ruleList);
            }

            return new GitIgnoreEvaluator(rootFullPath, ruleList);
        }
        catch
        {
            return null;
        }
    }

    public bool IsIgnored(string fullPath, bool isDirectory)
    {
        if (rules.Count == 0)
        {
            return false;
        }

        string relativePath;
        try
        {
            relativePath = NormalizeRelative(Path.GetRelativePath(rootDirectory, fullPath));
        }
        catch
        {
            return false;
        }

        if (string.IsNullOrEmpty(relativePath))
        {
            return false;
        }

        bool ignored = false;
        foreach (var rule in rules)
        {
            if (rule.Matches(relativePath, isDirectory))
            {
                ignored = !rule.IsNegation;
            }
        }

        return ignored;
    }

    private static int GetDepth(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath) || relativePath == ".")
        {
            return 0;
        }

        return relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static string NormalizeRelative(string path)
    {
        if (string.IsNullOrEmpty(path) || path == ".")
        {
            return string.Empty;
        }

        return path.Replace('\\', '/').TrimEnd('/');
    }

    private sealed class GitIgnoreRule
    {
        private readonly Regex regex;

        public string BasePath { get; }
        public bool IsNegation { get; }
        public bool DirectoryOnly { get; }
        public bool Anchored { get; }

        private GitIgnoreRule(string basePath, bool isNegation, bool directoryOnly, bool anchored, Regex regex)
        {
            BasePath = basePath;
            IsNegation = isNegation;
            DirectoryOnly = directoryOnly;
            Anchored = anchored;
            this.regex = regex;
        }

        public static GitIgnoreRule? Parse(string? line, string basePath)
        {
            if (string.IsNullOrEmpty(line))
            {
                return null;
            }

            string trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                return null;
            }

            if (trimmed.StartsWith("#"))
            {
                return null;
            }

            bool isNegation = trimmed.StartsWith("!");
            if (isNegation)
            {
                trimmed = trimmed.Substring(1);
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    return null;
                }
                trimmed = trimmed.Trim();
            }

            bool directoryOnly = trimmed.EndsWith("/");
            if (directoryOnly)
            {
                trimmed = trimmed.TrimEnd('/');
            }

            bool anchored = trimmed.StartsWith("/");
            if (anchored)
            {
                trimmed = trimmed.TrimStart('/');
            }

            if (trimmed.Length == 0)
            {
                return null;
            }

            string pattern = BuildRegexPattern(trimmed, anchored, directoryOnly);
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

            return new GitIgnoreRule(basePath, isNegation, directoryOnly, anchored, regex);
        }

        public bool Matches(string relativePath, bool isDirectory)
        {
            if (DirectoryOnly && !isDirectory)
            {
                return false;
            }

            string pathToEvaluate = relativePath;

            if (!string.IsNullOrEmpty(BasePath))
            {
                if (string.Equals(relativePath, BasePath, StringComparison.OrdinalIgnoreCase))
                {
                    pathToEvaluate = string.Empty;
                }
                else if (relativePath.StartsWith(BasePath + "/", StringComparison.OrdinalIgnoreCase))
                {
                    pathToEvaluate = relativePath.Substring(BasePath.Length + 1);
                }
                else
                {
                    return false;
                }
            }

            return regex.IsMatch(pathToEvaluate);
        }

        private static string BuildRegexPattern(string pattern, bool anchored, bool directoryOnly)
        {
            var sb = new StringBuilder();

            if (anchored)
            {
                sb.Append('^');
            }
            else
            {
                sb.Append("(^|.*/)");
            }

            for (int i = 0; i < pattern.Length; i++)
            {
                char c = pattern[i];
                if (c == '*')
                {
                    bool isDouble = i + 1 < pattern.Length && pattern[i + 1] == '*';
                    if (isDouble)
                    {
                        sb.Append(".*");
                        i++;
                    }
                    else
                    {
                        sb.Append("[^/]*");
                    }
                }
                else if (c == '?')
                {
                    sb.Append("[^/]");
                }
                else if (c == '/')
                {
                    sb.Append('/');
                }
                else if (c == '[')
                {
                    int end = pattern.IndexOf(']', i + 1);
                    if (end > i + 1)
                    {
                        string content = pattern.Substring(i + 1, end - i - 1);
                        bool negated = content.Length > 0 && (content[0] == '!' || content[0] == '^');
                        if (negated)
                        {
                            content = content.Substring(1);
                        }

                        var classBuilder = new StringBuilder();
                        for (int j = 0; j < content.Length; j++)
                        {
                            char cc = content[j];
                            if (cc == '\\')
                            {
                                classBuilder.Append("\\\\");
                            }
                            else
                            {
                                classBuilder.Append(cc);
                            }
                        }

                        sb.Append('[');
                        if (negated)
                        {
                            sb.Append('^');
                        }
                        sb.Append(classBuilder);
                        sb.Append(']');
                        i = end;
                    }
                    else
                    {
                        sb.Append(@"\[");
                    }
                }
                else
                {
                    sb.Append(Regex.Escape(c.ToString()));
                }
            }

            if (directoryOnly)
            {
                sb.Append("(/.*)?$");
            }
            else
            {
                sb.Append('$');
            }

            return sb.ToString();
        }
    }
}



/// <summary>
/// The main form for the Code Prompt Builder application.
/// </summary>
public class MainForm : Form
{
    // --- UI Controls ---
    private TextBox txtSourceDirectory = null!;
    private Button btnBrowse = null!;
    private TextBox txtFileExtensions = null!;
    private Button btnSelectExtensions = null!;
    private TreeView treeViewContent = null!;
    private Button btnClearSelection = null!;
    private CheckBox chkUserPrompt = null!;
    private TextBox txtUserPrompt = null!;
    private CheckBox chkSystemPrompt = null!;
    private TextBox txtSystemPrompt = null!;
    private Button btnGenerate = null!;
    private TextBox txtPreview = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel statusLabel = null!;

    // --- State ---
    private AppSettings currentSettings = new AppSettings();
    private string? currentDirectory;
    private bool isApplyingSettings;
    private GitIgnoreEvaluator? gitIgnoreEvaluator;

    public MainForm()
    {
        InitializeComponent();
        LoadInitialDirectory();
    }

    /// <summary>
    /// Sets up all the UI components and their properties using a professional layout.
    /// </summary>
    private void InitializeComponent()
    {
        this.Text = "Code Prompt Builder";
        this.Size = new Size(1200, 800);
        this.MinimumSize = new Size(900, 650);
        this.Icon = SystemIcons.Application;

        var mainTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        mainTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        this.Controls.Add(mainTable);

        var topPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10, 10, 10, 0),
            ColumnCount = 3
        };
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainTable.Controls.Add(topPanel, 0, 0);

        topPanel.Controls.Add(new Label { Text = "Source Directory", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        txtSourceDirectory = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        topPanel.Controls.Add(txtSourceDirectory, 1, 0);
        btnBrowse = new Button { Text = "Browse...", AutoSize = true, Anchor = AnchorStyles.Left };
        topPanel.Controls.Add(btnBrowse, 2, 0);

        topPanel.Controls.Add(new Label { Text = "File Extensions", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        txtFileExtensions = new TextBox { Dock = DockStyle.Fill };
        topPanel.Controls.Add(txtFileExtensions, 1, 1);
        btnSelectExtensions = new Button { Text = "Select", AutoSize = true, Anchor = AnchorStyles.Left };
        topPanel.Controls.Add(btnSelectExtensions, 2, 1);

        var contentTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(10)
        };
        contentTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        contentTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        mainTable.Controls.Add(contentTable, 0, 1);

        // --- LEFT PANEL: File Selection ---
        var fileGroup = new GroupBox { Text = "Source Files", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var fileLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        fileLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fileLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        fileGroup.Controls.Add(fileLayout);

        var fileButtonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 5)
        };
        btnClearSelection = new Button { Text = "Clear", AutoSize = true };
        fileButtonsPanel.Controls.Add(btnClearSelection);
        fileLayout.Controls.Add(fileButtonsPanel, 0, 0);

        treeViewContent = new TreeView { Dock = DockStyle.Fill, CheckBoxes = true, Font = new Font("Consolas", 9F) };
        fileLayout.Controls.Add(treeViewContent, 0, 1);
        contentTable.Controls.Add(fileGroup, 0, 0);

        // --- RIGHT PANEL ---
        var rightTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3
        };
        rightTable.RowStyles.Clear();
        rightTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3333F));
        rightTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3333F));
        rightTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3333F));
        contentTable.Controls.Add(rightTable, 1, 0);

        // Row 1: System Prompt
        var systemGroup = new GroupBox { Text = "System Prompt", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var systemLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        systemLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        systemLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        chkSystemPrompt = new CheckBox { Text = "Include System Prompt", Checked = true, AutoSize = true };
        txtSystemPrompt = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 9F) };
        systemLayout.Controls.Add(chkSystemPrompt, 0, 0);
        systemLayout.Controls.Add(txtSystemPrompt, 0, 1);
        systemGroup.Controls.Add(systemLayout);
        rightTable.Controls.Add(systemGroup, 0, 0);

        // Row 2: User Prompt
        var userGroup = new GroupBox { Text = "User Prompt", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var userLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        userLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        userLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        chkUserPrompt = new CheckBox { Text = "Include User Prompt", Checked = true, AutoSize = true };
        txtUserPrompt = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 9F) };
        userLayout.Controls.Add(chkUserPrompt, 0, 0);
        userLayout.Controls.Add(txtUserPrompt, 0, 1);
        userGroup.Controls.Add(userLayout);
        rightTable.Controls.Add(userGroup, 0, 1);

        // Row 3: Preview
        var previewGroup = new GroupBox { Text = "Preview", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var previewLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        previewLayout.ColumnStyles.Clear();
        previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        previewLayout.RowStyles.Clear();
        previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        previewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        txtPreview = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Font = new Font("Consolas", 9F), BackColor = SystemColors.Window, Margin = new Padding(0) };
        btnGenerate = new Button { Text = "Generate", AutoSize = true, Margin = new Padding(8, 8, 0, 0) };

        var previewButtonPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.None,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0),
            Padding = new Padding(0)
        };

        previewButtonPanel.Controls.Add(btnGenerate);

        previewLayout.Controls.Add(txtPreview, 0, 0);
        previewLayout.Controls.Add(previewButtonPanel, 0, 1);

        previewGroup.Controls.Add(previewLayout);
        rightTable.Controls.Add(previewGroup, 0, 2);

        // Status strip
        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel("Ready.");
        statusStrip.Items.Add(statusLabel);
        this.Controls.Add(statusStrip);

        // Event handlers
        btnBrowse.Click += BtnBrowse_Click;
        btnClearSelection.Click += BtnClearSelection_Click;
        btnSelectExtensions.Click += BtnSelectExtensions_Click;
        btnGenerate.Click += BtnGenerate_Click;

        txtSourceDirectory.TextChanged += TxtSourceDirectory_TextChanged;
        txtFileExtensions.TextChanged += OnSettingsFieldChanged;
        txtSystemPrompt.TextChanged += OnSettingsFieldChanged;
        txtUserPrompt.TextChanged += OnSettingsFieldChanged;
        chkSystemPrompt.CheckedChanged += OnSettingsFieldChanged;
        chkUserPrompt.CheckedChanged += OnSettingsFieldChanged;

        treeViewContent.BeforeCheck += TreeViewContent_BeforeCheck;
        treeViewContent.BeforeSelect += TreeViewContent_BeforeSelect;
        treeViewContent.AfterCheck += TreeViewContent_AfterCheck;
        treeViewContent.AfterCollapse += TreeViewContent_AfterCollapseExpand;
        treeViewContent.AfterExpand += TreeViewContent_AfterCollapseExpand;

        this.FormClosing += (_, __) => SaveSettings();
    }

    private void LoadInitialDirectory()
    {
        string startupDirectory = Directory.GetCurrentDirectory();
        var startupSettings = SettingsManager.Load(startupDirectory);

        string? initialDirectory = NormalizeDirectoryPath(startupSettings.SourceDirectory);
        if (initialDirectory != null && Directory.Exists(initialDirectory))
        {
            ApplyInitialDirectory(initialDirectory);
            return;
        }

        string? normalizedStartup = NormalizeDirectoryPath(startupDirectory);
        if (normalizedStartup != null)
        {
            ApplyInitialDirectory(normalizedStartup);
        }
        else
        {
            LoadDirectoryContext(null);
        }
    }

    private void ApplyInitialDirectory(string directory)
    {
        bool previousApplying = isApplyingSettings;
        isApplyingSettings = true;
        txtSourceDirectory.Text = directory;
        isApplyingSettings = previousApplying;
        LoadDirectoryContext(directory);
    }

    private void TxtSourceDirectory_TextChanged(object? sender, EventArgs e)
    {
        if (isApplyingSettings)
        {
            return;
        }

        HandleSourceDirectoryChanged(txtSourceDirectory.Text);
    }

    private void HandleSourceDirectoryChanged(string? directory)
    {
        if (!string.Equals(currentDirectory, directory, StringComparison.OrdinalIgnoreCase))
        {
            SaveSettings();
        }

        LoadDirectoryContext(directory);
    }

    private void LoadDirectoryContext(string? directory)
    {
        string? preferredDirectory = NormalizeDirectoryPath(directory);

        currentSettings = SettingsManager.Load(preferredDirectory);
        currentSettings.CheckedPaths ??= new List<string>();
        currentSettings.CollapsedPaths ??= new List<string>();

        string? resolvedDirectory = NormalizeDirectoryPath(currentSettings.SourceDirectory);
        bool directoryExists = !string.IsNullOrWhiteSpace(resolvedDirectory) && Directory.Exists(resolvedDirectory!);

        if (directoryExists)
        {
            currentDirectory = resolvedDirectory!;
            gitIgnoreEvaluator = GitIgnoreEvaluator.Load(resolvedDirectory!);
            TrySetProcessCurrentDirectory(resolvedDirectory!);
        }
        else
        {
            currentDirectory = null;
            gitIgnoreEvaluator = null;
        }

        bool previousApplying = isApplyingSettings;
        isApplyingSettings = true;

        try
        {
            string directoryText = directoryExists
                ? resolvedDirectory!
                : (preferredDirectory ?? currentSettings.SourceDirectory ?? string.Empty);
            currentSettings.SourceDirectory = directoryText;
            txtSourceDirectory.Text = directoryText;

            txtFileExtensions.Text = string.IsNullOrWhiteSpace(currentSettings.FileExtensions)
                ? new AppSettings().FileExtensions
                : currentSettings.FileExtensions;
            chkSystemPrompt.Checked = currentSettings.IsSystemPromptEnabled;
            chkUserPrompt.Checked = currentSettings.IsUserPromptEnabled;
            txtSystemPrompt.Text = currentSettings.SystemPrompt;
            txtUserPrompt.Text = currentSettings.UserPrompt;

            treeViewContent.BeginUpdate();
            try
            {
                treeViewContent.Nodes.Clear();

                if (directoryExists)
                {
                    var rootDirInfo = new DirectoryInfo(resolvedDirectory!);
                    var rootNode = CreateDirectoryNode(rootDirInfo);
                    treeViewContent.Nodes.Add(rootNode);
                    RecursivePopulate(rootNode, rootDirInfo);
                }
            }
            finally
            {
                treeViewContent.EndUpdate();
            }

            if (directoryExists)
            {
                ExpandAllNodes();
                ApplyCheckedPaths(currentSettings.CheckedPaths);
                ApplyCollapsedPaths(currentSettings.CollapsedPaths);
                statusLabel.Text = $"Loaded directory: {resolvedDirectory}";
            }
            else
            {
                statusLabel.Text = string.IsNullOrWhiteSpace(directoryText) ? "Ready." : "Directory not found.";
            }
        }
        finally
        {
            isApplyingSettings = previousApplying;
        }
    }

    private static string? NormalizeDirectoryPath(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(directory);
        }
        catch
        {
            return null;
        }
    }

    private void TrySetProcessCurrentDirectory(string directory)
    {
        try
        {
            string current = Directory.GetCurrentDirectory();
            if (string.Equals(current, directory, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Directory.SetCurrentDirectory(directory);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to set current directory: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private TreeNode CreateDirectoryNode(DirectoryInfo directoryInfo)
    {
        return new TreeNode(directoryInfo.Name) { Tag = directoryInfo.FullName };
    }

    private void ExpandAllNodes()
    {
        treeViewContent.BeginUpdate();
        foreach (TreeNode node in treeViewContent.Nodes)
        {
            node.ExpandAll();
        }
        treeViewContent.EndUpdate();
    }

    private void RecursivePopulate(TreeNode parentNode, DirectoryInfo directoryInfo)
    {
        try
        {
            foreach (var dir in directoryInfo.GetDirectories())
            {
                if (ShouldHidePath(dir.FullName, true))
                {
                    continue;
                }

                var dirNode = CreateDirectoryNode(dir);
                parentNode.Nodes.Add(dirNode);
                RecursivePopulate(dirNode, dir);
            }

            foreach (var file in directoryInfo.GetFiles())
            {
                if (ShouldHidePath(file.FullName, false))
                {
                    continue;
                }

                var fileNode = new TreeNode(file.Name) { Tag = file.FullName };
                parentNode.Nodes.Add(fileNode);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories that cannot be accessed.
        }
    }

    private bool ShouldHidePath(string fullPath, bool isDirectory)
    {
        if (isDirectory)
        {
            string directoryName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!string.IsNullOrEmpty(directoryName) && directoryName[0] == '.')
            {
                return true;
            }
        }

        if (gitIgnoreEvaluator == null)
        {
            return false;
        }

        return gitIgnoreEvaluator.IsIgnored(fullPath, isDirectory);
    }

    private void ApplyCheckedPaths(IEnumerable<string>? checkedPaths)
    {
        if (checkedPaths == null)
        {
            return;
        }

        var pathSet = new HashSet<string>(checkedPaths, StringComparer.OrdinalIgnoreCase);
        bool previousApplying = isApplyingSettings;
        isApplyingSettings = true;

        treeViewContent.BeginUpdate();

        void Traverse(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is string path && File.Exists(path) && pathSet.Contains(path))
                {
                    node.Checked = true;
                }

                if (node.Nodes.Count > 0)
                {
                    Traverse(node.Nodes);
                }
            }
        }

        Traverse(treeViewContent.Nodes);

        treeViewContent.EndUpdate();
        isApplyingSettings = previousApplying;
    }

    private void ApplyCollapsedPaths(IEnumerable<string>? collapsedPaths)
    {
        if (collapsedPaths == null)
        {
            return;
        }

        var pathSet = new HashSet<string>(collapsedPaths, StringComparer.OrdinalIgnoreCase);
        bool previousApplying = isApplyingSettings;
        isApplyingSettings = true;

        treeViewContent.BeginUpdate();

        void Traverse(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Nodes.Count > 0)
                {
                    if (node.Tag is string path && pathSet.Contains(path))
                    {
                        node.Collapse();
                    }

                    Traverse(node.Nodes);
                }
            }
        }

        Traverse(treeViewContent.Nodes);

        treeViewContent.EndUpdate();
        isApplyingSettings = previousApplying;
    }

    private List<string> GetCheckedNodePaths()
    {
        var paths = new List<string>();

        void Traverse(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Checked && node.Tag is string path && File.Exists(path))
                {
                    paths.Add(path);
                }

                if (node.Nodes.Count > 0)
                {
                    Traverse(node.Nodes);
                }
            }
        }

        Traverse(treeViewContent.Nodes);
        return paths;
    }

    private List<string> GetCollapsedNodePaths()
    {
        var collapsed = new List<string>();

        void Traverse(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Nodes.Count > 0)
                {
                    if (!node.IsExpanded && node.Tag is string path && Directory.Exists(path))
                    {
                        collapsed.Add(path);
                    }

                    Traverse(node.Nodes);
                }
            }
        }

        Traverse(treeViewContent.Nodes);
        return collapsed;
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the root source code directory"
        };

        if (!string.IsNullOrWhiteSpace(currentDirectory) && Directory.Exists(currentDirectory))
        {
            dialog.SelectedPath = currentDirectory;
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            if (string.Equals(txtSourceDirectory.Text, dialog.SelectedPath, StringComparison.OrdinalIgnoreCase))
            {
                SaveSettings();
                LoadDirectoryContext(dialog.SelectedPath);
            }
            else
            {
                txtSourceDirectory.Text = dialog.SelectedPath;
            }
        }
    }

    private void BtnClearSelection_Click(object? sender, EventArgs e)
    {
        bool previousApplying = isApplyingSettings;
        isApplyingSettings = true;

        treeViewContent.BeginUpdate();
        foreach (TreeNode node in treeViewContent.Nodes)
        {
            UncheckAllNodes(node);
        }
        treeViewContent.EndUpdate();

        isApplyingSettings = previousApplying;

        currentSettings.CheckedPaths = GetCheckedNodePaths();
        SaveSettings();
        statusLabel.Text = "Selection cleared.";
    }

    private void UncheckAllNodes(TreeNode node)
    {
        node.Checked = false;
        foreach (TreeNode child in node.Nodes)
        {
            UncheckAllNodes(child);
        }
    }

    private void BtnSelectExtensions_Click(object? sender, EventArgs e)
    {
        var patterns = ParseExtensionPatterns().ToList();
        if (!patterns.Any())
        {
            statusLabel.Text = "No extensions specified.";
            return;
        }

        int matches = 0;
        bool previousApplying = isApplyingSettings;
        isApplyingSettings = true;

        treeViewContent.BeginUpdate();

        void Traverse(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is string path && File.Exists(path))
                {
                    string fileName = Path.GetFileName(path);
                    if (patterns.Any(pattern => IsMatch(fileName, pattern)))
                    {
                        if (!node.Checked)
                        {
                            node.Checked = true;
                        }
                        matches++;
                    }
                }

                if (node.Nodes.Count > 0)
                {
                    Traverse(node.Nodes);
                }
            }
        }

        Traverse(treeViewContent.Nodes);

        treeViewContent.EndUpdate();
        isApplyingSettings = previousApplying;

        currentSettings.CheckedPaths = GetCheckedNodePaths();
        SaveSettings();

        statusLabel.Text = matches > 0
            ? $"Selected {matches} matching file(s)."
            : "No files matched the provided extensions.";
    }

    private IEnumerable<string> ParseExtensionPatterns()
    {
        return txtFileExtensions.Text
            .Split(';')
            .Select(ext => ext.Trim())
            .Where(ext => !string.IsNullOrEmpty(ext));
    }

    private void TreeViewContent_BeforeCheck(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node?.Tag is string path && Directory.Exists(path))
        {
            e.Cancel = true;
        }
    }

    private void TreeViewContent_BeforeSelect(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node?.Tag is string path && Directory.Exists(path))
        {
            e.Cancel = true;
        }
    }

    private void TreeViewContent_AfterCheck(object? sender, TreeViewEventArgs e)
    {
        if (isApplyingSettings)
        {
            return;
        }

        currentSettings.CheckedPaths = GetCheckedNodePaths();
        SaveSettings();
    }

    private void TreeViewContent_AfterCollapseExpand(object? sender, TreeViewEventArgs e)
    {
        if (isApplyingSettings)
        {
            return;
        }

        currentSettings.CollapsedPaths = GetCollapsedNodePaths();
        SaveSettings();
    }

    private void OnSettingsFieldChanged(object? sender, EventArgs e)
    {
        if (isApplyingSettings)
        {
            return;
        }

        SaveSettings();
    }

    private void SaveSettings()
    {
        if (isApplyingSettings)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(currentDirectory) || !Directory.Exists(currentDirectory))
        {
            return;
        }

        CaptureSettingsFromControls();
        SettingsManager.Save(currentSettings);
    }

    private void CaptureSettingsFromControls()
    {
        currentSettings.SourceDirectory = currentDirectory ?? string.Empty;
        currentSettings.FileExtensions = txtFileExtensions.Text;
        currentSettings.SystemPrompt = txtSystemPrompt.Text;
        currentSettings.UserPrompt = txtUserPrompt.Text;
        currentSettings.IsSystemPromptEnabled = chkSystemPrompt.Checked;
        currentSettings.IsUserPromptEnabled = chkUserPrompt.Checked;
        currentSettings.CheckedPaths ??= new List<string>();
        currentSettings.CollapsedPaths ??= new List<string>();
    }

    private static string NormalizeLineEndingsToCRLF(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        string crlf = NormalizeLineEndingsToLF(content).Replace("\n", "\r\n");
        return crlf;
    }

    private static string NormalizeLineEndingsToLF(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        string lf = content.Replace("\r\n", "\n").Replace("\n\r", "\n").Replace("\r", "\n");
        return lf;
    }

    private void BtnGenerate_Click(object? sender, EventArgs e)
    {
        try
        {
            statusLabel.Text = "Generating prompt...";
            Application.DoEvents();

            var promptBuilder = new StringBuilder();
            bool hasAddedPrompt = false;

            if (chkSystemPrompt.Checked && !string.IsNullOrWhiteSpace(txtSystemPrompt.Text))
            {
                promptBuilder.Append(txtSystemPrompt.Text.Trim());
                hasAddedPrompt = true;
            }

            if (chkUserPrompt.Checked && !string.IsNullOrWhiteSpace(txtUserPrompt.Text))
            {
                if (hasAddedPrompt)
                {
                    promptBuilder.AppendLine();
                    promptBuilder.AppendLine();
                }
                promptBuilder.Append(txtUserPrompt.Text.Trim());
            }

            List<string> filesToInclude = CollectFilesForPrompt();

            if (filesToInclude.Any())
            {
                if (hasAddedPrompt || (chkUserPrompt.Checked && !string.IsNullOrWhiteSpace(txtUserPrompt.Text)))
                {
                    promptBuilder.AppendLine();
                    promptBuilder.AppendLine();
                }
                promptBuilder.AppendLine("--- CONTENT ---");
            }

            foreach (var filePath in filesToInclude.OrderBy(f => f))
            {
                try
                {
                    string content = File.ReadAllText(filePath);

                    promptBuilder.AppendLine($"```{filePath}");
                    promptBuilder.AppendLine(content);
                    promptBuilder.AppendLine("```");
                    promptBuilder.AppendLine();
                }
                catch (Exception ex)
                {
                    promptBuilder.AppendLine($"// ERROR: Could not read file {filePath}. Reason: {ex.Message}");
                }
            }

            string finalPrompt = NormalizeLineEndingsToCRLF(promptBuilder.ToString().Trim());

            txtPreview.Text = finalPrompt;

            if (!string.IsNullOrWhiteSpace(finalPrompt))
            {
                Clipboard.SetText(finalPrompt);
            }
            else
            {
                Clipboard.Clear();
            }

            statusLabel.Text = $"Success! Prompt copied to clipboard. ({filesToInclude.Count} files, {finalPrompt.Length:N0} characters)";
        }
        catch (Exception ex)
        {
            string errorMessage = $"An unexpected error occurred: {ex.Message}";
            txtPreview.Text = errorMessage;
            statusLabel.Text = "Error generating prompt.";
            MessageBox.Show(errorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private List<string> CollectFilesForPrompt()
    {
        var fileSet = new HashSet<string>();
        var patterns = ParseExtensionPatterns().ToList();

        void Traverse(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                string? path = node.Tag as string;
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (node.Checked && File.Exists(path))
                {
                    if (!ShouldHidePath(path, false))
                    {
                        fileSet.Add(path);
                    }
                }
                else if (node.Checked && Directory.Exists(path))
                {
                    if (ShouldHidePath(path, true))
                    {
                        continue;
                    }

                    try
                    {
                        var filesInDir = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly);

                        foreach (string filePath in filesInDir)
                        {
                            var fileName = Path.GetFileName(filePath);
                            if (ShouldHidePath(filePath, false))
                            {
                                continue;
                            }

                            if (!patterns.Any() || patterns.Any(pattern => IsMatch(fileName, pattern)))
                            {
                                fileSet.Add(filePath);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore directories we cannot access.
                    }
                }

                if (node.Nodes.Count > 0)
                {
                    Traverse(node.Nodes);
                }
            }
        }

        Traverse(treeViewContent.Nodes);
        return fileSet.ToList();
    }

    private bool IsMatch(string input, string pattern)
    {
        string escaped = Regex.Escape(pattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".");

        var regexPattern = "^" + escaped + "$";
        return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
    }
}