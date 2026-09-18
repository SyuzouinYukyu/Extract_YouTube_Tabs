namespace ExtractYoutubeTabsV100;

internal sealed class AppSettings
{
    public string RecoveryPath { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public string OutputFileName { get; set; } = "list.txt";
    public string ProfileHint { get; set; } = string.Empty;
}
