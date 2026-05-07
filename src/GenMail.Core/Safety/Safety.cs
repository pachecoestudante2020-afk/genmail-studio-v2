using GenMail.Core.Models;

namespace GenMail.Core.Safety;

public sealed class OutputEstimator
{
    public SafetyEstimate Estimate(long inputLines, int rulesPerInput, int numbersPerBase) =>
        new SafetyEstimate(inputLines, rulesPerInput, numbersPerBase, inputLines * rulesPerInput * Math.Max(1, numbersPerBase));
}

public sealed class SafetyGuard
{
    public void EnsureWithinLimits(SafetyEstimate estimate, GenerationOptions options)
    {
        if (estimate.NumbersPerBase > options.MaxNumbersPerBase)
        {
            throw new InvalidOperationException("Too many numbers per base username.");
        }

        if (estimate.EstimatedOutput > options.MaxOutputEmails)
        {
            throw new InvalidOperationException("Estimated output exceeds maximum allowed.");
        }
    }
}
