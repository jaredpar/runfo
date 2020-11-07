SELECT TOP (10) *
FROM ModelTimelineIssues


SELECT [m].[JobName], [m].[RecordName], [m].[TaskName], [m].[Message]
FROM [ModelTimelineIssues] AS [m]
LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
WHERE ((([m0].[DefinitionName] = 'runtime') AND (CAST([m0].[StartTime] AS datetimeoffset) >= '2020-11-02')) AND ((CHARINDEX('CmdLine', [m].[TaskName]) > 0))) AND CONTAINS([m].[Message], 'error')

SELECT TOP(10) [m0].[BuildNumber], [m].[JobName], [m].[RecordName], [m].[TaskName], [m].[Message]
FROM [ModelTimelineIssues] AS [m]
LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
WHERE (([m0].[DefinitionName] = 'runtime') AND (CAST([m0].[StartTime] AS datetimeoffset) >= '2020-11-02'))

SELECT TOP(10) [m].[JobName], [m].[RecordName], [m].[TaskName], [m].[Message]
FROM [ModelTimelineIssues] AS [m]
LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
WHERE (([m0].[DefinitionName] = 'runtime') AND ([m0].[StartTime] > '2020-11-02'))

SELECT [m].[JobName], [m].[RecordName], [m].[TaskName], [m].[Message]
FROM [ModelTimelineIssues] AS [m]
LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
WHERE (([m0].[DefinitionName] = 'runtime') AND (CAST([m0].[StartTime] AS datetimeoffset) >= '2020-11-02')) 
ORDER BY [m0].[BuildNumber] DESC
OFFSET 0 ROWS FETCH NEXT 25 ROWS ONLY

SELECT [m1].[BuildNumber], [t].[Message], [t].[JobName], [t].[IssueType], [t].[Attempt]
FROM (
  SELECT [m].[Id], [m].[Attempt], [m].[IssueType], [m].[JobName], [m].[Message], [m].[ModelBuildAttemptId], [m].[ModelBuildId], [m].[RecordId], [m].[RecordName], [m].[TaskName], [m0].[BuildNumber]
  FROM [ModelTimelineIssues] AS [m]
  LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
  WHERE ([m0].[DefinitionId] = 686) AND ([m0].[StartTime] >= '2020-11-05')
  ORDER BY [m0].[BuildNumber] DESC
  OFFSET 0 ROWS FETCH NEXT 25 ROWS ONLY
) AS [t]
LEFT JOIN [ModelBuilds] AS [m1] ON [t].[ModelBuildId] = [m1].[Id]
ORDER BY [t].[BuildNumber] DESC
