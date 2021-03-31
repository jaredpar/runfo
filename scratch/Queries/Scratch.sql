
/* This is a TimelineIssuesDisplay query */
/*
SELECT [m].[Id], [m].[Attempt], [m].[IssueType], [m].[JobName], [m].[Message], [m].[ModelBuildAttemptId], [m].[ModelBuildId], [m].[RecordId], [m].[RecordName]
FROM [ModelTimelineIssues] AS [m]
LEFT JOIN [ModelBuilds] AS [m0] ON [m].[ModelBuildId] = [m0].[Id]
LEFT JOIN [ModelBuildDefinitions] AS [m1] ON [m0].[ModelBuildDefinitionId] = [m1].[Id]
WHERE [m1].[DefinitionName] = 'roslyn-ci'
ORDER BY [m0].[BuildNumber] DESC
OFFSET 0 ROWS FETCH NEXT 25 ROWS ONLY
*/

/* Show all foreign keys including cascade actions */
 SELECT
    f.name constraint_name
   ,OBJECT_NAME(f.parent_object_id) referencing_table_name
   ,COL_NAME(fc.parent_object_id, fc.parent_column_id) referencing_column_name
   ,OBJECT_NAME (f.referenced_object_id) referenced_table_name
   ,COL_NAME(fc.referenced_object_id, fc.referenced_column_id) referenced_column_name
   ,delete_referential_action_desc
   ,update_referential_action_desc
FROM sys.foreign_keys AS f
INNER JOIN sys.foreign_key_columns AS fc
   ON f.object_id = fc.constraint_object_id
ORDER BY f.name

SELECT COUNT(*)
FROM ModelBuilds
WHERE AzureProject is NULL

SELECT COUNT(*)
FROM ModelTestRuns
WHERE ModelBuildId is NULL

SELECT TOP 10 b.BuildNumber, b.GitHubOrganization, b.GitHubRepository, b.StartTime
FROM ModelTestRuns as r
LEFT JOIN ModelBuilds as b ON b.Id = r.ModelBuildId
WHERE b.StartTime IS NOT NULl
ORDER BY b.StartTime 

SELECT COUNT(r.Id)
FROM ModelTestRuns as r
LEFT JOIN ModelBuilds as b ON r.ModelBuildId = b.Id
WHERE b.StartTime < '2020-11-01'

SELECT COUNT(Id)
FROM ModelBuilds
WHERE StartTIme < '2020-11-01'

/* Delete Time */
SELECT COUNT(*)
FROM ModelTestResults m
WHERE m.ModelBuildId IN (
	SELECT Id 
	FROM ModelBuilds
	WHERE StartTime < '2020-12-01')

DELETE m
FROM ModelTestResults m
WHERE m.ModelBuildId IN (
	SELECT Id 
	FROM ModelBuilds
	WHERE StartTime < '2020-12-01')

DELETE m
FROM ModelTrackingIssueMatches m
WHERE m.ModelBuildAttemptId IN (
	SELECT Id 
	FROM ModelBuildAttempts
	WHERE StartTime < '2020-12-15')

SELECT COUNT(m.Id)
FROM ModelTestResults m
LEFT JOIN ModelBuilds as b ON m.ModelBuildId = b.Id
WHERE b.StartTime < '2020-11-01'


SELECT TOP 10
FROM ModelTestResults
ORDER BY 
WHERE ModelBuildId = ''


SELECT TOP 100 *
FROM ModelBuilds
WHERE AzureOrganization is NULL
ORDER BY BuildNumber DESC

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
