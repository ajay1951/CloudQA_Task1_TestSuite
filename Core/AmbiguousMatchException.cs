using System;

namespace CloudQA.ResilientAutomation.Core
{
    /// <summary>
    /// Thrown when the resilient locator discovers multiple competing elements with identical
    /// or indistinguishable confidence scores (margin < 15 points), indicating ambiguous markup.
    /// </summary>
    public class AmbiguousMatchException : Exception
    {
        public int TopScore { get; }
        public int RunnerScore { get; }
        public int ScoreMargin { get; }
        public string TopCandidateDetails { get; }
        public string RunnerCandidateDetails { get; }
        public string ControlType { get; }

        public AmbiguousMatchException(string message) : base(message)
        {
            TopCandidateDetails = string.Empty;
            RunnerCandidateDetails = string.Empty;
            ControlType = string.Empty;
        }

        public AmbiguousMatchException(
            string message,
            string controlType,
            int topScore,
            int runnerScore,
            int scoreMargin,
            string topDetails,
            string runnerDetails) : base(message)
        {
            ControlType = controlType;
            TopScore = topScore;
            RunnerScore = runnerScore;
            ScoreMargin = scoreMargin;
            TopCandidateDetails = topDetails ?? string.Empty;
            RunnerCandidateDetails = runnerDetails ?? string.Empty;
        }
    }
}
