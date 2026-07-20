# Admin Area Design

Date: 2026-07-15

## Goal

Add a Razor Pages admin area under `/Admin` to EventBoard.Api, gated to the `Admin`
role, for managing Events, Users, and Bookings, plus a dashboard with placeholder
analytics cards. Bootstrap 5 for styling. Local SQL Server via EF Core (existing
`DefaultConnection` LocalDB connection string).

## Current state (before this work)

- ASP.NET Core 8 Web API, no Razor Pages, no auth of any kind.
- `Models/User.cs`: `Id (int)`, `Name`, `Email`. Referenced by `Event.UserId`.
- `Models/Event.cs`: `Id`, `Title`, `Description`, `Date`, `UserId` (FK).
- No `Booking` model.
- `AppDbContext : DbContext` with `Users`, `Events` sets.
- `UsersController` / `EventsController` do raw CRUD via `AppDbContext` directly
  (no repository pattern for Events, a `UserRepository` exists but is unused by
  `UsersController`).
- React frontend consumes this API separately (CORS policy `AllowReact` ->
  `http://localhost:5173`); out of scope for this change.

## Packages to add

- `Microsoft.AspNetCore.Identity.EntityFrameworkCore`

(No Identity.UI scaffolding — all admin pages are hand-written, not the
scaffolded default UI.)

## Data model changes

- `User` becomes `public class User : IdentityUser<int>`.
  - Keep custom `Name` property.
  - Remove the hand-rolled `Email` property/`[EmailAddress]` attribute — Identity's
    base class already supplies `Email`/`UserName`/`NormalizedEmail` etc.
  - Keep `Events` navigation collection.
- New `Booking` model:
  ```csharp
  public class Booking
  {
      public int Id { get; set; }
      public int EventId { get; set; }
      public Event? Event { get; set; }
      public int UserId { get; set; }
      public User? User { get; set; }
      public DateTime CreatedAt { get; set; }
      public BookingStatus Status { get; set; }
  }

  public enum BookingStatus { Pending, Confirmed, Cancelled }
  ```
- `AppDbContext` changes from `DbContext` to
  `IdentityDbContext<User, IdentityRole<int>, int>`. Adds `DbSet<Booking> Bookings`.
  Existing `OnModelCreating` FK config for `User -> Events` stays; add FK config
  for `Booking -> Event` and `Booking -> User` (both `Restrict` delete behavior,
  since cascading through two FKs to the same tables would otherwise conflict).
- One new EF Core migration covering: Identity tables (`AspNetUsers`,
  `AspNetRoles`, `AspNetUserRoles`, etc.), the `User` table column changes, and
  the new `Bookings` table.

## Impact on existing API

`UsersController.PostUser`/`PutUser` continue to do raw
`_context.Users.Add/Update` — unchanged. Rows created this way will have empty
Identity columns (no password hash, no normalized fields), which is fine: that
API doesn't authenticate anyone today, and this change doesn't add authentication
to it. `EventsController` is unaffected (`Event` model unchanged).

## Auth wiring (Program.cs)

- `builder.Services.AddIdentity<User, IdentityRole<int>>(options => { /* default
  password/lockout options */ }).AddEntityFrameworkStores<AppDbContext>()`.
- Cookie auth (Identity's default scheme) — no JWT, this is server-rendered
  Razor Pages only, separate from the existing API's (currently absent) auth.
- `builder.Services.AddRazorPages()`; `app.UseAuthentication()` before
  `app.UseAuthorization()`; `app.MapRazorPages()`.
- Authorization convention: everything under Areas/Admin requires
  `Roles = "Admin"`, configured once via
  `options.Conventions.AuthorizeAreaFolder("Admin", "/", "Admin")` combined with
  `AllowAnonymousToPage` on the Login page specifically — not per-page
  `[Authorize]` attributes scattered across files.

### Admin bootstrap (startup seeding)

After `app.Build()` (or a small `IHostedService`), in a scope:
- Read `Admin:Email` / `Admin:Password` from configuration (local dev: user
  secrets; never committed).
- If the `Admin` role doesn't exist, create it via `RoleManager<IdentityRole<int>>`.
- If no user is currently in the `Admin` role, create the configured user via
  `UserManager<User>.CreateAsync` and assign the role.
- Idempotent — safe to run on every startup.

## Admin area structure

`Areas/Admin/Pages/`, shared layout `_Layout.cshtml` pulling Bootstrap 5 from
CDN, plus a small `_ViewStart.cshtml` pointing to it.

- `Login.cshtml` / `Login.cshtml.cs` — the one page `AllowAnonymous`. Email +
  password form, `SignInManager<User>.PasswordSignInAsync`. Redirects to
  `/Admin` on success.
- `Logout.cshtml.cs` (POST handler only, no view) — `SignInManager.SignOutAsync`,
  redirect to Login.
- `Index.cshtml` — dashboard. Placeholder analytics cards: Total Events, Total
  Users, Total Bookings, each a simple Bootstrap card with a live `COUNT` query
  against `AppDbContext` (real numbers, no charts/trends/date-range filtering —
  that's the "placeholder" part).
- `Events/Index.cshtml`, `Events/Create.cshtml`, `Events/Edit.cshtml`,
  `Events/Delete.cshtml` — standard Razor Pages CRUD against `AppDbContext`,
  Bootstrap table + forms.
- `Users/Index.cshtml` (list: name, email, role), `Users/Edit.cshtml` (name,
  email, role dropdown sourced from `AspNetRoles`), `Users/Delete.cshtml`. No
  `Create` page — accounts are created via the startup seed or (future, out of
  scope) self-registration elsewhere.
- `Bookings/Index.cshtml`, `Bookings/Create.cshtml`, `Bookings/Edit.cshtml`,
  `Bookings/Delete.cshtml` — Event and User pickers as `<select>` dropdowns,
  Status as a dropdown over the `BookingStatus` enum.

## Explicitly out of scope

- Changes to the existing API controllers' auth (they remain unauthenticated).
- React frontend changes.
- Password reset, email confirmation, 2FA.
- Pagination/search/filtering on admin list pages (small data volumes assumed).
- Real analytics/charts on the dashboard — placeholder counts only.
