namespace PedrosCantina.Models;

/// <summary>
/// En medarbejder hos Pedros Cantina (også Pedro og Jacoby).
/// CanLead = true betyder, at personen kan være leder på en vagt.
/// </summary>
public class Employee
{
    public int EmployeeId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public bool CanLead { get; set; }
    public bool IsActive { get; set; } = true;

    public string FullName => $"{FirstName} {LastName}";

    public override string ToString() =>
        $"[{EmployeeId}] {FullName}, tlf {Phone}, {Email}, leder: {(CanLead ? "ja" : "nej")}, aktiv: {(IsActive ? "ja" : "nej")}";
}
