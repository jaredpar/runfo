using System;


namespace DevOps.Util
{
    public enum TestOutcome
    {
        Unspecified,
        None,
        Passed,
        Failed,
        Inconclusive,
        Timeout,
        Aborted,
        Blocked,
        NotExecuted,
        NotApplicable,
        Paused,
        InProgress,
        NotImpacted
    }
}