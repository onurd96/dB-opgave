using System.Text;
using Microsoft.Data.SqlClient;
using PedrosCantina;
using PedrosCantina.Data;
using PedrosCantina.Models;

Console.OutputEncoding = Encoding.UTF8;

// Connection string: kan overskrives med miljøvariablen PEDROS_CONNECTION.

string connectionString =
    Environment.GetEnvironmentVariable("PEDROS_CONNECTION")
    ?? @"Server=(localdb)\MSSQLLocalDB;Database=PedrosCantina;Trusted_Connection=True;TrustServerCertificate=True;";

const int PlanYear = 2026;
const int PlanMonth = 10;
const int Morning = 1;
const int Afternoon = 2;

var employees = new EmployeeRepository(connectionString);
var schedule = new ScheduleRepository(connectionString);

try
{
    // =====================================================================
    // Opgave 6a: CRUD på medarbejdere via EmployeeRepository
    // =====================================================================
    Heading("CRUD på medarbejdere (EmployeeRepository)");

    Console.WriteLine("Alle medarbejdere før:");
    foreach (var e in employees.GetAll()) Console.WriteLine("  " + e);

    Console.WriteLine("\nCREATE - opretter ny medarbejder:");
    var newEmployee = new Employee
    {
        FirstName = "Test",
        LastName = "Testesen",
        Phone = "+45 20 00 00 99",
        Email = "test@pedroscantina.dk",
        CanLead = false,
        IsActive = true
    };
    int newId = employees.Create(newEmployee);
    Console.WriteLine($"  Oprettet med EmployeeId = {newId}");

    Console.WriteLine("\nREAD - henter medarbejderen igen:");
    var fetched = employees.GetById(newId);
    Console.WriteLine("  " + fetched);

    Console.WriteLine("\nUPDATE - ændrer telefon og gør til leder:");
    fetched!.Phone = "+45 20 00 00 98";
    fetched.CanLead = true;
    Console.WriteLine($"  Opdateret: {employees.Update(fetched)}");
    Console.WriteLine("  " + employees.GetById(newId));

    Console.WriteLine("\nDELETE - sletter medarbejderen:");
    Console.WriteLine($"  Slettet: {employees.Delete(newId)}");
    Console.WriteLine($"  Findes stadig? {(employees.GetById(newId) != null ? "ja" : "nej")}");

    Console.WriteLine("\nAlle medarbejdere efter:");
    foreach (var e in employees.GetAll()) Console.WriteLine("  " + e);

    // =====================================================================
    // Opgave 6b: Forespørgslerne fra opgave 5
    // =====================================================================
    Heading($"5.1 Månedsplan for {PlanYear}-{PlanMonth:00} (de første 7 dage)");
    ConsoleTable.Print(schedule.GetMonthPlan(PlanYear, PlanMonth, fromDay: 1, toDay: 7));

    Heading($"5.2 Belastning pr. medarbejder i {PlanYear}-{PlanMonth:00}");
    ConsoleTable.Print(schedule.GetMonthlyLoad(PlanYear, PlanMonth));

    Heading("5.3 Kontaktinfo hvis en medarbejder er syg");
    const int sickDay = 12;
    var onShift = schedule.GetEmployeesOnShift(PlanYear, PlanMonth, sickDay, Morning);
    var sick = onShift.First(e => !e.CanLead);
    Console.WriteLine($"  {sick.FullName} er syg {PlanYear}-{PlanMonth:00}-{sickDay} (formiddag).");
    Console.WriteLine("  Øvrige på vagten: " + string.Join(", ", onShift.Where(e => e.EmployeeId != sick.EmployeeId).Select(e => e.FirstName)));
    Console.WriteLine("  Mulige afløsere (laveste belastning først):");
    ConsoleTable.Print(schedule.GetReplacementCandidates(PlanYear, PlanMonth, sickDay, Morning, sick.EmployeeId));

    Heading($"5.4 Belastning hen over året {PlanYear} (vagter pr. måned)");
    ConsoleTable.Print(schedule.GetYearlyLoad(PlanYear));

    Heading("5.5 Ny månedsplan og justeringer (januar 2027)");
    
    schedule.DeleteMonthPlan(2027, 1);

    int planId = schedule.CreateMonthPlan(2027, 1);
    Console.WriteLine($"  Ny månedsplan oprettet (MonthPlanId = {planId}). Alle vagter er tomme.");

    var all = employees.GetAll().Where(e => e.IsActive).ToList();
    var leaders = all.Where(e => e.CanLead).ToList();
    var staff = all.Where(e => !e.CanLead).ToList();

    // Bemand 1. januar
    schedule.AssignEmployee(2027, 1, 1, Morning, leaders[0].EmployeeId);
    schedule.AssignEmployee(2027, 1, 1, Morning, staff[0].EmployeeId);
    schedule.AssignEmployee(2027, 1, 1, Morning, staff[1].EmployeeId);
    schedule.AssignEmployee(2027, 1, 1, Afternoon, leaders[1].EmployeeId);
    schedule.AssignEmployee(2027, 1, 1, Afternoon, staff[2].EmployeeId);
    Console.WriteLine("\n  Bemandet 1. januar:");
    ConsoleTable.Print(schedule.GetMonthPlan(2027, 1, 1, 1));

    // Reglen om højst 3 personer håndhæves af en trigger i databasen
    Console.WriteLine("\n  Forsøger at tilføje en 4. person på formiddagsvagten:");
    try
    {
        schedule.AssignEmployee(2027, 1, 1, Morning, staff[3].EmployeeId);
        Console.WriteLine("  (uventet: det lykkedes)");
    }
    catch (SqlException ex)
    {
        Console.WriteLine($"  Afvist af databasen: {ex.Message}");
    }

    // Justering: byt en medarbejder ud
    schedule.ReplaceEmployee(2027, 1, 1, Afternoon, staff[2].EmployeeId, staff[3].EmployeeId);
    Console.WriteLine($"\n  Justeret: {staff[2].FirstName} er byttet ud med {staff[3].FirstName} på eftermiddagsvagten:");
    ConsoleTable.Print(schedule.GetMonthPlan(2027, 1, 1, 1));

    // Kontrol af reglerne: fjern lederen fra eftermiddagsvagten
    schedule.RemoveEmployee(2027, 1, 1, Afternoon, leaders[1].EmployeeId);
    Console.WriteLine($"\n  Fjernede {leaders[1].FirstName} fra eftermiddagsvagten. Kontrol af 1. januar:");
    ConsoleTable.Print(schedule.GetInvalidShifts(2027, 1, 1, 1));

    // 
    schedule.DeleteMonthPlan(2027, 1);
    Console.WriteLine("\n  Demo-planen for januar 2027 er slettet igen (CASCADE sletter vagter og tildelinger).");

    Console.WriteLine("\nFærdig.");
}
catch (SqlException ex)
{
    Console.WriteLine();
    Console.WriteLine($"Databasefejl: {ex.Message}");
    Console.WriteLine("Har du kørt sql/01_create.sql og sql/02_seed.sql, og passer connection string?");
    Environment.ExitCode = 1;
}

static void Heading(string text)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 78));
    Console.WriteLine(text);
    Console.WriteLine(new string('=', 78));
}
