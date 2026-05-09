using BudgetDesk.Models;
using System.Globalization;

namespace BudgetDesk.Services;

public static class BudgetCalculator
{
    public static string CurrentMonthKey()
    {
        var now = DateTime.Now;
        return $"{now.Year}-{now.Month:D2}";
    }

    public static bool IsMonthKey(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (value.Length != 7) return false;
        var parts = value.Split('-');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out var year)) return false;
        if (!int.TryParse(parts[1], out var month)) return false;
        return year is >= 1 and <= 9999 && month is >= 1 and <= 12;
    }

    public static string NormalizeMonthKey(string? value, string? fallback = null)
    {
        var safeFallback = IsMonthKey(fallback) ? fallback! : CurrentMonthKey();
        return IsMonthKey(value) ? value! : safeFallback;
    }

    public static bool IsISODate(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 10) return false;
        var parts = value.Split('-');
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var year) || !int.TryParse(parts[1], out var month) || !int.TryParse(parts[2], out var day))
            return false;
        try
        {
            var d = new DateTime(year, month, day);
            return d.Year == year && d.Month == month && d.Day == day;
        }
        catch { return false; }
    }

    public static string ToMonthKey(string date) => IsISODate(date) ? date[..7] : "";

    public static string FormatMonth(string month)
    {
        var m = NormalizeMonthKey(month);
        var parts = m.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[0], out var year) && int.TryParse(parts[1], out var mon))
        {
            var d = new DateTime(year, mon, 1);
            return d.ToString("MMMM yyyy");
        }
        return month;
    }

    public static string AddMonths(string month, int offset)
    {
        var m = NormalizeMonthKey(month);
        var parts = m.Split('-');
        int.TryParse(parts[0], out var year);
        int.TryParse(parts[1], out var mon);
        var d = new DateTime(year, mon, 1).AddMonths(offset);
        return $"{d.Year}-{d.Month:D2}";
    }

    public static List<string> MonthRange(string endingMonth, int months)
    {
        var result = new List<string>();
        for (int i = months - 1; i >= 0; i--)
            result.Add(AddMonths(endingMonth, -i));
        return result;
    }

    public static string YearFromMonth(string month) => NormalizeMonthKey(month)[..4];

    public static List<string> YearToDateMonthRange(string month)
    {
        var m = NormalizeMonthKey(month);
        var year = YearFromMonth(m);
        int.TryParse(m[5..7], out var endMonth);
        var result = new List<string>();
        for (int i = 1; i <= endMonth; i++)
            result.Add($"{year}-{i:D2}");
        return result;
    }

    static int DaysInMonth(string month)
    {
        var m = NormalizeMonthKey(month);
        var parts = m.Split('-');
        int.TryParse(parts[0], out var year);
        int.TryParse(parts[1], out var mon);
        return DateTime.DaysInMonth(year, mon);
    }

    public static bool RecurringAppliesToMonth(RecurringPurchase purchase, string month)
    {
        if (!purchase.Active) return false;
        var normalizedMonth = NormalizeMonthKey(month);
        var startMonth = NormalizeMonthKey(purchase.StartMonth);
        if (string.Compare(startMonth, normalizedMonth, StringComparison.Ordinal) > 0) return false;
        if (IsMonthKey(purchase.EndMonth))
        {
            if (string.Compare(purchase.EndMonth, normalizedMonth, StringComparison.Ordinal) < 0) return false;
        }
        return true;
    }

    public static List<Transaction> MaterializeRecurring(List<RecurringPurchase> recurring, string month)
    {
        var m = NormalizeMonthKey(month);
        return recurring
            .Where(item => RecurringAppliesToMonth(item, m))
            .Select(item =>
            {
                var day = Math.Min(Math.Max(1, item.Day), DaysInMonth(m));
                var parts = m.Split('-');
                int.TryParse(parts[0], out var year);
                int.TryParse(parts[1], out var mon);
                return new Transaction
                {
                    Id = $"recurring-{item.Id}-{m}",
                    Date = $"{year}-{mon:D2}-{day:D2}",
                    Merchant = item.Merchant,
                    Category = item.Type == TransactionType.Income ? "Income" : item.Category,
                    Amount = Math.Round(item.Amount, 2),
                    Type = item.Type,
                    Account = item.Account,
                    Notes = item.Notes,
                    Source = TransactionSource.Recurring
                };
            }).ToList();
    }

    public static List<Transaction> MonthTransactions(BudgetState state, string month)
    {
        var m = NormalizeMonthKey(month);
        var oneTime = state.Transactions.Where(t => ToMonthKey(t.Date) == m).ToList();
        var materialized = MaterializeRecurring(state.Recurring, m);
        return oneTime.Concat(materialized).OrderByDescending(t => t.Date).ToList();
    }

    public static MonthSummary SummarizeMonth(BudgetState state, string month)
    {
        var rows = MonthTransactions(state, month);
        double actualIncome = 0, expenses = 0, recurring = 0, recurringIncome = 0, manualExpenses = 0;

        foreach (var item in rows)
        {
            if (item.Type == TransactionType.Income)
            {
                actualIncome += item.Amount;
                if (item.Source == TransactionSource.Recurring) recurringIncome += item.Amount;
            }
            else
            {
                expenses += item.Amount;
                if (item.Source == TransactionSource.Recurring) recurring += item.Amount;
                else manualExpenses += item.Amount;
            }
        }

        actualIncome = Math.Round(actualIncome, 2);
        expenses = Math.Round(expenses, 2);
        recurring = Math.Round(recurring, 2);
        var plannedIncome = Math.Max(0, Math.Round(state.PlannedMonthlyIncome, 2));
        var usesPlannedIncome = actualIncome <= 0 && plannedIncome > 0;
        var income = usesPlannedIncome ? plannedIncome : actualIncome;
        var remaining = Math.Round(income - expenses, 2);

        return new MonthSummary
        {
            Rows = rows,
            ActualIncome = actualIncome,
            PlannedIncome = plannedIncome,
            Income = income,
            UsesPlannedIncome = usesPlannedIncome,
            Expenses = expenses,
            Recurring = recurring,
            RecurringIncome = recurringIncome,
            ManualExpenses = manualExpenses,
            Remaining = remaining,
            SavingsRate = income > 0 ? remaining / income * 100 : 0
        };
    }

    public static List<CategoryBreakdownItem> CategoryBreakdown(BudgetState state, string month)
    {
        var rows = MonthTransactions(state, month).Where(t => t.Type == TransactionType.Expense);
        var totals = new Dictionary<string, double>();
        var colors = BuildCategoryColorMap(state.Budgets);

        foreach (var item in rows)
        {
            if (!totals.ContainsKey(item.Category)) totals[item.Category] = 0;
            totals[item.Category] += item.Amount;
        }

        return totals
            .Select(kv => new CategoryBreakdownItem
            {
                Category = kv.Key,
                Value = Math.Round(kv.Value, 2),
                Color = colors.GetValueOrDefault(kv.Key, "#9ca3af")
            })
            .OrderByDescending(x => x.Value)
            .ToList();
    }

    public static List<CategoryBreakdownItem> CategoryBreakdownForMonths(BudgetState state, List<string> months)
    {
        var totals = new Dictionary<string, double>();
        var colors = BuildCategoryColorMap(state.Budgets);

        foreach (var month in months)
        {
            foreach (var item in MonthTransactions(state, month).Where(t => t.Type == TransactionType.Expense))
            {
                if (!totals.ContainsKey(item.Category)) totals[item.Category] = 0;
                totals[item.Category] += item.Amount;
            }
        }

        return totals
            .Select(kv => new CategoryBreakdownItem
            {
                Category = kv.Key,
                Value = Math.Round(kv.Value, 2),
                Color = colors.GetValueOrDefault(kv.Key, "#9ca3af")
            })
            .OrderByDescending(x => x.Value)
            .ToList();
    }

    public static List<TrendPoint> TrendData(BudgetState state, string selectedMonth)
    {
        return MonthRange(selectedMonth, 6).Select(month =>
        {
            var summary = SummarizeMonth(state, month);
            var parts = month.Split('-');
            int.TryParse(parts[0], out var year);
            int.TryParse(parts[1], out var mon);
            var label = new DateTime(year, mon, 1).ToString("MMM");
            return new TrendPoint
            {
                Month = label,
                Income = summary.Income,
                Expenses = summary.Expenses,
                RecurringAmount = summary.Recurring
            };
        }).ToList();
    }

    public static YearlySummary GetYearlySummary(BudgetState state, string selectedMonth)
    {
        var months = YearToDateMonthRange(selectedMonth);
        var monthlySummaries = months.Select(m =>
        {
            var s = SummarizeMonth(state, m);
            var parts = m.Split('-');
            int.TryParse(parts[0], out var year);
            int.TryParse(parts[1], out var mon);
            return (month: m, label: new DateTime(year, mon, 1).ToString("MMM"), summary: s);
        }).ToList();

        var income = monthlySummaries.Sum(x => x.summary.Income);
        var expenses = monthlySummaries.Sum(x => x.summary.Expenses);
        var recurring = monthlySummaries.Sum(x => x.summary.Recurring);
        var remaining = Math.Round(income - expenses, 2);
        var activeMonths = monthlySummaries.Where(x => x.summary.Rows.Count > 0).ToList();
        var bestMonth = monthlySummaries.OrderByDescending(x => x.summary.Remaining).FirstOrDefault();
        var highestExpense = monthlySummaries.OrderByDescending(x => x.summary.Expenses).FirstOrDefault();
        var endLabel = monthlySummaries.LastOrDefault().label ?? "";
        var yearStr = YearFromMonth(selectedMonth);
        var periodLabel = months.Count == 12 ? yearStr : $"Jan-{endLabel} {yearStr}";

        return new YearlySummary
        {
            Year = yearStr,
            PeriodLabel = periodLabel,
            MonthCount = months.Count,
            Income = Math.Round(income, 2),
            Expenses = Math.Round(expenses, 2),
            Recurring = Math.Round(recurring, 2),
            Remaining = remaining,
            SavingsRate = income > 0 ? remaining / income * 100 : 0,
            AverageMonthlyExpenses = months.Count > 0 ? Math.Round(expenses / months.Count, 2) : 0,
            ActiveMonthCount = activeMonths.Count,
            BestMonthLabel = bestMonth.label ?? "",
            BestMonthRemaining = bestMonth.summary?.Remaining ?? 0,
            HighestExpenseMonthLabel = highestExpense.label ?? "",
            HighestExpenseMonthExpenses = highestExpense.summary?.Expenses ?? 0,
            YearlyBreakdown = CategoryBreakdownForMonths(state, months)
        };
    }

    public static List<BudgetBarItem> GetBudgetBars(BudgetState state, string month)
    {
        var breakdown = CategoryBreakdown(state, month);
        var spentByCategory = breakdown
            .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Value), StringComparer.OrdinalIgnoreCase);

        return state.Budgets
            .Select(b => new BudgetBarItem
            {
                Category = b.CategoryName,
                Spent = Math.Round(spentByCategory.GetValueOrDefault(b.CategoryName, 0), 2),
                Budget = Math.Round(b.MonthlyLimit, 2),
                Color = b.Color
            })
            .Where(x => x.Budget > 0)
            .OrderByDescending(x => x.Budget > 0 ? x.Spent / x.Budget : 0)
            .ToList();
    }

    public static string DetectCategory(string merchant, List<BudgetCategory> budgets)
    {
        var normalized = merchant.Trim();
        var match = budgets.FirstOrDefault(b =>
            b.Keywords.Any(k => !string.IsNullOrWhiteSpace(k)
                && normalized.Contains(k.Trim(), StringComparison.OrdinalIgnoreCase)));
        return match?.CategoryName ?? budgets.FirstOrDefault(b => b.CategoryName != "Income")?.CategoryName ?? "Other";
    }

    public static string MakeId() => Guid.NewGuid().ToString();

    public static string FormatCurrency(double value) => value.ToString("C0", CultureInfo.GetCultureInfo("en-US"));
    public static string FormatCurrencyPrecise(double value) => value.ToString("C2", CultureInfo.GetCultureInfo("en-US"));

    static Dictionary<string, string> BuildCategoryColorMap(IEnumerable<BudgetCategory> budgets)
    {
        var colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var budget in budgets)
        {
            if (string.IsNullOrWhiteSpace(budget.CategoryName)) continue;
            colors.TryAdd(budget.CategoryName, budget.Color);
        }
        return colors;
    }
}
