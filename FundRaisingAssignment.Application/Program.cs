using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Security;

// ✅ ADD THESE (B-C-E integration)
using FundRaisingAssignment.Application.Services;   // CampaignService
//using FundRaisingAssignment.Application.Repositories; // CampaignRepository

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------
// DATABASE CONFIG
// -----------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

if (connectionString.Contains("__REPLACE_ME__"))
{
    throw new InvalidOperationException("DefaultConnection is still using the placeholder value. Set it via environment variables.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Service (Control Layer)
builder.Services.AddScoped<CampaignService>();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// -----------------------------
// IDENTITY CONFIG
// -----------------------------
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// -----------------------------
// 🔑 B-C-E LAYER REGISTRATION
// -----------------------------

// Repository (Data Layer)
//builder.Services.AddScoped<CampaignRepository>();

// -----------------------------
// AUTHORIZATION
// -----------------------------
builder.Services.AddScoped<IAuthorizationHandler, MinimumJoinTimeHandler>();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireThreeDaysJoined", policy =>
        policy.Requirements.Add(new MinimumJoinTimeRequirement(3)));

// -----------------------------
// MVC / RAZOR
// -----------------------------
builder.Services.AddControllersWithViews();

// -----------------------------
// BUILD APP
// -----------------------------
var app = builder.Build();

// -----------------------------
// MIDDLEWARE PIPELINE
// -----------------------------
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// ✅ IMPORTANT (missing in your original)
app.UseAuthentication();
app.UseAuthorization();

// -----------------------------
// ROUTING
// -----------------------------
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages()
   .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    dbContext.Database.Migrate();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    foreach (var role in ApplicationRole.All)
    {
        if (role.Name != null && !await roleManager.RoleExistsAsync(role.Name))
        {
            await roleManager.CreateAsync(new ApplicationRole(role.Name));
        }
    }
}

// -----------------------------
app.Run();