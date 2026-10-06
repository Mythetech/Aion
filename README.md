# Aion SQL Client

Aion is a cross-platform SQL client for developers and database administrators, built with .NET and Blazor. There are two ways to use it: the desktop app, which connects to PostgreSQL, MySQL, SQL Server and LiteDB, and the browser build at [mythetech.github.io/Aion](https://mythetech.github.io/Aion/), which runs SQLite and PostgreSQL (PGlite) databases inside the browser.

<img width="1728" alt="image" src="https://github.com/user-attachments/assets/d9cf7205-ae9a-4bf9-8ff6-7f8f74fdf968" />

## Features

Both hosts share the same UI, so everything below applies to the desktop app and the browser build unless it says otherwise.

### Supported databases

| Engine | Host | Notes |
| --- | --- | --- |
| PostgreSQL | Desktop | |
| MySQL | Desktop | |
| SQL Server | Desktop | SQL or Windows authentication |
| LiteDB | Desktop | Opens a database file and runs LiteDB's own SQL-like queries |
| SQLite | Browser | Runs as WebAssembly |
| PostgreSQL (PGlite) | Browser | Runs as WebAssembly |

The browser build cannot reach database servers. Its databases are created and stored in the browser.

LiteDB support covers browsing, querying and transactions. Data editing, query plans, foreign key navigation and test data generation are not available for it.

### Connections

- Save any number of connections and work with several at once; each query tab picks its own connection and database.
- Create a connection from individual fields or from a raw connection string. Editing a saved connection reads its string back into the fields and keeps options the dialog does not manage.
- Test a connection before saving it.
- Keep each password in the operating system's secret store (macOS Keychain, Windows Credential Manager or libsecret on Linux) or in 1Password, or store nothing and be asked when connecting. Passwords are never written to the connections file.
- See each connection's status, kept current by background health checks, and reconnect from the status chip.
- Saved connections are restored and reconnected on startup.

These apply to the desktop app. In the browser build a connection is an in-browser database, with no server address or password.

### Schema explorer

- Browse databases, tables, views, columns, indexes and foreign keys. Functions and procedures are listed for PostgreSQL, MySQL and SQL Server.
- See each column's short type, nullability and primary or foreign key marker.
- See row counts beside tables: estimates for PostgreSQL, MySQL and SQL Server, exact counts for the other engines.
- Filter the tree by table and column name.
- Open the first 1000 rows of a table or view, open a table for editing, or generate test data from a table's menu.
- Start a `CREATE TABLE` statement from a template, and a `CREATE DATABASE` statement on PostgreSQL, MySQL and SQL Server.
- The tree refreshes after a statement changes the schema.
- Search across connections, databases, tables and open query tabs from the search box at the top of the window.

### Query editor

- Write SQL in a Monaco editor with one tab per query. Tabs can be renamed, cloned, reordered and color tagged.
- Tabs save automatically and reopen with the app.
- IntelliSense completes SQL keywords and the tables and columns of the tab's connection and database. On PostgreSQL, MySQL and SQL Server it also completes functions and procedures once they have been loaded in the schema explorer.
- Run the whole query, or only the selected text.
- Cancel a running query.
- Expand a selected `SELECT * FROM table` into the table's column list.
- Format the SQL in the active tab.
- When a query fails, the error location is marked in the editor and an error card shows the line, column and engine error code, with a button to jump to it.
- For a misspelled table or column name, the error card suggests the closest name from the schema.
- Save a query as a `.sql` file or copy it to the clipboard.

### Results grid

- Sort by any column. Headers show each column's type.
- Row numbers, with row selection by click, Ctrl/Cmd-click, Shift-click or checkbox.
- Find in results filters the grid to the rows containing the text.
- Right-click a row to copy a cell, copy the row as CSV or JSON, view the row as JSON, or export the selected rows.
- JSON objects in a cell are detected and open in a JSON viewer with tree, formatted and raw views.
- Large results show the first 1000 rows (configurable), with options to load more or show all.
- A Messages tab logs each statement and transaction event with its outcome and duration.
- An info panel shows when the query ran, how long it took, and its row and column counts.

### Editing data

- Open a table with Edit Data, or switch a single-table `SELECT` into edit mode. The table needs a primary key.
- Edit cells in place with spreadsheet-style keyboard navigation.
- Set a cell to an explicit NULL or to an empty string.
- Mark rows for deletion.
- A pending changes bar counts the edits and lets you review the generated SQL, apply everything in one transaction, or discard.

Editing changes and deletes existing rows; adding rows is done with SQL. It is available for every engine except LiteDB.

### Export

- Export a result to CSV, Excel (`.xlsx`) or JSON. The export follows the current filter and sort.
- Export only the selected rows, or copy them to the clipboard as CSV.

### Transactions

- Turn on transactions for a tab so that each run joins one open transaction.
- A transaction bar above the editor shows how many statements have run, how many rows they changed and when the transaction started, with Commit and Rollback buttons.
- A tab holding an open transaction is marked with a padlock, and its connection cannot be switched until you commit or roll back.
- Closing a tab rolls back its open transaction.

### Query plans and analysis

- Get the estimated plan with a run, or on its own with Explain.
- Get the actual plan with Explain analyze, which runs the statement and rolls its changes back.
- PostgreSQL plans on the desktop app are drawn as a zoomable diagram showing each node's relative cost, estimated and actual rows, and details on click. Other engines show the plan as the engine returns it.
- Estimated plans are available for every engine except LiteDB. Actual plans are available for PostgreSQL, MySQL, SQL Server and PGlite.
- The Analyze tab groups a result by a column, aggregates a numeric column (count, sum, average, minimum, maximum) and shows it as a bar chart, a line chart or a table.

### Foreign key navigation

- In results opened from a table (first 1000 rows or Edit Data), foreign key cells link to the row they reference.
- View the referenced row in the side panel, switch between the row's foreign keys, or open the related rows in a new tab and keep following them.

### Query history

- Every run is recorded with its SQL, connection, database, status, row count and duration. Result rows are not stored. The most recent 500 runs are kept.
- Search history by SQL text. Entries are grouped by day and can be limited to the current tab's connection.
- Open a past query in a new tab, run it again, or copy its SQL.
- Clear the history.

### Test data generation

- Generate between 1 and 10,000 rows for a table.
- Each column gets a generator suggested from its name and type: random numbers, text, names, emails, dates, UUIDs, booleans, JSON, a value from your own list, or an existing value of a referenced foreign key.
- Rows are inserted in one transaction, so a failure inserts nothing.

Available for every engine except LiteDB.

### Browser build

- On the first visit, choose SQLite or PGlite, then load a sample store database or start from scratch.
- Create a database with the New Database wizard: name it, pick the engine, and define each table's columns (name, type, nullable, primary key, default).
- Databases, query tabs, settings and history are kept in the browser's storage and survive reloads.
- Clear a single database, or all stored data, from the File menu.
- PGlite is loaded from the jsDelivr CDN.

### Application

- Open the command palette with Ctrl/Cmd+K to run a command or jump to a connection.
- Keyboard shortcuts: Ctrl/Cmd+Enter runs the query (or cancels a running one) and Ctrl/Cmd+Shift+E expands `SELECT *`. The desktop app adds Ctrl/Cmd+N for a new query, Ctrl/Cmd+Shift+N for a new connection and Ctrl/Cmd+S to save.
- Use the native menu bar on the desktop app.
- Choose a light or dark theme, or follow the system.
- Adjust editor font size, word wrap, minimap and line numbers, and other settings, in the settings dialog.
- Installed desktop builds check for an update on startup and offer to download it and restart. The check can be turned off in settings.
- Crash and error reporting on the desktop app is opt-in and off by default.
- The desktop app keeps connections, saved queries and history as JSON files, and settings in a LiteDB file, in an `Aion` folder under the user's application data directory.

<img width="1728" alt="image" src="https://github.com/user-attachments/assets/f31b0fce-d0d5-4fd4-af45-741a2dff8055" />

## Tech Stack

- **.NET 11** and C#
- **Blazor** with **MudBlazor** for the UI, shared by both hosts
- **Hermes** for the desktop shell: the window, webview and native menus
- **Blazor WebAssembly** for the browser build
- **Mythetech.Framework** for the message bus, settings, secret managers and update service
- **Monaco** through BlazorMonaco for the SQL editor
- **Npgsql**, **MySql.Data**, **Microsoft.Data.SqlClient** and **LiteDB** for the desktop engines; LiteDB also stores the desktop app's settings
- **SqliteWasmBlazor** and **PGlite** for the browser engines
- **Velopack** for desktop installs and updates
- **ClosedXML** for Excel export and **Z.Blazor.Diagrams** for plan diagrams
- **xUnit**, **bUnit**, **NSubstitute**, **Shouldly** and **Testcontainers** for tests

## Project Structure

| Project | Role |
| --- | --- |
| `Aion.Desktop` | Desktop host: startup, dependency registration, native menus, file-based storage, secret store and updates |
| `Aion.Web` | Browser host: Blazor WebAssembly startup, the SQLite and PGlite providers, onboarding and browser storage |
| `Aion.Components` | Shared Razor components and state: connections, schema explorer, query editor, results, history, visualization and settings |
| `Aion.Core` | Database providers for PostgreSQL, MySQL, SQL Server and LiteDB |
| `Aion.Contracts` | Provider interfaces and the models shared by the other projects |
| `Aion.Test` | Unit, component and integration tests |

## Getting Started

### Prerequisites

- .NET 11 SDK; the exact build is pinned in `global.json`
- For the browser build, the `wasm-tools` workload: `dotnet workload install wasm-tools`
- Docker, only for the integration tests

### Clone

```bash
git clone https://github.com/Mythetech/Aion.git
cd Aion
```

### Run the desktop app

```bash
dotnet run --project Aion.Desktop
```

### Run the browser build

```bash
dotnet run --project Aion.Web
```

Then open the URL it prints, `http://localhost:5000` by default. Add `--urls http://localhost:5217` to use another port.

### Run the tests

```bash
dotnet test
```

This runs the whole suite. The tests under `Aion.Test/Integration` start PostgreSQL, MySQL and SQL Server in containers through Testcontainers, so they need Docker running. To run everything else without Docker:

```bash
dotnet test Aion.Test --filter "FullyQualifiedName!~Aion.Test.Integration"
```

## Roadmap

Planned work that has not shipped yet:

- [ ] Server grouping for better organization
- [ ] Feature parity across all database providers
- [ ] NoSQL database support
    - MongoDB
    - Azure Cosmos DB
    - Amazon DynamoDB
- [ ] Schema comparison tools

## Development Status

Aion is currently in alpha stage. While it's stable for basic development work, we're actively adding features and improvements. We welcome feedback and contributions from the community.

## Contributing

We welcome contributions! Whether it's:
- Bug reports
- Feature requests
- Code contributions
- Documentation improvements

## License
[![MIT License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
