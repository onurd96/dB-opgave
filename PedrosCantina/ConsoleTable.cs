using System.Data;

namespace PedrosCantina;


public static class ConsoleTable
{
    public static void Print(DataTable table)
    {
        if (table.Rows.Count == 0)
        {
            Console.WriteLine("  (ingen rækker)");
            return;
        }

        var columns = table.Columns.Cast<DataColumn>().ToList();
        var cells = table.Rows.Cast<DataRow>()
            .Select(row => columns.Select(c => Format(row[c])).ToArray())
            .ToList();

        var widths = columns
            .Select((c, i) => Math.Max(c.ColumnName.Length, cells.Max(r => r[i].Length)))
            .ToArray();

        Console.WriteLine("  " + string.Join(" | ", columns.Select((c, i) => c.ColumnName.PadRight(widths[i]))));
        Console.WriteLine("  " + string.Join("-+-", widths.Select(w => new string('-', w))));
        foreach (var row in cells)
        {
            Console.WriteLine("  " + string.Join(" | ", row.Select((v, i) => v.PadRight(widths[i]))));
        }
        Console.WriteLine($"  ({table.Rows.Count} rækker)");
    }

    private static string Format(object value) => value switch
    {
        DBNull => "",
        DateTime d => d.ToString("yyyy-MM-dd"),
        bool b => b ? "Ja" : "Nej",
        _ => value.ToString() ?? ""
    };
}
