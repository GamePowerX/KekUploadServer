using KekUploadServer.Models;
using Microsoft.EntityFrameworkCore;

namespace KekUploadServer.Database;

public class UploadDataContext : DbContext
{
    public UploadDataContext(DbContextOptions<UploadDataContext> options) : base(options)
    {
    }
    
    public DbSet<UploadItem> UploadItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseSerialColumns();
    }
}