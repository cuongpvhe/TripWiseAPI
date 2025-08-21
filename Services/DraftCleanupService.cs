using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TripWiseAPI.Models;
using TripWiseAPI.Utils;
using static TripWiseAPI.Services.VnPayService;

public class DraftCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public DraftCleanupService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TripWiseDBContext>();
            var now = TimeHelper.GetVietnamTime();

            //  Xoá booking Draft đã hết hạn
            var expiredDrafts = await db.Bookings
                .Where(b => b.BookingStatus == "Draft" && b.ExpiredDate < now)
                .ToListAsync(stoppingToken);

            if (expiredDrafts.Any())
            {
                db.Bookings.RemoveRange(expiredDrafts);
            }

            //  Tìm booking Pending > 5 phút
            var expiredPendingBookings = await db.Bookings
                .Where(b => b.BookingStatus == PaymentStatus.Pending
                            && b.CreatedDate.HasValue
                            && b.CreatedDate.Value.AddMinutes(5) < now)
                .ToListAsync(stoppingToken);

            if (expiredPendingBookings.Any())
            {
                var expiredBookingIds = expiredPendingBookings.Select(b => b.BookingId).ToList();

                // Xoá PaymentTransaction liên quan
                var relatedPayments = await db.PaymentTransactions
                    .Where(p => expiredBookingIds.Contains((int)p.BookingId))
                    .ToListAsync(stoppingToken);

                db.PaymentTransactions.RemoveRange(relatedPayments);

                // Xoá Booking
                db.Bookings.RemoveRange(expiredPendingBookings);
            }

            //  Xoá luôn PaymentTransaction Pending > 5 phút (nếu có cái lẻ không gắn booking)
            var expiredPayments = await db.PaymentTransactions
                .Where(p => p.PaymentStatus == PaymentStatus.Pending
                            && p.CreatedDate.HasValue
                            && p.CreatedDate.Value.AddMinutes(5) < now)
                .ToListAsync(stoppingToken);

            if (expiredPayments.Any())
            {
                db.PaymentTransactions.RemoveRange(expiredPayments);
            }

            // 4️⃣ Lưu thay đổi nếu có
            if (expiredDrafts.Any() || expiredPendingBookings.Any() || expiredPayments.Any())
            {
                await db.SaveChangesAsync(stoppingToken);
            }

            // Chạy lại sau 1 phút
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
