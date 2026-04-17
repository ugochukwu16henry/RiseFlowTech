namespace RiseFlow.Api.Models;

public record MarketingLeadDto(Guid Id, string Email, string Source, DateTime CreatedAtUtc);

public record SubmitMarketingLeadRequest(string? Email);
