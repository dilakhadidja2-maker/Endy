using Endy.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Endy.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<RequestEvent> RequestHistory => Set<RequestEvent>();
    public DbSet<AppNotification> Notifications => Set<AppNotification>();
    public DbSet<VisitTracking> VisitTrackings => Set<VisitTracking>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Department>().HasIndex(x => x.Name).IsUnique();
        builder.Entity<ApplicationUser>().HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<AccessRequest>().HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AccessRequest>().HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RequestEvent>().HasOne(x => x.AccessRequest).WithMany(x => x.History).HasForeignKey(x => x.AccessRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<VisitTracking>().HasOne(x => x.AccessRequest).WithOne(x => x.Visit).HasForeignKey<VisitTracking>(x => x.AccessRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<AppNotification>().HasOne(x => x.RecipientUser).WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<AppNotification>().HasOne(x => x.AccessRequest).WithMany().HasForeignKey(x => x.AccessRequestId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<AccessRequest>().HasIndex(x => new { x.Status, x.RequestDate });
        builder.Entity<AccessRequest>().HasIndex(x => x.RegistrationNumber);
    }
}
