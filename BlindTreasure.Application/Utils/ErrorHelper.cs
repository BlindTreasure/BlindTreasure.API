namespace BlindTreasure.Application.Utils;

public static class ErrorHelper
{
    public static Exception WithStatus(int statusCode, string message)
    {
        var ex = new Exception(message);
        ex.Data["StatusCode"] = statusCode;
        return ex;
    }

    public static Exception Unauthorized(string message = "Không được phép.")
        => WithStatus(401, message);

    public static Exception NotFound(string message = "Không tìm thấy tài nguyên.")
        => WithStatus(404, message);

    public static Exception BadRequest(string message = "Dữ liệu không hợp lệ.")
        => WithStatus(400, message);

    public static Exception Forbidden(string message = "Truy cập bị từ chối.")
        => WithStatus(403, message);

    public static Exception Conflict(string message = "Xung đột dữ liệu.")
        => WithStatus(409, message);

    public static Exception Internal(string message = "Lỗi hệ thống.")
        => WithStatus(500, message);
}
