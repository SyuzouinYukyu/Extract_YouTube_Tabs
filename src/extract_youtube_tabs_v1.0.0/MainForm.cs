using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using K4os.Compression.LZ4;

namespace ExtractYoutubeTabsV100;

public sealed class MainForm : Form
{
    private const string AppName = "extract_youtube_tabs";
    private const string AppVersion = "v1.0.0";
    private const string DisplayTitle = AppName + "_" + AppVersion;
    private const string SettingsFileName = "extract_youtube_tabs_v1.0.0_settings.json";
    private const string DefaultOutputFileName = "list.txt";
    private static readonly byte[] MozLz4Magic = Encoding.ASCII.GetBytes("mozLz40\0");
    private static readonly Regex WatchRegex = new(@"^https?://(www\.)?youtube\.com/watch\?(?:.*?&)?v=([A-Za-z0-9_-]{11})(?:[&#].*)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ShortsRegex = new(@"^https?://(www\.)?youtube\.com/shorts/([A-Za-z0-9_-]{11})(?:[/?&#].*)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly TextBox _txtRecoveryPath = new();
    private readonly TextBox _txtProfileHint = new();
    private readonly TextBox _txtOutputDirectory = new();
    private readonly TextBox _txtOutputFileName = new();
    private readonly Label _lblOutputPreview = new();
    private readonly Label _lblStatus = new();
    private readonly Label _lblCount = new();
    private readonly RichTextBox _txtLog = new();
    private readonly Button _btnRun = new();

    public MainForm()
    {
        Text = DisplayTitle;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 760);
        Size = new Size(1080, 860);

        
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // アイコン取得失敗時は既定のまま
        }

BuildUi();
        ConfigureDragDrop();
        LoadSettings();
        RefreshOutputPreview();

        Log(DisplayTitle + " を起動しました。");
        Log("Firefox の recovery.jsonlz4 またはプロファイルフォルダを指定してください。");
        Log("D&D は全てのパス入力欄で利用できます。");
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 7,
            AutoSize = false,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        Controls.Add(root);

        var lblTitle = new Label
        {
            Text = DisplayTitle,
            Font = new Font("Yu Gothic UI", 16f, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(3, 3, 3, 2)
        };
        root.Controls.Add(lblTitle);

        var lblDesc = new Label
        {
            Text = "Firefox のタブ情報から YouTube / Shorts のURLを抽出し、youtu.be 形式で保存します。",
            Font = new Font("Yu Gothic UI", 10f, FontStyle.Regular),
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(3, 0, 3, 10)
        };
        root.Controls.Add(lblDesc);

        root.Controls.Add(CreateInputGroup());
        root.Controls.Add(CreateOutputGroup());
        root.Controls.Add(CreateInfoGroup());
        root.Controls.Add(CreateActionRow());
        root.Controls.Add(CreateStatusGroup());
        root.Controls.Add(CreateLogGroup());

        _txtRecoveryPath.TextChanged += (_, _) => RefreshOutputPreview();
        _txtOutputDirectory.TextChanged += (_, _) => RefreshOutputPreview();
        _txtOutputFileName.TextChanged += (_, _) => RefreshOutputPreview();
    }

    private Control CreateInputGroup()
    {
        var group = new GroupBox
        {
            Text = "入力設定",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10)
        };

        var table = Create4ColumnTable();
        group.Controls.Add(table);

        table.Controls.Add(CreateLabel("recovery.jsonlz4"), 0, 0);
        _txtRecoveryPath.Dock = DockStyle.Fill;
        table.Controls.Add(_txtRecoveryPath, 1, 0);
        table.Controls.Add(CreateButton("ファイル参照...", (_, _) => BrowseRecoveryFile()), 2, 0);
        table.Controls.Add(CreateButton("貼り付け", (_, _) => PasteRecoveryPath()), 3, 0);

        table.Controls.Add(CreateLabel("Firefoxプロファイル"), 0, 1);
        _txtProfileHint.Dock = DockStyle.Fill;
        table.Controls.Add(_txtProfileHint, 1, 1);
        table.Controls.Add(CreateButton("プロファイル参照...", (_, _) => BrowseFirefoxProfile()), 2, 1);
        table.Controls.Add(CreateButton("既定候補を入れる", (_, _) => FillDefaultFirefoxCandidates()), 3, 1);

        var hint = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(122, 74, 0),
            Text = "ヒント: プロファイルフォルダを選ぶと、その中の sessionstore-backups\\recovery.jsonlz4 を自動設定します。",
            Margin = new Padding(3, 0, 3, 6)
        };
        table.Controls.Add(hint, 0, 2);
        table.SetColumnSpan(hint, 4);

        return group;
    }

    private Control CreateOutputGroup()
    {
        var group = new GroupBox
        {
            Text = "出力設定",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10)
        };

        var table = Create4ColumnTable();
        group.Controls.Add(table);

        table.Controls.Add(CreateLabel("出力先フォルダ"), 0, 0);
        _txtOutputDirectory.Dock = DockStyle.Fill;
        table.Controls.Add(_txtOutputDirectory, 1, 0);
        table.Controls.Add(CreateButton("参照...", (_, _) => BrowseOutputDirectory()), 2, 0);
        table.Controls.Add(CreateButton("入力ファイルと同じ", (_, _) => SetOutputDirectorySameAsInput()), 3, 0);

        table.Controls.Add(CreateLabel("出力ファイル名"), 0, 1);
        _txtOutputFileName.Dock = DockStyle.Fill;
        _txtOutputFileName.Text = DefaultOutputFileName;
        table.Controls.Add(_txtOutputFileName, 1, 1);
        table.Controls.Add(CreateButton("初期化", (_, _) => ResetOutputFileName()), 2, 1);

        table.Controls.Add(CreateLabel("出力先プレビュー"), 0, 2);
        _lblOutputPreview.AutoSize = true;
        _lblOutputPreview.ForeColor = Color.FromArgb(0, 74, 127);
        _lblOutputPreview.Margin = new Padding(3, 8, 3, 6);
        table.Controls.Add(_lblOutputPreview, 1, 2);
        table.SetColumnSpan(_lblOutputPreview, 3);

        return group;
    }

    private Control CreateInfoGroup()
    {
        var group = new GroupBox
        {
            Text = "D&D状態",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10)
        };

        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        group.Controls.Add(panel);

        panel.Controls.Add(new Label { AutoSize = true, Text = "D&D: 有効" });
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(122, 74, 0),
            Text = "・recovery.jsonlz4欄: ファイルを落とすとそのまま設定、フォルダを落とすと recovery.jsonlz4 を自動探索\n"
                 + "・Firefoxプロファイル欄: フォルダを落とすと表示し、その中の recovery.jsonlz4 も自動設定\n"
                 + "・出力先フォルダ欄: ファイルを落とすと親フォルダ、フォルダを落とすとそのまま設定"
        });

        return group;
    }

    private Control CreateActionRow()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 10)
        };

        _btnRun.Text = "抽出して保存";
        _btnRun.AutoSize = true;
        _btnRun.Click += (_, _) => RunExtract();
        panel.Controls.Add(_btnRun);

        panel.Controls.Add(CreateButton("出力ファイルを開く", (_, _) => OpenOutputFile()));
        panel.Controls.Add(CreateButton("ログをクリア", (_, _) => _txtLog.Clear()));
        panel.Controls.Add(CreateButton("設定を保存", (_, _) => SaveSettings()));

        return panel;
    }

    private Control CreateStatusGroup()
    {
        var group = new GroupBox
        {
            Text = "状態",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10)
        };

        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        group.Controls.Add(panel);

        _lblStatus.AutoSize = true;
        _lblStatus.Text = "待機中";
        panel.Controls.Add(_lblStatus);

        _lblCount.AutoSize = true;
        _lblCount.Text = "抽出件数: 0";
        panel.Controls.Add(_lblCount);

        return group;
    }

    private Control CreateLogGroup()
    {
        var group = new GroupBox
        {
            Text = "ログ",
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        _txtLog.Dock = DockStyle.Fill;
        _txtLog.ReadOnly = true;
        _txtLog.Font = new Font("Consolas", 10f, FontStyle.Regular);
        _txtLog.BackColor = Color.White;
        group.Controls.Add(_txtLog);
        return group;
    }

    private static TableLayoutPanel Create4ColumnTable()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 3,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return table;
    }

    private static Label CreateLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(3, 8, 10, 8)
    };

    private static Button CreateButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(6)
        };
        button.Click += onClick;
        return button;
    }

    private void ConfigureDragDrop()
    {
        ConfigurePathDragDrop(_txtRecoveryPath, HandleRecoveryDrop);
        ConfigurePathDragDrop(_txtProfileHint, HandleProfileDrop);
        ConfigurePathDragDrop(_txtOutputDirectory, HandleOutputDirectoryDrop);
    }

    private static void ConfigurePathDragDrop(Control control, Action<string[]> onDrop)
    {
        control.AllowDrop = true;
        control.DragEnter += (_, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        };
        control.DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            {
                onDrop(paths);
            }
        };
    }

    private void HandleRecoveryDrop(string[] paths)
    {
        foreach (var item in paths)
        {
            if (File.Exists(item))
            {
                _txtRecoveryPath.Text = item;
                Log($"D&D入力(recovery): ファイル採用 -> {item}");
                return;
            }

            if (Directory.Exists(item))
            {
                var found = FindRecoveryFromFolder(item);
                if (found is not null)
                {
                    _txtRecoveryPath.Text = found;
                    Log($"D&D入力(recovery): フォルダから自動検出 -> {found}");
                }
                else
                {
                    var guess = Path.Combine(item, "sessionstore-backups", "recovery.jsonlz4");
                    _txtRecoveryPath.Text = guess;
                    Log($"D&D入力(recovery): 候補パスを設定 -> {guess}");
                }
                return;
            }
        }
    }

    private void HandleProfileDrop(string[] paths)
    {
        foreach (var item in paths)
        {
            string targetFolder;
            if (File.Exists(item))
            {
                targetFolder = Path.GetDirectoryName(item) ?? item;
            }
            else if (Directory.Exists(item))
            {
                targetFolder = item;
            }
            else
            {
                continue;
            }

            _txtProfileHint.Text = targetFolder;
            var found = FindRecoveryFromFolder(targetFolder);
            if (found is not null)
            {
                _txtRecoveryPath.Text = found;
                Log($"D&D入力(プロファイル): {targetFolder}");
                Log($"recovery.jsonlz4 を自動設定 -> {found}");
            }
            else
            {
                var guess = Path.Combine(targetFolder, "sessionstore-backups", "recovery.jsonlz4");
                _txtRecoveryPath.Text = guess;
                Log($"D&D入力(プロファイル): {targetFolder}");
                Log($"recovery.jsonlz4 候補を設定 -> {guess}");
            }
            return;
        }
    }

    private void HandleOutputDirectoryDrop(string[] paths)
    {
        foreach (var item in paths)
        {
            if (File.Exists(item))
            {
                var parent = Path.GetDirectoryName(item) ?? string.Empty;
                _txtOutputDirectory.Text = parent;
                Log($"D&D入力(出力先): ファイルの親フォルダ採用 -> {parent}");
                return;
            }

            if (Directory.Exists(item))
            {
                _txtOutputDirectory.Text = item;
                Log($"D&D入力(出力先): フォルダ採用 -> {item}");
                return;
            }
        }
    }

    private static string? FindRecoveryFromFolder(string folder)
    {
        var direct = Path.Combine(folder, "sessionstore-backups", "recovery.jsonlz4");
        if (File.Exists(direct))
        {
            return direct;
        }

        if (string.Equals(Path.GetFileName(folder), "sessionstore-backups", StringComparison.OrdinalIgnoreCase))
        {
            var direct2 = Path.Combine(folder, "recovery.jsonlz4");
            if (File.Exists(direct2))
            {
                return direct2;
            }
        }

        var candidate = Path.Combine(folder, "recovery.jsonlz4");
        return File.Exists(candidate) ? candidate : null;
    }

    private string GetSettingsPath() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);

    private void SaveSettings()
    {
        try
        {
            var settings = new AppSettings
            {
                RecoveryPath = _txtRecoveryPath.Text.Trim(),
                OutputDirectory = _txtOutputDirectory.Text.Trim(),
                OutputFileName = string.IsNullOrWhiteSpace(_txtOutputFileName.Text) ? DefaultOutputFileName : _txtOutputFileName.Text.Trim(),
                ProfileHint = _txtProfileHint.Text.Trim(),
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetSettingsPath(), json, new UTF8Encoding(false));
            Log($"設定保存: {GetSettingsPath()}");
        }
        catch (Exception ex)
        {
            Log($"設定保存失敗: {ex.Message}");
        }
    }

    private void LoadSettings()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            if (settings is null)
            {
                return;
            }

            _txtRecoveryPath.Text = settings.RecoveryPath ?? string.Empty;
            _txtOutputDirectory.Text = settings.OutputDirectory ?? string.Empty;
            _txtOutputFileName.Text = string.IsNullOrWhiteSpace(settings.OutputFileName) ? DefaultOutputFileName : settings.OutputFileName;
            _txtProfileHint.Text = settings.ProfileHint ?? string.Empty;
            Log($"設定読込: {path}");
        }
        catch (Exception ex)
        {
            Log($"設定読込失敗: {ex.Message}");
        }
    }

    private void BrowseRecoveryFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "recovery.jsonlz4 を選択",
            Filter = "Firefox recovery (recovery.jsonlz4)|recovery.jsonlz4|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _txtRecoveryPath.Text = dialog.FileName;
            Log($"入力ファイル選択: {dialog.FileName}");
        }
    }

    private void BrowseFirefoxProfile()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Firefox プロファイルフォルダを選択"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _txtProfileHint.Text = dialog.SelectedPath;
            var recovery = Path.Combine(dialog.SelectedPath, "sessionstore-backups", "recovery.jsonlz4");
            _txtRecoveryPath.Text = recovery;
            Log($"Firefoxプロファイル選択: {dialog.SelectedPath}");
            Log($"自動設定した recovery.jsonlz4: {recovery}");
        }
    }

    private void FillDefaultFirefoxCandidates()
    {
        try
        {
            var candidates = new List<string>();
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
            {
                var profilesRoot = Path.Combine(appData, "Mozilla", "Firefox", "Profiles");
                if (Directory.Exists(profilesRoot))
                {
                    foreach (var directory in Directory.GetDirectories(profilesRoot).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    {
                        candidates.Add(directory);
                    }
                }
            }

            var text = candidates.Count > 0 ? string.Join(" / ", candidates) : "候補が見つかりませんでした。";
            _txtProfileHint.Text = text;
            Log($"Firefoxプロファイル候補: {text}");
        }
        catch (Exception ex)
        {
            Log($"Firefoxプロファイル候補取得失敗: {ex.Message}");
        }
    }

    private void BrowseOutputDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "出力先フォルダを選択"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _txtOutputDirectory.Text = dialog.SelectedPath;
            Log($"出力先フォルダ選択: {dialog.SelectedPath}");
        }
    }

    private void SetOutputDirectorySameAsInput()
    {
        var recoveryPath = _txtRecoveryPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(recoveryPath))
        {
            return;
        }

        var parent = Path.GetDirectoryName(recoveryPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            _txtOutputDirectory.Text = parent;
            Log($"出力先を入力ファイルと同じ場所へ設定: {parent}");
        }
    }

    private void PasteRecoveryPath()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText().Trim();
                _txtRecoveryPath.Text = text;
                Log($"クリップボード貼り付け: {text}");
            }
        }
        catch (Exception ex)
        {
            Log($"クリップボード貼り付け失敗: {ex.Message}");
        }
    }

    private void ResetOutputFileName()
    {
        _txtOutputFileName.Text = DefaultOutputFileName;
        Log($"出力ファイル名を初期化: {DefaultOutputFileName}");
    }

    private void RefreshOutputPreview()
    {
        var outputPath = BuildOutputPath(ensureDirectory: false);
        _lblOutputPreview.Text = outputPath;
    }

    private string BuildOutputPath(bool ensureDirectory)
    {
        var outputFileName = string.IsNullOrWhiteSpace(_txtOutputFileName.Text) ? DefaultOutputFileName : _txtOutputFileName.Text.Trim();
        if (!outputFileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            outputFileName += ".txt";
        }

        var outputDirectory = _txtOutputDirectory.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            var recoveryPath = _txtRecoveryPath.Text.Trim();
            outputDirectory = !string.IsNullOrWhiteSpace(recoveryPath) && !string.IsNullOrWhiteSpace(Path.GetDirectoryName(recoveryPath))
                ? Path.GetDirectoryName(recoveryPath)!
                : AppDomain.CurrentDomain.BaseDirectory;
        }

        if (ensureDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
        }

        return Path.Combine(outputDirectory, outputFileName);
    }

    private void RunExtract()
    {
        var recoveryPath = _txtRecoveryPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(recoveryPath))
        {
            MessageBox.Show(this, "recovery.jsonlz4 を指定してください。", DisplayTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!File.Exists(recoveryPath))
        {
            MessageBox.Show(this, $"入力ファイルが見つかりません。\n{recoveryPath}", DisplayTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _btnRun.Enabled = false;
            _lblStatus.Text = "抽出中...";
            _lblCount.Text = "抽出件数: 計算中";
            Log($"読み込み開始: {recoveryPath}");

            var urls = LoadFirefoxTabs(recoveryPath);
            Log($"タブURL読込件数: {urls.Count}");

            var shortUrls = urls
                .Select(ConvertToYoutuBe)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList()!;

            var outputPath = BuildOutputPath(ensureDirectory: true);
            File.WriteAllLines(outputPath, shortUrls, new UTF8Encoding(true));

            _lblCount.Text = $"抽出件数: {shortUrls.Count}";
            _lblStatus.Text = "完了";
            Log($"保存完了: {outputPath}");
            Log($"抽出件数: {shortUrls.Count}");
            SaveSettings();

            MessageBox.Show(this, $"{shortUrls.Count} 件の URL を保存しました。\n{outputPath}", DisplayTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _lblStatus.Text = "エラー";
            Log($"❌ エラー: {ex.Message}");
            Log(ex.ToString());
            MessageBox.Show(this, $"エラーが発生しました。\n{ex.Message}", DisplayTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnRun.Enabled = true;
        }
    }

    private static List<string> LoadFirefoxTabs(string recoveryPath)
    {
        var bytes = File.ReadAllBytes(recoveryPath);
        if (bytes.Length < 12)
        {
            throw new InvalidDataException("ファイルサイズが不正です。Firefox の recovery.jsonlz4 を確認してください。");
        }

        for (var i = 0; i < MozLz4Magic.Length; i++)
        {
            if (bytes[i] != MozLz4Magic[i])
            {
                throw new InvalidDataException("Firefox の recovery.jsonlz4 ではありません。");
            }
        }

        var uncompressedLength = BitConverter.ToInt32(bytes, 8);
        if (uncompressedLength <= 0)
        {
            throw new InvalidDataException("復号サイズが不正です。Firefox セッションファイルを確認してください。");
        }

        var compressed = new byte[bytes.Length - 12];
        Buffer.BlockCopy(bytes, 12, compressed, 0, compressed.Length);

        var decoded = new byte[uncompressedLength];
        var decodedLength = LZ4Codec.Decode(compressed, 0, compressed.Length, decoded, 0, decoded.Length);
        if (decodedLength <= 0)
        {
            throw new InvalidDataException("LZ4 展開に失敗しました。");
        }

        var json = Encoding.UTF8.GetString(decoded, 0, decodedLength);
        using var document = JsonDocument.Parse(json);

        var result = new List<string>();
        if (!document.RootElement.TryGetProperty("windows", out var windowsElement) || windowsElement.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var windowElement in windowsElement.EnumerateArray())
        {
            if (!windowElement.TryGetProperty("tabs", out var tabsElement) || tabsElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var tabElement in tabsElement.EnumerateArray())
            {
                var index = 0;
                if (tabElement.TryGetProperty("index", out var indexElement) && indexElement.TryGetInt32(out var oneBasedIndex))
                {
                    index = Math.Max(oneBasedIndex - 1, 0);
                }

                if (!tabElement.TryGetProperty("entries", out var entriesElement) || entriesElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var entries = entriesElement.EnumerateArray().ToList();
                if (index < 0 || index >= entries.Count)
                {
                    continue;
                }

                var entry = entries[index];
                if (entry.ValueKind == JsonValueKind.Object
                    && entry.TryGetProperty("url", out var urlElement)
                    && urlElement.ValueKind == JsonValueKind.String)
                {
                    var url = urlElement.GetString();
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        result.Add(url);
                    }
                }
            }
        }

        return result;
    }

    private static string? ConvertToYoutuBe(string url)
    {
        var watchMatch = WatchRegex.Match(url);
        if (watchMatch.Success)
        {
            return $"https://youtu.be/{watchMatch.Groups[2].Value}";
        }

        var shortsMatch = ShortsRegex.Match(url);
        if (shortsMatch.Success)
        {
            return $"https://youtu.be/{shortsMatch.Groups[2].Value}";
        }

        return null;
    }

    private void OpenOutputFile()
    {
        try
        {
            var outputPath = BuildOutputPath(ensureDirectory: false);
            if (!File.Exists(outputPath))
            {
                MessageBox.Show(this, "出力ファイルがまだ存在しません。", DisplayTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = outputPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"出力ファイルを開けませんでした。\n{ex.Message}", DisplayTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Log(string message)
    {
        var line = message + Environment.NewLine;
        _txtLog.AppendText(line);
        _txtLog.SelectionStart = _txtLog.TextLength;
        _txtLog.ScrollToCaret();
    }
}