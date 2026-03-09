using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace RiseFlow.Application;

/// <summary>
/// Resolves the current school (tenant) for the executing request.
/// Backed by the authenticated user's JWT claims or other request metadata.
/// </summary>
public interface ISchoolContext
{
    Guid SchoolId { get; }
}

/// <summary>
/// Default implementation that reads SchoolId from the current HttpContext user claims.
/// If the claim is missing or invalid, SchoolId is Guid.Empty, which causes tenant filters
/// to return no data by default (secure by design).
/// </summary>
public class SchoolContext : ISchoolContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SchoolContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid SchoolId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var claim = httpContext?.User.FindFirst("SchoolId")?.Value
                        ?? httpContext?.User.FindFirst(ClaimTypes.GroupSid)?.Value;

            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }
}

