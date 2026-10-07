/* =====================================================================
   Pedros Cantina - 01_create.sql
  
   ===================================================================== */
IF DB_ID(N'PedrosCantina') IS NULL
    CREATE DATABASE PedrosCantina;
GO
USE PedrosCantina;
GO

DROP VIEW      IF EXISTS dbo.vw_ShiftStaffing;
DROP PROCEDURE IF EXISTS dbo.usp_CreateMonthPlan;
DROP TABLE     IF EXISTS dbo.ShiftAssignment;
DROP TABLE     IF EXISTS dbo.Shift;
DROP TABLE     IF EXISTS dbo.MonthPlan;
DROP TABLE     IF EXISTS dbo.ShiftType;
DROP TABLE     IF EXISTS dbo.Employee;
GO

/* ---------------------------------------------------------------------
   Employee: alle ansatte inkl. Pedro og Jacoby.
   CanLead = 1 betyder, at personen kan vaere leder paa en vagt.
   IsActive = 0 bruges til medarbejdere, der er stoppet (bevarer historik).
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Employee
(
    EmployeeId  INT IDENTITY(1,1) NOT NULL,
    FirstName   NVARCHAR(50)  NOT NULL,
    LastName    NVARCHAR(50)  NOT NULL,
    Phone       NVARCHAR(20)  NOT NULL,
    Email       NVARCHAR(100) NOT NULL,
    CanLead     BIT NOT NULL CONSTRAINT DF_Employee_CanLead  DEFAULT (0),
    IsActive    BIT NOT NULL CONSTRAINT DF_Employee_IsActive DEFAULT (1),
    CONSTRAINT PK_Employee       PRIMARY KEY (EmployeeId),
    CONSTRAINT UQ_Employee_Email UNIQUE (Email)
);
GO

/* ---------------------------------------------------------------------
   ShiftType: de to vagttyper (9-14 og 14-19).
   --------------------------------------------------------------------- */
CREATE TABLE dbo.ShiftType
(
    ShiftTypeId TINYINT      NOT NULL,
    Name        NVARCHAR(30) NOT NULL,
    StartTime   TIME(0)      NOT NULL,
    EndTime     TIME(0)      NOT NULL,
    CONSTRAINT PK_ShiftType      PRIMARY KEY (ShiftTypeId),
    CONSTRAINT UQ_ShiftType_Name UNIQUE (Name),
    CONSTRAINT CK_ShiftType_Time CHECK (EndTime > StartTime)
);
GO

/* ---------------------------------------------------------------------
   MonthPlan: en plan for en bestemt maaned (Pedro laegger en maaned ad gangen).
   --------------------------------------------------------------------- */
CREATE TABLE dbo.MonthPlan
(
    MonthPlanId INT IDENTITY(1,1) NOT NULL,
    [Year]      SMALLINT  NOT NULL,
    [Month]     TINYINT   NOT NULL,
    CreatedAt   DATETIME2(0) NOT NULL CONSTRAINT DF_MonthPlan_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_MonthPlan       PRIMARY KEY (MonthPlanId),
    CONSTRAINT UQ_MonthPlan_YM    UNIQUE ([Year], [Month]),
    CONSTRAINT CK_MonthPlan_Year  CHECK ([Year]  BETWEEN 2000 AND 2100),
    CONSTRAINT CK_MonthPlan_Month CHECK ([Month] BETWEEN 1 AND 12)
);
GO

/* ---------------------------------------------------------------------
   Shift: en konkret vagt = en dag i en maanedsplan + en vagttype.
   Datoen gemmes som DayOfMonth, saa aar/maaned kun staar ét sted (MonthPlan).
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Shift
(
    ShiftId     INT IDENTITY(1,1) NOT NULL,
    MonthPlanId INT     NOT NULL,
    DayOfMonth  TINYINT NOT NULL,
    ShiftTypeId TINYINT NOT NULL,
    CONSTRAINT PK_Shift            PRIMARY KEY (ShiftId),
    CONSTRAINT UQ_Shift_Plan_Day   UNIQUE (MonthPlanId, DayOfMonth, ShiftTypeId),
    CONSTRAINT CK_Shift_Day        CHECK (DayOfMonth BETWEEN 1 AND 31),
    CONSTRAINT FK_Shift_MonthPlan  FOREIGN KEY (MonthPlanId)
        REFERENCES dbo.MonthPlan (MonthPlanId) ON DELETE CASCADE,
    CONSTRAINT FK_Shift_ShiftType  FOREIGN KEY (ShiftTypeId)
        REFERENCES dbo.ShiftType (ShiftTypeId)
);
GO

/* ---------------------------------------------------------------------
   ShiftAssignment: hvem der arbejder paa hvilken vagt (mange-til-mange).
   En medarbejder kan have begge vagter samme dag (lang dag), da de har
   forskellige ShiftId.
   --------------------------------------------------------------------- */
CREATE TABLE dbo.ShiftAssignment
(
    ShiftId    INT NOT NULL,
    EmployeeId INT NOT NULL,
    CONSTRAINT PK_ShiftAssignment          PRIMARY KEY (ShiftId, EmployeeId),
    CONSTRAINT FK_ShiftAssignment_Shift    FOREIGN KEY (ShiftId)
        REFERENCES dbo.Shift (ShiftId) ON DELETE CASCADE,
    CONSTRAINT FK_ShiftAssignment_Employee FOREIGN KEY (EmployeeId)
        REFERENCES dbo.Employee (EmployeeId)
);
GO

CREATE INDEX IX_ShiftAssignment_Employee ON dbo.ShiftAssignment (EmployeeId);
GO

/* ---------------------------------------------------------------------
   Trigger: haandhaever "hoejst 3 personer paa en vagt".
   (Minimum 2 og "mindst en leder" kan ikke tjekkes ved hver INSERT, da en
   vagt bygges op en medarbejder ad gangen. Det tjekkes i vw_ShiftStaffing.)
   --------------------------------------------------------------------- */
CREATE TRIGGER dbo.trg_ShiftAssignment_MaxThree
ON dbo.ShiftAssignment
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1
               FROM dbo.ShiftAssignment sa
               JOIN inserted i ON i.ShiftId = sa.ShiftId
               GROUP BY sa.ShiftId
               HAVING COUNT(*) > 3)
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50001, N'A shift can have at most 3 employees.', 1;
    END
END
GO

/* ---------------------------------------------------------------------
   View: bemanding pr. vagt + om vagten overholder reglerne
   (2-3 personer og mindst en leder).
   --------------------------------------------------------------------- */
CREATE VIEW dbo.vw_ShiftStaffing
AS
SELECT  mp.[Year],
        mp.[Month],
        s.ShiftId,
        s.DayOfMonth,
        DATEFROMPARTS(mp.[Year], mp.[Month], s.DayOfMonth) AS ShiftDate,
        s.ShiftTypeId,
        st.Name AS ShiftName,
        COUNT(sa.EmployeeId) AS Staff,
        COALESCE(SUM(CASE WHEN e.CanLead = 1 THEN 1 ELSE 0 END), 0) AS Leaders,
        CASE WHEN COUNT(sa.EmployeeId) BETWEEN 2 AND 3
              AND COALESCE(SUM(CASE WHEN e.CanLead = 1 THEN 1 ELSE 0 END), 0) >= 1
             THEN 1 ELSE 0 END AS IsValid
FROM    dbo.Shift s
JOIN    dbo.MonthPlan mp ON mp.MonthPlanId = s.MonthPlanId
JOIN    dbo.ShiftType st ON st.ShiftTypeId = s.ShiftTypeId
LEFT JOIN dbo.ShiftAssignment sa ON sa.ShiftId = s.ShiftId
LEFT JOIN dbo.Employee e         ON e.EmployeeId = sa.EmployeeId
GROUP BY mp.[Year], mp.[Month], s.ShiftId, s.DayOfMonth, s.ShiftTypeId, st.Name;
GO

/* ---------------------------------------------------------------------
   Procedure: opretter en ny maanedsplan med tomme vagter
   (to vagter for hver dag i maaneden).
   --------------------------------------------------------------------- */
CREATE PROCEDURE dbo.usp_CreateMonthPlan
    @Year  SMALLINT,
    @Month TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM dbo.MonthPlan WHERE [Year] = @Year AND [Month] = @Month)
        THROW 50010, N'A plan already exists for that month.', 1;

    DECLARE @DaysInMonth INT = DAY(EOMONTH(DATEFROMPARTS(@Year, @Month, 1)));
    DECLARE @PlanId INT;

    BEGIN TRANSACTION;

    INSERT INTO dbo.MonthPlan ([Year], [Month]) VALUES (@Year, @Month);
    SET @PlanId = SCOPE_IDENTITY();

    INSERT INTO dbo.Shift (MonthPlanId, DayOfMonth, ShiftTypeId)
    SELECT @PlanId, d.DayNo, st.ShiftTypeId
    FROM (SELECT TOP (@DaysInMonth)
                 CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS TINYINT) AS DayNo
          FROM sys.all_objects) AS d
    CROSS JOIN dbo.ShiftType st;

    COMMIT TRANSACTION;

    SELECT @PlanId AS MonthPlanId;
END
GO
