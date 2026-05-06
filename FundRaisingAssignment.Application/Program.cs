using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Security;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

// ── Authorization ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthorizationHandler, MinimumJoinTimeHandler>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireThreeDaysJoined",
        policy => policy.Requirements.Add(new MinimumJoinTimeRequirement(3)));

// ── Application services ──────────────────────────────────────────────────────
// ICampaignService lives in Services namespace; CampaignService is the DB-backed implementation.
builder.Services.AddScoped<ICampaignService, CampaignService>();

builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ── HTTP pipeline ─────────────────────────────────────────────────────────────
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
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages()
   .WithStaticAssets();

// ── Database initialisation ───────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    foreach (var role in ApplicationRole.All)
    {
        if (role.Name != null && !await roleManager.RoleExistsAsync(role.Name))
            await roleManager.CreateAsync(new ApplicationRole(role.Name));
    }
}

app.Run();
