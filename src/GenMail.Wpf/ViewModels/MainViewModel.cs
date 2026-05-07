using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GenMail.Core.Generation;
using GenMail.Core.Models;
using GenMail.Core.Pipeline;
using GenMail.Core.Safety;
using GenMail.Wpf.Commands;
using GenMail.Wpf.Services;

namespace GenMail.Wpf.ViewModels;

public sealed class RuleItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    public string Id { get; init; } = string.Empty;
    public string Example { get; init; } = string.Empty;
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

    public string InputPath { get; set; } = string.Empty;
    public string Domain { get; set; } = "example.com";
    public ObservableCollection<RuleItemViewModel> RuleItems { get; } = new ObservableCollection<RuleItemViewModel>();
    public NumberMode NumberMode { get; set; } = NumberMode.BaseOnly;
    public NumberPlacementMode NumberPlacementMode { get; set; } = NumberPlacementMode.SuffixOnly;
    public string NumberRangeText { get; set; } = string.Empty;
    public DedupeMode DedupeMode { get; set; } = DedupeMode.PerRun;
    public AliasFilterMode AliasFilterMode { get; set; } = AliasFilterMode.None;
    public int MaxOutputEmails { get; set; } = 1_000_000;
    public int MaxNumbersPerBase { get; set; } = 1_000;
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

    public IEnumerable<NumberMode> NumberModes => Enum.GetValues<NumberMode>();
    public IEnumerable<NumberPlacementMode> NumberPlacementModes => Enum.GetValues<NumberPlacementMode>();
    public IEnumerable<DedupeMode> DedupeModes => Enum.GetValues<DedupeMode>();
    public IEnumerable<AliasFilterMode> AliasFilterModes => Enum.GetValues<AliasFilterMode>();

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        foreach (IUsernameRule rule in BuiltInUsernameRules.CreateDefault())
        {
            RuleItems.Add(new RuleItemViewModel { Id = rule.Id, Example = rule.Apply(new NormalizedName("John Smith", "john smith", "john", string.Empty, "smith", "johnsmith", "smithjohn")), IsSelected = true });
        }

        BrowseInputFileCommand = new RelayCommand(_ => { string? p = _fileDialog.PickTxtFile(); if (!string.IsNullOrWhiteSpace(p)) { InputPath = p; OnPropertyChanged(nameof(InputPath)); } });
        SelectAllRulesCommand = new RelayCommand(_ => { foreach (RuleItemViewModel item in RuleItems) item.IsSelected = true; });
        ClearAllRulesCommand = new RelayCommand(_ => { foreach (RuleItemViewModel item in RuleItems) item.IsSelected = false; });
        DefaultsRulesCommand = new RelayCommand(_ => { foreach (RuleItemViewModel item in RuleItems) item.IsSelected = item.Id is "firstlast" or "first.dot.last"; });
        EstimateCommand = new AsyncRelayCommand(_ => Task.Run(Estimate));
        StartCommand = new AsyncRelayCommand(_ => StartAsync(), _ => !_isRunning);
        CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => _isRunning);
        OpenOutputFolderCommand = new RelayCommand(_ => _folderOpen.Open(OutputFolder), _ => Directory.Exists(OutputFolder));
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
        _isRunning = true;
        RaiseCommandStates();
        _cts = new CancellationTokenSource();
        try
        {
            GenerationOptions options = new GenerationOptions(Domain, Path.Combine(Environment.CurrentDirectory, "output"), NumberMode: NumberMode, NumberPlacementMode: NumberPlacementMode, NumberPattern: NumberRangeText, SelectedRuleIds: RuleItems.Where(r => r.IsSelected).Select(r => r.Id).ToList(), DedupeMode: DedupeMode, MaxOutputEmails: MaxOutputEmails, MaxNumbersPerBase: MaxNumbersPerBase);
            Progress<ProgressSnapshot> progress = new Progress<ProgressSnapshot>(p =>
            {
                InputLinesRead = p.InputLinesRead; UsernamesGenerated = p.UsernamesGenerated; EmailsWritten = p.EmailsWritten; DuplicatesSkipped = p.DuplicatesSkipped; QualityRejected = p.QualityRejected; StatusText = p.Status;
                OnPropertyChanged(nameof(InputLinesRead)); OnPropertyChanged(nameof(UsernamesGenerated)); OnPropertyChanged(nameof(EmailsWritten)); OnPropertyChanged(nameof(DuplicatesSkipped)); OnPropertyChanged(nameof(QualityRejected)); OnPropertyChanged(nameof(StatusText));
            });
            ProcessingResult result = await _pipeline.RunAsync(InputPath, options, progress, _cts.Token).ConfigureAwait(true);
            OutputFolder = result.OutputDirectory;
            StatusText = "Completed.";
            OnPropertyChanged(nameof(OutputFolder)); OnPropertyChanged(nameof(StatusText));
        }
        catch (OperationCanceledException) { StatusText = "Cancelled."; OnPropertyChanged(nameof(StatusText)); }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; OnPropertyChanged(nameof(StatusText)); }
        finally { _isRunning = false; RaiseCommandStates(); }
    }

    private void RaiseCommandStates()
    {
        StartCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        OpenOutputFolderCommand.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
