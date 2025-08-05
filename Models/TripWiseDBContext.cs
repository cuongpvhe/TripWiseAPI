using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TripWiseAPI.Models
{
    public partial class TripWiseDBContext : DbContext
    {
        public TripWiseDBContext()
        {
        }

        public TripWiseDBContext(DbContextOptions<TripWiseDBContext> options)
            : base(options)
        {
        }

        public virtual DbSet<AppSetting> AppSettings { get; set; } = null!;
        public virtual DbSet<Blog> Blogs { get; set; } = null!;
        public virtual DbSet<BlogImage> BlogImages { get; set; } = null!;
        public virtual DbSet<Booking> Bookings { get; set; } = null!;
        public virtual DbSet<EditVersionTravelPlan> EditVersionTravelPlans { get; set; } = null!;
        public virtual DbSet<GenerateTravelPlan> GenerateTravelPlans { get; set; } = null!;
        public virtual DbSet<Image> Images { get; set; } = null!;
        public virtual DbSet<Partner> Partners { get; set; } = null!;
        public virtual DbSet<PaymentTransaction> PaymentTransactions { get; set; } = null!;
        public virtual DbSet<Plan> Plans { get; set; } = null!;
        public virtual DbSet<Review> Reviews { get; set; } = null!;
        public virtual DbSet<SignupOtp> SignupOtps { get; set; } = null!;
        public virtual DbSet<Tour> Tours { get; set; } = null!;
        public virtual DbSet<TourAttraction> TourAttractions { get; set; } = null!;
        public virtual DbSet<TourAttractionImage> TourAttractionImages { get; set; } = null!;
        public virtual DbSet<TourImage> TourImages { get; set; } = null!;
        public virtual DbSet<TourItinerary> TourItineraries { get; set; } = null!;
        public virtual DbSet<TourType> TourTypes { get; set; } = null!;
        public virtual DbSet<TransactionHistory> TransactionHistories { get; set; } = null!;
        public virtual DbSet<User> Users { get; set; } = null!;
        public virtual DbSet<UserPlan> UserPlans { get; set; } = null!;
        public virtual DbSet<UserRefreshToken> UserRefreshTokens { get; set; } = null!;
        public virtual DbSet<VnpayLog> VnpayLogs { get; set; } = null!;
        public virtual DbSet<Wishlist> Wishlists { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var builder = new ConfigurationBuilder()
.SetBasePath(Directory.GetCurrentDirectory())
.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            IConfigurationRoot configuration = builder.Build();
            optionsBuilder.UseSqlServer(configuration.GetConnectionString("DBContext"));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AppSetting>(entity =>
            {
                entity.HasIndex(e => e.Key, "UQ__AppSetti__C41E0289A74E9F47")
                    .IsUnique();

                entity.Property(e => e.Key).HasMaxLength(100);
            });

            modelBuilder.Entity<Blog>(entity =>
            {
                entity.ToTable("Blog");

                entity.Property(e => e.BlogId).HasColumnName("BlogID");

                entity.Property(e => e.BlogName).HasMaxLength(255);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedReason).HasMaxLength(255);
            });

            modelBuilder.Entity<BlogImage>(entity =>
            {
                entity.Property(e => e.BlogImageId).HasColumnName("BlogImageID");

                entity.Property(e => e.BlogId).HasColumnName("BlogID");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.ImageId).HasColumnName("ImageID");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedReason).HasMaxLength(255);

                entity.HasOne(d => d.Blog)
                    .WithMany(p => p.BlogImages)
                    .HasForeignKey(d => d.BlogId)
                    .HasConstraintName("FK_BlogImages_Blog");

                entity.HasOne(d => d.Image)
                    .WithMany(p => p.BlogImages)
                    .HasForeignKey(d => d.ImageId)
                    .HasConstraintName("FK_BlogImages_Images");
            });

            modelBuilder.Entity<Booking>(entity =>
            {
                entity.HasIndex(e => e.OrderCode, "UQ__Bookings__999B522941CF6868")
                    .IsUnique();

                entity.Property(e => e.BookingId).HasColumnName("BookingID");

                entity.Property(e => e.BookingStatus)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("('Pending')");

                entity.Property(e => e.CreatedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.OrderCode).HasMaxLength(50);

                entity.Property(e => e.RemovedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedReason).HasMaxLength(255);

                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.TourId).HasColumnName("TourID");

                entity.Property(e => e.UserId).HasColumnName("UserID");

                entity.HasOne(d => d.Tour)
                    .WithMany(p => p.Bookings)
                    .HasForeignKey(d => d.TourId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Orders_Tours");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Bookings)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Orders_Users");
            });

            modelBuilder.Entity<EditVersionTravelPlan>(entity =>
            {
                entity.ToTable("EditVersionTravelPlan");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.ConversationId).HasMaxLength(50);

                entity.Property(e => e.EditTime).HasColumnType("datetime");

                entity.Property(e => e.GenerateTravelPlanId).HasColumnName("GenerateTravelPlanID");

                entity.HasOne(d => d.GenerateTravelPlan)
                    .WithMany(p => p.EditVersionTravelPlans)
                    .HasForeignKey(d => d.GenerateTravelPlanId)
                    .HasConstraintName("FK_EditVersionTravelPlan_GenerateTravelPlan");
            });

            modelBuilder.Entity<GenerateTravelPlan>(entity =>
            {
                entity.ToTable("GenerateTravelPlan");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.ConversationId).HasMaxLength(255);

                entity.Property(e => e.ResponseTime).HasColumnType("datetime");

                entity.Property(e => e.TourId).HasColumnName("TourID");

                entity.Property(e => e.UserId).HasColumnName("UserID");

                entity.HasOne(d => d.Tour)
                    .WithMany(p => p.GenerateTravelPlans)
                    .HasForeignKey(d => d.TourId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_GenerateTravelPlan_Tours");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.GenerateTravelPlans)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("FK_GenerateTravelPlan_Users");
            });

            modelBuilder.Entity<Image>(entity =>
            {
                entity.Property(e => e.ImageId).HasColumnName("ImageID");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.ImageAlt).HasMaxLength(255);

                entity.Property(e => e.ImageUrl)
                    .HasMaxLength(255)
                    .HasColumnName("ImageURL");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedReason).HasMaxLength(255);
            });

            modelBuilder.Entity<Partner>(entity =>
            {
                entity.HasIndex(e => e.UserId, "UQ__Partners__1788CCADE54E8A1E")
                    .IsUnique();

                entity.Property(e => e.PartnerId).HasColumnName("PartnerID");

                entity.Property(e => e.Address).HasMaxLength(255);

                entity.Property(e => e.CompanyName).HasMaxLength(100);

                entity.Property(e => e.CreatedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.IsActive).HasDefaultValueSql("((1))");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.PhoneNumber).HasMaxLength(20);

                entity.Property(e => e.RemovedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedReason).HasMaxLength(255);

                entity.Property(e => e.UserId).HasColumnName("UserID");

                entity.Property(e => e.Website).HasMaxLength(100);

                entity.HasOne(d => d.User)
                    .WithOne(p => p.Partner)
                    .HasForeignKey<Partner>(d => d.UserId)
                    .HasConstraintName("FK_Partners_Users");
            });

            modelBuilder.Entity<PaymentTransaction>(entity =>
            {
                entity.HasKey(e => e.TransactionId)
                    .HasName("PK__PaymentT__55433A4B75A20251");

                entity.Property(e => e.TransactionId).HasColumnName("TransactionID");

                entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.BankCode).HasMaxLength(50);

                entity.Property(e => e.CreatedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.OrderCode).HasMaxLength(50);

                entity.Property(e => e.PaymentStatus)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("('Pending')");

                entity.Property(e => e.PaymentTime).HasColumnType("datetime");

                entity.Property(e => e.RemovedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedReason).HasMaxLength(255);

                entity.Property(e => e.UserId).HasColumnName("UserID");

                entity.Property(e => e.VnpTransactionNo).HasMaxLength(50);

                entity.HasOne(d => d.User)
                    .WithMany(p => p.PaymentTransactions)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("FK_Transactions_Users");
            });

            modelBuilder.Entity<Plan>(entity =>
            {
                entity.Property(e => e.PlanId).HasColumnName("PlanID");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.Description).HasMaxLength(255);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.PlanName).HasMaxLength(50);

                entity.Property(e => e.RemovedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedReason).HasMaxLength(255);
            });

            modelBuilder.Entity<Review>(entity =>
            {
                entity.Property(e => e.ReviewId).HasColumnName("ReviewID");

                entity.Property(e => e.Comment).HasColumnType("ntext");

                entity.Property(e => e.CreatedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedReason).HasMaxLength(255);

                entity.Property(e => e.TourId).HasColumnName("TourID");

                entity.Property(e => e.UserId).HasColumnName("UserID");

                entity.HasOne(d => d.Tour)
                    .WithMany(p => p.Reviews)
                    .HasForeignKey(d => d.TourId)
                    .HasConstraintName("FK__Reviews__TourID__4F7CD00D");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Reviews)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("FK__Reviews__UserID__4E88ABD4");
            });

            modelBuilder.Entity<SignupOtp>(entity =>
            {
                entity.HasKey(e => e.SignupRequestId)
                    .HasName("SignupOTP_pk");

                entity.ToTable("SignupOTP");

                entity.Property(e => e.SignupRequestId)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.ExpiresAt).HasColumnType("datetime");

                entity.Property(e => e.Otpstring)
                    .HasMaxLength(10)
                    .IsUnicode(false)
                    .HasColumnName("OTPString");
            });

            modelBuilder.Entity<Tour>(entity =>
            {
                entity.Property(e => e.TourId).HasColumnName("TourID");

                entity.Property(e => e.Category).HasMaxLength(150);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.Duration).HasMaxLength(10);

                entity.Property(e => e.Location).HasMaxLength(150);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.PartnerId).HasColumnName("PartnerID");

                entity.Property(e => e.Price).HasColumnType("decimal(10, 2)");

                entity.Property(e => e.PriceAdult).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.PriceChild5To10).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.PriceChildUnder5).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.RejectReason).HasMaxLength(500);

                entity.Property(e => e.RemovedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedReason).HasMaxLength(255);

                entity.Property(e => e.StartTime).HasColumnType("datetime");

                entity.Property(e => e.Status)
                    .HasMaxLength(50)
                    .HasDefaultValueSql("('Draft')");

                entity.Property(e => e.TourName).HasMaxLength(200);

                entity.Property(e => e.TourTypesId).HasColumnName("TourTypesID");

                entity.HasOne(d => d.Partner)
                    .WithMany(p => p.Tours)
                    .HasForeignKey(d => d.PartnerId)
                    .HasConstraintName("FK_Tours_Partners");

                entity.HasOne(d => d.TourTypes)
                    .WithMany(p => p.Tours)
                    .HasForeignKey(d => d.TourTypesId)
                    .HasConstraintName("FK_Tours_TourTypes");
            });

            modelBuilder.Entity<TourAttraction>(entity =>
            {
                entity.HasKey(e => e.TourAttractionsId)
                    .HasName("PK__Activiti__45F4A7F194AA7FA6");

                entity.Property(e => e.TourAttractionsId).HasColumnName("TourAttractionsID");

                entity.Property(e => e.Category).HasMaxLength(100);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.EndTime).HasColumnType("time(0)");

                entity.Property(e => e.ImageUrl).HasColumnName("imageUrl");

                entity.Property(e => e.ItineraryId).HasColumnName("ItineraryID");

                entity.Property(e => e.Localtion).HasMaxLength(255);

                entity.Property(e => e.MapUrl).HasColumnName("mapUrl");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.Price).HasColumnType("decimal(10, 2)");

                entity.Property(e => e.RemovedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedReason).HasMaxLength(255);

                entity.Property(e => e.StartTime).HasColumnType("time(0)");

                entity.HasOne(d => d.Itinerary)
                    .WithMany(p => p.TourAttractions)
                    .HasForeignKey(d => d.ItineraryId)
                    .HasConstraintName("FK_TourAttractions_Itinerary");
            });

            modelBuilder.Entity<TourAttractionImage>(entity =>
            {
                entity.Property(e => e.TourAttractionImageId).HasColumnName("TourAttractionImageID");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.ImageId).HasColumnName("ImageID");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedReason).HasMaxLength(255);

                entity.Property(e => e.TourAttractionId).HasColumnName("TourAttractionID");

                entity.HasOne(d => d.Image)
                    .WithMany(p => p.TourAttractionImages)
                    .HasForeignKey(d => d.ImageId)
                    .HasConstraintName("FK_TourAttractionImages_Images");

                entity.HasOne(d => d.TourAttraction)
                    .WithMany(p => p.TourAttractionImages)
                    .HasForeignKey(d => d.TourAttractionId)
                    .HasConstraintName("FK_TourAttractionImages_TourAttractions");
            });

            modelBuilder.Entity<TourImage>(entity =>
            {
                entity.Property(e => e.TourImageId).HasColumnName("TourImageID");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.ImageId).HasColumnName("ImageID");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedReason).HasMaxLength(255);

                entity.Property(e => e.TourId).HasColumnName("TourID");

                entity.HasOne(d => d.Image)
                    .WithMany(p => p.TourImages)
                    .HasForeignKey(d => d.ImageId)
                    .HasConstraintName("FK_TourImages_Images");

                entity.HasOne(d => d.Tour)
                    .WithMany(p => p.TourImages)
                    .HasForeignKey(d => d.TourId)
                    .HasConstraintName("FK_TourImages_Tours");
            });

            modelBuilder.Entity<TourItinerary>(entity =>
            {
                entity.HasKey(e => e.ItineraryId)
                    .HasName("PK__TourItin__361216A66B633092");

                entity.ToTable("TourItinerary");

                entity.Property(e => e.ItineraryId).HasColumnName("ItineraryID");

                entity.Property(e => e.Category).HasMaxLength(100);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.EndTime).HasColumnType("time(0)");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedReason).HasMaxLength(255);

                entity.Property(e => e.StartTime).HasColumnType("time(0)");

                entity.Property(e => e.TourId).HasColumnName("TourID");

                entity.HasOne(d => d.Tour)
                    .WithMany(p => p.TourItineraries)
                    .HasForeignKey(d => d.TourId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__TourItine__TourI__440B1D61");
            });

            modelBuilder.Entity<TourType>(entity =>
            {
                entity.HasKey(e => e.TourTypesId);

                entity.Property(e => e.TourTypesId).HasColumnName("TourTypesID");

                entity.Property(e => e.TypeName).HasMaxLength(200);
            });

            modelBuilder.Entity<TransactionHistory>(entity =>
            {
                entity.ToTable("TransactionHistory");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.Actor).HasMaxLength(100);

                entity.Property(e => e.Created)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.Reason).HasMaxLength(100);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Email, "UQ__Users__A9D10534D4FDDBBE")
                    .IsUnique();

                entity.Property(e => e.UserId).HasColumnName("UserID");

                entity.Property(e => e.City).HasMaxLength(50);

                entity.Property(e => e.Country).HasMaxLength(50);

                entity.Property(e => e.CreatedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.District).HasMaxLength(50);

                entity.Property(e => e.Email).HasMaxLength(100);

                entity.Property(e => e.FirstName).HasMaxLength(50);

                entity.Property(e => e.LastName).HasMaxLength(50);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.PasswordHash).HasMaxLength(255);

                entity.Property(e => e.PhoneNumber).HasMaxLength(20);

                entity.Property(e => e.RemovedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedReason).HasMaxLength(255);

                entity.Property(e => e.Role).HasMaxLength(20);

                entity.Property(e => e.StreetAddress).HasMaxLength(50);

                entity.Property(e => e.UserName).HasMaxLength(100);

                entity.Property(e => e.Ward).HasMaxLength(50);
            });

            modelBuilder.Entity<UserPlan>(entity =>
            {
                entity.HasKey(e => new { e.UserPlanId, e.UserId, e.PlanId })
                    .HasName("PK_UserPlans_1");

                entity.Property(e => e.UserPlanId)
                    .ValueGeneratedOnAdd()
                    .HasColumnName("UserPlanID");

                entity.Property(e => e.UserId).HasColumnName("UserID");

                entity.Property(e => e.PlanId).HasColumnName("PlanID");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.EndDate).HasColumnType("datetime");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.StartDate).HasColumnType("datetime");

                entity.HasOne(d => d.Plan)
                    .WithMany(p => p.UserPlans)
                    .HasForeignKey(d => d.PlanId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_UserPlans_Plans");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.UserPlans)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_UserPlans_Users");
            });

            modelBuilder.Entity<UserRefreshToken>(entity =>
            {
                entity.ToTable("UserRefreshToken");

                entity.Property(e => e.CreatedAt).HasColumnType("datetime");

                entity.Property(e => e.DeviceId).HasColumnName("DeviceID");

                entity.Property(e => e.ExpiresAt).HasColumnType("datetime");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.UserRefreshTokens)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_UserRefreshToken_Users");
            });

            modelBuilder.Entity<VnpayLog>(entity =>
            {
                entity.HasKey(e => e.LogId)
                    .HasName("PK__VnpayLog__5E5499A8A99B20CE");

                entity.ToTable("VnpayLog");

                entity.Property(e => e.LogId).HasColumnName("LogID");

                entity.Property(e => e.CreatedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.OrderCode).HasMaxLength(50);

                entity.Property(e => e.RemovedDate).HasColumnType("datetime");

                entity.Property(e => e.RemovedReason).HasMaxLength(255);
            });

            modelBuilder.Entity<Wishlist>(entity =>
            {
                entity.Property(e => e.WishlistId).HasColumnName("WishlistID");

                entity.Property(e => e.DateAdded)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.TourId).HasColumnName("TourID");

                entity.Property(e => e.UserId).HasColumnName("UserID");

                entity.HasOne(d => d.Tour)
                    .WithMany(p => p.Wishlists)
                    .HasForeignKey(d => d.TourId)
                    .HasConstraintName("FK__Wishlists__TourI__5535A963");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Wishlists)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("FK__Wishlists__UserI__5441852A");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
