/* SELECT * FROM dbo.BuildCloneTime WHERE DefinitionId = 15 and BuildStartTime > '8/14	/2019' and maxduration > '00:10:00' */

/* SELECT * FROM dbo.JobCloneTime WHERE BuildId = 311446 */
SELECT BuildUri FROM dbo.BuildCloneTime 
WHERE BuildStartTime > '2019/08/18' AND MaxDuration > '00:20:00' and  DefinitionId = 22