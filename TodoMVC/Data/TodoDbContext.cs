using Microsoft.EntityFrameworkCore;
using TodoMVC.Models;

namespace TodoMVC.Data;

public class TodoDbContext : DbContext
{
    public TodoDbContext(DbContextOptions<TodoDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<Todo> Todos => Set<Todo>();
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Users table
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();

            entity.HasOne(u => u.Credential)
                  .WithOne(c => c.User)
                  .HasForeignKey<UserCredential>(c => c.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.Todos)
                  .WithOne(t => t.User)
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure UserCredential table
        modelBuilder.Entity<UserCredential>(entity =>
        {
            entity.ToTable("UserCredential");
            entity.HasIndex(c => c.UserId).IsUnique();
        });

        // Configure Categories table
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasIndex(c => c.Name).IsUnique();

            entity.HasMany(c => c.Todos)
                  .WithOne(t => t.Category)
                  .HasForeignKey(t => t.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Seed default categories
            entity.HasData(
                new Category { Id = 1, Name = "Work & Business", Description = "Professional tasks and projects", ColorCode = "#4F46E5", Icon = "bi-briefcase-fill", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Category { Id = 2, Name = "Personal & Life", Description = "Personal chores and errands", ColorCode = "#0EA5E9", Icon = "bi-person-fill", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Category { Id = 3, Name = "Development & Code", Description = "Software engineering and bugfixes", ColorCode = "#10B981", Icon = "bi-code-slash", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Category { Id = 4, Name = "Meetings & Calls", Description = "Appointments and conferences", ColorCode = "#F59E0B", Icon = "bi-calendar-event-fill", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Category { Id = 5, Name = "Health & Fitness", Description = "Workouts and wellness", ColorCode = "#EC4899", Icon = "bi-heart-pulse-fill", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Category { Id = 6, Name = "Learning & Study", Description = "Courses, reading, and research", ColorCode = "#8B5CF6", Icon = "bi-book-fill", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            );
        });

        // Configure Todos table
        modelBuilder.Entity<Todo>(entity =>
        {
            entity.ToTable("Todos");
            entity.HasIndex(t => t.UserId);
            entity.HasIndex(t => t.CategoryId);
            entity.HasIndex(t => new { t.UserId, t.StartTime, t.ExpectedEndTime });
        });
    }
}
