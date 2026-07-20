# Admin Area Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Razor Pages admin area under `/Admin`, gated to the `Admin` role via ASP.NET Core Identity, with CRUD pages for Events/Users/Bookings and a placeholder-analytics dashboard.

**Architecture:** `User` becomes an `IdentityUser<int>` so `Event.UserId`'s existing int FK is untouched; `AppDbContext` becomes `IdentityDbContext<User, IdentityRole<int>, int>`. A new `Booking` entity links `Event`+`User`. Cookie auth via `AddIdentity`, Razor Pages area-folder authorization convention gates everything under `/Admin` except `/Admin/Login`. A startup seeder creates the first Admin account from configuration. Bootstrap 5 via CDN — no local static assets.

**Tech Stack:** ASP.NET Core 8, EF Core 8.0.11 (SqlServer + Identity.EntityFrameworkCore), Razor Pages, ASP.NET Core Identity, Bootstrap 5 (CDN), xUnit + `Microsoft.AspNetCore.Mvc.Testing` + EF Core InMemory for the one integration test.

## Global Constraints

- Target framework: `net8.0` (matches existing project).
- EF Core packages pinned to `8.0.11` to match the existing `Microsoft.EntityFrameworkCore.SqlServer` version.
- Reuse the existing `ConnectionStrings:DefaultConnection` (LocalDB) — do not add a second connection string.
- Bootstrap 5 loaded from the jsDelivr CDN in the shared layout — no `wwwroot`, no `UseStaticFiles`, no npm/webpack tooling.
- No client-side validation scripts — server round-trip validation via `asp-validation-summary`/`asp-validation-for` tag helpers is sufficient (values render from `ModelState` on the postback that fails validation).
- Area name is exactly `"Admin"`; role name is exactly `"Admin"`.
- Testing approach (per user decision during brainstorming): CRUD pages are verified manually (steps given in each task); one automated integration test suite (Task 8) covers the security-critical role/auth gate end-to-end. No per-page unit tests.
- `EventsController`/`UsersController` (existing Web API) are not modified. `UsersController.PostUser` will keep working exactly as before; the only behavioral side-effect of merging `User` into `IdentityUser<int>` is that `Email` is no longer marked `[Required]` at the model level (Identity's base `Email` property carries no validation attribute) — this is a documented, accepted consequence of the merge, not a bug to work around.

---

### Task 1: Booking model + Identity-enabled User/AppDbContext + migration

**Files:**
- Modify: `EventBoard.Api.csproj`
- Modify: `Models/User.cs`
- Modify: `Data/AppDbContext.cs`
- Create: `Models/Booking.cs`
- Create (generated): `Migrations/<timestamp>_AddIdentityAndBookings.cs` and its `.Designer.cs`, plus updated `Migrations/AppDbContextModelSnapshot.cs`

**Interfaces:**
- Produces: `EventBoard.Api.Models.User : IdentityUser<int>` with `Name` (string) and `Events` (ICollection<Event>) — consumed by every later task that touches users.
- Produces: `EventBoard.Api.Models.Booking` — `Id` (int), `EventId` (int), `Event?`, `UserId` (int), `User?`, `CreatedAt` (DateTime), `Status` (`BookingStatus`) — consumed by Task 7.
- Produces: `EventBoard.Api.Models.BookingStatus` enum — `Pending`, `Confirmed`, `Cancelled`.
- Produces: `EventBoard.Api.Data.AppDbContext : IdentityDbContext<User, IdentityRole<int>, int>` with `DbSet<Event> Events` and `DbSet<Booking> Bookings` — consumed by every later task.

- [ ] **Step 1: Add the Identity EF Core package**

Edit `EventBoard.Api.csproj`, add this line inside the existing `<ItemGroup>` (alongside `Microsoft.EntityFrameworkCore.SqlServer`):

```xml
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.11" />
```

- [ ] **Step 2: Restore packages**

Run: `dotnet restore`
Expected: `Restore complete` / no errors.

- [ ] **Step 3: Rewrite `Models/User.cs` to extend `IdentityUser<int>`**

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace EventBoard.Api.Models;

public class User : IdentityUser<int>
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
```

- [ ] **Step 4: Create `Models/Booking.cs`**

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventBoard.Api.Models;

public enum BookingStatus
{
    Pending,
    Confirmed,
    Cancelled
}

public class Booking
{
    public int Id { get; set; }

    [Required]
    public int EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }

    [Required]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public BookingStatus Status { get; set; } = BookingStatus.Pending;
}
```

- [ ] **Step 5: Rewrite `Data/AppDbContext.cs` to extend `IdentityDbContext`**

```csharp
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventBoard.Api.Data;

public class AppDbContext : IdentityDbContext<User, IdentityRole<int>, int>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Event> Events => Set<Event>();

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasMany(u => u.Events)
            .WithOne(e => e.User)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Event)
            .WithMany()
            .HasForeignKey(b => b.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

`base.OnModelCreating(modelBuilder)` is required here — it configures the Identity tables (`AspNetUsers`, `AspNetRoles`, etc.). Omitting it breaks Identity's schema.

- [ ] **Step 6: Build to confirm it compiles**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 7: Generate the migration**

Run: `dotnet ef migrations add AddIdentityAndBookings`
Expected: `Done. To undo this action, use 'ef migrations remove'` and three new files under `Migrations/`.

- [ ] **Step 8: Apply the migration to the local database**

Run: `dotnet ef database update`
Expected: `Done.` — creates/alters `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, etc., and the new `Bookings` table in the `EventBoardDb` LocalDB database.

- [ ] **Step 9: Commit**

```bash
git add EventBoard.Api.csproj Models/User.cs Models/Booking.cs Data/AppDbContext.cs Migrations/
git commit -m "feat: add Booking entity, convert User to IdentityUser<int>"
```

---

### Task 2: Identity/RazorPages wiring + admin bootstrap seeding

**Files:**
- Modify: `Program.cs`
- Create: `Services/AdminSeeder.cs`

**Interfaces:**
- Consumes: `EventBoard.Api.Models.User`, `EventBoard.Api.Data.AppDbContext` (Task 1).
- Produces: `EventBoard.Api.Services.AdminSeeder.SeedAsync(IServiceProvider services, IConfiguration configuration)` (static) — called once from `Program.cs`; not consumed elsewhere.
- Produces: cookie auth scheme with `LoginPath = "/Admin/Login"`, area-folder authorization convention requiring the `Admin` role for everything under Area `Admin`, with `/Admin/Login` explicitly anonymous — consumed by every page task (3-7) implicitly (they get the gate for free) and verified directly by Task 8.

- [ ] **Step 1: Create `Services/AdminSeeder.cs`**

```csharp
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace EventBoard.Api.Services;

public static class AdminSeeder
{
    public const string AdminRole = "Admin";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var userManager = services.GetRequiredService<UserManager<User>>();

        if (!await roleManager.RoleExistsAsync(AdminRole))
        {
            await roleManager.CreateAsync(new IdentityRole<int>(AdminRole));
        }

        var usersInAdminRole = await userManager.GetUsersInRoleAsync(AdminRole);
        if (usersInAdminRole.Count > 0)
        {
            return;
        }

        var adminEmail = configuration["Admin:Email"];
        var adminPassword = configuration["Admin:Password"];

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        var existingUser = await userManager.FindByEmailAsync(adminEmail);
        if (existingUser is not null)
        {
            await userManager.AddToRoleAsync(existingUser, AdminRole);
            return;
        }

        var adminUser = new User
        {
            UserName = adminEmail,
            Email = adminEmail,
            Name = "Admin",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, AdminRole);
        }
        else
        {
            throw new InvalidOperationException(
                $"Failed to seed admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
}
```

- [ ] **Step 2: Wire Identity, Razor Pages, and the authorization convention into `Program.cs`**

Replace the top of `Program.cs` (from the `using` statements through `var app = builder.Build();`) with:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using EventBoard.Api.Data;
using EventBoard.Api.Models;
using EventBoard.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<User, IdentityRole<int>>(options =>
{
    options.Password.RequireNonAlphanumeric = false;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Admin/Login";
    options.AccessDeniedPath = "/Admin/Login";
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeAreaFolder("Admin", "/", "Admin");
    options.Conventions.AllowAnonymousToAreaPage("Admin", "/Login");
});

builder.Services.AddHttpClient("OpenWeather", client =>
{
    var baseUrl = builder.Configuration["OpenWeather:BaseUrl"] ?? "https://api.openweathermap.org/data/2.5/";
    client.BaseAddress = new Uri(baseUrl);
});

// Allow React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();
```

- [ ] **Step 3: Update the request pipeline and add the seeding call**

Replace from `// Configure the HTTP request pipeline.` through `app.MapControllers();` with:

```csharp
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowReact");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
```

Leave the `app.MapGet("/weatherforecast", ...)` block exactly as-is below this.

- [ ] **Step 4: Add the seeding call before `app.Run()`**

Immediately before the existing `app.Run();` line, add:

```csharp
using (var scope = app.Services.CreateScope())
{
    await AdminSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}

app.Run();
```

(This replaces the bare `app.Run();` line — the seeding block runs once at startup, before the app starts accepting requests.)

- [ ] **Step 5: Add local admin credentials via user secrets**

Run:
```bash
dotnet user-secrets init
dotnet user-secrets set "Admin:Email" "admin@eventboard.local"
dotnet user-secrets set "Admin:Password" "ChangeMe123!"
```
Expected: each command prints `Successfully saved ...` (the first also prints the generated `UserSecretsId`, which gets added to `EventBoard.Api.csproj`).

- [ ] **Step 6: Build and run to confirm the seed executes without error**

Run: `dotnet build`
Expected: `Build succeeded.`

Run: `dotnet run` (let it start, then Ctrl+C to stop)
Expected: no exceptions in console output; app starts listening on `https://localhost:7165`.

- [ ] **Step 7: Commit**

```bash
git add Program.cs Services/AdminSeeder.cs EventBoard.Api.csproj
git commit -m "feat: wire up ASP.NET Core Identity, Razor Pages, and admin bootstrap seeding"
```

---

### Task 3: Shared layout + Login/Logout pages

**Files:**
- Create: `Areas/Admin/Pages/_ViewImports.cshtml`
- Create: `Areas/Admin/Pages/_ViewStart.cshtml`
- Create: `Areas/Admin/Pages/Shared/_Layout.cshtml`
- Create: `Areas/Admin/Pages/Login.cshtml`
- Create: `Areas/Admin/Pages/Login.cshtml.cs`
- Create: `Areas/Admin/Pages/Logout.cshtml`
- Create: `Areas/Admin/Pages/Logout.cshtml.cs`

**Interfaces:**
- Consumes: `SignInManager<User>` (from Identity, registered in Task 2).
- Produces: `_Layout.cshtml` (Bootstrap 5 shell + nav) — consumed by every page in Tasks 4-7 via `_ViewStart.cshtml`.

- [ ] **Step 1: `Areas/Admin/Pages/_ViewImports.cshtml`**

```cshtml
@namespace EventBoard.Api.Areas.Admin.Pages
@using EventBoard.Api.Models
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

- [ ] **Step 2: `Areas/Admin/Pages/_ViewStart.cshtml`**

```cshtml
@{
    Layout = "_Layout";
}
```

- [ ] **Step 3: `Areas/Admin/Pages/Shared/_Layout.cshtml`**

```cshtml
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>@ViewData["Title"] - Event Board Admin</title>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css" rel="stylesheet"
          integrity="sha384-QWTKZyjpPEjISv5WaRU9OFeRpok6YctnYmDr5pNlyT2bRjXh0JMhjY6hW+ALEwIH" crossorigin="anonymous">
</head>
<body>
    <nav class="navbar navbar-expand-lg navbar-dark bg-dark mb-4">
        <div class="container-fluid">
            <a class="navbar-brand" asp-area="Admin" asp-page="/Index">Event Board Admin</a>
            <div class="collapse navbar-collapse">
                <ul class="navbar-nav me-auto">
                    <li class="nav-item"><a class="nav-link" asp-area="Admin" asp-page="/Index">Dashboard</a></li>
                    <li class="nav-item"><a class="nav-link" asp-area="Admin" asp-page="/Events/Index">Events</a></li>
                    <li class="nav-item"><a class="nav-link" asp-area="Admin" asp-page="/Users/Index">Users</a></li>
                    <li class="nav-item"><a class="nav-link" asp-area="Admin" asp-page="/Bookings/Index">Bookings</a></li>
                </ul>
                @if (User.Identity?.IsAuthenticated == true)
                {
                    <form asp-area="Admin" asp-page="/Logout" method="post" class="d-flex">
                        <button type="submit" class="btn btn-outline-light">Log out (@User.Identity.Name)</button>
                    </form>
                }
            </div>
        </div>
    </nav>
    <div class="container">
        @RenderBody()
    </div>
</body>
</html>
```

- [ ] **Step 4: `Areas/Admin/Pages/Login.cshtml.cs`**

```csharp
using System.ComponentModel.DataAnnotations;
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EventBoard.Api.Areas.Admin.Pages;

public class LoginModel : PageModel
{
    private readonly SignInManager<User> _signInManager;

    public LoginModel(SignInManager<User> signInManager)
    {
        _signInManager = signInManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(
            Input.Email, Input.Password, isPersistent: false, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            return RedirectToPage("/Index", new { area = "Admin" });
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return Page();
    }
}
```

- [ ] **Step 5: `Areas/Admin/Pages/Login.cshtml`**

```cshtml
@page
@model EventBoard.Api.Areas.Admin.Pages.LoginModel
@{
    ViewData["Title"] = "Log in";
}

<h1>Log in</h1>

<div class="row">
    <div class="col-md-4">
        <form method="post">
            <div asp-validation-summary="All" class="text-danger"></div>
            <div class="mb-3">
                <label asp-for="Input.Email" class="form-label"></label>
                <input asp-for="Input.Email" class="form-control" />
                <span asp-validation-for="Input.Email" class="text-danger"></span>
            </div>
            <div class="mb-3">
                <label asp-for="Input.Password" class="form-label"></label>
                <input asp-for="Input.Password" type="password" class="form-control" />
                <span asp-validation-for="Input.Password" class="text-danger"></span>
            </div>
            <button type="submit" class="btn btn-primary">Log in</button>
        </form>
    </div>
</div>
```

- [ ] **Step 6: `Areas/Admin/Pages/Logout.cshtml.cs`**

```csharp
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EventBoard.Api.Areas.Admin.Pages;

public class LogoutModel : PageModel
{
    private readonly SignInManager<User> _signInManager;

    public LogoutModel(SignInManager<User> signInManager)
    {
        _signInManager = signInManager;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _signInManager.SignOutAsync();
        return RedirectToPage("/Login", new { area = "Admin" });
    }
}
```

- [ ] **Step 7: `Areas/Admin/Pages/Logout.cshtml`**

```cshtml
@page
@model EventBoard.Api.Areas.Admin.Pages.LogoutModel
```

- [ ] **Step 8: Build**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 9: Manual verification**

Run: `dotnet run`, open `https://localhost:7165/Admin` in a browser.
Expected: redirected to `https://localhost:7165/Admin/Login?ReturnUrl=%2FAdmin`, login form renders with Bootstrap styling. Log in with the `Admin:Email` / `Admin:Password` from Task 2 Step 5 → redirected to `/Admin` (blank/404 page is fine for now, `Index.cshtml` doesn't exist until Task 4 — a 404 *after* successful redirect confirms the login succeeded).

- [ ] **Step 10: Commit**

```bash
git add Areas/Admin/Pages/_ViewImports.cshtml Areas/Admin/Pages/_ViewStart.cshtml Areas/Admin/Pages/Shared/ Areas/Admin/Pages/Login.cshtml Areas/Admin/Pages/Login.cshtml.cs Areas/Admin/Pages/Logout.cshtml Areas/Admin/Pages/Logout.cshtml.cs
git commit -m "feat: add admin login/logout pages and shared Bootstrap layout"
```

---

### Task 4: Dashboard page

**Files:**
- Create: `Areas/Admin/Pages/Index.cshtml`
- Create: `Areas/Admin/Pages/Index.cshtml.cs`

**Interfaces:**
- Consumes: `AppDbContext` (Task 1).

- [ ] **Step 1: `Areas/Admin/Pages/Index.cshtml.cs`**

```csharp
using EventBoard.Api.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventBoard.Api.Areas.Admin.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public int TotalEvents { get; set; }
    public int TotalUsers { get; set; }
    public int TotalBookings { get; set; }

    public async Task OnGetAsync()
    {
        TotalEvents = await _context.Events.CountAsync();
        TotalUsers = await _context.Users.CountAsync();
        TotalBookings = await _context.Bookings.CountAsync();
    }
}
```

- [ ] **Step 2: `Areas/Admin/Pages/Index.cshtml`**

```cshtml
@page
@model EventBoard.Api.Areas.Admin.Pages.IndexModel
@{
    ViewData["Title"] = "Dashboard";
}

<h1>Dashboard</h1>

<div class="row g-3">
    <div class="col-md-4">
        <div class="card text-bg-primary">
            <div class="card-body">
                <h5 class="card-title">Total Events</h5>
                <p class="card-text display-6">@Model.TotalEvents</p>
            </div>
        </div>
    </div>
    <div class="col-md-4">
        <div class="card text-bg-success">
            <div class="card-body">
                <h5 class="card-title">Total Users</h5>
                <p class="card-text display-6">@Model.TotalUsers</p>
            </div>
        </div>
    </div>
    <div class="col-md-4">
        <div class="card text-bg-info">
            <div class="card-body">
                <h5 class="card-title">Total Bookings</h5>
                <p class="card-text display-6">@Model.TotalBookings</p>
            </div>
        </div>
    </div>
</div>
```

- [ ] **Step 3: Manual verification**

Run: `dotnet run`, log in at `/Admin/Login`.
Expected: redirected to `/Admin`, three Bootstrap cards render showing current counts (0s if the database is empty — confirm against `SELECT COUNT(*) FROM Events`, etc. if you want to double check against real data).

- [ ] **Step 4: Commit**

```bash
git add Areas/Admin/Pages/Index.cshtml Areas/Admin/Pages/Index.cshtml.cs
git commit -m "feat: add admin dashboard with placeholder analytics cards"
```

---

### Task 5: Events CRUD pages

**Files:**
- Create: `Areas/Admin/Pages/Events/Index.cshtml`
- Create: `Areas/Admin/Pages/Events/Index.cshtml.cs`
- Create: `Areas/Admin/Pages/Events/Create.cshtml`
- Create: `Areas/Admin/Pages/Events/Create.cshtml.cs`
- Create: `Areas/Admin/Pages/Events/Edit.cshtml`
- Create: `Areas/Admin/Pages/Events/Edit.cshtml.cs`
- Create: `Areas/Admin/Pages/Events/Delete.cshtml`
- Create: `Areas/Admin/Pages/Events/Delete.cshtml.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `EventBoard.Api.Models.Event`, `EventBoard.Api.Models.User` (Task 1).

- [ ] **Step 1: `Areas/Admin/Pages/Events/Index.cshtml.cs`**

```csharp
using EventBoard.Api.Data;
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventBoard.Api.Areas.Admin.Pages.Events;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public IList<Event> Events { get; set; } = new List<Event>();

    public async Task OnGetAsync()
    {
        Events = await _context.Events.Include(e => e.User).OrderBy(e => e.Date).ToListAsync();
    }
}
```

- [ ] **Step 2: `Areas/Admin/Pages/Events/Index.cshtml`**

```cshtml
@page
@model EventBoard.Api.Areas.Admin.Pages.Events.IndexModel
@{
    ViewData["Title"] = "Events";
}

<h1>Events</h1>

<p><a class="btn btn-primary" asp-page="Create">Create New</a></p>

<table class="table table-striped">
    <thead>
        <tr>
            <th>Title</th>
            <th>Date</th>
            <th>Owner</th>
            <th></th>
        </tr>
    </thead>
    <tbody>
    @foreach (var item in Model.Events)
    {
        <tr>
            <td>@item.Title</td>
            <td>@item.Date.ToString("g")</td>
            <td>@item.User?.Name</td>
            <td>
                <a class="btn btn-sm btn-secondary" asp-page="Edit" asp-route-id="@item.Id">Edit</a>
                <a class="btn btn-sm btn-danger" asp-page="Delete" asp-route-id="@item.Id">Delete</a>
            </td>
        </tr>
    }
    </tbody>
</table>
```

- [ ] **Step 3: `Areas/Admin/Pages/Events/Create.cshtml.cs`**

```csharp
using EventBoard.Api.Data;
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EventBoard.Api.Areas.Admin.Pages.Events;

public class CreateModel : PageModel
{
    private readonly AppDbContext _context;

    public CreateModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Event Event { get; set; } = new();

    public SelectList Users { get; set; } = default!;

    public async Task OnGetAsync()
    {
        await PopulateUsersAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ModelState.Remove("Event.User");

        if (!ModelState.IsValid)
        {
            await PopulateUsersAsync();
            return Page();
        }

        _context.Events.Add(Event);
        await _context.SaveChangesAsync();

        return RedirectToPage("Index");
    }

    private async Task PopulateUsersAsync()
    {
        var users = await _context.Users.OrderBy(u => u.Name).ToListAsync();
        Users = new SelectList(users, nameof(User.Id), nameof(User.Name));
    }
}
```

- [ ] **Step 4: `Areas/Admin/Pages/Events/Create.cshtml`**

```cshtml
@page
@model EventBoard.Api.Areas.Admin.Pages.Events.CreateModel
@{
    ViewData["Title"] = "Create Event";
}

<h1>Create Event</h1>

<form method="post">
    <div asp-validation-summary="All" class="text-danger"></div>
    <div class="mb-3">
        <label asp-for="Event.Title" class="form-label"></label>
        <input asp-for="Event.Title" class="form-control" />
        <span asp-validation-for="Event.Title" class="text-danger"></span>
    </div>
    <div class="mb-3">
        <label asp-for="Event.Description" class="form-label"></label>
        <textarea asp-for="Event.Description" class="form-control"></textarea>
    </div>
    <div class="mb-3">
        <label asp-for="Event.Date" class="form-label"></label>
        <input asp-for="Event.Date" class="form-control" />
        <span asp-validation-for="Event.Date" class="text-danger"></span>
    </div>
    <div class="mb-3">
        <label asp-for="Event.UserId" class="form-label">Owner</label>
        <select asp-for="Event.UserId" asp-items="Model.Users" class="form-select"></select>
    </div>
    <button type="submit" class="btn btn-primary">Create</button>
    <a asp-page="Index" class="btn btn-secondary">Cancel</a>
</form>
```

- [ ] **Step 5: `Areas/Admin/Pages/Events/Edit.cshtml.cs`**

```csharp
using EventBoard.Api.Data;
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EventBoard.Api.Areas.Admin.Pages.Events;

public class EditModel : PageModel
{
    private readonly AppDbContext _context;

    public EditModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Event Event { get; set; } = default!;

    public SelectList Users { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var eventItem = await _context.Events.FindAsync(id);
        if (eventItem is null)
        {
            return NotFound();
        }

        Event = eventItem;
        await PopulateUsersAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ModelState.Remove("Event.User");

        if (!ModelState.IsValid)
        {
            await PopulateUsersAsync();
            return Page();
        }

        _context.Attach(Event).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        return RedirectToPage("Index");
    }

    private async Task PopulateUsersAsync()
    {
        var users = await _context.Users.OrderBy(u => u.Name).ToListAsync();
        Users = new SelectList(users, nameof(User.Id), nameof(User.Name));
    }
}
```

- [ ] **Step 6: `Areas/Admin/Pages/Events/Edit.cshtml`**

```cshtml
@page "{id:int}"
@model EventBoard.Api.Areas.Admin.Pages.Events.EditModel
@{
    ViewData["Title"] = "Edit Event";
}

<h1>Edit Event</h1>

<form method="post">
    <input type="hidden" asp-for="Event.Id" />
    <div asp-validation-summary="All" class="text-danger"></div>
    <div class="mb-3">
        <label asp-for="Event.Title" class="form-label"></label>
        <input asp-for="Event.Title" class="form-control" />
        <span asp-validation-for="Event.Title" class="text-danger"></span>
    </div>
    <div class="mb-3">
        <label asp-for="Event.Description" class="form-label"></label>
        <textarea asp-for="Event.Description" class="form-control"></textarea>
    </div>
    <div class="mb-3">
        <label asp-for="Event.Date" class="form-label"></label>
        <input asp-for="Event.Date" class="form-control" />
        <span asp-validation-for="Event.Date" class="text-danger"></span>
    </div>
    <div class="mb-3">
        <label asp-for="Event.UserId" class="form-label">Owner</label>
        <select asp-for="Event.UserId" asp-items="Model.Users" class="form-select"></select>
    </div>
    <button type="submit" class="btn btn-primary">Save</button>
    <a asp-page="Index" class="btn btn-secondary">Cancel</a>
</form>
```

- [ ] **Step 7: `Areas/Admin/Pages/Events/Delete.cshtml.cs`**

```csharp
using EventBoard.Api.Data;
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EventBoard.Api.Areas.Admin.Pages.Events;

public class DeleteModel : PageModel
{
    private readonly AppDbContext _context;

    public DeleteModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Event Event { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var eventItem = await _context.Events.FindAsync(id);
        if (eventItem is null)
        {
            return NotFound();
        }

        Event = eventItem;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var eventItem = await _context.Events.FindAsync(id);
        if (eventItem is not null)
        {
            _context.Events.Remove(eventItem);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage("Index");
    }
}
```

- [ ] **Step 8: `Areas/Admin/Pages/Events/Delete.cshtml`**

```cshtml
@page "{id:int}"
@model EventBoard.Api.Areas.Admin.Pages.Events.DeleteModel
@{
    ViewData["Title"] = "Delete Event";
}

<h1>Delete Event</h1>

<p>Are you sure you want to delete this event?</p>

<dl class="row">
    <dt class="col-sm-2">Title</dt>
    <dd class="col-sm-10">@Model.Event.Title</dd>
    <dt class="col-sm-2">Date</dt>
    <dd class="col-sm-10">@Model.Event.Date.ToString("g")</dd>
</dl>

<form method="post">
    <input type="hidden" asp-for="Event.Id" />
    <button type="submit" class="btn btn-danger">Delete</button>
    <a asp-page="Index" class="btn btn-secondary">Cancel</a>
</form>
```

- [ ] **Step 9: Manual verification**

Run: `dotnet run`, log in, go to `/Admin/Events`.
Expected: list renders (empty table if no events exist). Click "Create New", fill the form (pick a user in the dropdown — if the dropdown is empty, first create a user via the existing `POST /api/Users` endpoint or SQL insert), submit → redirected to the list with the new row visible. Edit it, change the title, save → updated row visible. Delete it → confirmation page, then removed from the list.

- [ ] **Step 10: Commit**

```bash
git add Areas/Admin/Pages/Events/
git commit -m "feat: add admin Events CRUD pages"
```

---

### Task 6: Users pages (list, edit, delete)

**Files:**
- Create: `Areas/Admin/Pages/Users/Index.cshtml`
- Create: `Areas/Admin/Pages/Users/Index.cshtml.cs`
- Create: `Areas/Admin/Pages/Users/Edit.cshtml`
- Create: `Areas/Admin/Pages/Users/Edit.cshtml.cs`
- Create: `Areas/Admin/Pages/Users/Delete.cshtml`
- Create: `Areas/Admin/Pages/Users/Delete.cshtml.cs`

**Interfaces:**
- Consumes: `UserManager<User>`, `RoleManager<IdentityRole<int>>` (from Identity, registered in Task 2), `AppDbContext` (Task 1).

- [ ] **Step 1: `Areas/Admin/Pages/Users/Index.cshtml.cs`**

```csharp
using EventBoard.Api.Data;
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventBoard.Api.Areas.Admin.Pages.Users;

public class UserRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Roles { get; set; } = string.Empty;
}

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly UserManager<User> _userManager;

    public IndexModel(AppDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public List<UserRow> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        var users = await _context.Users.OrderBy(u => u.Name).ToListAsync();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            Rows.Add(new UserRow
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email ?? string.Empty,
                Roles = string.Join(", ", roles)
            });
        }
    }
}
```

- [ ] **Step 2: `Areas/Admin/Pages/Users/Index.cshtml`**

```cshtml
@page
@model EventBoard.Api.Areas.Admin.Pages.Users.IndexModel
@{
    ViewData["Title"] = "Users";
}

<h1>Users</h1>

<table class="table table-striped">
    <thead>
        <tr>
            <th>Name</th>
            <th>Email</th>
            <th>Roles</th>
            <th></th>
        </tr>
    </thead>
    <tbody>
    @foreach (var item in Model.Rows)
    {
        <tr>
            <td>@item.Name</td>
            <td>@item.Email</td>
            <td>@item.Roles</td>
            <td>
                <a class="btn btn-sm btn-secondary" asp-page="Edit" asp-route-id="@item.Id">Edit</a>
                <a class="btn btn-sm btn-danger" asp-page="Delete" asp-route-id="@item.Id">Delete</a>
            </td>
        </tr>
    }
    </tbody>
</table>
```

- [ ] **Step 3: `Areas/Admin/Pages/Users/Edit.cshtml.cs`**

```csharp
using System.ComponentModel.DataAnnotations;
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EventBoard.Api.Areas.Admin.Pages.Users;

public class EditModel : PageModel
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;

    public EditModel(UserManager<User> userManager, RoleManager<IdentityRole<int>> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public SelectList Roles { get; set; } = default!;

    public class InputModel
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);

        Input = new InputModel
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email ?? string.Empty,
            Role = roles.FirstOrDefault() ?? string.Empty
        };

        await PopulateRolesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await PopulateRolesAsync();
            return Page();
        }

        var user = await _userManager.FindByIdAsync(Input.Id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        user.Name = Input.Name;
        user.Email = Input.Email;
        user.UserName = Input.Email;
        await _userManager.UpdateAsync(user);

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }
        await _userManager.AddToRoleAsync(user, Input.Role);

        return RedirectToPage("Index");
    }

    private async Task PopulateRolesAsync()
    {
        var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
        Roles = new SelectList(roles);
    }
}
```

- [ ] **Step 4: `Areas/Admin/Pages/Users/Edit.cshtml`**

```cshtml
@page "{id:int}"
@model EventBoard.Api.Areas.Admin.Pages.Users.EditModel
@{
    ViewData["Title"] = "Edit User";
}

<h1>Edit User</h1>

<form method="post">
    <input type="hidden" asp-for="Input.Id" />
    <div asp-validation-summary="All" class="text-danger"></div>
    <div class="mb-3">
        <label asp-for="Input.Name" class="form-label"></label>
        <input asp-for="Input.Name" class="form-control" />
        <span asp-validation-for="Input.Name" class="text-danger"></span>
    </div>
    <div class="mb-3">
        <label asp-for="Input.Email" class="form-label"></label>
        <input asp-for="Input.Email" class="form-control" />
        <span asp-validation-for="Input.Email" class="text-danger"></span>
    </div>
    <div class="mb-3">
        <label asp-for="Input.Role" class="form-label"></label>
        <select asp-for="Input.Role" asp-items="Model.Roles" class="form-select"></select>
    </div>
    <button type="submit" class="btn btn-primary">Save</button>
    <a asp-page="Index" class="btn btn-secondary">Cancel</a>
</form>
```

- [ ] **Step 5: `Areas/Admin/Pages/Users/Delete.cshtml.cs`**

```csharp
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EventBoard.Api.Areas.Admin.Pages.Users;

public class DeleteModel : PageModel
{
    private readonly UserManager<User> _userManager;

    public DeleteModel(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public User TargetUser { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        TargetUser = user;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is not null)
        {
            await _userManager.DeleteAsync(user);
        }

        return RedirectToPage("Index");
    }
}
```

- [ ] **Step 6: `Areas/Admin/Pages/Users/Delete.cshtml`**

```cshtml
@page "{id:int}"
@model EventBoard.Api.Areas.Admin.Pages.Users.DeleteModel
@{
    ViewData["Title"] = "Delete User";
}

<h1>Delete User</h1>

<p>Are you sure you want to delete this user?</p>

<dl class="row">
    <dt class="col-sm-2">Name</dt>
    <dd class="col-sm-10">@Model.TargetUser.Name</dd>
    <dt class="col-sm-2">Email</dt>
    <dd class="col-sm-10">@Model.TargetUser.Email</dd>
</dl>

<form method="post">
    <button type="submit" class="btn btn-danger">Delete</button>
    <a asp-page="Index" class="btn btn-secondary">Cancel</a>
</form>
```

Note: deleting a user who has Bookings will throw `DbUpdateException` (the `Booking -> User` FK is `Restrict`, by design per the spec). This is accepted as-is for this minimal scope — not handled with a friendly error message.

- [ ] **Step 7: Manual verification**

Run: `dotnet run`, log in, go to `/Admin/Users`.
Expected: list shows all users (including the seeded admin) with their roles. Edit a non-admin user's name/email/role, save → change reflected in the list. Delete a user with no events/bookings → removed from the list.

- [ ] **Step 8: Commit**

```bash
git add Areas/Admin/Pages/Users/
git commit -m "feat: add admin Users list/edit/delete pages"
```

---

### Task 7: Bookings CRUD pages

**Files:**
- Create: `Areas/Admin/Pages/Bookings/Index.cshtml`
- Create: `Areas/Admin/Pages/Bookings/Index.cshtml.cs`
- Create: `Areas/Admin/Pages/Bookings/Create.cshtml`
- Create: `Areas/Admin/Pages/Bookings/Create.cshtml.cs`
- Create: `Areas/Admin/Pages/Bookings/Edit.cshtml`
- Create: `Areas/Admin/Pages/Bookings/Edit.cshtml.cs`
- Create: `Areas/Admin/Pages/Bookings/Delete.cshtml`
- Create: `Areas/Admin/Pages/Bookings/Delete.cshtml.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `EventBoard.Api.Models.Booking`, `BookingStatus`, `Event`, `User` (Task 1).

- [ ] **Step 1: `Areas/Admin/Pages/Bookings/Index.cshtml.cs`**

```csharp
using EventBoard.Api.Data;
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventBoard.Api.Areas.Admin.Pages.Bookings;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public IList<Booking> Bookings { get; set; } = new List<Booking>();

    public async Task OnGetAsync()
    {
        Bookings = await _context.Bookings
            .Include(b => b.Event)
            .Include(b => b.User)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }
}
```

- [ ] **Step 2: `Areas/Admin/Pages/Bookings/Index.cshtml`**

```cshtml
@page
@model EventBoard.Api.Areas.Admin.Pages.Bookings.IndexModel
@{
    ViewData["Title"] = "Bookings";
}

<h1>Bookings</h1>

<p><a class="btn btn-primary" asp-page="Create">Create New</a></p>

<table class="table table-striped">
    <thead>
        <tr>
            <th>Event</th>
            <th>User</th>
            <th>Status</th>
            <th>Created</th>
            <th></th>
        </tr>
    </thead>
    <tbody>
    @foreach (var item in Model.Bookings)
    {
        <tr>
            <td>@item.Event?.Title</td>
            <td>@item.User?.Name</td>
            <td>@item.Status</td>
            <td>@item.CreatedAt.ToString("g")</td>
            <td>
                <a class="btn btn-sm btn-secondary" asp-page="Edit" asp-route-id="@item.Id">Edit</a>
                <a class="btn btn-sm btn-danger" asp-page="Delete" asp-route-id="@item.Id">Delete</a>
            </td>
        </tr>
    }
    </tbody>
</table>
```

- [ ] **Step 3: `Areas/Admin/Pages/Bookings/Create.cshtml.cs`**

```csharp
using EventBoard.Api.Data;
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EventBoard.Api.Areas.Admin.Pages.Bookings;

public class CreateModel : PageModel
{
    private readonly AppDbContext _context;

    public CreateModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Booking Booking { get; set; } = new();

    public SelectList Events { get; set; } = default!;
    public SelectList Users { get; set; } = default!;
    public SelectList Statuses { get; set; } = default!;

    public async Task OnGetAsync()
    {
        await PopulateDropdownsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ModelState.Remove("Booking.Event");
        ModelState.Remove("Booking.User");

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return Page();
        }

        Booking.CreatedAt = DateTime.UtcNow;
        _context.Bookings.Add(Booking);
        await _context.SaveChangesAsync();

        return RedirectToPage("Index");
    }

    private async Task PopulateDropdownsAsync()
    {
        var events = await _context.Events.OrderBy(e => e.Title).ToListAsync();
        var users = await _context.Users.OrderBy(u => u.Name).ToListAsync();

        Events = new SelectList(events, nameof(Event.Id), nameof(Event.Title));
        Users = new SelectList(users, nameof(User.Id), nameof(User.Name));
        Statuses = new SelectList(Enum.GetValues<BookingStatus>());
    }
}
```

- [ ] **Step 4: `Areas/Admin/Pages/Bookings/Create.cshtml`**

```cshtml
@page
@model EventBoard.Api.Areas.Admin.Pages.Bookings.CreateModel
@{
    ViewData["Title"] = "Create Booking";
}

<h1>Create Booking</h1>

<form method="post">
    <div asp-validation-summary="All" class="text-danger"></div>
    <div class="mb-3">
        <label asp-for="Booking.EventId" class="form-label">Event</label>
        <select asp-for="Booking.EventId" asp-items="Model.Events" class="form-select"></select>
    </div>
    <div class="mb-3">
        <label asp-for="Booking.UserId" class="form-label">User</label>
        <select asp-for="Booking.UserId" asp-items="Model.Users" class="form-select"></select>
    </div>
    <div class="mb-3">
        <label asp-for="Booking.Status" class="form-label"></label>
        <select asp-for="Booking.Status" asp-items="Model.Statuses" class="form-select"></select>
    </div>
    <button type="submit" class="btn btn-primary">Create</button>
    <a asp-page="Index" class="btn btn-secondary">Cancel</a>
</form>
```

- [ ] **Step 5: `Areas/Admin/Pages/Bookings/Edit.cshtml.cs`**

```csharp
using EventBoard.Api.Data;
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EventBoard.Api.Areas.Admin.Pages.Bookings;

public class EditModel : PageModel
{
    private readonly AppDbContext _context;

    public EditModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Booking Booking { get; set; } = default!;

    public SelectList Events { get; set; } = default!;
    public SelectList Users { get; set; } = default!;
    public SelectList Statuses { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var booking = await _context.Bookings.FindAsync(id);
        if (booking is null)
        {
            return NotFound();
        }

        Booking = booking;
        await PopulateDropdownsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ModelState.Remove("Booking.Event");
        ModelState.Remove("Booking.User");

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return Page();
        }

        _context.Attach(Booking).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        return RedirectToPage("Index");
    }

    private async Task PopulateDropdownsAsync()
    {
        var events = await _context.Events.OrderBy(e => e.Title).ToListAsync();
        var users = await _context.Users.OrderBy(u => u.Name).ToListAsync();

        Events = new SelectList(events, nameof(Event.Id), nameof(Event.Title), Booking.EventId);
        Users = new SelectList(users, nameof(User.Id), nameof(User.Name), Booking.UserId);
        Statuses = new SelectList(Enum.GetValues<BookingStatus>(), Booking.Status);
    }
}
```

- [ ] **Step 6: `Areas/Admin/Pages/Bookings/Edit.cshtml`**

```cshtml
@page "{id:int}"
@model EventBoard.Api.Areas.Admin.Pages.Bookings.EditModel
@{
    ViewData["Title"] = "Edit Booking";
}

<h1>Edit Booking</h1>

<form method="post">
    <input type="hidden" asp-for="Booking.Id" />
    <input type="hidden" asp-for="Booking.CreatedAt" />
    <div asp-validation-summary="All" class="text-danger"></div>
    <div class="mb-3">
        <label asp-for="Booking.EventId" class="form-label">Event</label>
        <select asp-for="Booking.EventId" asp-items="Model.Events" class="form-select"></select>
    </div>
    <div class="mb-3">
        <label asp-for="Booking.UserId" class="form-label">User</label>
        <select asp-for="Booking.UserId" asp-items="Model.Users" class="form-select"></select>
    </div>
    <div class="mb-3">
        <label asp-for="Booking.Status" class="form-label"></label>
        <select asp-for="Booking.Status" asp-items="Model.Statuses" class="form-select"></select>
    </div>
    <button type="submit" class="btn btn-primary">Save</button>
    <a asp-page="Index" class="btn btn-secondary">Cancel</a>
</form>
```

- [ ] **Step 7: `Areas/Admin/Pages/Bookings/Delete.cshtml.cs`**

```csharp
using EventBoard.Api.Data;
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventBoard.Api.Areas.Admin.Pages.Bookings;

public class DeleteModel : PageModel
{
    private readonly AppDbContext _context;

    public DeleteModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Booking Booking { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var booking = await _context.Bookings
            .Include(b => b.Event)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking is null)
        {
            return NotFound();
        }

        Booking = booking;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var booking = await _context.Bookings.FindAsync(id);
        if (booking is not null)
        {
            _context.Bookings.Remove(booking);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage("Index");
    }
}
```

- [ ] **Step 8: `Areas/Admin/Pages/Bookings/Delete.cshtml`**

```cshtml
@page "{id:int}"
@model EventBoard.Api.Areas.Admin.Pages.Bookings.DeleteModel
@{
    ViewData["Title"] = "Delete Booking";
}

<h1>Delete Booking</h1>

<p>Are you sure you want to delete this booking?</p>

<dl class="row">
    <dt class="col-sm-2">Event</dt>
    <dd class="col-sm-10">@Model.Booking.Event?.Title</dd>
    <dt class="col-sm-2">User</dt>
    <dd class="col-sm-10">@Model.Booking.User?.Name</dd>
    <dt class="col-sm-2">Status</dt>
    <dd class="col-sm-10">@Model.Booking.Status</dd>
</dl>

<form method="post">
    <input type="hidden" asp-for="Booking.Id" />
    <button type="submit" class="btn btn-danger">Delete</button>
    <a asp-page="Index" class="btn btn-secondary">Cancel</a>
</form>
```

- [ ] **Step 9: Manual verification**

Run: `dotnet run`, log in, go to `/Admin/Bookings`.
Expected: list renders (empty if none exist). Create a booking picking an existing Event and User → appears in the list with status "Pending". Edit it, change Status to "Confirmed", save → updated. Delete it → removed.

- [ ] **Step 10: Commit**

```bash
git add Areas/Admin/Pages/Bookings/
git commit -m "feat: add admin Bookings CRUD pages"
```

---

### Task 8: Role-gate integration test

**Files:**
- Modify: `Program.cs`
- Create: `EventBoard.Api.Tests/EventBoard.Api.Tests.csproj`
- Create: `EventBoard.Api.Tests/AdminAreaAuthTests.cs`

**Interfaces:**
- Consumes: `Program` (made public below), `AppDbContext` (Task 1).

- [ ] **Step 1: Expose `Program` for `WebApplicationFactory<Program>`**

At the very end of `Program.cs` (after the `app.Run();` / seeding block and the existing `record WeatherForecast(...)` declaration), add:

```csharp
public partial class Program { }
```

(Top-level statement programs generate an internal `Program` class by default; `WebApplicationFactory<T>` needs it to be accessible from the separate test assembly.)

- [ ] **Step 2: Create `EventBoard.Api.Tests/EventBoard.Api.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.11" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\EventBoard.Api.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Write the failing test — `EventBoard.Api.Tests/AdminAreaAuthTests.cs`**

```csharp
using System.Net;
using EventBoard.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace EventBoard.Api.Tests;

public class AdminAreaAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AdminAreaAuthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase("AdminAreaAuthTests"));
            });
        });
    }

    [Fact]
    public async Task AnonymousRequestToAdminDashboard_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Admin/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task LoginPage_IsReachableAnonymously()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Admin/Login");

        response.EnsureSuccessStatusCode();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test EventBoard.Api.Tests/EventBoard.Api.Tests.csproj`
Expected: `Passed! - Failed: 0, Passed: 2, Skipped: 0`

If `AnonymousRequestToAdminDashboard_RedirectsToLogin` fails with a 200 or 404 instead of a redirect, the area authorization convention in `Program.cs` (Task 2, Step 2) isn't wired correctly — check `AuthorizeAreaFolder("Admin", "/", "Admin")` is present.

- [ ] **Step 5: Commit**

```bash
git add Program.cs EventBoard.Api.Tests/
git commit -m "test: add integration test verifying the admin area role gate"
```

---

## Post-plan manual smoke test (whole feature)

1. `dotnet ef database update` (if not already applied).
2. `dotnet user-secrets list` — confirm `Admin:Email`/`Admin:Password` are set.
3. `dotnet run`, browse to `https://localhost:7165/Admin` → redirected to Login.
4. Log in with the seeded admin → Dashboard shows counts.
5. Walk through Events/Users/Bookings create → edit → delete for each.
6. Log out → confirm redirected to Login and `/Admin` is blocked again.
7. `dotnet test EventBoard.Api.Tests/EventBoard.Api.Tests.csproj` → both tests pass.
