using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TripWiseAPI.Models;
using TripWiseAPI.Utils;

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
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TripWiseDBContext>();
                var now = TimeHelper.GetVietnamTime();

                var expiredDrafts = await db.Bookings
                    .Where(b => b.BookingStatus == "Draft" && b.ExpiredDate < now)
                    .ToListAsync();

                if (expiredDrafts.Any())
                {
                    db.Bookings.RemoveRange(expiredDrafts);
                    await db.SaveChangesAsync();
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }
}
