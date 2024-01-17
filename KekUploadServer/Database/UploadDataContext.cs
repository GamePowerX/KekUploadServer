using KekUploadServer.Models;
using Microsoft.EntityFrameworkCore;

namespace KekUploadServer.Database;

public class UploadDataContext(DbContextOptions<UploadDataContext> options) : DbContext(options)
{
    public DbSet<UploadItem> UploadItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseSerialColumns();
    }
}