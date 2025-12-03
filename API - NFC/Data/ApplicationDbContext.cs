using Microsoft.EntityFrameworkCore;

using API___NFC.Models;

namespace API_NFC.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Aprendiz> Aprendiz { get; set; }
        public DbSet<Elemento> Elemento { get; set; }
        public DbSet<ElementoProceso> ElementoProceso { get; set; }
        public DbSet<Ficha> Ficha { get; set; }
        public DbSet<Proceso> Proceso { get; set; }
        public DbSet<Programa> Programa { get; set; }
        public DbSet<RegistroNFC> RegistroNFC { get; set; }
        public DbSet<TipoElemento> TipoElemento { get; set; }
        public DbSet<TipoProceso> TipoProceso { get; set; }
        public DbSet<Usuario> Usuario { get; set; }
        public DbSet<TagAsignado> TagAsignado { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Aprendiz
            modelBuilder.Entity<Aprendiz>(entity =>
            {
                entity.HasKey(e => e.IdAprendiz);
                entity.HasIndex(e => e.Correo).IsUnique();
                entity.HasIndex(e => e.NumeroDocumento).IsUnique();
                entity.HasIndex(e => e.CodigoBarras).IsUnique();

                entity.Property(e => e.FechaCreacion).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.FechaActualizacion).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.Estado).HasDefaultValue(true);

                entity.HasOne(e => e.Ficha)
                      .WithMany()
                      .HasForeignKey(e => e.IdFicha)
                      .OnDelete(DeleteBehavior.Restrict);

                // CHECK constraint TipoDocumento
                entity.HasCheckConstraint("CHK_Aprendiz_TipoDocumento", "(TipoDocumento='PA' OR TipoDocumento='CE' OR TipoDocumento='TI' OR TipoDocumento='CC')");
            });


            // Elemento
            modelBuilder.Entity<Elemento>(entity =>
            {
                entity.HasKey(e => e.IdElemento);
                entity.HasIndex(e => e.Serial).IsUnique();
                entity.HasIndex(e => e.CodigoNFC).IsUnique();

                entity.Property(e => e.FechaCreacion).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.FechaActualizacion).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.Estado).HasDefaultValue(true);

                entity.HasOne(e => e.TipoElemento)
                      .WithMany()
                      .HasForeignKey(e => e.IdTipoElemento)
                      .OnDelete(DeleteBehavior.Restrict);

                // CHECK TipoPropietario
                entity.HasCheckConstraint("CHK_Elemento_TipoPropietario", "(TipoPropietario='Usuario' OR TipoPropietario='Aprendiz')");
            });

            // ElementoProceso
            modelBuilder.Entity<ElementoProceso>(entity =>
            {
                entity.HasKey(e => e.IdElementoProceso);
                entity.Property(e => e.Validado).HasDefaultValue(false);

                entity.HasOne(e => e.Elemento)
                      .WithMany()
                      .HasForeignKey(e => e.IdElemento)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Proceso)
                      .WithMany()
                      .HasForeignKey(e => e.IdProceso)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Ficha>(entity =>
            {
                entity.ToTable("Ficha"); 
                entity.HasKey(e => e.IdFicha);
                entity.HasIndex(e => e.Codigo).IsUnique();

                entity.Property(e => e.Estado).HasDefaultValue(true);
                entity.Property(e => e.FechaCreacion).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.FechaActualizacion).HasDefaultValueSql("GETDATE()");

                // ✅ Configuración explícita de la relación
                entity.HasOne(f => f.Programa)
                      .WithMany(p => p.Fichas)
                      .HasForeignKey(f => f.IdPrograma)
                      .HasPrincipalKey(p => p.IdPrograma)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired();
            });

            // Proceso
            modelBuilder.Entity<Proceso>(entity =>
            {
                entity.HasKey(e => e.IdProceso);

                entity.Property(e => e.TimeStampEntradaSalida)
                      .HasDefaultValueSql("GETDATE()");

                entity.Property(e => e.RequiereOtrosProcesos)
                      .HasDefaultValue(false);

                entity.Property(e => e.SincronizadoBD)
                      .HasDefaultValue(false);

                entity.HasOne(e => e.Aprendiz)
                      .WithMany()
                      .HasForeignKey(e => e.IdAprendiz)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Usuario)
                      .WithMany()
                      .HasForeignKey(e => e.IdUsuario)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.TipoProceso)
                      .WithMany()
                      .HasForeignKey(e => e.IdTipoProceso)
                      .OnDelete(DeleteBehavior.Restrict);

                // CHECK TipoPersona (‘Usuario’ o ‘Aprendiz’)
                entity.HasCheckConstraint("CHK_Proceso_TipoPersona", "(TipoPersona='Usuario' OR TipoPersona='Aprendiz')");
            });

            // Programa
            modelBuilder.Entity<Programa>(entity =>
            {
                entity.ToTable("Programa"); 
                entity.HasKey(e => e.IdPrograma);
                entity.HasIndex(e => e.Codigo).IsUnique();

                entity.Property(e => e.FechaCreacion).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.FechaActualizacion).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.Estado).HasDefaultValue(true);

                // ✅ Configuración explícita de la relación inversa
                entity.HasMany(p => p.Fichas)
                      .WithOne(f => f.Programa)
                      .HasForeignKey(f => f.IdPrograma)
                      .HasPrincipalKey(p => p.IdPrograma) 
                      .OnDelete(DeleteBehavior.Restrict);

                // CHECK NivelFormacion
                entity.HasCheckConstraint("CHK_Programa_NivelFormacion", "(NivelFormacion='Operario' OR NivelFormacion='Especialización' OR NivelFormacion='Tecnólogo' OR NivelFormacion='Técnico')");
            });
            // RegistroNFC
            modelBuilder.Entity<RegistroNFC>(entity =>
            {
                entity.HasKey(e => e.IdRegistro);

                entity.Property(e => e.TipoRegistro)
                      .HasMaxLength(50)
                      .IsRequired();

                entity.Property(e => e.Estado)
                      .HasMaxLength(20);

                entity.Property(e => e.FechaRegistro)
                      .HasDefaultValueSql("GETDATE()");

                entity.HasOne(e => e.Aprendiz)
                      .WithMany()
                      .HasForeignKey(e => e.IdAprendiz)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired(false);  // ✅ ahora permite null

                entity.HasOne(e => e.Usuario)
                      .WithMany()
                      .HasForeignKey(e => e.IdUsuario)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired(false);  // ✅ también permite null
            });


            // TipoElemento
            modelBuilder.Entity<TipoElemento>(entity =>
            {
                entity.HasKey(e => e.IdTipoElemento);
                entity.HasIndex(e => e.Tipo).IsUnique();

                entity.Property(e => e.RequiereNFC).HasDefaultValue(false);
                entity.Property(e => e.Estado).HasDefaultValue(true);
                entity.Property(e => e.FechaCreacion).HasDefaultValueSql("GETDATE()");
            });

            // TipoProceso
            modelBuilder.Entity<TipoProceso>(entity =>
            {
                entity.HasKey(e => e.IdTipoProceso);
                entity.HasIndex(e => e.Tipo).IsUnique();

                entity.Property(e => e.Estado).HasDefaultValue(true);
            });

            // Usuario
            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.HasKey(e => e.IdUsuario);
                entity.HasIndex(e => e.Correo).IsUnique();
                entity.HasIndex(e => e.NumeroDocumento).IsUnique();
                entity.HasIndex(e => e.CodigoBarras).IsUnique();

                entity.Property(e => e.FechaCreacion).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.FechaActualizacion).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.Estado).HasDefaultValue(true);

                // CHECKs
                entity.HasCheckConstraint("CHK_Usuario_Rol", "(Rol='Administrador' OR Rol='Guardia' OR Rol='Funcionario')");
                entity.HasCheckConstraint("CHK_Usuario_TipoDocumento", "(TipoDocumento='PA' OR TipoDocumento='CE' OR TipoDocumento='TI' OR TipoDocumento='CC')");
            });
        }
    }
}