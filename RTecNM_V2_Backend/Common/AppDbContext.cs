using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Activities;
using TecNM.Residency.Advisors;
using TecNM.Residency.Auth;
using TecNM.Residency.Companies;
using TecNM.Residency.Documents;
using TecNM.Residency.Evaluations;
using TecNM.Residency.Projects;
using TecNM.Residency.Students;

namespace TecNM.Residency.Common;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRoleAssignment> UserRoles => Set<UserRoleAssignment>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Advisor> Advisors => Set<Advisor>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectObjective> ProjectObjectives => Set<ProjectObjective>();
    public DbSet<WeeklyActivity> WeeklyActivities => Set<WeeklyActivity>();
    public DbSet<WeeklyProgress> WeeklyProgresses => Set<WeeklyProgress>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<AdvisorySession> AdvisorySessions => Set<AdvisorySession>();
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new ModuleConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new RolePermissionConfiguration());
        modelBuilder.ApplyConfiguration(new StudentConfiguration());
        modelBuilder.ApplyConfiguration(new AdvisorConfiguration());
        modelBuilder.ApplyConfiguration(new CompanyConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectObjectiveConfiguration());
        modelBuilder.ApplyConfiguration(new WeeklyActivityConfiguration());
        modelBuilder.ApplyConfiguration(new WeeklyProgressConfiguration());
        modelBuilder.ApplyConfiguration(new EvaluationConfiguration());
        modelBuilder.ApplyConfiguration(new AdvisorySessionConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentConfiguration());
    }
}





