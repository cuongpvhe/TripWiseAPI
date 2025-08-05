using Firebase.Database;
using TripWiseAPI.Models.APIModel;
using TripWiseAPI.Models;
using Microsoft.EntityFrameworkCore;
using Firebase.Database.Query;
using System.Text.Json;

public class FirebaseLogService
{
	private readonly FirebaseClient _firebaseClient;
	private readonly TripWiseDBContext _dbContext;

	public FirebaseLogService(TripWiseDBContext dbContext)
	{
		string firebaseUrl = "https://logss-eb273-default-rtdb.firebaseio.com/"; // đảm bảo đúng URL
		_dbContext = dbContext;
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
		await _firebaseClient.Child("logCounter").PutAsync(nextId);
		return nextId;
	}

	public async Task<List<APILogs>> GetRawLogsAsync()
	{
		using var http = new HttpClient();
		var url = "https://logss-eb273-default-rtdb.firebaseio.com/logs.json";
		string resp;

		try
		{
			resp = await http.GetStringAsync(url);
		}
		catch (Exception ex)
		{
			Console.WriteLine("[FirebaseLogService] HTTP error: " + ex);
			return new List<APILogs>();
		}

		Console.WriteLine("[FirebaseLogService] Raw JSON from REST: " + resp);

		if (string.IsNullOrWhiteSpace(resp) || resp.Trim() == "null")
			return new List<APILogs>();

		var result = new List<APILogs>();

		try
		{
			var element = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(resp);
			if (element.ValueKind == System.Text.Json.JsonValueKind.Null)
				return result;

			if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
			{
				foreach (var item in element.EnumerateArray())
				{
					if (item.ValueKind == System.Text.Json.JsonValueKind.Null)
						continue; // bỏ qua null
					var log = item.Deserialize<APILogs>();
					if (log != null)
						result.Add(log);
				}
			}
			else if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
			{
				foreach (var prop in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
				{
					if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Null)
						continue; // bỏ qua null

					var log = prop.Value.Deserialize<APILogs>();
					if (log != null && int.TryParse(prop.Name, out var id))
						log.Id = id;
					if (log != null)
						result.Add(log);
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("[FirebaseLogService] Parse error: " + ex);
		}

		return result;
	}




	public async Task<IEnumerable<APILogs>> QueryLogsAsync(
		int? userId = null,
		string action = null,
		DateTime? from = null,
		DateTime? to = null)
	{
		var all = await GetRawLogsAsync();
		var query = all.AsQueryable();

		if (userId.HasValue)
			query = query.Where(l => l.UserId == userId.Value);
		if (!string.IsNullOrWhiteSpace(action))
			query = query.Where(l => l.Action != null && l.Action.Equals(action, StringComparison.OrdinalIgnoreCase));
		if (from.HasValue)
			query = query.Where(l => l.CreatedDate >= from.Value);
		if (to.HasValue)
			query = query.Where(l => l.CreatedDate <= to.Value);

		return query.OrderByDescending(l => l.ExpireAt).ToList();
	}

	public async Task<IEnumerable<APILogs>> GetExpiredLogsAsync()
	{
		var all = await GetRawLogsAsync();
		return all.Where(l => l.ExpireAt.HasValue && l.ExpireAt.Value < DateTime.UtcNow).ToList();
	}

	public async Task DeleteLogByIdAsync(int id)
	{
		await _firebaseClient
			.Child("logs")
			.Child(id.ToString())
			.DeleteAsync();
	}

	public async Task CleanupExpiredLogsAsync()
	{
		var expired = await GetExpiredLogsAsync();
		foreach (var log in expired)
			await DeleteLogByIdAsync(log.Id);
	}

	public async Task<bool> PingAsync()
	{
		try
		{
			// đọc counter để xác thực kết nối
			_ = await _firebaseClient.Child("logCounter").OnceSingleAsync<int?>();
			return true;
		}
		catch
		{
			return false;
		}
	}

	private async Task<string> GetUserNameByIdAsync(int userId)
	{
		var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
		return user?.UserName ?? "Unknown";
	}

	public async Task LogAsync(int userId, string action, string message, int statusCode,
		DateTime? createdDate = null, int? createdBy = null,
		DateTime? modifiedDate = null, int? modifiedBy = null,
		DateTime? removedDate = null, int? removedBy = null)
	{
		var userName = await GetUserNameByIdAsync(userId);

		var log = new APILogs
		{
			UserId = userId,
			UserName = userName,
			Action = action,
			Message = message,
			StatusCode = statusCode,
			CreatedDate = createdDate ?? DateTime.UtcNow,
			CreatedBy = createdBy,
			ModifiedDate = modifiedDate,
			ModifiedBy = modifiedBy,
			RemovedDate = removedDate,
			RemovedBy = removedBy,
			ExpireAt = DateTime.UtcNow.AddHours(1)
		};

		await LogToFirebase(log);
	}
}
