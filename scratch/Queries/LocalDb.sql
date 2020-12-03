/* SELECT * FROM dbo.BuildCloneTime WHERE DefinitionId = 15 and BuildStartTime > '8/14	/2019' and maxduration > '00:10:00' */

CREATE TABLE ExPlanOperator_P3 (
  ID INT IDENTITY(1, 1)
  ,STD_Name VARCHAR(50)
  ,STD_BirthDate DATETIME
  ,STD_Address VARCHAR(MAX)
  ,STD_Grade INT
  )
GO
 
INSERT INTO ExPlanOperator_P3
VALUES (
  'AA'
  ,'1998-05-30'
  ,'BB'
  ,93
  ) GO 1000
 
INSERT INTO ExPlanOperator_P3
VALUES (
  'CC'
  ,'1998-10-13'
  ,'DD'
  ,78
  ) GO 1000
 
INSERT INTO ExPlanOperator_P3
VALUES (
  'EE'
  ,'1998-06-24'
  ,'FF'
  ,85
  ) GO 1000