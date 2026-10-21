using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Sample.WebApi.Infrastructure;

/// <summary>
/// 將已知的唯一值與關聯限制衝突轉成 409，不將 SQL 原始訊息傳回用戶端。
/// 其他例外交由 ASP.NET Core 的預設錯誤處理回應。
/// </summary>
public sealed class SqlConstraintExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;

    public SqlConstraintExceptionHandler(IProblemDetailsService problemDetailsService)
    {
        this._problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not SqlException sqlException)
        {
            return false;
        }

        var title = sqlException.Number switch
        {
            2601 or 2627 => "公司名稱已存在",
            547 => "資料關聯限制衝突",
            _ => null
        };

        if (title is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        // 沒有支援 Accept 的 writer 時仍保留 409，以空本文回應，避免錯誤處理再次拋出例外。
        await this._problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = title,
                Detail = sqlException.Number is 2601 or 2627
                    ? "請使用不同的公司名稱。"
                    : "此操作違反資料關聯限制，請先處理相關訂單。",
                Instance = httpContext.Request.Path
            }
        });
        return true;
    }
}
