
/* This is a TimelineIssuesDisplay query */
SELECT [m].[Id], [m].[Attempt], [m].[IssueType], [m].[JobName], [m].[Message], [m].[ModelBuildAttemptId], [m].[ModelBuildId], [m].[RecordId], [m].[RecordName]
FROM [ModelTimelineIssues] AS [m]
LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
LEFT JOIN [ModelBuildDefinitions] AS [m1] ON [m0].[ModelBuildDefinitionId] = [m1].[Id]
WHERE [m1].[DefinitionName] = 'roslyn-ci'
ORDER BY [m0].[BuildNumber] DESC
OFFSET 0 ROWS FETCH NEXT 25 ROWS ONLY


/* SELECT * FROM dbo.BuildCloneTime WHERE DefinitionId = 15 and BuildStartTime > '8/14	/2019' and maxduration > '00:10:00' */

/* SELECT * FROM dbo.JobCloneTime WHERE BuildId = 311446 */

/*
SELECT COUNT(*), CAST(CAST(AVG(CAST(CAST(Duration as datetime) as float)) as datetime) as time) FROM dbo.JobCloneTime 
where buildstarttime > '2019/08/19' AND Build
*/
/*
SELECT TOP 10 DefinitionId, Count(*) as JobCount
FROM dbo.JobCloneTime 
WHERE buildstarttime > '2019/08/19'
GROUP BY DefinitionId
OrdER BY JobCount DESC

*/

/*
SELECT BuildUri
FROM JobCloneTime
WHERE DefinitionId = 228 AND BuildStartTime > '2019/08/27' AND Duration > '00:20:00'
GROUP BY BuildUri
*/

/*
SELECT CAST(CAST(AVG(CAST(CAST(Duration as datetime) as float)) as datetime) as time) As Duration
FROM JobCloneTime
WHERE DefinitionId = 228 AND BuildStartTime > '2019/08/27' 
*/

/*
SELECT COUNT(*)P:\random\dotnet\Azure\DevOpsFun\Queries\Scratch.sql
FROM BuildCloneTime
WHERE DefinitionId = 228 AND BuildStartTime > '2019/08/27'
*/

/*
SELECT BuildId 
FROM JobCloneTime
WHERE AverageFetchSpeed IS NOT NULL
GROUP BY BuildId
*/
