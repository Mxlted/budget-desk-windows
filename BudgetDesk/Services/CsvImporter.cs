using BudgetDesk.Models;
using System.Globalization;
using System.Text;

namespace BudgetDesk.Services;

public static class CsvImporter
{
    public static List<StatementRow> ParseStatement(string text, string account, string statementType, List<BudgetCategory> budgets)
    {
        var rows = ParseDelimitedText(text);
        if (rows.Count < 2) return [];

        var headers = rows[0].Select(NormalizeHeader).ToList();
        int dateCol = FindColumn(headers, ["transactiondate", "posteddate", "postdate", "date"]);
        int merchantCol = FindColumn(headers, ["description", "merchant", "name", "payee", "details"]);
        int amountCol = FindColumn(headers, ["amount", "transactionamount"]);
        int debitCol = FindColumn(headers, ["debit", "withdrawal", "charge"]);
        int creditCol = FindColumn(headers, ["credit", "deposit", "payment"]);

        if (dateCol == -1 || merchantCol == -1 || (amountCol == -1 && debitCol == -1 && creditCol == -1))
            return [];

        var result = new List<StatementRow>();
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var date = ParseDate(row.ElementAtOrDefault(dateCol) ?? "");
            var merchant = row.ElementAtOrDefault(merchantCol)?.Trim();
            if (string.IsNullOrEmpty(merchant)) merchant = "Imported transaction";
            if (string.IsNullOrEmpty(date)) continue;

            double amount;
            bool expense;

            if (amountCol != -1)
            {
                var signedAmount = ParseNumber(row.ElementAtOrDefault(amountCol) ?? "");
                amount = Math.Round(Math.Abs(signedAmount), 2);
                expense = statementType.Equals("credit-card", StringComparison.OrdinalIgnoreCase) ? signedAmount > 0 : signedAmount < 0;
            }
            else
            {
                var debit = debitCol != -1 ? Math.Round(Math.Abs(ParseNumber(row.ElementAtOrDefault(debitCol) ?? "")), 2) : 0;
                var credit = creditCol != -1 ? Math.Round(Math.Abs(ParseNumber(row.ElementAtOrDefault(creditCol) ?? "")), 2) : 0;
                expense = debit > 0;
                amount = debit > 0 ? debit : credit;
            }

            if (amount == 0) continue;

            result.Add(new StatementRow
            {
                Date = date,
                Merchant = merchant,
                Category = expense ? BudgetCalculator.DetectCategory(merchant, budgets) : "Income",
                Amount = amount,
                Type = expense ? TransactionType.Expense : TransactionType.Income,
                Account = account,
                Notes = "Imported from statement"
            });
        }
        return result;
    }

    static string NormalizeHeader(string value) => new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    static int FindColumn(List<string> headers, string[] candidates) =>
        headers.FindIndex(h => candidates.Any(c => h.Contains(c)));

    static double ParseNumber(string value)
    {
        var normalized = value.Trim()
            .Replace("$", "").Replace(",", "").Replace(" ", "");
        if (normalized.StartsWith('(') && normalized.EndsWith(')'))
            normalized = "-" + normalized[1..^1];
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    static string ParseDate(string value)
    {
        var trimmed = value.Trim();
        if (BudgetCalculator.IsISODate(trimmed)) return trimmed;

        // US format: M/D/YYYY
        var parts = trimmed.Split('/', '-', '.');
        if (parts.Length == 3)
        {
            if (int.TryParse(parts[0], out var m) && int.TryParse(parts[1], out var d) && int.TryParse(parts[2], out var y))
            {
                if (y > 100 && m >= 1 && m <= 12 && d >= 1 && d <= 31)
                {
                    var isoDate = $"{y}-{m:D2}-{d:D2}";
                    if (BudgetCalculator.IsISODate(isoDate)) return isoDate;
                }
            }
        }

        if (DateTime.TryParse(trimmed, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out var parsed)
            || DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            return $"{parsed.Year}-{parsed.Month:D2}-{parsed.Day:D2}";

        return "";
    }

    static List<List<string>> ParseDelimitedText(string text)
    {
        var firstLine = text.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "";
        var delimiter = firstLine.Contains('\t') ? '\t' : firstLine.Contains(';') ? ';' : ',';

        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';

            if (ch == '"' && next == '"') { cell.Append('"'); i++; continue; }
            if (ch == '"') { inQuotes = !inQuotes; continue; }
            if (ch == delimiter && !inQuotes) { row.Add(cell.ToString().Trim()); cell.Clear(); continue; }
            if ((ch == '\n' || ch == '\r') && !inQuotes)
            {
                if (ch == '\r' && next == '\n') i++;
                row.Add(cell.ToString().Trim()); cell.Clear();
                if (row.Any(c => !string.IsNullOrEmpty(c))) rows.Add(row);
                row = []; continue;
            }
            cell.Append(ch);
        }

        row.Add(cell.ToString().Trim());
        if (row.Any(c => !string.IsNullOrEmpty(c))) rows.Add(row);
        return rows;
    }
}
