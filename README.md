# Budget Desk

Budget Desk is a Windows budgeting app for tracking monthly income, purchases, recurring bills, category budgets, and savings goals. It keeps everything local on your computer and gives you a clear month-by-month view of where money is coming from, where it is going, and what is left.

![Windows](https://img.shields.io/badge/platform-Windows-blue?style=flat-square) ![.NET 9](https://img.shields.io/badge/.NET-9.0-purple?style=flat-square) ![WPF](https://img.shields.io/badge/UI-WPF-green?style=flat-square)

## Features

- Monthly dashboard with income, expenses, cash flow, transaction count, spending trends, category charts, and savings progress.
- Purchase ledger for adding, editing, deleting, searching, and filtering transactions.
- Recurring monthly entries for bills, subscriptions, income, transfers, and automatic savings.
- Category budgets with spending pressure indicators.
- Multiple budget profiles for separate budgets, scenarios, or sample data.
- CSV transaction import with preview before saving.
- JSON backup and restore from inside the app.
- Local data storage with no account sign-in required.
- Dark Windows desktop interface with charting and tab-based navigation.

## Download And Run

1. Open the [Budget Desk releases page](https://github.com/Mxlted/budget-desk-windows/releases/latest).
2. Download the latest Windows release asset, usually `BudgetDesk.exe` or a `.zip` containing it.
3. If the download is zipped, extract it.
4. Run `BudgetDesk.exe`.

The release build is self-contained, so the app can run without installing the .NET runtime separately. Windows may show a SmartScreen warning for unsigned builds; choose **More info** and **Run anyway** if you want to continue.

## App Sections

| Section | What It Does |
| --- | --- |
| Dashboard | Shows monthly totals, charts, yearly summary, category pressure, and savings goals |
| Purchases | Adds and manages one-time expense or income transactions |
| Monthly | Manages recurring items such as rent, subscriptions, paychecks, and savings transfers |
| Categories | Sets and adjusts monthly spending limits |
| Import & Data | Handles profiles, CSV imports, JSON backups, restores, and resets |

## Data Storage

Budget Desk stores profiles locally at:

```text
%LocalAppData%\BudgetDesk\profiles.json
```

That file contains your profiles, transactions, recurring items, category budgets, savings goals, and settings. You can export or restore data from the **Import & Data** tab.

## Technical Details

| Area | Technology |
| --- | --- |
| UI | WPF |
| Runtime | .NET 9 |
| App Pattern | MVVM with CommunityToolkit.Mvvm |
| Charts | LiveCharts2 with SkiaSharp |
| Storage | System.Text.Json |
| Release Build | Self-contained Windows executable |

### Project Layout

```text
BudgetDesk/
  Converters/       WPF value converters
  Models/           Budget and transaction models
  Resources/        XAML styles and theme resources
  Services/         Storage, calculations, CSV import, and sample data
  ViewModels/       Main app state and commands
  App.xaml          Application startup
  MainWindow.xaml   Main user interface
```

## Build From Source

### Requirements

- Windows 10 or Windows 11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Run In Development

```powershell
cd BudgetDesk
dotnet run
```

### Publish A Local Release Build

```powershell
cd BudgetDesk
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "../publish"
```

This creates a local `publish/` folder containing `BudgetDesk.exe`. The `publish/`, `bin/`, and `obj/` folders are generated build output and are ignored by Git.

## License

Built by Mxlted.
