namespace GenMail.Core.Models;

public enum RejectionReason
{
    None = 0,
    Empty,
    TooShort,
    TooLong,
    InvalidCharacter,
    RepeatedSeparator,
    LeadingOrTrailingSeparator,
    LooksLikeEmail,
    LooksLikeUrl,
    AllDigits,
    Whitespace,
    InvalidInput,
    SafetyLimitExceeded,
}

public enum DedupeMode
{
    None = 0,
    PerRun,
    Persistent,
}

public enum NumberMode
{
    BaseOnly = 0,
    NumberedOnly,
    BaseAndNumbered,
}

public enum NumberPlacementMode
{
    SuffixOnly = 0,
    PrefixOnly,
    InfixBeforeLastToken,
    SuffixAndPrefix,
    All,
}

public enum AliasFilterMode
{
    None = 0,
    AllowList,
    BlockList,
}
