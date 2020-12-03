/* SELECT * FROM dbo.BuildCloneTime WHERE DefinitionId = 15 and BuildStartTime > '8/14	/2019' and maxduration > '00:10:00' */

/* SELECT * FROM dbo.JobCloneTime WHERE BuildId = 311446 */
SELECT BuildUri FROM dbo.BuildCloneTime 
WHERE BuildStartTime > '2019/08/18' AND MaxDuration > '00:20:00' and  DefinitionId = 22

SELECT COUNT(*)
FROM [ModelTestResults] AS [m]
LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
WHERE (((([m].[IsHelixTestResult] = CAST(1 AS bit)) AND ([m0].[DefinitionId] = 686)) AND ([m0].[StartTime] >= '2020-11-29')) AND [m0].[PullRequestNumber] IS NULL) AND (([m0].[BuildResult] <> N'Succeeded') OR [m0].[BuildResult] IS NULL)

SELECT COUNT(*)
FROM [ModelTestResults] 

SELECT CAST(b.StartTime As DATE), COUNT(tr.Id)
FROM [ModelTestResults] As tr
LEFT JOIN [ModelBuilds] as b on tr.ModelBuildId = b.Id
WHERE b.StartTime > '2020-11-01' AnD b.DefinitionId = 686
GROUP BY CAST(b.StartTime As DATE)
ORDER BY CAST(b.StartTime As DATE)

SELECT b.DefinitionId, COUNT(tr.Id)
FROM [ModelTestResults] As tr
LEFT JOIN [ModelBuilds] as b on tr.ModelBuildId = b.Id
WHERE b.StartTime > '2020-11-01' 
GROUP BY b.DefinitionId
ORDER BY COUNT(tr.Id) DESC

SELECT TOP 10 Id
FROM ModelBuilds
WHERE DefinitionId = 686 
ORDER BY StartTime DESC

SELECT * 
FROM ModelTestResults
WHERE ModelBuildId In ('dnceng-public-906837', 'dnceng-public-906830', 'dnceng-public-906816', 'dnceng-public-906812')

      SELECT COUNT(*)
      FROM [ModelTestResults] AS [m]
      WHERE m.ModelBuildId In (
      LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
      WHERE ((([m0].[DefinitionId] = 686) AND ([m0].[StartTime] >= '2020-12-2')) AND (([m0].[BuildResult] <> N'Succeeded') OR [m0].[BuildResult] IS NULL)) AND ([m].[IsHelixTestResult] = CAST(1 AS bit))


      SELECT COUNT(*)
      FROM [ModelBuilds] AS [m]
      WHERE (([m].[DefinitionId] = 686) AND ([m].[StartTime] >= '2020-11-30')) AND (([m].[BuildResult] <> N'Succeeded') OR [m].[BuildResult] IS NULL)

      SELECT [m0].[BuildNumber], [m0].[AzureOrganization], [m0].[AzureProject], [m0].[StartTime], [m0].[GitHubOrganization], [m0].[GitHubRepository], [m0].[GitHubTargetBranch], [m0].[PullRequestNumber], [m].[HelixConsoleUri], [m].[HelixCoreDumpUri], [m].[HelixRunClientUri], [m].[HelixTestResultsUri]
      FROM [ModelTestResults] AS [m]
      LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
      WHERE [m].[ModelBuildId] IN (N'dnceng-public-902582', N'dnceng-public-902594', N'dnceng-public-902604', N'dnceng-public-902671', N'dnceng-public-902693', N'dnceng-public-902704', N'dnceng-public-902750', N'dnceng-public-902761', N'dnceng-public-902765', N'dnceng-public-902812', N'dnceng-public-902865', N'dnceng-public-902964', N'dnceng-public-902967', N'dnceng-public-902973', N'dnceng-public-902979', N'dnceng-public-902998', N'dnceng-public-903003', N'dnceng-public-902293', N'dnceng-public-903026', N'dnceng-public-903034', N'dnceng-public-903036', N'dnceng-public-903068', N'dnceng-public-903083', N'dnceng-public-903102', N'dnceng-public-903157')

      SELECT [m].[ModelBuildId], [m].[HelixConsoleUri], [m].[HelixCoreDumpUri], [m].[HelixRunClientUri], [m].[HelixTestResultsUri]
      FROM [ModelTestResults] AS [m]
      WHERE [m].[ModelBuildId] IN (N'dnceng-public-902582', N'dnceng-public-902594', N'dnceng-public-902604', N'dnceng-public-902671', N'dnceng-public-902693', N'dnceng-public-902704', N'dnceng-public-902750', N'dnceng-public-902761', N'dnceng-public-902765', N'dnceng-public-902812')
