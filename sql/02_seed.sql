/* =====================================================================
   Pedros Cantina - 02_seed.sql
   Testdata: vagttyper, medarbejdere og maanedsplaner for hele 2026.
   ===================================================================== */
USE PedrosCantina;
GO
SET NOCOUNT ON;

INSERT INTO dbo.ShiftType (ShiftTypeId, Name, StartTime, EndTime) VALUES
    (1, N'Formiddag',   '09:00', '14:00'),
    (2, N'Eftermiddag', '14:00', '19:00');

INSERT INTO dbo.Employee (FirstName, LastName, Phone, Email, CanLead, IsActive) VALUES
    (N'Pedro',   N'Martinez',   N'+45 20 00 00 01', N'pedro@pedroscantina.dk',   1, 1),
    (N'Jacoby',  N'Nielsen',    N'+45 20 00 00 02', N'jacoby@pedroscantina.dk',  1, 1),
    (N'Maria',   N'Jensen',     N'+45 20 00 00 03', N'maria@pedroscantina.dk',   0, 1),
    (N'Lars',    N'Pedersen',   N'+45 20 00 00 04', N'lars@pedroscantina.dk',    0, 1),
    (N'Sofie',   N'Andersen',   N'+45 20 00 00 05', N'sofie@pedroscantina.dk',   0, 1),
    (N'Mads',    N'Larsen',     N'+45 20 00 00 06', N'mads@pedroscantina.dk',    0, 1),
    (N'Emma',    N'Christensen',N'+45 20 00 00 07', N'emma@pedroscantina.dk',    0, 1),
    (N'Oliver',  N'Madsen',     N'+45 20 00 00 08', N'oliver@pedroscantina.dk',  0, 1);

/* Opret tomme maanedsplaner for januar-december 2026 via proceduren */
DECLARE @Created TABLE (MonthPlanId INT);
DECLARE @m INT = 1;
WHILE @m <= 12
BEGIN
    INSERT INTO @Created EXEC dbo.usp_CreateMonthPlan @Year = 2026, @Month = @m;
    SET @m += 1;
END

/* Fyld vagterne med et roterende moenster:
   - leder: Pedro og Jacoby skiftes, saa hver af dem har én vagt pr. dag
   - 1. medarbejder paa alle vagter, 2. medarbejder paa hver anden vagt
   => alle vagter har 2-3 personer og praecis én leder                     */
DECLARE @Pedro  INT = (SELECT EmployeeId FROM dbo.Employee WHERE Email = N'pedro@pedroscantina.dk');
DECLARE @Jacoby INT = (SELECT EmployeeId FROM dbo.Employee WHERE Email = N'jacoby@pedroscantina.dk');

;WITH Numbered AS
(
    SELECT s.ShiftId,
           ROW_NUMBER() OVER (ORDER BY mp.[Year], mp.[Month], s.DayOfMonth, s.ShiftTypeId) - 1 AS n
    FROM dbo.Shift s
    JOIN dbo.MonthPlan mp ON mp.MonthPlanId = s.MonthPlanId
),
Staff AS
(
    SELECT EmployeeId,
           ROW_NUMBER() OVER (ORDER BY EmployeeId) - 1 AS rn
    FROM dbo.Employee
    WHERE CanLead = 0 AND IsActive = 1
),
K AS (SELECT COUNT(*) AS k FROM Staff)
INSERT INTO dbo.ShiftAssignment (ShiftId, EmployeeId)
SELECT nb.ShiftId, CASE WHEN nb.n % 4 IN (0, 3) THEN @Pedro ELSE @Jacoby END
FROM Numbered nb
UNION ALL
SELECT nb.ShiftId, s.EmployeeId
FROM Numbered nb CROSS JOIN K JOIN Staff s ON s.rn = nb.n % K.k
UNION ALL
SELECT nb.ShiftId, s.EmployeeId
FROM Numbered nb CROSS JOIN K JOIN Staff s ON s.rn = (nb.n + 2) % K.k
WHERE nb.n % 2 = 0;
GO

SELECT 'Employees' AS TableName, COUNT(*) AS Rows FROM dbo.Employee
UNION ALL SELECT 'MonthPlan',       COUNT(*) FROM dbo.MonthPlan
UNION ALL SELECT 'Shift',           COUNT(*) FROM dbo.Shift
UNION ALL SELECT 'ShiftAssignment', COUNT(*) FROM dbo.ShiftAssignment;
GO
