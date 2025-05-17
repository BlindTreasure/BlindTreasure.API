﻿namespace BlindTreasure.Application.Utils;

public static class ErrorHelper
{
    public static Exception WithStatus(int statusCode, string message)
    {
        var ex = new Exception(message);
        ex.Data["StatusCode"] = statusCode;
        return ex;
    }

    public static Exception Unauthorized(string message = "Không được phép.")
    {
        return WithStatus(401, message);
    }

    public static Exception NotFound(string message = "Không tìm thấy tài nguyên.")
    {
        return WithStatus(404, message);
    }

    public static Exception BadRequest(string message = "Dữ liệu không hợp lệ.")
    {
        return WithStatus(400, message);
    }

    public static Exception Forbidden(string message = "Truy cập bị từ chối.")
    {
        return WithStatus(403, message);
    }

    public static Exception Conflict(string message = "Xung đột dữ liệu.")
    {
        return WithStatus(409, message);
    }

    public static Exception Internal(string message = "Lỗi hệ thống.")
    {
        return WithStatus(500, message);
    }
}