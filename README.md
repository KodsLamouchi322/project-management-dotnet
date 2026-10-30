# project-management-dotnet

A full-stack **ASP.NET Core MVC** project management application with role-based access control, real-time dashboards, and automated background services.

---

## Features

### Role-Based Access
| Role | Capabilities |
|---|---|
| **Admin** | Global dashboard, charts, team overview, user management |
| **SeniorDev** | Create/manage projects & tasks, assign developers, monitor progress |
| **Developer** | View assigned tasks, update task status, score tracking |
| **Client** | Track project progress, rate projects, project messaging |

### Core Modules
- **Project Management** — Create, edit, soft-delete projects with deadline tracking
- **Task Management** — Assign tasks to developers with priority, deadline, and duration
- **Dashboards** — Role-specific dashboards with Chart.js visualizations (progress trends, task status, leaderboards)
- **Notifications** — Automatic alerts for task assignments, project progress milestones (20%/40%/60%/80%/100%), and status changes
- **Messaging** — Project-scoped chat between Client, SeniorDev, and Developers
- **Developer Scoring** — Weekly and monthly performance scores based on task completion and deadlines
- **Background Services** — Daily progress snapshots + hourly notification processing

---

## Tech Stack

- **Framework:** ASP.NET Core MVC (.NET 10)
- **ORM:** Entity Framework Core with SQL Server (LocalDB)
- **Auth:** ASP.NET Core Identity with role-based authorization
- **Frontend:** Razor Views, Bootstrap 5, Chart.js, jQuery
- **Architecture:** MVC + Service Layer + Repository pattern via EF Core

---

## Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server LocalDB (included with Visual Studio)

### Run Locally

```bash
git clone https://github.com/KodsLamouchi322/project-management-dotnet.git
cd project-management-dotnet/monprojet.NET
dotnet run
```

The app will automatically:
1. Apply all pending EF Core migrations
2. Seed the database with default roles and a demo Admin account

### Default Admin Account
| Field | Value |
|---|---|
| Email | `admin@projectmanager.com` |
| Password | `Admin@123` |

---

## Project Structure

```
monprojet.NET/
├── Controllers/          # MVC Controllers (Account, Project, Task, Dashboard, Admin...)
├── Models/               # ViewModels organized by feature
├── Views/                # Razor views per controller
├── Middleware/           # Global exception handler
├── Migrations/           # EF Core migrations
├── ProjectManager.Core/
│   ├── Entities/         # Domain models (Project, ProjectTask, ApplicationUser...)
│   └── Enums/            # ProjectStatus, TaskStatus, TaskPriority, UserRole
└── ProjectManager.Infrastructure/
    ├── Data/             # ApplicationDbContext + DatabaseSeeder
    └── Services/         # ProjectService, TaskService, NotificationService, Background services
```

---

## License

MIT
