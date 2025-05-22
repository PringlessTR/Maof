using Microsoft.EntityFrameworkCore;
using MaofAPI.Models;
using MaofAPI.Models.Enums;
using System;
using System.Collections.Generic;

namespace MaofAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Store Management
        public DbSet<Store> Stores { get; set; }

        // User Management
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }

        // Product Management
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductTransaction> ProductTransactions { get; set; }
        public DbSet<Currency> Currencies { get; set; }

        // Promotion Management
        public DbSet<Promotion> Promotions { get; set; }

        // Sales Management
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<Payment> Payments { get; set; }
        
        // Customer Management
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Company> Companies { get; set; }

        // Sync Management
        public DbSet<SyncLog> SyncLogs { get; set; }
        public DbSet<SyncBatch> SyncBatches { get; set; }

        // Audit
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure composite keys for junction tables
            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            // Configure Store relationships
            modelBuilder.Entity<Store>()
                .HasMany(s => s.Users)
                .WithOne(u => u.Store)
                .HasForeignKey(u => u.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Store>()
                .HasMany(s => s.Products)
                .WithOne(p => p.Store)
                .HasForeignKey(p => p.StoreId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Store>()
                .HasMany(s => s.Sales)
                .WithOne(s => s.Store)
                .HasForeignKey(s => s.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Store>()
                .HasMany(s => s.Promotions)
                .WithOne(p => p.Store)
                .HasForeignKey(p => p.StoreId)2	2	2025-05-21 15:18:26.2500000	0	6c495ce0-7825-4ed7-9d14-29831ea6032b
NULL	NULL	NULL	NULL	NULL
                .OnDelete(DeleteBehavior.Restrict);
                
            modelBuilder.Entity<Store>()
                .HasMany(s => s.SyncLogs)
                .WithOne(sl => sl.Store)
                .HasForeignKey(sl => sl.StoreId)
                .OnDelete(DeleteBehavior.Restrict);
                
            modelBuilder.Entity<Store>()
                .HasMany(s => s.SyncBatches)
                .WithOne(sb => sb.Store)
                .HasForeignKey(sb => sb.StoreId)
                .OnDelete(DeleteBehavior.Restrict);
                
            modelBuilder.Entity<Store>()
                .HasMany(s => s.AuditLogs)
                .WithOne(a => a.Store)
                .HasForeignKey(a => a.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Store>()
                .HasMany(s => s.ProductTransactions)
                .WithOne(pt => pt.Store)
                .HasForeignKey(pt => pt.StoreId)
                .OnDelete(DeleteBehavior.Restrict);
                
            modelBuilder.Entity<Store>()
                .HasMany(s => s.Payments)
                .WithOne(p => p.Store)
                .HasForeignKey(p => p.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Store>()
                .HasMany(s => s.SaleItems)
                .WithOne(si => si.Store)
                .HasForeignKey(si => si.StoreId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Configure Customer relationships
            modelBuilder.Entity<Customer>()
                .HasOne(c => c.Company)
                .WithMany(c => c.Customers)
                .HasForeignKey(c => c.CompanyID)
                .OnDelete(DeleteBehavior.Restrict);
                
            modelBuilder.Entity<Customer>()
                .HasMany(c => c.Sales)
                .WithOne(s => s.Customer)
                .HasForeignKey(s => s.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Configure Sale relationships with Customer
            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Customer)
                .WithMany(c => c.Sales)
                .HasForeignKey(s => s.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Configure Store relationships with Sale
            modelBuilder.Entity<Store>()
                .HasMany(s => s.Sales)
                .WithOne(s => s.Store)
                .HasForeignKey(s => s.StoreId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Configure Company relationships
            modelBuilder.Entity<Company>()
                .HasMany(c => c.Customers)
                .WithOne(c => c.Company)
                .HasForeignKey(c => c.CompanyID)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Configure Store relationships with Company
            // Removed Customers relationship as it doesn't exist in the Store model
            
            // Configure User relationships
            modelBuilder.Entity<User>()
                .HasMany(u => u.UserRoles)
                .WithOne(ur => ur.User)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Sales)
                .WithOne(s => s.User)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);                
                
            modelBuilder.Entity<User>()
                .HasMany(u => u.ProductTransactions)
                .WithOne(pt => pt.User)
                .HasForeignKey(pt => pt.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Removed SyncLogs and SyncBatches relationships as they don't exist in the User model

            modelBuilder.Entity<Role>()
                .HasMany(r => r.UserRoles)
                .WithOne(ur => ur.Role)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Role>()
                .HasMany(r => r.RolePermissions)
                .WithOne(rp => rp.Role)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Permission relationships
            modelBuilder.Entity<Permission>()
                .HasMany(p => p.RolePermissions)
                .WithOne(rp => rp.Permission)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Product relationships
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
                
            modelBuilder.Entity<Product>()
                .HasMany(p => p.ProductTransactions)
                .WithOne(pt => pt.Product)
                .HasForeignKey(pt => pt.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Configure Promotion-Product relationship
            modelBuilder.Entity<Promotion>()
                .HasOne(p => p.Product)
                .WithMany(p => p.Promotions)
                .HasForeignKey(p => p.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Sale relationships
            modelBuilder.Entity<Sale>()
                .HasMany(s => s.SaleItems)
                .WithOne(si => si.Sale)
                .HasForeignKey(si => si.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Sale>()
                .HasMany(s => s.Payments)
                .WithOne(p => p.Sale)
                .HasForeignKey(p => p.SaleId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Configure Store-Promotion relationship
            modelBuilder.Entity<Store>()
                .HasMany(s => s.Promotions)
                .WithOne(p => p.Store)
                .HasForeignKey(p => p.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure SyncBatch relationships
            modelBuilder.Entity<SyncBatch>()
                .HasMany(sb => sb.SyncLogs)
                .WithOne(sl => sl.SyncBatch)
                .HasForeignKey(sl => sl.SyncBatchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Store>()
                .Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(100);
        }
    }
}
