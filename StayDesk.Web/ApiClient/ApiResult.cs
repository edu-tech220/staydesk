namespace StayDesk.Web.ApiClient;

public class ApiResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public int StatusCode { get; init; }

    public static ApiResult<T> Success(T data, int statusCode) => new() { IsSuccess = true, Data = data, StatusCode = statusCode };

    public static ApiResult<T> Failure(string errorMessage, int statusCode) => new() { IsSuccess = false, ErrorMessage = errorMessage, StatusCode = statusCode };
}
