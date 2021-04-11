
/**
 * Migration queries
 */
SELECT Id,AzureOrganization, AzureProject, DefinitionName, DefinitionId
FROM ModelBuildDefinitions

SELECT *
FROM ModelTrackingIssues

SELECT r.Id, r.ModelTrackingIssueId, r.ModelBuildAttemptId, a.ModelBuildId, a.Attempt
FROM ModelTrackingIssueResults r
JOIN ModelBuildAttempts a ON a.Id = r.ModelBuildAttemptId
WHERE IsPresent = 1

SELECT m.Id, m.ModelTrackingIssueId, m.ModelBuildAttemptId, a.ModelBuildId, a.Attempt, m.HelixLogUri, m.JobName, m.HelixLogKind
FROM ModelTrackingIssueMatches m
JOIN ModelBuildAttempts a ON a.Id = m.ModelBuildAttemptId

/* random
*/

SELECT 
    t.NAME AS TableName,
    s.Name AS SchemaName,
    p.rows AS RowCounts,
    SUM(a.total_pages) * 8 AS TotalSpaceKB, 
    CAST(ROUND(((SUM(a.total_pages) * 8) / 1024.00), 2) AS NUMERIC(36, 2)) AS TotalSpaceMB,
    SUM(a.used_pages) * 8 AS UsedSpaceKB, 
    CAST(ROUND(((SUM(a.used_pages) * 8) / 1024.00), 2) AS NUMERIC(36, 2)) AS UsedSpaceMB, 
    (SUM(a.total_pages) - SUM(a.used_pages)) * 8 AS UnusedSpaceKB,
    CAST(ROUND(((SUM(a.total_pages) - SUM(a.used_pages)) * 8) / 1024.00, 2) AS NUMERIC(36, 2)) AS UnusedSpaceMB
FROM 
    sys.tables t
INNER JOIN      
    sys.indexes i ON t.OBJECT_ID = i.object_id
INNER JOIN 
    sys.partitions p ON i.object_id = p.OBJECT_ID AND i.index_id = p.index_id
INNER JOIN 
    sys.allocation_units a ON p.partition_id = a.container_id
LEFT OUTER JOIN 
    sys.schemas s ON t.schema_id = s.schema_id
INNER JOIN  sysobjects so on t.object_id = so.id
INNER JOIN  syscolumns SC on (so.id = sc.id)
INNER JOIN systypes st on (st.type = sc.type)
WHERE 
    t.NAME NOT LIKE 'dt%' 
    AND t.is_ms_shipped = 0
    AND i.OBJECT_ID > 255 
    AND so.type = 'U'
and st.name IN ('DATETIME', 'DATE', 'TIME')
GROUP BY 
    t.Name, s.Name, p.Rows
ORDER BY 
    p.rows DESC
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

DECLARE @__started_DateTime_Date_0 AS DateTime2 = '2021-04-03'
DECLARE @__Definition_1 As nvarchar(100) = 'roslyn-ci'
SELECT COUNT(*)
FROM [ModelTestResults] AS [m]
WHERE ([m].[StartTime] >= @__started_DateTime_Date_0) AND ([m].[DefinitionName] = @__Definition_1)


DECLARE @__started_DateTime_Date_0 AS DateTime2 = '2021-04-03'
DECLARE @__Definition_1 As nvarchar(100) = 'runtime'
SELECT COUNT(*)
FROM [ModelTestResults] AS [m]
WHERE ([m].[DefinitionName] = @__Definition_1) AND ([m].[StartTime] >= @__started_DateTime_Date_0)


DECLARE @__started_DateTime_Date_0 AS DateTime2 = '2021-04-03'
DECLARE @__Definition_1 As nvarchar(100) = 'runtime'
DECLARE @__p_3 As Integer = 50
DECLARE @__p_4 As Integer = 25
DECLARE @__k_2 As INteger = 2
SELECT [t].[Id], [t].[Attempt], [t].[BuildKind], [t].[BuildResult], [t].[DefinitionName], [t].[DefinitionNumber], [t].[ErrorMessage], [t].[GitHubTargetBranch], [t].[HelixConsoleUri], [t].[HelixCoreDumpUri], [t].[HelixRunClientUri], [t].[HelixTestResultsUri], [t].[IsHelixTestResult], [t].[IsSubResult], [t].[IsSubResultContainer], [t].[ModelBuildAttemptId], [t].[ModelBuildDefinitionId], [t].[ModelBuildId], [t].[ModelTestRunId], [t].[Outcome], [t].[StartTime], [t].[TestFullName], [t].[TestRunName], [m0].[Id], [m0].[AzureOrganization], [m0].[AzureProject], [m0].[BuildKind], [m0].[BuildNumber], [m0].[BuildResult], [m0].[DefinitionName], [m0].[DefinitionNumber], [m0].[FinishTime], [m0].[GitHubOrganization], [m0].[GitHubRepository], [m0].[GitHubTargetBranch], [m0].[ModelBuildDefinitionId], [m0].[NameKey], [m0].[PullRequestNumber], [m0].[QueueTime], [m0].[StartTime]
FROM (
  SELECT [m].[Id], [m].[Attempt], [m].[BuildKind], [m].[BuildResult], [m].[DefinitionName], [m].[DefinitionNumber], [m].[ErrorMessage], [m].[GitHubTargetBranch], [m].[HelixConsoleUri], [m].[HelixCoreDumpUri], [m].[HelixRunClientUri], [m].[HelixTestResultsUri], [m].[IsHelixTestResult], [m].[IsSubResult], [m].[IsSubResultContainer], [m].[ModelBuildAttemptId], [m].[ModelBuildDefinitionId], [m].[ModelBuildId], [m].[ModelTestRunId], [m].[Outcome], [m].[StartTime], [m].[TestFullName], [m].[TestRunName]
  FROM [ModelTestResults] AS [m]
  WHERE (([m].[StartTime] >= @__started_DateTime_Date_0) AND ([m].[DefinitionName] = @__Definition_1)) AND ([m].[BuildKind] <> @__k_2)
  ORDER BY [m].[StartTime] DESC
  OFFSET @__p_3 ROWS FETCH NEXT @__p_4 ROWS ONLY
) AS [t]
INNER JOIN [ModelBuilds] AS [m0] ON [t].[ModelBuildId] = [m0].[Id]
ORDER BY [t].[StartTime] DESC

/* Search timelines initial */
DECLARE @__started_DateTime_Date_1 AS DateTime2 = '2021-04-03'
DECLARE @__Definition_0 As nvarchar(100) = 'roslyn-ci'
SELECT COUNT(*)
FROM [ModelTimelineIssues] AS [m]
WHERE ([m].[DefinitionName] = @__Definition_0) AND ([m].[StartTime] >= @__started_DateTime_Date_1)

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
