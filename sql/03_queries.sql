/* =====================================================================
   Pedros Cantina - 03_queries.sql
   ===================================================================== */
USE PedrosCantina;
GO

/* ---------------------------------------------------------------------
   5.1  Vis en maanedsplan
   Én raekke pr. vagt med medarbejderne samlet i en kolonne (leder markeret).
   --------------------------------------------------------------------- */
DECLARE @Year SMALLINT = 2026, @Month TINYINT = 10;
DECLARE @FromDay INT = NULL, @ToDay INT = NULL;

SELECT  DATEFROMPARTS(mp.[Year], mp.[Month], s.DayOfMonth)             AS ShiftDate,
        DATENAME(WEEKDAY, DATEFROMPARTS(mp.[Year], mp.[Month], s.DayOfMonth)) AS [Weekday],
        st.Name                                                         AS Shift,
        CONVERT(CHAR(5), st.StartTime, 108) + '-' + CONVERT(CHAR(5), st.EndTime, 108) AS [Time],
        STRING_AGG(CASE WHEN e.CanLead = 1 THEN e.FirstName + ' (leder)' ELSE e.FirstName END, ', ')
            WITHIN GROUP (ORDER BY e.CanLead DESC, e.FirstName)         AS Employees
FROM    dbo.MonthPlan mp
JOIN    dbo.Shift s            ON s.MonthPlanId = mp.MonthPlanId
JOIN    dbo.ShiftType st       ON st.ShiftTypeId = s.ShiftTypeId
LEFT JOIN dbo.ShiftAssignment sa ON sa.ShiftId = s.ShiftId
LEFT JOIN dbo.Employee e       ON e.EmployeeId = sa.EmployeeId
WHERE   mp.[Year] = @Year AND mp.[Month] = @Month
  AND   (@FromDay IS NULL OR s.DayOfMonth >= @FromDay)
  AND   (@ToDay   IS NULL OR s.DayOfMonth <= @ToDay)
GROUP BY mp.[Year], mp.[Month], s.DayOfMonth, s.ShiftTypeId, st.Name, st.StartTime, st.EndTime
ORDER BY ShiftDate, s.ShiftTypeId;
GO

/* ---------------------------------------------------------------------
   5.2  Belastning af de enkelte medarbejdere for en maaned
   Antal vagter og timer. Medarbejdere uden vagter vises med 0.
   --------------------------------------------------------------------- */
DECLARE @Year SMALLINT = 2026, @Month TINYINT = 10;

WITH MonthAssignments AS
(
    SELECT sa.EmployeeId, s.ShiftId, DATEDIFF(HOUR, st.StartTime, st.EndTime) AS Hours
    FROM dbo.ShiftAssignment sa
    JOIN dbo.Shift s       ON s.ShiftId = sa.ShiftId
    JOIN dbo.MonthPlan mp  ON mp.MonthPlanId = s.MonthPlanId
    JOIN dbo.ShiftType st  ON st.ShiftTypeId = s.ShiftTypeId
    WHERE mp.[Year] = @Year AND mp.[Month] = @Month
)
SELECT  e.EmployeeId,
        e.FirstName + ' ' + e.LastName   AS Employee,
        COUNT(ma.ShiftId)                AS Shifts,
        COALESCE(SUM(ma.Hours), 0)       AS Hours
FROM    dbo.Employee e
LEFT JOIN MonthAssignments ma ON ma.EmployeeId = e.EmployeeId
WHERE   e.IsActive = 1
GROUP BY e.EmployeeId, e.FirstName, e.LastName
ORDER BY Hours DESC, Employee;
GO

/* ---------------------------------------------------------------------
   5.3  Kontaktinformation hvis en medarbejder er syg
   Finder aktive medarbejdere, der IKKE allerede er paa den vagt, med
   telefon og e-mail. Hvis den syge var vagtens eneste leder, vises kun
   medarbejdere der kan vaere leder. Lavest belastning denne maaned foerst.
   --------------------------------------------------------------------- */
DECLARE @Year SMALLINT = 2026, @Month TINYINT = 10, @Day INT = 12, @ShiftTypeId TINYINT = 1;
DECLARE @SickEmployeeId INT = 3;   -- ret til den medarbejder, der er syg

WITH TheShift AS
(
    SELECT s.ShiftId, s.MonthPlanId, s.DayOfMonth
    FROM dbo.Shift s
    JOIN dbo.MonthPlan mp ON mp.MonthPlanId = s.MonthPlanId
    WHERE mp.[Year] = @Year AND mp.[Month] = @Month
      AND s.DayOfMonth = @Day AND s.ShiftTypeId = @ShiftTypeId
),
MonthLoad AS
(
    SELECT sa.EmployeeId, COUNT(*) AS ShiftsThisMonth
    FROM dbo.ShiftAssignment sa
    JOIN dbo.Shift s      ON s.ShiftId = sa.ShiftId
    JOIN dbo.MonthPlan mp ON mp.MonthPlanId = s.MonthPlanId
    WHERE mp.[Year] = @Year AND mp.[Month] = @Month
    GROUP BY sa.EmployeeId
),
LeadersLeft AS
(
    SELECT COUNT(*) AS LeadersLeft
    FROM dbo.ShiftAssignment sa
    JOIN TheShift t ON t.ShiftId = sa.ShiftId
    JOIN dbo.Employee e ON e.EmployeeId = sa.EmployeeId
    WHERE e.CanLead = 1 AND e.EmployeeId <> @SickEmployeeId
)
SELECT  e.EmployeeId,
        e.FirstName, e.LastName, e.Phone, e.Email, e.CanLead,
        COALESCE(ml.ShiftsThisMonth, 0) AS ShiftsThisMonth,
        CASE WHEN EXISTS (SELECT 1
                          FROM dbo.ShiftAssignment sa2
                          JOIN dbo.Shift s2 ON s2.ShiftId = sa2.ShiftId
                          JOIN TheShift t   ON t.MonthPlanId = s2.MonthPlanId
                                           AND t.DayOfMonth  = s2.DayOfMonth
                          WHERE sa2.EmployeeId = e.EmployeeId)
             THEN 'Ja' ELSE 'Nej' END   AS WorksOtherShiftSameDay
FROM    dbo.Employee e
CROSS JOIN LeadersLeft ll
LEFT JOIN MonthLoad ml ON ml.EmployeeId = e.EmployeeId
WHERE   e.IsActive = 1
  AND   e.EmployeeId <> @SickEmployeeId
  AND   NOT EXISTS (SELECT 1
                    FROM dbo.ShiftAssignment sa
                    JOIN TheShift t ON t.ShiftId = sa.ShiftId
                    WHERE sa.EmployeeId = e.EmployeeId)
  AND   (ll.LeadersLeft > 0 OR e.CanLead = 1)
ORDER BY ShiftsThisMonth, e.FirstName;
GO

/* ---------------------------------------------------------------------
   5.4  Belastning af medarbejdere hen over aaret
   Antal vagter pr. maaned (jan-dec) samt total for vagter og timer.
   --------------------------------------------------------------------- */
DECLARE @Year SMALLINT = 2026;

SELECT  e.EmployeeId,
        e.FirstName + ' ' + e.LastName AS Employee,
        SUM(CASE WHEN x.[Month] = 1  THEN 1 ELSE 0 END) AS Jan,
        SUM(CASE WHEN x.[Month] = 2  THEN 1 ELSE 0 END) AS Feb,
        SUM(CASE WHEN x.[Month] = 3  THEN 1 ELSE 0 END) AS Mar,
        SUM(CASE WHEN x.[Month] = 4  THEN 1 ELSE 0 END) AS Apr,
        SUM(CASE WHEN x.[Month] = 5  THEN 1 ELSE 0 END) AS May,
        SUM(CASE WHEN x.[Month] = 6  THEN 1 ELSE 0 END) AS Jun,
        SUM(CASE WHEN x.[Month] = 7  THEN 1 ELSE 0 END) AS Jul,
        SUM(CASE WHEN x.[Month] = 8  THEN 1 ELSE 0 END) AS Aug,
        SUM(CASE WHEN x.[Month] = 9  THEN 1 ELSE 0 END) AS Sep,
        SUM(CASE WHEN x.[Month] = 10 THEN 1 ELSE 0 END) AS Oct,
        SUM(CASE WHEN x.[Month] = 11 THEN 1 ELSE 0 END) AS Nov,
        SUM(CASE WHEN x.[Month] = 12 THEN 1 ELSE 0 END) AS Dec,
        COUNT(x.ShiftId)               AS TotalShifts,
        COALESCE(SUM(x.Hours), 0)      AS TotalHours
FROM    dbo.Employee e
LEFT JOIN
(
    SELECT sa.EmployeeId, s.ShiftId, mp.[Month], DATEDIFF(HOUR, st.StartTime, st.EndTime) AS Hours
    FROM dbo.ShiftAssignment sa
    JOIN dbo.Shift s       ON s.ShiftId = sa.ShiftId
    JOIN dbo.MonthPlan mp  ON mp.MonthPlanId = s.MonthPlanId
    JOIN dbo.ShiftType st  ON st.ShiftTypeId = s.ShiftTypeId
    WHERE mp.[Year] = @Year
) x ON x.EmployeeId = e.EmployeeId
WHERE   e.IsActive = 1
GROUP BY e.EmployeeId, e.FirstName, e.LastName
ORDER BY TotalHours DESC, Employee;
GO

/* ---------------------------------------------------------------------
   5.5  Lav nye maanedsplaner og juster eksisterende planer
   --------------------------------------------------------------------- */

-- (a) Ny maanedsplan: opretter planen + to tomme vagter for hver dag
EXEC dbo.usp_CreateMonthPlan @Year = 2027, @Month = 1;
GO

-- (b) Saet en medarbejder paa en vagt (fx 1. januar 2027, formiddag)
DECLARE @Year SMALLINT = 2027, @Month TINYINT = 1, @Day INT = 1, @ShiftTypeId TINYINT = 1;
DECLARE @EmployeeId INT = 1;

INSERT INTO dbo.ShiftAssignment (ShiftId, EmployeeId)
SELECT s.ShiftId, @EmployeeId
FROM dbo.Shift s
JOIN dbo.MonthPlan mp ON mp.MonthPlanId = s.MonthPlanId
WHERE mp.[Year] = @Year AND mp.[Month] = @Month
  AND s.DayOfMonth = @Day AND s.ShiftTypeId = @ShiftTypeId;
GO

-- (c) Byt en medarbejder ud med en anden (fx ved sygdom)
DECLARE @Year SMALLINT = 2027, @Month TINYINT = 1, @Day INT = 1, @ShiftTypeId TINYINT = 1;
DECLARE @OldEmployeeId INT = 1, @NewEmployeeId INT = 2;

UPDATE sa
SET    sa.EmployeeId = @NewEmployeeId
FROM   dbo.ShiftAssignment sa
JOIN   dbo.Shift s      ON s.ShiftId = sa.ShiftId
JOIN   dbo.MonthPlan mp ON mp.MonthPlanId = s.MonthPlanId
WHERE  mp.[Year] = @Year AND mp.[Month] = @Month
  AND  s.DayOfMonth = @Day AND s.ShiftTypeId = @ShiftTypeId
  AND  sa.EmployeeId = @OldEmployeeId;
GO

-- (d) Fjern en medarbejder fra en vagt
DECLARE @Year SMALLINT = 2027, @Month TINYINT = 1, @Day INT = 1, @ShiftTypeId TINYINT = 1;
DECLARE @EmployeeId INT = 2;

DELETE sa
FROM   dbo.ShiftAssignment sa
JOIN   dbo.Shift s      ON s.ShiftId = sa.ShiftId
JOIN   dbo.MonthPlan mp ON mp.MonthPlanId = s.MonthPlanId
WHERE  mp.[Year] = @Year AND mp.[Month] = @Month
  AND  s.DayOfMonth = @Day AND s.ShiftTypeId = @ShiftTypeId
  AND  sa.EmployeeId = @EmployeeId;
GO

-- (e) Kontrol: vagter der bryder reglerne (2-3 personer og mindst en leder)
DECLARE @Year SMALLINT = 2027, @Month TINYINT = 1;
DECLARE @FromDay INT = NULL, @ToDay INT = NULL;

SELECT  ShiftDate, ShiftName, Staff, Leaders,
        CASE WHEN Staff < 2 THEN 'For faa medarbejdere'
             WHEN Staff > 3 THEN 'For mange medarbejdere'
             WHEN Leaders = 0 THEN 'Mangler leder' END AS Problem
FROM    dbo.vw_ShiftStaffing
WHERE   [Year] = @Year AND [Month] = @Month AND IsValid = 0
  AND   (@FromDay IS NULL OR DayOfMonth >= @FromDay)
  AND   (@ToDay   IS NULL OR DayOfMonth <= @ToDay)
ORDER BY ShiftDate, ShiftTypeId;
GO

-- (f) Slet en hel maanedsplan
DELETE FROM dbo.MonthPlan WHERE [Year] = 2027 AND [Month] = 1;
GO
