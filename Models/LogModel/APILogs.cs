namespace TripWiseAPI.Models.LogModel
{
	public class APILogs
	{
		public int Id { get; set; }
		public int UserId { get; set; }
		public string UserName { get; set; }
		public string Action { get; set; }
		public string Message { get; set; }
		public int StatusCode { get; set; }
		public DateTime? CreatedDate { get; set; }
		public DateTime? ModifiedDate { get; set; }
		public DateTime? RemovedDate { get; set; }
		public int? CreatedBy { get; set; }
		public int? ModifiedBy { get; set; }
		public int? RemovedBy { get; set; }
		public DateTime ExpireAt { get; set; } 
	}

}
