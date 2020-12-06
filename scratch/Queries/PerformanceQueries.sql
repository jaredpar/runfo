/*SELECT * FROM ModelTimelineIssues */
/*SELECT COUNT(*) FROM ModelTimelineIssues*/
SELECT * FROM ModelTestResults

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
