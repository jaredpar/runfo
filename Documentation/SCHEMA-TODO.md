Items:
- X Move to the numeric ID for ModelBuild
- Delete the old migrations
- Evaluate every string for a `[Required]` value
- Change the date time type. Just use the C# type `DateTime` and EF Core will
generate `datetime2(7)`
- X Change the tables to have the columns we need for queries
- X what is using Org.BouncyCastle.Math.EC.Rfc7748?
- SearchTestsRequests need to alias job name and test run name. Same value.
- X rename  GetModelBuildId to GetModelBuildKeyName
- ensure all query properties set 
    - specifically the updating to merged pull request
- Delete all the filter map code in SearchBuildsRequests
- X Ensure all enum use int conversion
- Delete  GetModelBuildKind

Indexes:
- X ModelBuild 
    - key: namekey
- X ModelBuildAttempt
    - key: (NameKey, int Attempt)
    - key: (BuildId, int Attempt)
- X Universal query: ModelBuild, ModelTestResult, ModelTimelineIssues, ModelBuildAttempt
    - key: started, definition
    - included columns: result, kind, targetBranch