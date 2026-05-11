using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Data.Seeding;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Security;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using FundRaisingAssignment.Application.Interfaces;
using OfficeOpenXml;

// ─────────────────────────────────────────────────────────────────────────────
// User Story:   Cross-cutting (composition root)            Owner: Team
// BCE Role:     Configuration / wiring
// Description:  Application entry point. Wires DI registrations, Identity,
//               authorization policies, MVC + Razor Pages, the HTTP pipeline,
//               role seeding, and the optional dataset seeder.
// Notes:        Per-registration Format-B annotations identify which user
//               story each binding supports. Pending stories (FR04, PM05,
//               and the standalone PM06 leaderboard page) are flagged in
//               the regions at the bottom of this file.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// ── Database ─────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
if (connectionString.Contains("__REPLACE_ME__"))
    throw new InvalidOperationException("DefaultConnection is still using the placeholder. Set it via environment variables.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));                          // Cross-cutting — entity persistence
builder.Services.AddDatabaseDeveloperPageExceptionFilter();        // Cross-cutting — dev diagnostics

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>    // UA01 — user accounts
    options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<ApplicationRole>()                                   // UA01 — role assignment
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName)); // FR03 — email settings

var emailSettings = builder.Configuration.GetSection(EmailSettings.SectionName).Get<EmailSettings>();

if (emailSettings != null && !string.IsNullOrEmpty(emailSettings.ApiKey) && !string.IsNullOrEmpty(emailSettings.ApiSecret))
{
    builder.Services.AddTransient<IEmailService, MailjetEmailService>();           // FR03 — Send Thank-You Message (Nicholas)
    builder.Services.AddTransient<IEmailSender>(sp => sp.GetRequiredService<IEmailService>()); // FR03 — Identity adapter for the same transport
}

// ── Authorization ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthorizationHandler, MinimumJoinTimeHandler>();   // Cross-cutting — auth policy handler
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireThreeDaysJoined",
        policy => policy.Requirements.Add(new MinimumJoinTimeRequirement(TimeSpan.FromSeconds(10)))); // Cross-cutting — guard policy

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddScoped<ICampaignService, CampaignService>();   // DN01, DN03, FR01, PM01, PM06 — backbone service
builder.Services.AddScoped<DashboardService>();   // PM05 — Platform Analytics Dashboard
builder.Services.AddScoped<AnalyticsService>();   // PM05 — Platform Analytics Dashboard


// ── MVC / Razor ────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();   // DN03 — Web API (DonationsController)
builder.Services.AddHttpClient();             // FR03 — Mailjet HTTP client
builder.Services.AddRazorPages();             // Cross-cutting — Razor Pages host

// ── EPPlus license (UA02) ─────────────────────────────────────────────────────
ExcelPackage.License.SetNonCommercialPersonal("Karthik");          // UA02 — Excel exports (Karthik)

var app = builder.Build();

// ── HTTP pipeline ─────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
    app.UseMigrationsEndPoint();
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.MapRazorPages()
   .WithStaticAssets();

// ── Database initialisation & role seeding ────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    dbContext.Database.Migrate();

    // Seed all defined roles
    foreach (var role in ApplicationRole.All)
    {
        if (role.Name != null && !await roleManager.RoleExistsAsync(role.Name))
            await roleManager.CreateAsync(new ApplicationRole(role.Name));
    }

    // Assign Admin role to the admin seed account (change email in appsettings if needed)
    var adminEmail = builder.Configuration["AdminSeedEmail"] ?? "admin@example.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser is not null && !await userManager.IsInRoleAsync(adminUser, ApplicationRole.Names.Admin))
        await userManager.AddToRoleAsync(adminUser, ApplicationRole.Names.Admin);

    // Assign PlatformManager role to the seed account (change email in appsettings if needed)
    var pmEmail = builder.Configuration["PlatformManagerSeedEmail"] ?? "manager@example.com";
    var pmUser = await userManager.FindByEmailAsync(pmEmail);
    if (pmUser is not null && !await userManager.IsInRoleAsync(pmUser, "PlatformManager"))
        await userManager.AddToRoleAsync(pmUser, "PlatformManager");


    // ── Optional one-shot dataset seed (run with `dotnet run -- --seed`) ──────
    if (args.Contains("--seed"))
    {
        var force = args.Contains("--force");
        var donationCount = ParseIntArg(args, "--donations") ?? 1000;
        var campaignCount = ParseIntArg(args, "--campaigns") ?? 100;
        var donorCount = ParseIntArg(args, "--donors") ?? 50;
        var ownerCount = ParseIntArg(args, "--owners") ?? 20;

        var existing = await dbContext.Donations.CountAsync();
        if (existing > 0 && !force)
        {
            Console.WriteLine($"[seed] Skipped: {existing} donations already exist. Pass --force to seed anyway.");
        }
        else
        {
            Console.WriteLine($"[seed] Seeding {donationCount} donations across {campaignCount} campaigns ({donorCount} donors, {ownerCount} owners)...");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await DataSeeder.SeedLargeDatasetAsync(
                dbContext,
                ownerCount: ownerCount,
                donorCount: donorCount,
                campaignCount: campaignCount,
                donationCount: donationCount);
            sw.Stop();
            Console.WriteLine($"[seed] Done in {sw.ElapsedMilliseconds} ms. " +
                              $"Owners={result.OwnerIds.Count}, Donors={result.DonorIds.Count}, " +
                              $"Campaigns={result.CampaignIds.Count}, Donations={result.DonationIds.Count}, " +
                              $"Refunds={result.RefundedCount}.");
        }
        return;
    }
}

app.Run();

static int? ParseIntArg(string[] args, string name)
{
    var idx = Array.IndexOf(args, name);
    if (idx < 0 || idx + 1 >= args.Length) return null;
    return int.TryParse(args[idx + 1], out var v) ? v : null;
}

// ─────────────────────────────────────────────────────────────────────────────
// Gap analysis — user stories with no implementation in the current codebase.
// Surfacing them here so the Final Report's traceability matrix has a single
// authoritative source for "not done" state. Update or remove the regions
// below as work lands.
// ─────────────────────────────────────────────────────────────────────────────

#region FR04 (Pending — owner: Yong Jun Jie)
// User Story:   FR04 – Campaign Access Delegation
// Owner:        Yong Jun Jie (JJ)
// Status:       Pending — no Delegate*/CampaignAccess* files exist.
//               No DI registrations or entities for delegated campaign
//               ownership / co-fundraiser permissions.
#endregion

#region PM05 (Pending — owner: Khoo Si Kai)
// User Story:   PM05 – View Platform Analytics Dashboard
// Owner:        Khoo Si Kai
// Status:       Pending — no analytics dashboard page. The Reports area
//               (UA02 / Karthik) covers ad-hoc admin reporting with charts,
//               but there is no always-on dashboard surface.
#endregion

#region PM06 (Partial — owner: Ho Dan Ze)
// User Story:   PM06 – View Top Donors Leaderboard
// Owner:        Ho Dan Ze
// Status:       Partial — leaderboard data is exposed via
//               ICampaignService.GetTopDonationsAsync and surfaced as an
//               inline tab on Areas/Dashboard/Pages/CampaignPage.cshtml.
//               There is no standalone /Leaderboard page or PM-wide view.
#endregion
