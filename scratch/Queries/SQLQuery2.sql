      // Executed DbCommand (2,278ms) [Parameters=[@__definitionId_0='?' (DbType = Int32), @__started_DateTime_1='?' (DbType = DateTimeOffset), @__Name_2='?' (Size = 4000), @__p_3='?' (DbType = Int32), @__p_4='?' (DbType = Int32)], CommandType='Text', CommandTimeout='30']

      SELECT [t].[Id], [t].[ErrorMessage], [t].[HelixConsoleUri], [t].[HelixCoreDumpUri], [t].[HelixRunClientUri], [t].[HelixTestResultsUri], [t].[IsHelixTestResult], [t].[IsSubResult], [t].[IsSubResultContainer], [t].[ModelBuildId], [t].[ModelTestRunId], [t].[Outcome], [t].[TestFullName], [m2].[Id], [m2].[Attempt], [m2].[AzureOrganization], [m2].[AzureProject], [m2].[ModelBuildId], [m2].[Name], [m2].[TestRunId], [m3].[Id], [m3].[AzureOrganization], [m3].[AzureProject], [m3].[BuildNumber], [m3].[BuildResult], [m3].[FinishTime], [m3].[GitHubOrganization], [m3].[GitHubRepository], [m3].[GitHubTargetBranch], [m3].[IsMergedPullRequest], [m3].[ModelBuildDefinitionId], [m3].[PullRequestNumber], [m3].[QueueTime], [m3].[StartTime]
      FROM (
          SELECT [m].[Id], [m].[ErrorMessage], [m].[HelixConsoleUri], [m].[HelixCoreDumpUri], [m].[HelixRunClientUri], [m].[HelixTestResultsUri], [m].[IsHelixTestResult], [m].[IsSubResult], [m].[IsSubResultContainer], [m].[ModelBuildId], [m].[ModelTestRunId], [m].[Outcome], [m].[TestFullName], [m0].[BuildNumber]
          FROM [ModelTestResults] AS [m]
          LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
          LEFT JOIN [ModelBuildDefinitions] AS [m1] ON [m0].[ModelBuildDefinitionId] = [m1].[Id]
          WHERE ((([m1].[DefinitionId] = 686) AND (CAST([m0].[StartTime] AS datetimeoffset) >= '2020-10-25') AND ([m0].[BuildResult] = N'Failed')) AND ((@__Name_2 = N'') OR (CHARINDEX(@__Name_2, [m].[TestFullName]) > 0))
          ORDER BY [m0].[BuildNumber] DESC
          OFFSET @__p_3 ROWS FETCH NEXT @__p_4 ROWS ONLY
      ) AS [t]
      INNER JOIN [ModelTestRuns] AS [m2] ON [t].[ModelTestRunId] = [m2].[Id]
      LEFT JOIN [ModelBuilds] AS [m3] ON [t].[ModelBuildId] = [m3].[Id]
      ORDER BY [t].[BuildNumber] DESC

  SELECT *
  FROM ModelBuildDefinitions
  WHERE DefinitionName = 'runtime'

  SELECT COUNT(*)
  FROM [ModelTestResults] AS [m]
  LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
  LEFT JOIN [ModelBuildDefinitions] AS [m1] ON [m0].[ModelBuildDefinitionId] = [m1].[Id]
  WHERE ((([m].[IsHelixTestResult] = CAST(1 AS bit)) AND ([m1].[DefinitionId] = 686)) AND (CAST([m0].[StartTime] AS datetimeoffset) >= '2020-10-25')) AND (([m0].[BuildResult] <> N'Succeeded') OR [m0].[BuildResult] IS NULL)

  SELECT COUNT(*)
  FROM [ModelTestResults] AS [m]
  LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
  LEFT JOIN [ModelBuildDefinitions] AS [m1] ON [m0].[ModelBuildDefinitionId] = [m1].[Id]
  WHERE ((([m].[IsHelixTestResult] = CAST(1 AS bit)) AND ([m1].[DefinitionId] = 686)) AND (CAST([m0].[StartTime] AS datetimeoffset) >= '2020-10-25')) 

  SELECT COUNT(*)
  FROM [ModelBuilds] AS [m0] 
  LEFT JOIN [ModelBuildDefinitions] AS [m1] ON [m0].[ModelBuildDefinitionId] = [m1].[Id]
  WHERE ((([m1].[DefinitionId] = 686)) AND (CAST([m0].[StartTime] AS datetimeoffset) >= '2020-10-25')) 

  SELECt COUNT(*)
  FROM ModelBuilds AS [m0]
  WHERE (CAST([m0].[StartTime] AS datetimeoffset) >= '2020-10-25') 

  SELECt COUNT(*)
  FROM ModelBuilds AS [m0]
  WHERE (CAST([m0].[StartTime] AS datetimeoffset) >= '2020-10-25')  AND ModelBuildDefinitionId > 42

  SELECt COUNT(*)
  FROM ModelBuilds AS [m0]
  WHERE (CAST([m0].[StartTime] AS datetimeoffset) >= '2020-10-25')  AND DefinitionName = 'roslyn-ci'

--  CREATE INDEX IX_Manual_ModelBuilds_StartTime
-- DROP INDEX IX_Manual_ModelBuilds_StartTime ON ModelBuilds
-- CREATE INDEX IX_Manual_ModelBuildDefinitions_DefinitionId ON ModelBuildDefinitions (DefinitionId)
-- CREATE INDEX IX_Manual_ModelBuildDefinitions_DefinitionName ON ModelBuildDefinitions (DefinitionName)
