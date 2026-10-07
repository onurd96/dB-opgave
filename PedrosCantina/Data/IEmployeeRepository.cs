using PedrosCantina.Models;

namespace PedrosCantina.Data;

public interface IEmployeeRepository
{
    /// <summary>Opretter medarbejderen og returnerer det nye EmployeeId.</summary>
    int Create(Employee employee);

    /// <summary>Henter én medarbejder, eller null hvis id ikke findes.</summary>
    Employee? GetById(int employeeId);

    /// <summary>Henter alle medarbejdere (aktive og inaktive).</summary>
    List<Employee> GetAll();

    /// <summary>Opdaterer en medarbejder. Returnerer true hvis en række blev ændret.</summary>
    bool Update(Employee employee);

    /// <summary>Sletter en medarbejder. Returnerer true hvis en række blev slettet.</summary>
    bool Delete(int employeeId);
}
