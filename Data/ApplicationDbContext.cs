using Microsoft.EntityFrameworkCore;
using MyMvcApp.Models;

namespace MyMvcApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<AcademicProfile> AcademicProfiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Account configuration
            modelBuilder.Entity<Account>(entity =>
            {
                entity.HasKey(e => e.AccountId);
                entity.Property(e => e.AccountId).HasColumnName("account_id");
                entity.Property(e => e.Username).IsRequired().HasMaxLength(150).HasColumnName("username");
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(150).HasColumnName("password_hash");
                entity.Property(e => e.Email).IsRequired().HasMaxLength(150).HasColumnName("email");
                entity.Property(e => e.Role).IsRequired().HasConversion<string>().HasColumnName("roles");
                entity.Property(e => e.RequestStatus).IsRequired().HasConversion<string>().HasDefaultValue(RequestStatus.Pending).HasColumnName("request_status");
                entity.Property(e => e.IsOnline).IsRequired().HasDefaultValue(true).HasColumnName("is_online");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnName("created_at");

                entity.HasIndex(e => e.Username).IsUnique().HasDatabaseName("uq_accounts_userename");
            });

            // Student configuration
            modelBuilder.Entity<Student>(entity =>
            {
                entity.Property(e => e.StudentId).HasColumnName("student_id");
                entity.Property(e => e.AccountId).IsRequired().HasColumnName("account_id");
                entity.Property(e => e.Lastname).HasMaxLength(50).HasColumnName("lastname");
                entity.Property(e => e.Firstname).HasMaxLength(50).HasColumnName("firstname");
                entity.Property(e => e.MiddleName).HasMaxLength(10).HasColumnName("middle_name");

                entity.HasIndex(e => e.AccountId).IsUnique().HasDatabaseName("uq_students_account_id");

                entity.HasOne(e => e.Account)
                    .WithOne(a => a.Student)
                    .HasForeignKey<Student>(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Course configuration
            modelBuilder.Entity<Course>(entity =>
            {
                entity.HasKey(e => e.CourseId);
                entity.Property(e => e.CourseId).HasColumnName("course_id");
                entity.Property(e => e.CourseCode).IsRequired().HasMaxLength(20).HasColumnName("course_code");
                entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true).HasColumnName("is_active");

                entity.HasIndex(e => e.CourseCode).IsUnique().HasDatabaseName("uq_courses_course_code");
            });

            // AcademicProfile configuration
            modelBuilder.Entity<AcademicProfile>(entity =>
            {
                entity.HasKey(e => e.AcademicProfileId);
                entity.Property(e => e.AcademicProfileId).HasColumnName("academic_profile_id");
                entity.Property(e => e.StudentId).IsRequired().HasColumnName("student_id");
                entity.Property(e => e.CourseId).IsRequired().HasColumnName("course_id");
                entity.Property(e => e.YearLevel).HasConversion<string>().HasColumnName("year_level");
                entity.Property(e => e.Section).HasMaxLength(50).HasColumnName("section");
                entity.Property(e => e.AcademicStatus).IsRequired().HasConversion<string>().HasDefaultValue(AcademicStatus.Enrolled).HasColumnName("academic_status");

                entity.HasIndex(e => e.StudentId).IsUnique().HasDatabaseName("uq_academic_profile_student_id");

                entity.HasOne(e => e.Student)
                    .WithOne(s => s.AcademicProfile)
                    .HasForeignKey<AcademicProfile>(e => e.StudentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Course)
                    .WithMany(c => c.AcademicProfiles)
                    .HasForeignKey(e => e.CourseId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
