using ConexaoSolidaria.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConexaoSolidaria.Infrastructure.Persistence;

public sealed class ConexaoSolidariaDbContext(DbContextOptions<ConexaoSolidariaDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Campanha> Campanhas => Set<Campanha>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ong");

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("usuarios");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.NomeCompleto).HasMaxLength(200).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(200).IsRequired();
            entity.Property(u => u.Cpf).HasMaxLength(11).IsRequired();
            entity.Property(u => u.SenhaHash).HasMaxLength(200).IsRequired();
            entity.Property(u => u.Perfil).HasConversion<int>();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.Cpf).IsUnique();
        });

        modelBuilder.Entity<Campanha>(entity =>
        {
            entity.ToTable("campanhas");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Titulo).HasMaxLength(150).IsRequired();
            entity.Property(c => c.Descricao).HasMaxLength(4000).IsRequired();
            entity.Property(c => c.MetaFinanceira).HasPrecision(18, 2);
            entity.Property(c => c.ValorArrecadado).HasPrecision(18, 2);
            entity.Property(c => c.Status).HasConversion<int>();
            entity.HasIndex(c => c.Status);
        });
    }
}
