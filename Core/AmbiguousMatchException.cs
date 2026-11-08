using System;

namespace CloudQA.ResilientAutomation.Core
{
    /// <summary>
    /// Thrown when the resilient locator discovers multiple competing elements with identical
    /// or indistinguishable confidence scores, indicating ambiguous markup.
    /// </summary>
    public class AmbiguousMatchException : Exception
    {
        public AmbiguousMatchException(string message) : base(message) { }
    }
}

