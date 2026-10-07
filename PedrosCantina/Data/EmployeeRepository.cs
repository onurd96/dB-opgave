using System.Data;
using Microsoft.Data.SqlClient;
using PedrosCantina.Models;

namespace PedrosCantina.Data;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly string _connectionString;

    public EmployeeRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    // ---------- CREATE ----------
    public int Create(Employee employee)
    {
        const string sql = @"
            INSERT INTO Employee (FirstName, LastName, Phone, Email, CanLead, IsActive)
            OUTPUT INSERTED.EmployeeId
            VALUES (@FirstName, @LastName, @Phone, @Email, @CanLead, @IsActive);";

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        AddParameters(command, employee);

        connection.Open();
        employee.EmployeeId = (int)command.ExecuteScalar()!;
        return employee.EmployeeId;
    }

    // ---------- READ (én) ----------
    public Employee? GetById(int employeeId)
    {
        const string sql = @"
            SELECT EmployeeId, FirstName, LastName, Phone, Email, CanLead, IsActive
            FROM Employee
            WHERE EmployeeId = @EmployeeId;";

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = employeeId;

        connection.Open();
        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    // ---------- READ (alle) ----------
    public List<Employee> GetAll()
    {
        const string sql = @"
            SELECT EmployeeId, FirstName, LastName, Phone, Email, CanLead, IsActive
            FROM Employee
            ORDER BY EmployeeId;";

        var result = new List<Employee>();

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);

        connection.Open();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(Map(reader));
        }
        return result;
    }

    // ---------- UPDATE ----------
    public bool Update(Employee employee)
    {
        const string sql = @"
            UPDATE Employee
            SET FirstName = @FirstName,
                LastName  = @LastName,
                Phone     = @Phone,
                Email     = @Email,
                CanLead   = @CanLead,
                IsActive  = @IsActive
            WHERE EmployeeId = @EmployeeId;";

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        AddParameters(command, employee);
        command.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = employee.EmployeeId;

        connection.Open();
        return command.ExecuteNonQuery() > 0;
    }

    // ---------- DELETE ----------
    public bool Delete(int employeeId)
    {
       
        const string sql = "DELETE FROM Employee WHERE EmployeeId = @EmployeeId;";

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = employeeId;

        connection.Open();
        return command.ExecuteNonQuery() > 0;
    }

    // ---------- hjælpemetoder ----------
    private static void AddParameters(SqlCommand command, Employee e)
    {
        command.Parameters.Add("@FirstName", SqlDbType.NVarChar, 50).Value = e.FirstName;
        command.Parameters.Add("@LastName", SqlDbType.NVarChar, 50).Value = e.LastName;
        command.Parameters.Add("@Phone", SqlDbType.NVarChar, 20).Value = e.Phone;
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value = e.Email;
        command.Parameters.Add("@CanLead", SqlDbType.Bit).Value = e.CanLead;
        command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = e.IsActive;
    }

    private static Employee Map(SqlDataReader reader) => new()
    {
        EmployeeId = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
        FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
        LastName = reader.GetString(reader.GetOrdinal("LastName")),
        Phone = reader.GetString(reader.GetOrdinal("Phone")),
        Email = reader.GetString(reader.GetOrdinal("Email")),
        CanLead = reader.GetBoolean(reader.GetOrdinal("CanLead")),
        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
    };
}
