using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TecNM.Residency.Activities;
using TecNM.Residency.Admin;
using TecNM.Residency.Advisors;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Common.Notifications;
using TecNM.Residency.Common.Settings;
using TecNM.Residency.Companies;
using TecNM.Residency.Documents;
using TecNM.Residency.Evaluations;
using TecNM.Residency.Projects;
using TecNM.Residency.Searches;
using TecNM.Residency.Students;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IAdvisorRepository, AdvisorRepository>();
builder.Services.AddScoped<IAdvisorService, AdvisorService>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IActivityRepository, ActivityRepository>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IEvaluationRepository, EvaluationRepository>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();
builder.Services.AddScoped<IDashboardMetricsService, DashboardMetricsService>();
builder.Services.AddScoped<IReportGeneratorService, ReportGeneratorService>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddSingleton<SearchRegistry>();
builder.Services.AddScoped<ISearchService, SearchService>();

// Email Notification Services
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.AddSingleton<IEmailQueue, EmailQueue>();
builder.Services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<ISystemSettingService, SystemSettingService>();
builder.Services.AddHostedService<EmailBackgroundWorker>();

var app = builder.Build();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var uploadsDir = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, app.Configuration["Uploads:Path"] ?? "uploads", "documents"));
    if (!Directory.Exists(uploadsDir))
    {
        Directory.CreateDirectory(uploadsDir);
    }

    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        // Bootstrap de catálogos RBAC y admin inicial: corre SIEMPRE (producción incluida).
        var adminPassword = app.Configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            adminPassword = "Admin2026!";
            if (!app.Environment.IsDevelopment())
                Console.WriteLine("[Seed Warning] 'Seed:AdminPassword' no configurada; usando contraseña por defecto para el admin inicial.");
        }
        await DbSeeder.BootstrapSystemAsync(dbContext, adminPassword);

        // Generar/Asegurar plantillas Excel por defecto (Plantilla_Alumnos.xlsx y Plantilla_Empresas.xlsx)
        ExcelTemplateSeeder.EnsureTemplatesExist(app.Environment.ContentRootPath, app.Logger);

        // Datos demo (usuarios de prueba y anteproyecto con cronograma): solo desarrollo.
        var demoEnabled = app.Environment.IsDevelopment()
                          || app.Configuration.GetValue<bool>("Seed:Enabled");
        if (demoEnabled)
        {
            await DbSeeder.SeedDemoDataAsync(dbContext);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Database Warning] {ex.ToString()}");
    }
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    var addresses = app.Services.GetService<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()?.Addresses;
    var primaryUrl = addresses?.FirstOrDefault() ?? "http://localhost:5185";

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("\n==================================================");
    Console.WriteLine(" 🚀 RTecNM V2 BACKEND (REST API) EN EJECUCIÓN");
    Console.WriteLine($" 📌 Servidor API iniciado en: {primaryUrl}");
    Console.WriteLine($" 🔗 Base Endpoint:             {primaryUrl}/api/v1");
    Console.WriteLine("==================================================\n");
    Console.ResetColor();
});

app.Run();

