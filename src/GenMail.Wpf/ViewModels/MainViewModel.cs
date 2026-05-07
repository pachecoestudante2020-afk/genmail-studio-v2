using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using GenMail.Core.Emailing;
using GenMail.Core.Generation;
using GenMail.Core.Models;
using GenMail.Core.Normalization;
using GenMail.Core.Numbering;
using GenMail.Core.Pipeline;
using GenMail.Core.Safety;
using GenMail.Wpf.Commands;
using GenMail.Wpf.Services;

namespace GenMail.Wpf.ViewModels;

public sealed record OptionItem<T>(T Value, string Label) where T : struct, Enum;

public sealed class RuleItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    public string Id { get; init; } = string.Empty;
    public string Example { get; set; } = string.Empty;
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly FileDialogService _fileDialog = new FileDialogService();
    private readonly FolderOpenService _folderOpen = new FolderOpenService();
    private readonly GenMailPipeline _pipeline = new GenMailPipeline();
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private bool _changingPreset;

    public string InputPath { get; set; } = string.Empty;
    public string Domain { get => _domain; set { _domain = value; OnPropertyChanged(); RefreshPreview(); } }
    private string _domain = "example.com";

    public ObservableCollection<RuleItemViewModel> RuleItems { get; } = new();
    public ObservableCollection<string> PreviewUsernames { get; } = new();
    public ObservableCollection<string> PreviewEmails { get; } = new();

    public ObservableCollection<OptionItem<NumberMode>> NumberModeOptions { get; } = new();
    public ObservableCollection<OptionItem<NumberPlacementMode>> NumberPlacementOptions { get; } = new();
    public ObservableCollection<OptionItem<DedupeMode>> DedupeModeOptions { get; } = new();
    public ObservableCollection<OptionItem<AliasFilterMode>> AliasFilterModeOptions { get; } = new();
    public ObservableCollection<string> Presets { get; } = new() { "Simple corporate", "Common email formats", "Full name formats", "Initial-based formats", "Maximum coverage", "Custom" };

    public string SelectedPreset { get => _selectedPreset; set { _selectedPreset = value; OnPropertyChanged(); ApplyPreset(); } }
    private string _selectedPreset = "Simple corporate";

    public OptionItem<NumberMode> SelectedNumberMode { get => _selectedNumberMode; set { _selectedNumberMode = value; NumberMode = value.Value; OnPropertyChanged(); RefreshPreview(); } }
    public OptionItem<NumberPlacementMode> SelectedNumberPlacementMode { get => _selectedNumberPlacementMode; set { _selectedNumberPlacementMode = value; NumberPlacementMode = value.Value; OnPropertyChanged(); RefreshPreview(); } }
    public OptionItem<DedupeMode> SelectedDedupeMode { get => _selectedDedupeMode; set { _selectedDedupeMode = value; DedupeMode = value.Value; OnPropertyChanged(); } }
    public OptionItem<AliasFilterMode> SelectedAliasFilterMode { get => _selectedAliasFilterMode; set { _selectedAliasFilterMode = value; AliasFilterMode = value.Value; OnPropertyChanged(); } }

    private OptionItem<NumberMode> _selectedNumberMode = new(NumberMode.BaseOnly, "No numbers");
    private OptionItem<NumberPlacementMode> _selectedNumberPlacementMode = new(NumberPlacementMode.SuffixOnly, "Add number at the end");
    private OptionItem<DedupeMode> _selectedDedupeMode = new(DedupeMode.PerRun, "Fast in-memory filtering");
    private OptionItem<AliasFilterMode> _selectedAliasFilterMode = new(AliasFilterMode.None, "Process names and usernames");

    public NumberMode NumberMode { get; set; } = NumberMode.BaseOnly;
    public NumberPlacementMode NumberPlacementMode { get; set; } = NumberPlacementMode.SuffixOnly;
    public string NumberRangeText { get => _numberRangeText; set { _numberRangeText = value; OnPropertyChanged(); RefreshPreview(); } }
    private string _numberRangeText = string.Empty;
    public DedupeMode DedupeMode { get; set; } = DedupeMode.PerRun;
    public AliasFilterMode AliasFilterMode { get; set; } = AliasFilterMode.None;
    public int MaxOutputEmails { get; set; } = 1_000_000;
    public int MaxNumbersPerBase { get; set; } = 1_000;
    public bool SplitOutputFiles { get; set; }
    public int? RowsPerOutputFile { get; set; } = 50000;
    public string StatusText { get; set; } = "Ready.";
    public string OutputFolder { get; set; } = string.Empty;
    public long InputLinesRead { get; set; }
    public long UsernamesGenerated { get; set; }
    public long EmailsWritten { get; set; }
    public long DuplicatesSkipped { get; set; }
    public long QualityRejected { get; set; }

    public RelayCommand BrowseInputFileCommand { get; }
    public RelayCommand SelectAllRulesCommand { get; }
    public RelayCommand ClearAllRulesCommand { get; }
    public RelayCommand DefaultsRulesCommand { get; }
    public AsyncRelayCommand EstimateCommand { get; }
    public AsyncRelayCommand StartCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand OpenOutputFolderCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        NumberModeOptions.Add(new(NumberMode.BaseOnly, "No numbers"));
        NumberModeOptions.Add(new(NumberMode.NumberedOnly, "Only numbered usernames"));
        NumberModeOptions.Add(new(NumberMode.BaseAndNumbered, "Base + numbered usernames"));
        NumberPlacementOptions.Add(new(NumberPlacementMode.SuffixOnly, "Add number at the end"));
        NumberPlacementOptions.Add(new(NumberPlacementMode.PrefixOnly, "Add number at the beginning"));
        NumberPlacementOptions.Add(new(NumberPlacementMode.InfixBeforeLastToken, "Add number before last name part"));
        NumberPlacementOptions.Add(new(NumberPlacementMode.SuffixAndPrefix, "End and beginning"));
        NumberPlacementOptions.Add(new(NumberPlacementMode.All, "All positions"));
        DedupeModeOptions.Add(new(DedupeMode.None, "No duplicate filtering"));
        DedupeModeOptions.Add(new(DedupeMode.PerRun, "Fast in-memory filtering"));
        DedupeModeOptions.Add(new(DedupeMode.Persistent, "SQLite filtering for large files"));
        AliasFilterModeOptions.Add(new(AliasFilterMode.None, "Process names and usernames"));
        AliasFilterModeOptions.Add(new(AliasFilterMode.AllowList, "Only existing usernames"));
        AliasFilterModeOptions.Add(new(AliasFilterMode.BlockList, "Only full names"));

        SelectedNumberMode = NumberModeOptions[0];
        SelectedNumberPlacementMode = NumberPlacementOptions[0];
        SelectedDedupeMode = DedupeModeOptions[1];
        SelectedAliasFilterMode = AliasFilterModeOptions[0];

        foreach (IUsernameRule rule in BuiltInUsernameRules.CreateDefault())
        {
            RuleItemViewModel item = new RuleItemViewModel { Id = rule.Id, IsSelected = true };
            item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(RuleItemViewModel.IsSelected) && !_changingPreset) { SelectedPreset = "Custom"; RefreshPreview(); } };
            RuleItems.Add(item);
        }

        BrowseInputFileCommand = new RelayCommand(_ => { string? p = _fileDialog.PickTxtFile(); if (!string.IsNullOrWhiteSpace(p)) { InputPath = p; OnPropertyChanged(nameof(InputPath)); } });
        SelectAllRulesCommand = new RelayCommand(_ => { foreach (RuleItemViewModel item in RuleItems) item.IsSelected = true; RefreshPreview(); });
        ClearAllRulesCommand = new RelayCommand(_ => { foreach (RuleItemViewModel item in RuleItems) item.IsSelected = false; RefreshPreview(); });
        DefaultsRulesCommand = new RelayCommand(_ => { SelectedPreset = "Simple corporate"; });
        EstimateCommand = new AsyncRelayCommand(_ => Task.Run(Estimate));
        StartCommand = new AsyncRelayCommand(_ => StartAsync(), _ => !_isRunning);
        CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => _isRunning);
        OpenOutputFolderCommand = new RelayCommand(_ => _folderOpen.Open(OutputFolder), _ => Directory.Exists(OutputFolder));

        RefreshPreview();
    }

    private void ApplyPreset()
    {
        _changingPreset = true;
        HashSet<string> ids = SelectedPreset switch
        {
            "Simple corporate" => new HashSet<string>(new[] { "firstlast", "first.dot.last" }),
            "Common email formats" => new HashSet<string>(new[] { "first.dot.last", "firstlast", "flast", "firstl" }),
            "Full name formats" => new HashSet<string>(new[] { "firstlast", "lastfirst", "firstmiddlelast", "all" }),
            "Initial-based formats" => new HashSet<string>(new[] { "flast", "firstl", "f.dot.last", "first.dot.l" }),
            "Maximum coverage" => RuleItems.Select(r => r.Id).ToHashSet(),
            _ => RuleItems.Where(r => r.IsSelected).Select(r => r.Id).ToHashSet(),
        };
        if (SelectedPreset != "Custom") foreach (RuleItemViewModel item in RuleItems) item.IsSelected = ids.Contains(item.Id);
        _changingPreset = false;
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        try
        {
            PreviewUsernames.Clear(); PreviewEmails.Clear();
            NormalizedName sample = new DefaultNameNormalizer().Normalize("Nguyen Van A");
            List<IUsernameRule> rules = BuiltInUsernameRules.CreateDefault().Where(r => RuleItems.Any(i => i.Id == r.Id && i.IsSelected)).ToList();
            foreach (RuleItemViewModel item in RuleItems)
            {
                IUsernameRule? rule = BuiltInUsernameRules.CreateDefault().FirstOrDefault(r => r.Id == item.Id);
                if (rule is not null) item.Example = rule.Apply(sample);
            }
            IReadOnlyList<string> nums = new NumberRangeParser().Parse(NumberRangeText, 1000);
            NumberExpansionService exp = new NumberExpansionService();
            EmailBuilder builder = new EmailBuilder();
            foreach (UsernameCandidate candidate in new UsernameGenerator().Generate(sample, rules))
            {
                foreach (string u in exp.Expand(candidate.Username, nums, NumberMode, NumberPlacementMode))
                {
                    if (PreviewUsernames.Count >= 20) break;
                    PreviewUsernames.Add(u);
                    PreviewEmails.Add(builder.Build(u, Domain));
                }
                if (PreviewUsernames.Count >= 20) break;
            }
            OnPropertyChanged(nameof(PreviewUsernames)); OnPropertyChanged(nameof(PreviewEmails));
        }
        catch { }
    }

    private void Estimate()
    {
        int selectedRules = RuleItems.Count(r => r.IsSelected);
        SafetyEstimate estimate = new OutputEstimator().Estimate(Math.Max(1, InputLinesRead), Math.Max(1, selectedRules), string.IsNullOrWhiteSpace(NumberRangeText) ? 1 : 10);
        StatusText = $"Estimated output (conservative): {estimate.EstimatedOutput}";
        OnPropertyChanged(nameof(StatusText));
    }

    private async Task StartAsync()
    {
        _isRunning = true; RaiseCommandStates(); _cts = new CancellationTokenSource();
        try
        {
            GenerationOptions options = new GenerationOptions(Domain, Path.Combine(Environment.CurrentDirectory, "output"), NumberMode: NumberMode, NumberPlacementMode: NumberPlacementMode, NumberPattern: NumberRangeText, SelectedRuleIds: RuleItems.Where(r => r.IsSelected).Select(r => r.Id).ToList(), DedupeMode: DedupeMode, MaxOutputEmails: MaxOutputEmails, MaxNumbersPerBase: MaxNumbersPerBase, SplitOutputFiles: SplitOutputFiles, RowsPerOutputFile: RowsPerOutputFile);
            Progress<ProgressSnapshot> progress = new Progress<ProgressSnapshot>(p => { InputLinesRead = p.InputLinesRead; UsernamesGenerated = p.UsernamesGenerated; EmailsWritten = p.EmailsWritten; DuplicatesSkipped = p.DuplicatesSkipped; QualityRejected = p.QualityRejected; StatusText = p.Status; OnPropertyChanged(nameof(InputLinesRead)); OnPropertyChanged(nameof(UsernamesGenerated)); OnPropertyChanged(nameof(EmailsWritten)); OnPropertyChanged(nameof(DuplicatesSkipped)); OnPropertyChanged(nameof(QualityRejected)); OnPropertyChanged(nameof(StatusText)); });
            ProcessingResult result = await _pipeline.RunAsync(InputPath, options, progress, _cts.Token).ConfigureAwait(true);
            OutputFolder = result.OutputDirectory; StatusText = "Completed."; OnPropertyChanged(nameof(OutputFolder)); OnPropertyChanged(nameof(StatusText));
        }
        catch (OperationCanceledException) { StatusText = "Cancelled."; OnPropertyChanged(nameof(StatusText)); }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; OnPropertyChanged(nameof(StatusText)); }
        finally { _isRunning = false; RaiseCommandStates(); }
    }

    private void RaiseCommandStates() { StartCommand.RaiseCanExecuteChanged(); CancelCommand.RaiseCanExecuteChanged(); OpenOutputFolderCommand.RaiseCanExecuteChanged(); }
    private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
