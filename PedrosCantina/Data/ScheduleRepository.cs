using System.Data;
using Microsoft.Data.SqlClient;
using PedrosCantina.Models;

namespace PedrosCantina.Data;

/// <summary>
/// Vagtplaner og rapporter (opgave 5) med ADO.NET.
/// SQL-teksterne svarer til sql/03_queries.sql.
/// </summary>
public class ScheduleRepository
{
    private readonly string _connectionString;

    public ScheduleRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    // ================= 5.1 Månedsplan =================
    public DataTable GetMonthPlan(int year, int month, int? fromDay = null, int? toDay = null)
    {
        const string sql = @"
            SELECT  DATEFROMPARTS(mp.[Year], mp.[Month], s.DayOfMonth) AS ShiftDate,
                    DATENAME(WEEKDAY, DATEFROMPARTS(mp.[Year], mp.[Month], s.DayOfMonth)) AS [Weekday],
                    st.Name AS Shift,
                    CONVERT(CHAR(5), st.StartTime, 108) + '-' + CONVERT(CHAR(5), st.EndTime, 108) AS [Time],
                    STRING_AGG(CASE WHEN e.CanLead = 1 THEN e.FirstName + ' (leder)' ELSE e.FirstName END, ', ')
                        WITHIN GROUP (ORDER BY e.CanLead DESC, e.FirstName) AS Employees
            FROM    MonthPlan mp
            JOIN    Shift s       ON s.MonthPlanId = mp.MonthPlanId
            JOIN    ShiftType st  ON st.ShiftTypeId = s.ShiftTypeId
            LEFT JOIN ShiftAssignment sa ON sa.ShiftId = s.ShiftId
            LEFT JOIN Employee e  ON e.EmployeeId = sa.EmployeeId
            WHERE   mp.[Year] = @Year AND mp.[Month] = @Month
              AND   (@FromDay IS NULL OR s.DayOfMonth >= @FromDay)
              AND   (@ToDay   IS NULL OR s.DayOfMonth <= @ToDay)
            GROUP BY mp.[Year], mp.[Month], s.DayOfMonth, s.ShiftTypeId, st.Name, st.StartTime, st.EndTime
            ORDER BY ShiftDate, s.ShiftTypeId;";

        return Query(sql,
            P("@Year", year), P("@Month", month),
            P("@FromDay", fromDay), P("@ToDay", toDay));
    }

    // ================= 5.2 Belastning for en måned =================
    public DataTable GetMonthlyLoad(int year, int month)
    {
        const string sql = @"
            WITH MonthAssignments AS
            (
                SELECT sa.EmployeeId, s.ShiftId, DATEDIFF(HOUR, st.StartTime, st.EndTime) AS Hours
                FROM ShiftAssignment sa
                JOIN Shift s      ON s.ShiftId = sa.ShiftId
                JOIN MonthPlan mp ON mp.MonthPlanId = s.MonthPlanId
                JOIN ShiftType st ON st.ShiftTypeId = s.ShiftTypeId
                WHERE mp.[Year] = @Year AND mp.[Month] = @Month
            )
            SELECT  e.EmployeeId,
                    e.FirstName + ' ' + e.LastName AS Employee,
                    COUNT(ma.ShiftId)              AS Shifts,
                    COALESCE(SUM(ma.Hours), 0)     AS Hours
            FROM    Employee e
            LEFT JOIN MonthAssignments ma ON ma.EmployeeId = e.EmployeeId
            WHERE   e.IsActive = 1
            GROUP BY e.EmployeeId, e.FirstName, e.LastName
            ORDER BY Hours DESC, Employee;";

        return Query(sql, P("@Year", year), P("@Month", month));
    }

    // ================= 5.3 Kontaktinfo ved sygdom =================
    public DataTable GetReplacementCandidates(int year, int month, int day, int shiftTypeId, int sickEmployeeId)
    {
        const string sql = @"
            WITH TheShift AS
            (
                SELECT s.ShiftId, s.MonthPlanId, s.DayOfMonth
                FROM Shift s
                JOIN MonthPlan mp ON mp.MonthPlanId = s.MonthPlanId
                WHERE mp.[Year] = @Year AND mp.[Month] = @Month
                  AND s.DayOfMonth = @Day AND s.ShiftTypeId = @ShiftTypeId
            ),
            MonthLoad AS
            (
                SELECT sa.EmployeeId, COUNT(*) AS ShiftsThisMonth
                FROM ShiftAssignment sa
                JOIN Shift s      ON s.ShiftId = sa.ShiftId
                JOIN MonthPlan mp ON mp.MonthPlanId = s.MonthPlanId
                WHERE mp.[Year] = @Year AND mp.[Month] = @Month
                GROUP BY sa.EmployeeId
            ),
            LeadersLeft AS
            (
                SELECT COUNT(*) AS LeadersLeft
                FROM ShiftAssignment sa
                JOIN TheShift t ON t.ShiftId = sa.ShiftId
                JOIN Employee e ON e.EmployeeId = sa.EmployeeId
                WHERE e.CanLead = 1 AND e.EmployeeId <> @SickEmployeeId
            )
            SELECT  e.EmployeeId, e.FirstName, e.LastName, e.Phone, e.Email, e.CanLead,
                    COALESCE(ml.ShiftsThisMonth, 0) AS ShiftsThisMonth,
                    CASE WHEN EXISTS (SELECT 1
                                      FROM ShiftAssignment sa2
                                      JOIN Shift s2   ON s2.ShiftId = sa2.ShiftId
                                      JOIN TheShift t ON t.MonthPlanId = s2.MonthPlanId
                                                     AND t.DayOfMonth  = s2.DayOfMonth
                                      WHERE sa2.EmployeeId = e.EmployeeId)
                         THEN 'Ja' ELSE 'Nej' END AS WorksOtherShiftSameDay
            FROM    Employee e
            CROSS JOIN LeadersLeft ll
            LEFT JOIN MonthLoad ml ON ml.EmployeeId = e.EmployeeId
            WHERE   e.IsActive = 1
              AND   e.EmployeeId <> @SickEmployeeId
              AND   NOT EXISTS (SELECT 1
                                FROM ShiftAssignment sa
                                JOIN TheShift t ON t.ShiftId = sa.ShiftId
                                WHERE sa.EmployeeId = e.EmployeeId)
              AND   (ll.LeadersLeft > 0 OR e.CanLead = 1)
            ORDER BY ShiftsThisMonth, e.FirstName;";

        return Query(sql,
            P("@Year", year), P("@Month", month), P("@Day", day),
            P("@ShiftTypeId", shiftTypeId), P("@SickEmployeeId", sickEmployeeId));
    }

    /// <summary>Medarbejderne på en bestemt vagt (bruges til at finde en "syg" i demoen).</summary>
    public List<Employee> GetEmployeesOnShift(int year, int month, int day, int shiftTypeId)
    {
        const string sql = @"
            SELECT e.EmployeeId, e.FirstName, e.LastName, e.Phone, e.Email, e.CanLead, e.IsActive
            FROM ShiftAssignment sa
            JOIN Shift s      ON s.ShiftId = sa.ShiftId
            JOIN MonthPlan mp ON mp.MonthPlanId = s.MonthPlanId
            JOIN Employee e   ON e.EmployeeId = sa.EmployeeId
            WHERE mp.[Year] = @Year AND mp.[Month] = @Month
              AND s.DayOfMonth = @Day AND s.ShiftTypeId = @ShiftTypeId
            ORDER BY e.CanLead DESC, e.FirstName;";

        var table = Query(sql, P("@Year", year), P("@Month", month), P("@Day", day), P("@ShiftTypeId", shiftTypeId));
        return table.Rows.Cast<DataRow>().Select(r => new Employee
        {
            EmployeeId = (int)r["EmployeeId"],
            FirstName = (string)r["FirstName"],
            LastName = (string)r["LastName"],
            Phone = (string)r["Phone"],
            Email = (string)r["Email"],
            CanLead = (bool)r["CanLead"],
            IsActive = (bool)r["IsActive"]
        }).ToList();
    }

    // ================= 5.4 Belastning hen over året =================
    public DataTable GetYearlyLoad(int year)
    {
        const string sql = @"
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
                    COUNT(x.ShiftId)          AS TotalShifts,
                    COALESCE(SUM(x.Hours), 0) AS TotalHours
            FROM    Employee e
            LEFT JOIN
            (
                SELECT sa.EmployeeId, s.ShiftId, mp.[Month], DATEDIFF(HOUR, st.StartTime, st.EndTime) AS Hours
                FROM ShiftAssignment sa
                JOIN Shift s      ON s.ShiftId = sa.ShiftId
                JOIN MonthPlan mp ON mp.MonthPlanId = s.MonthPlanId
                JOIN ShiftType st ON st.ShiftTypeId = s.ShiftTypeId
                WHERE mp.[Year] = @Year
            ) x ON x.EmployeeId = e.EmployeeId
            WHERE   e.IsActive = 1
            GROUP BY e.EmployeeId, e.FirstName, e.LastName
            ORDER BY TotalHours DESC, Employee;";

        return Query(sql, P("@Year", year));
    }

    // ================= 5.5 Nye planer og justeringer =================

    /// <summary>Opretter en ny månedsplan med tomme vagter. Returnerer MonthPlanId.</summary>
    public int CreateMonthPlan(int year, int month)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand("usp_CreateMonthPlan", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@Year", SqlDbType.SmallInt).Value = year;
        command.Parameters.Add("@Month", SqlDbType.TinyInt).Value = month;

        connection.Open();
        return (int)command.ExecuteScalar()!;
    }

    /// <summary>Sætter en medarbejder på en vagt. Returnerer antal rækker (0 = vagten findes ikke).</summary>
    public int AssignEmployee(int year, int month, int day, int shiftTypeId, int employeeId)
    {
        const string sql = @"
            INSERT INTO ShiftAssignment (ShiftId, EmployeeId)
            SELECT s.ShiftId, @EmployeeId
            FROM Shift s
            JOIN MonthPlan mp ON mp.MonthPlanId = s.MonthPlanId
            WHERE mp.[Year] = @Year AND mp.[Month] = @Month
              AND s.DayOfMonth = @Day AND s.ShiftTypeId = @ShiftTypeId;";

        return Execute(sql,
            P("@Year", year), P("@Month", month), P("@Day", day),
            P("@ShiftTypeId", shiftTypeId), P("@EmployeeId", employeeId));
    }

    /// <summary>Bytter en medarbejder på en vagt ud med en anden (fx ved sygdom).</summary>
    public int ReplaceEmployee(int year, int month, int day, int shiftTypeId, int oldEmployeeId, int newEmployeeId)
    {
        const string sql = @"
            UPDATE sa
            SET    sa.EmployeeId = @NewEmployeeId
            FROM   ShiftAssignment sa
            JOIN   Shift s      ON s.ShiftId = sa.ShiftId
            JOIN   MonthPlan mp ON mp.MonthPlanId = s.MonthPlanId
            WHERE  mp.[Year] = @Year AND mp.[Month] = @Month
              AND  s.DayOfMonth = @Day AND s.ShiftTypeId = @ShiftTypeId
              AND  sa.EmployeeId = @OldEmployeeId;";

        return Execute(sql,
            P("@Year", year), P("@Month", month), P("@Day", day), P("@ShiftTypeId", shiftTypeId),
            P("@OldEmployeeId", oldEmployeeId), P("@NewEmployeeId", newEmployeeId));
    }

    /// <summary>Fjerner en medarbejder fra en vagt.</summary>
    public int RemoveEmployee(int year, int month, int day, int shiftTypeId, int employeeId)
    {
        const string sql = @"
            DELETE sa
            FROM   ShiftAssignment sa
            JOIN   Shift s      ON s.ShiftId = sa.ShiftId
            JOIN   MonthPlan mp ON mp.MonthPlanId = s.MonthPlanId
            WHERE  mp.[Year] = @Year AND mp.[Month] = @Month
              AND  s.DayOfMonth = @Day AND s.ShiftTypeId = @ShiftTypeId
              AND  sa.EmployeeId = @EmployeeId;";

        return Execute(sql,
            P("@Year", year), P("@Month", month), P("@Day", day),
            P("@ShiftTypeId", shiftTypeId), P("@EmployeeId", employeeId));
    }

    /// <summary>Sletter en hel månedsplan (vagter og tildelinger følger med via CASCADE).</summary>
    public int DeleteMonthPlan(int year, int month)
    {
        const string sql = "DELETE FROM MonthPlan WHERE [Year] = @Year AND [Month] = @Month;";
        return Execute(sql, P("@Year", year), P("@Month", month));
    }

    /// <summary>Vagter der bryder reglerne (2-3 personer og mindst én leder).</summary>
    public DataTable GetInvalidShifts(int year, int month, int? fromDay = null, int? toDay = null)
    {
        const string sql = @"
            SELECT  ShiftDate, ShiftName, Staff, Leaders,
                    CASE WHEN Staff < 2 THEN 'For faa medarbejdere'
                         WHEN Staff > 3 THEN 'For mange medarbejdere'
                         WHEN Leaders = 0 THEN 'Mangler leder' END AS Problem
            FROM    vw_ShiftStaffing
            WHERE   [Year] = @Year AND [Month] = @Month AND IsValid = 0
              AND   (@FromDay IS NULL OR DayOfMonth >= @FromDay)
              AND   (@ToDay   IS NULL OR DayOfMonth <= @ToDay)
            ORDER BY ShiftDate, ShiftTypeId;";

        return Query(sql,
            P("@Year", year), P("@Month", month),
            P("@FromDay", fromDay), P("@ToDay", toDay));
    }

    // ---------- hjælpemetoder ----------
    private static SqlParameter P(string name, object? value) =>
        new(name, value ?? DBNull.Value);

    private DataTable Query(string sql, params SqlParameter[] parameters)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);

        connection.Open();
        using var reader = command.ExecuteReader();
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    private int Execute(string sql, params SqlParameter[] parameters)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);

        connection.Open();
        return command.ExecuteNonQuery();
    }
}
