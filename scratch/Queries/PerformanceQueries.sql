/*SELECT * FROM ModelTimelineIssues */
/*SELECT COUNT(*) FROM ModelTimelineIssues*/
SELECT * FROM ModelTestResults

/* Search for timeline issues by definition name not id */
exec sp_executesql N'SELECT [m1].[BuildNumber], [t].[Message], [t].[JobName], [t].[IssueType], [t].[Attempt]
FROM (
    SELECT [m].[Id], [m].[Attempt], [m].[IssueType], [m].[JobName], [m].[Message], [m].[ModelBuildAttemptId], [m].[ModelBuildId], [m].[RecordId], [m].[RecordName], [m].[TaskName], [m0].[BuildNumber]
    FROM [ModelTimelineIssues] AS [m]
    LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
    WHERE ([m0].[StartTime] >= @__started_DateTime_Date_0) AND ([m0].[DefinitionName] = @__definitionName_1)
    ORDER BY [m0].[BuildNumber] DESC
    OFFSET @__p_2 ROWS FETCH NEXT @__p_3 ROWS ONLY
) AS [t]
LEFT JOIN [ModelBuilds] AS [m1] ON [t].[ModelBuildId] = [m1].[Id]
ORDER BY [t].[BuildNumber] DESC',N'@__started_DateTime_Date_0 datetime,@__definitionName_1 nvarchar(100),@__p_2 int,@__p_3 int',@__started_DateTime_Date_0='2020-12-01 00:00:00',@__definitionName_1=N'runtime',@__p_2=0,@__p_3=25

/* Search for timeline issues by text */
SELECT [m1].[BuildNumber], [t].[Message], [t].[JobName], [t].[IssueType], [t].[Attempt]
FROM (
  SELECT [m].[Id], [m].[Attempt], [m].[IssueType], [m].[JobName], [m].[Message], [m].[ModelBuildAttemptId], [m].[ModelBuildId], [m].[RecordId], [m].[RecordName], [m].[TaskName], [m0].[BuildNumber]
  FROM [ModelTimelineIssues] AS [m]
  LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
  WHERE ([m0].[DefinitionId] = 686) AND CONTAINS([m].[Message], '"returned from process"')
  ORDER BY [m0].[BuildNumber] DESC
  OFFSET 0 ROWS FETCH NEXT 25 ROWS ONLY
) AS [t]
LEFT JOIN [ModelBuilds] AS [m1] ON [t].[ModelBuildId] = [m1].[Id]
ORDER BY [t].[BuildNumber] DESC

/* Search for timeline issues for a time frame and filter by job name */
SELECT [m1].[BuildNumber], [t].[Message], [t].[JobName], [t].[IssueType], [t].[Attempt]
FROM (
  SELECT [m].[Id], [m].[Attempt], [m].[IssueType], [m].[JobName], [m].[Message], [m].[ModelBuildAttemptId], [m].[ModelBuildId], [m].[RecordId], [m].[RecordName], [m].[TaskName], [m0].[BuildNumber]
  FROM [ModelTimelineIssues] AS [m]
  LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
  WHERE (([m0].[StartTime] >= '2020-12-01') AND ([m0].[DefinitionId] = 686)) AND (('CoreClr' = N'') OR (CHARINDEX('CoreClr', [m].[JobName]) > 0))
  ORDER BY [m0].[BuildNumber] DESC
  OFFSET 0 ROWS FETCH NEXT 25 ROWS ONLY
) AS [t]
LEFT JOIN [ModelBuilds] AS [m1] ON [t].[ModelBuildId] = [m1].[Id]
ORDER BY [t].[BuildNumber] DESC

/* Search for tests by the name of the test. Need to make sure this doesn't load all of the test rows before filtering 
because that can be a LOT of tests. In some cases seen the query load 43,000 rows but return 100 because of how the 
paging works on the website */
exec sp_executesql N'SELECT [t].[Id], [t].[ErrorMessage], [t].[HelixConsoleUri], [t].[HelixCoreDumpUri], [t].[HelixRunClientUri], [t].[HelixTestResultsUri], [t].[IsHelixTestResult], [t].[IsSubResult], [t].[IsSubResultContainer], [t].[ModelBuildId], [t].[ModelTestRunId], [t].[Outcome], [t].[TestFullName], [m1].[Id], [m1].[Attempt], [m1].[AzureOrganization], [m1].[AzureProject], [m1].[ModelBuildId], [m1].[Name], [m1].[TestRunId], [m2].[Id], [m2].[AzureOrganization], [m2].[AzureProject], [m2].[BuildNumber], [m2].[BuildResult], [m2].[DefinitionId], [m2].[DefinitionName], [m2].[FinishTime], [m2].[GitHubOrganization], [m2].[GitHubRepository], [m2].[GitHubTargetBranch], [m2].[IsMergedPullRequest], [m2].[ModelBuildDefinitionId], [m2].[PullRequestNumber], [m2].[QueueTime], [m2].[StartTime]
FROM (
    SELECT [m].[Id], [m].[ErrorMessage], [m].[HelixConsoleUri], [m].[HelixCoreDumpUri], [m].[HelixRunClientUri], [m].[HelixTestResultsUri], [m].[IsHelixTestResult], [m].[IsSubResult], [m].[IsSubResultContainer], [m].[ModelBuildId], [m].[ModelTestRunId], [m].[Outcome], [m].[TestFullName], [m0].[BuildNumber]
    FROM [ModelTestResults] AS [m]
    LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
    WHERE (([m0].[StartTime] >= @__started_DateTime_Date_0) AND ([m0].[DefinitionId] = @__definitionId_1)) AND ((@__Name_2 = N'''') OR (CHARINDEX(@__Name_2, [m].[TestFullName]) > 0))
    ORDER BY [m0].[BuildNumber] DESC
    OFFSET @__p_3 ROWS FETCH NEXT @__p_4 ROWS ONLY
) AS [t]
INNER JOIN [ModelTestRuns] AS [m1] ON [t].[ModelTestRunId] = [m1].[Id]
LEFT JOIN [ModelBuilds] AS [m2] ON [t].[ModelBuildId] = [m2].[Id]
ORDER BY [t].[BuildNumber] DESC',N'@__started_DateTime_Date_0 datetime,@__definitionId_1 int,@__Name_2 nvarchar(4000),@__p_3 int,@__p_4 int',@__started_DateTime_Date_0='2020-11-30 21:00:13',@__definitionId_1=686,@__Name_2=N'BCL',@__p_3=0,@__p_4=101


/* Search for timeline issue for a text but getting the count */
/*Failed executing DbCommand (30,359ms) [Parameters=[@__started_DateTime_0='2020-11-29T06:07:21' (Nullable = true) (DbType = DateTime), @__definitionId_1='686' (Nullable = true), @__text_3='failed' (Size = 4000)], CommandType='Text', CommandTimeout='30'] */
DECLARE @__started_DateTime_0 DateTime, @__definitionId_1 INT, @__text_3 NVARCHAR(400)
SET @__started_DateTime_0='2020-11-29T06:07:21'
SET @__definitionId_1='686'
SET @__text_3='Failed'
SELECT COUNT(*)
FROM [ModelTimelineIssues] AS [m]
LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
WHERE (([m0].[StartTime] >= @__started_DateTime_0) AND ([m0].[DefinitionId] = @__definitionId_1)) AND CONTAINS([m].[Message], @__text_3)




/*Failed executing DbCommand (30,359ms) [Parameters=[@__started_DateTime_0='2020-11-29T06:07:21' (Nullable = true) (DbType = DateTime), @__definitionId_1='686' (Nullable = true), @__text_3='failed' (Size = 4000)], CommandType='Text', CommandTimeout='30'] */
DECLARE @__started_DateTime_0 DateTime, @__definitionId_1 INT, @__text_3 NVARCHAR(400)
SET @__started_DateTime_0='2020-11-29T00:00:00'
SET @__definitionId_1='686'
SET @__text_3='Failed'
SELECT COUNT(*)
FROM [ModelTimelineIssues] AS [m]
LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
WHERE (([m0].[StartTime] >= @__started_DateTime_0) AND ([m0].[DefinitionId] = @__definitionId_1)) AND CONTAINS([m].[Message], @__text_3)
