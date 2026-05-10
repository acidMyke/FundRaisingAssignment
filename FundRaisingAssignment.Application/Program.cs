using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Security;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using FundRaisingAssignment.Application.Interfaces;
using OfficeOpenXml;

var builder = WebApplication.CreateBuilder(args);

// ── Database ─────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
if (connectionString.Contains("__REPLACE_ME__"))
    throw new InvalidOperationException("DefaultConnection is still using the placeholder. Set it via environment variables.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));

var emailSettings = builder.Configuration.GetSection(EmailSettings.SectionName).Get<EmailSettings>();

if (emailSettings != null && !string.IsNullOrEmpty(emailSettings.ApiKey) && !string.IsNullOrEmpty(emailSettings.ApiSecret))
{
    builder.Services.AddTransient<IEmailService, MailjetEmailService>();
    builder.Services.AddTransient<IEmailSender>(sp => sp.GetRequiredService<IEmailService>());
}
else
{
    builder.Services.AddTransient<IEmailService, LoggerEmailService>();
    builder.Services.AddTransient<IEmailSender>(sp => sp.GetRequiredService<IEmailService>());
}

// ── Authorization ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthorizationHandler, MinimumJoinTimeHandler>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireThreeDaysJoined",
        policy => policy.Requirements.Add(new MinimumJoinTimeRequirement(TimeSpan.FromSeconds(10))));

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddScoped<ICampaignService, CampaignService>();   // Josh's interface
builder.Services.AddScoped<DonationService>();                     // Karthik's donation service

// ── MVC / Razor ────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddRazorPages();

// ── EPPlus license (Karthik) ──────────────────────────────────────────────────
ExcelPackage.License.SetNonCommercialPersonal("Karthik");

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
}

app.Run();
