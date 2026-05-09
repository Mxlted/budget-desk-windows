using BudgetDesk.Models;

namespace BudgetDesk.Services;

public static class SampleData
{
    public static readonly List<BudgetCategory> CategoryCatalog =
    [
        new() { CategoryName = "Housing", MonthlyLimit = 1850, Color = "#6366f1", Keywords = ["rent", "mortgage", "leasing", "apartment"] },
        new() { CategoryName = "Groceries", MonthlyLimit = 620, Color = "#14b8a6", Keywords = ["grocery", "market", "trader joe", "aldi", "kroger", "whole foods", "walmart"] },
        new() { CategoryName = "Dining", MonthlyLimit = 310, Color = "#f97316", Keywords = ["restaurant", "cafe", "coffee", "doordash", "uber eats", "grubhub", "pizza"] },
        new() { CategoryName = "Transportation", MonthlyLimit = 420, Color = "#0891b2", Keywords = ["gas", "fuel", "uber", "lyft", "parking", "metro", "transit"] },
        new() { CategoryName = "Utilities", MonthlyLimit = 260, Color = "#ca8a04", Keywords = ["electric", "water", "internet", "utility", "verizon", "xfinity", "at&t"] },
        new() { CategoryName = "Entertainment", MonthlyLimit = 230, Color = "#ec4899", Keywords = ["netflix", "spotify", "hulu", "steam", "cinema", "concert", "ticket"] },
        new() { CategoryName = "Shopping", MonthlyLimit = 340, Color = "#a855f7", Keywords = ["amazon", "target", "best buy", "store", "shop"] },
        new() { CategoryName = "Health", MonthlyLimit = 220, Color = "#ef4444", Keywords = ["pharmacy", "doctor", "clinic", "dental", "health", "cvs", "walgreens"] },
        new() { CategoryName = "Savings", MonthlyLimit = 900, Color = "#2563eb", Keywords = ["transfer", "savings", "brokerage"] },
        new() { CategoryName = "Income", MonthlyLimit = 0, Color = "#3b82f6", Keywords = ["payroll", "deposit", "salary", "invoice"] },
        new() { CategoryName = "Other", MonthlyLimit = 250, Color = "#6b7280", Keywords = [] },
    ];

    public static readonly List<string> DefaultAccounts = ["Checking", "Credit Card", "Savings"];

    static string MakeDate(int monthOffset, int day)
    {
        var now = DateTime.Now;
        var target = new DateTime(now.Year, now.Month, 1).AddMonths(monthOffset);
        var maxDay = DateTime.DaysInMonth(target.Year, target.Month);
        day = Math.Min(day, maxDay);
        target = new DateTime(target.Year, target.Month, day);
        return $"{target.Year}-{target.Month:D2}-{target.Day:D2}";
    }

    static string CurrentMonth()
    {
        var now = DateTime.Now;
        return $"{now.Year}-{now.Month:D2}";
    }

    public static BudgetState CreateInitialBudgetState()
    {
        var cm = CurrentMonth();
        return new BudgetState
        {
            Transactions =
            [
                new() { Id = "seed-income-1", Date = MakeDate(0, 1), Merchant = "Payroll deposit", Category = "Income", Amount = 5400, Type = TransactionType.Income, Account = "Checking", Source = TransactionSource.Manual },
                new() { Id = "seed-grocery-1", Date = MakeDate(0, 3), Merchant = "Neighborhood Market", Category = "Groceries", Amount = 112.49, Type = TransactionType.Expense, Account = "Credit Card", Source = TransactionSource.Manual },
                new() { Id = "seed-dining-1", Date = MakeDate(0, 5), Merchant = "Friday dinner", Category = "Dining", Amount = 68.20, Type = TransactionType.Expense, Account = "Credit Card", Source = TransactionSource.Manual },
                new() { Id = "seed-transport-1", Date = MakeDate(0, 7), Merchant = "Fuel stop", Category = "Transportation", Amount = 47.82, Type = TransactionType.Expense, Account = "Credit Card", Source = TransactionSource.Manual },
                new() { Id = "seed-health-1", Date = MakeDate(0, 10), Merchant = "Pharmacy refill", Category = "Health", Amount = 24.50, Type = TransactionType.Expense, Account = "Credit Card", Source = TransactionSource.Manual },
                new() { Id = "seed-shopping-1", Date = MakeDate(-1, 12), Merchant = "Home office supplies", Category = "Shopping", Amount = 146.33, Type = TransactionType.Expense, Account = "Credit Card", Source = TransactionSource.Manual },
                new() { Id = "seed-grocery-2", Date = MakeDate(-1, 9), Merchant = "Weekly groceries", Category = "Groceries", Amount = 151.68, Type = TransactionType.Expense, Account = "Credit Card", Source = TransactionSource.Manual },
                new() { Id = "seed-income-2", Date = MakeDate(-1, 1), Merchant = "Payroll deposit", Category = "Income", Amount = 5400, Type = TransactionType.Income, Account = "Checking", Source = TransactionSource.Manual },
                new() { Id = "seed-utilities-1", Date = MakeDate(-2, 16), Merchant = "Electric company", Category = "Utilities", Amount = 128.90, Type = TransactionType.Expense, Account = "Checking", Source = TransactionSource.Manual },
                new() { Id = "seed-entertainment-1", Date = MakeDate(-2, 20), Merchant = "Concert tickets", Category = "Entertainment", Amount = 94.00, Type = TransactionType.Expense, Account = "Credit Card", Source = TransactionSource.Manual },
            ],
            Recurring =
            [
                new() { Id = "rec-rent", Merchant = "Rent", Category = "Housing", Amount = 1725, Type = TransactionType.Expense, Day = 1, Account = "Checking", Active = true, StartMonth = cm },
                new() { Id = "rec-internet", Merchant = "Fiber internet", Category = "Utilities", Amount = 78, Type = TransactionType.Expense, Day = 12, Account = "Checking", Active = true, StartMonth = cm },
                new() { Id = "rec-streaming", Merchant = "Streaming bundle", Category = "Entertainment", Amount = 42, Type = TransactionType.Expense, Day = 18, Account = "Credit Card", Active = true, StartMonth = cm },
                new() { Id = "rec-auto-save", Merchant = "Automatic savings", Category = "Savings", Amount = 650, Type = TransactionType.Expense, Day = 2, Account = "Checking", Active = true, StartMonth = cm },
            ],
            Budgets = CategoryCatalog.Select(c => new BudgetCategory
            {
                CategoryName = c.CategoryName,
                MonthlyLimit = c.MonthlyLimit,
                Color = c.Color,
                Keywords = [.. c.Keywords]
            }).ToList(),
            CategoryLimitsEnabled = true,
            SavingsGoals =
            [
                new() { Id = "goal-emergency", Name = "Emergency fund", Target = 12000, Saved = 7800, MonthlyContribution = 500, Color = "#2563eb" },
                new() { Id = "goal-travel", Name = "Trip fund", Target = 3000, Saved = 1220, MonthlyContribution = 150, Color = "#0891b2" },
            ],
            Accounts = [.. DefaultAccounts],
            PlannedMonthlyIncome = 5400
        };
    }

    public static BudgetState CreateEmptyBudgetState() => new()
    {
        Transactions = [],
        Recurring = [],
        Budgets = CategoryCatalog.Select(c => new BudgetCategory
        {
            CategoryName = c.CategoryName,
            MonthlyLimit = c.MonthlyLimit,
            Color = c.Color,
            Keywords = [.. c.Keywords]
        }).ToList(),
        CategoryLimitsEnabled = true,
        SavingsGoals = [],
        Accounts = [.. DefaultAccounts],
        PlannedMonthlyIncome = 0
    };
}
