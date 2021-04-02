Items:
- Move to the numeric ID for ModelBuild
- Delete the old migrations
- Need to make ModelBuild.NameKey and ModelBuildAttempt.(NameKey, int Attempt) have an index
- Evaluate every string for a `[Required]` value
- Change the date time type. Just use the C# type `DateTime` and EF Core will
generate `datetime2(7)`
- Change the tables to have the columns we need for queries
- what is using Org.BouncyCastle.Math.EC.Rfc7748?
- SearchTestsRequests need to alias job name and test run name. Same value.
- rename  GetModelBuildId to GetModelBuildKeyName
- ensure all query properties set 
    - specifically the updating to merged pull request

Indexes:
- ModelBuild id to tests, timelines and test runs

Universal query 
    key
    started:~7
    definition

    included columns
    result:failed
    kind:pr
    targetBranch