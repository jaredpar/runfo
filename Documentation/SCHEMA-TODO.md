Items:
- X Move to the numeric ID for ModelBuild
- Delete the old migrations
- Evaluate every string for a `[Required]` value
- Change the date time type. Just use the C# type `DateTime` and EF Core will
generate `datetime2(7)`
- X Change the tables to have the columns we need for queries
- what is using Org.BouncyCastle.Math.EC.Rfc7748?
- SearchTestsRequests need to alias job name and test run name. Same value.
- X rename  GetModelBuildId to GetModelBuildKeyName
- Remove DefinitionName from the universal query, we can just make the lookup of
If we decide against this then we need to ensure that we have an index created
for DefinitionName
- definition names efficient (cache in memory)
- ensure all query properties set 
    - specifically the updating to merged pull request


Indexes:
- ModelBuild 
    - key: namekey
- ModelBuildAttempt
    - key: (NameKey, int Attempt)
    - key: (BuildId, int Attempt)
- X Universal query: ModelBuild, ModelTestResult, ModelTimelineIssues, ModelBuildAttempt
    - key: started, definition
    - included columns: result, kind, targetBranch