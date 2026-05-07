namespace GenMail.Core.Models;

public sealed record GenerationOptions(
    string Domain,
    string OutputRoot,
    bool SkipEmptyLines = true,
    int MinUsernameLength = 3,
    int MaxUsernameLength = 32,
    bool AllowAllDigitsUsernames = false,
    NumberMode NumberMode = NumberMode.BaseOnly,
    NumberPlacementMode NumberPlacementMode = NumberPlacementMode.SuffixOnly,
    string NumberPattern = "",
    IReadOnlyList<string>? SelectedRuleIds = null,
    DedupeMode DedupeMode = DedupeMode.PerRun,
    string? DedupeDbPath = null,
    int ProgressReportInterval = 1000,
    int MaxOutputEmails = 1_000_000,
    int MaxNumbersPerBase = 1_000,
    int MaxInputLinesBeforeWarning = 500_000
);

public sealed record InputRecord(long LineNumber, string OriginalInput, string TrimmedInput);
public sealed record NormalizedName(string OriginalInput, string Lowered, string First, string Middle, string Last, string All, string ReverseAll);
public sealed record UsernameCandidate(string RuleId, string Username);
public sealed record EmailCandidate(string Username, string Email);

public sealed record ProcessingCounters(
    long InputLines,
    long ValidInputs,
    long UsernamesGenerated,
    long EmailsGenerated,
    long DuplicateSkipped,
    long QualityRejected,
    long RejectedInputs
);

public sealed record ProgressSnapshot(long InputLinesProcessed, long EmailsGenerated, string CurrentInput);
public sealed record SafetyEstimate(long InputLines, int RulesPerInput, int NumbersPerBase, long EstimatedOutput);
public sealed record UsernameRuleDefinition(string Id, string Template);
public sealed record DedupeEntry(string Scope, string KeyMode, string Key, string Username, string Email, string SourceInput, DateTimeOffset CreatedAtUtc);

public sealed record ProcessingResult(
    string OutputDirectory,
    ProcessingCounters Counters,
    IReadOnlyList<string> WarningMessages,
    bool Cancelled
);
