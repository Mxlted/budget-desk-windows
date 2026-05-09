using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BudgetDesk.Models;

namespace BudgetDesk.Services;

public class StorageService
{
    static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BudgetDesk");

    static readonly string ProfilesFile = Path.Combine(AppDataFolder, "profiles.json");

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public StorageService()
    {
        Directory.CreateDirectory(AppDataFolder);
    }

    public List<BudgetProfile> LoadProfiles()
    {
        try
        {
            if (!File.Exists(ProfilesFile)) return [CreateStarterProfile()];
            var json = File.ReadAllText(ProfilesFile);
            var profiles = JsonSerializer.Deserialize<List<BudgetProfile>>(json, JsonOptions);
            if (profiles == null || profiles.Count == 0) return [CreateStarterProfile()];
            return NormalizeProfiles(profiles);
        }
        catch
        {
            return [CreateStarterProfile()];
        }
    }

    public void SaveProfiles(List<BudgetProfile> profiles)
    {
        profiles = NormalizeProfiles(profiles);
        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        File.WriteAllText(ProfilesFile, json);
    }

    public string ExportProfile(BudgetState state, string profileName, string month)
    {
        var slug = profileName.ToLowerInvariant()
            .Replace(" ", "-")
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .Aggregate("", (s, c) => s + c);
        if (string.IsNullOrEmpty(slug)) slug = "budget";
        var fileName = $"budget-desk-{slug}-{month}.json";

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = fileName,
            Filter = "JSON files (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(dialog.FileName, json);
            return dialog.FileName;
        }
        return "";
    }

    public BudgetState? ImportProfile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var state = JsonSerializer.Deserialize<BudgetState>(json, JsonOptions);
                if (state != null)
                    return NormalizeState(state);
            }
            catch { }
        }
        return null;
    }

    static List<BudgetProfile> NormalizeProfiles(List<BudgetProfile> profiles)
    {
        var normalized = profiles
            .Where(p => p != null)
            .Select(NormalizeProfile)
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        return normalized.Count > 0 ? normalized : [CreateStarterProfile()];
    }

    static BudgetProfile NormalizeProfile(BudgetProfile profile)
    {
        profile.Id = string.IsNullOrWhiteSpace(profile.Id) ? $"profile-{BudgetCalculator.MakeId()}" : profile.Id.Trim();
        profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? "My budget" : profile.Name.Trim();
        profile.State = NormalizeState(profile.State);

        var now = DateTime.UtcNow.ToString("o");
        profile.CreatedAt = string.IsNullOrWhiteSpace(profile.CreatedAt) ? now : profile.CreatedAt;
        profile.UpdatedAt = string.IsNullOrWhiteSpace(profile.UpdatedAt) ? profile.CreatedAt : profile.UpdatedAt;
        return profile;
    }

    static BudgetState NormalizeState(BudgetState? state)
    {
        state ??= SampleData.CreateEmptyBudgetState();
        state.Transactions ??= [];
        state.Recurring ??= [];
        state.Budgets ??= [];
        state.SavingsGoals ??= [];
        state.Accounts ??= [];
        state.Transactions = state.Transactions.Where(t => t != null).ToList();
        state.Recurring = state.Recurring.Where(r => r != null).ToList();
        state.Budgets = state.Budgets.Where(b => b != null).ToList();
        state.SavingsGoals = state.SavingsGoals.Where(g => g != null).ToList();

        NormalizeBudgets(state);

        var fallbackCategory = state.Budgets.First(b => !b.CategoryName.Equals("Income", StringComparison.OrdinalIgnoreCase)).CategoryName;
        var fallbackAccount = state.Accounts.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)) ?? SampleData.DefaultAccounts[0];

        foreach (var transaction in state.Transactions)
        {
            transaction.Id = string.IsNullOrWhiteSpace(transaction.Id) ? BudgetCalculator.MakeId() : transaction.Id.Trim();
            transaction.Date = BudgetCalculator.IsISODate(transaction.Date) ? transaction.Date : $"{BudgetCalculator.CurrentMonthKey()}-01";
            transaction.Merchant = string.IsNullOrWhiteSpace(transaction.Merchant) ? "Transaction" : transaction.Merchant.Trim();
            transaction.Category = transaction.Type == TransactionType.Income
                ? "Income"
                : NormalizeCategory(transaction.Category, state.Budgets, fallbackCategory);
            transaction.Amount = Math.Round(Math.Abs(transaction.Amount), 2);
            transaction.Account = NormalizeAccount(transaction.Account, state.Accounts, fallbackAccount);
            transaction.Notes = string.IsNullOrWhiteSpace(transaction.Notes) ? null : transaction.Notes.Trim();
        }

        foreach (var recurring in state.Recurring)
        {
            recurring.Id = string.IsNullOrWhiteSpace(recurring.Id) ? BudgetCalculator.MakeId() : recurring.Id.Trim();
            recurring.Merchant = string.IsNullOrWhiteSpace(recurring.Merchant) ? "Monthly item" : recurring.Merchant.Trim();
            recurring.Category = recurring.Type == TransactionType.Income
                ? "Income"
                : NormalizeCategory(recurring.Category, state.Budgets, fallbackCategory);
            recurring.Amount = Math.Round(Math.Abs(recurring.Amount), 2);
            recurring.Day = Math.Clamp(recurring.Day, 1, 31);
            recurring.Account = NormalizeAccount(recurring.Account, state.Accounts, fallbackAccount);
            recurring.StartMonth = BudgetCalculator.NormalizeMonthKey(recurring.StartMonth);
            recurring.EndMonth = BudgetCalculator.IsMonthKey(recurring.EndMonth) ? recurring.EndMonth : null;
            if (recurring.EndMonth != null && string.Compare(recurring.EndMonth, recurring.StartMonth, StringComparison.Ordinal) < 0)
                recurring.EndMonth = null;
            recurring.Notes = string.IsNullOrWhiteSpace(recurring.Notes) ? null : recurring.Notes.Trim();
        }

        foreach (var goal in state.SavingsGoals)
        {
            goal.Id = string.IsNullOrWhiteSpace(goal.Id) ? BudgetCalculator.MakeId() : goal.Id.Trim();
            goal.Name = string.IsNullOrWhiteSpace(goal.Name) ? "Savings goal" : goal.Name.Trim();
            goal.Target = Math.Max(0, Math.Round(goal.Target, 2));
            goal.Saved = Math.Max(0, Math.Round(goal.Saved, 2));
            goal.MonthlyContribution = Math.Max(0, Math.Round(goal.MonthlyContribution, 2));
            goal.Color = IsHexColor(goal.Color) ? goal.Color : "#3b82f6";
        }

        state.PlannedMonthlyIncome = Math.Max(0, Math.Round(state.PlannedMonthlyIncome, 2));
        state.Theme = Enum.IsDefined(state.Theme) ? state.Theme : InterfaceTheme.Dark;
        return state;
    }

    static void NormalizeBudgets(BudgetState state)
    {
        if (state.Budgets.Count == 0)
        {
            state.Budgets = CloneDefaultBudgets();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<BudgetCategory>();
        foreach (var budget in state.Budgets)
        {
            var name = string.IsNullOrWhiteSpace(budget.CategoryName) ? "Other" : budget.CategoryName.Trim();
            if (name.Equals("Income", StringComparison.OrdinalIgnoreCase)) name = "Income";
            if (!seen.Add(name)) continue;

            budget.CategoryName = name;
            budget.MonthlyLimit = Math.Max(0, Math.Round(budget.MonthlyLimit, 2));
            budget.Color = IsHexColor(budget.Color) ? budget.Color : "#9ca3af";
            budget.Keywords ??= [];
            budget.Keywords = budget.Keywords
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            normalized.Add(budget);
        }

        state.Budgets = normalized;

        if (!state.Budgets.Any(b => b.CategoryName.Equals("Income", StringComparison.OrdinalIgnoreCase)))
        {
            var income = SampleData.CategoryCatalog.First(c => c.CategoryName == "Income");
            state.Budgets.Add(new BudgetCategory
            {
                CategoryName = income.CategoryName,
                MonthlyLimit = income.MonthlyLimit,
                Color = income.Color,
                Keywords = [.. income.Keywords]
            });
        }

        if (!state.Budgets.Any(b => !b.CategoryName.Equals("Income", StringComparison.OrdinalIgnoreCase)))
        {
            var other = SampleData.CategoryCatalog.First(c => c.CategoryName == "Other");
            state.Budgets.Add(new BudgetCategory
            {
                CategoryName = other.CategoryName,
                MonthlyLimit = other.MonthlyLimit,
                Color = other.Color,
                Keywords = [.. other.Keywords]
            });
        }

        state.Accounts = state.Accounts
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (state.Accounts.Count == 0)
            state.Accounts = [.. SampleData.DefaultAccounts];
    }

    static List<BudgetCategory> CloneDefaultBudgets() => SampleData.CategoryCatalog.Select(c => new BudgetCategory
    {
        CategoryName = c.CategoryName,
        MonthlyLimit = c.MonthlyLimit,
        Color = c.Color,
        Keywords = [.. c.Keywords]
    }).ToList();

    static string NormalizeCategory(string category, List<BudgetCategory> budgets, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(category))
        {
            var match = budgets.FirstOrDefault(b => b.CategoryName.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match != null) return match.CategoryName;
        }
        return fallback;
    }

    static string NormalizeAccount(string account, List<string> accounts, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(account))
        {
            var match = accounts.FirstOrDefault(a => a.Equals(account.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return fallback;
    }

    static bool IsHexColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#') return false;
        return value.Skip(1).All(Uri.IsHexDigit);
    }

    static BudgetProfile CreateStarterProfile()
    {
        var now = DateTime.UtcNow.ToString("o");
        return new BudgetProfile
        {
            Id = "default-profile",
            Name = "My budget",
            State = SampleData.CreateInitialBudgetState(),
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
