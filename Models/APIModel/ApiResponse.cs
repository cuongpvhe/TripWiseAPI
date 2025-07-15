namespace TripWiseAPI.Models.APIModel;

public class ApiResponse<T>
{
    public int StatusCode { get; set; }
    public string Message { get; set; }
    public T Data { get; set; }
    public bool IsSuccess { get; set; }
    public ApiResponse(T data, string message = "Request successful")
    {
        StatusCode = 200;
        IsSuccess = true;
        Message = message;
        Data = data;
    }
    public ApiResponse(int statusCode, string message, T data)
    {
        StatusCode = statusCode;
        Message = message;
        Data = data;
    }

    public ApiResponse(T data)
    {
        StatusCode = 200;
        IsSuccess = true;
        Message = "Request successful";
        Data = data;
    }

    public ApiResponse(int statusCode, string message)
    {
        StatusCode = statusCode;
        IsSuccess = false;
        Message = message;
        Data = default;
    }
}