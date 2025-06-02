namespace TripWiseAPI.Messages
{
    public class MessageSender
    {
        // Indicates if the operation was successful
        public bool IsSuccess { get; set; }

        // Holds a success message
        public string? SuccessMessage { get; set; }

        // Holds an error message, if any
        public string? ErrorMessage { get; set; }

        // Optional: error code for specific error identification
        public int? ErrorCode { get; set; }

        public DateTime Timestamp { get; set; }

        // Constructor for a success response
        public MessageSender(string successMessage)
        {
            IsSuccess = true;
            SuccessMessage = successMessage;
            Timestamp = DateTime.Now;
        }

        // Constructor for an error response
        public MessageSender(string errorMessage, int? errorCode = null)
        {
            IsSuccess = false;
            ErrorMessage = errorMessage;
            ErrorCode = errorCode;
            Timestamp = DateTime.Now;
        }

        // Factory method for success
        public static MessageSender Success(string message) => new MessageSender(message);

        // Factory method for error
        public static MessageSender Error(string message, int? errorCode = null) => new MessageSender(message, errorCode);
    }
}
