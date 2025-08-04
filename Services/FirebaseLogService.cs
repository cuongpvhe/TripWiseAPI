using Firebase.Database;
using Firebase.Database.Query;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using TripWiseAPI.Models;
using TripWiseAPI.Models.APIModel;


public class FirebaseLogService
{
	private readonly FirebaseClient _firebaseClient;
	private readonly TripWiseDBContext _dbContext;
	public FirebaseLogService()
	{
		// ✅ Đổi thành URL thực sự của Realtime Database (Settings > Database > Realtime Database > URL)
		string firebaseUrl = "https://logss-eb273-default-rtdb.firebaseio.com/";
		_dbContext = new TripWiseDBContext();
		_firebaseClient = new FirebaseClient(firebaseUrl);
	}

	public async Task LogToFirebase(APILogs log)
	{
		log.Id = await GetNextLogId();

		await _firebaseClient
			.Child("logs")
			.Child(log.Id.ToString())
			.PutAsync(log);
	}

	public async Task<int> GetNextLogId()
	{
		var counterRef = await _firebaseClient
			.Child("logCounter")
			.OnceSingleAsync<int?>();

		int nextId = (counterRef ?? 0) + 1;

		// Update counter in DB
		await _firebaseClient.Child("logCounter").PutAsync(nextId);

		return nextId;
	}
	public async Task LogAsync(int userId, string action, string message, int statusCode,
	DateTime? createdDate = null, int? createdBy = null,
	DateTime? modifiedDate = null, int? modifiedBy = null,
	DateTime? removedDate = null, int? removedBy = null)
	{
		var userName = GetUserNameByIdAsync(userId).Result; // Sử dụng Result để lấy giá trị đồng bộ

		await LogToFirebase(new APILogs
		{
			UserId = userId,
			UserName = userName,
			Action = action,
			Message = message,
			StatusCode = statusCode,
			CreatedDate = createdDate,
			CreatedBy = createdBy,
			ModifiedDate = modifiedDate,
			ModifiedBy = modifiedBy,
			RemovedDate = removedDate,
			RemovedBy = removedBy,
			ExpireAt = DateTime.UtcNow.AddHours(1)
		});
	}
	private async Task<string> GetUserNameByIdAsync(int userId)
	{
		var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
		return user?.UserName ?? "Unknown";
	}
}
