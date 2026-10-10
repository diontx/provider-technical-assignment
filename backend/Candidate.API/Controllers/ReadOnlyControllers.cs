using Candidate.API.Contracts;
using Candidate.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Candidate.API.Controllers;

/// <summary>The required license scenarios. Both evaluate "today" in US Eastern time.</summary>
[Route("api/reports")]
public sealed class ReportsController(IReportService reports) : ApiControllerBase
{
    /// <summary>Scenario 1: active providers and their currently valid licenses.</summary>
    [HttpGet("active-providers-active-licenses")]
    public async Task<ActionResult<ProviderLicenseReport>> ActiveProvidersWithActiveLicenses(CancellationToken cancellationToken) =>
        Ok(await reports.ActiveProvidersWithActiveLicensesAsync(cancellationToken));

    /// <summary>Scenario 2: providers that appear active but hold at least one expired license.</summary>
    [HttpGet("active-providers-expired-licenses")]
    public async Task<ActionResult<ProviderLicenseReport>> ActiveProvidersWithExpiredLicenses(CancellationToken cancellationToken) =>
        Ok(await reports.ActiveProvidersWithExpiredLicensesAsync(cancellationToken));
}

/// <summary>
/// Audit and troubleshooting. This is the only route that returns soft-deleted records.
/// It has no authorization because the assignment has no authentication; in production it
/// would be restricted to an auditor or support role.
/// </summary>
[Route("api/audit")]
public sealed class AuditController(IAuditService audit) : ApiControllerBase
{
    [HttpGet("providers/deleted")]
    public async Task<ActionResult<IReadOnlyList<DeletedProviderDto>>> DeletedProviders(CancellationToken cancellationToken) =>
        Ok(await audit.DeletedProvidersAsync(cancellationToken));
}

[Route("api/lookups")]
public sealed class LookupsController(ILookupService lookups) : ApiControllerBase
{
    [HttpGet("counties")]
    public async Task<ActionResult<IReadOnlyList<CountyDto>>> Counties(CancellationToken cancellationToken) =>
        Ok(await lookups.CountiesAsync(cancellationToken));

    [HttpGet("statuses")]
    public ActionResult<StatusLookups> Statuses() => Ok(lookups.Statuses());
}

[Route("api/dashboard")]
public sealed class DashboardController(IDashboardService dashboard) : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummary>> Summary(CancellationToken cancellationToken) =>
        Ok(await dashboard.SummaryAsync(cancellationToken));

    /// <summary>The providers behind a dashboard number. Filters: status, county (name), licenseGap.</summary>
    [HttpGet("providers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProviderListItem>>> Providers(
        [FromQuery] DashboardProviderQuery query, CancellationToken cancellationToken) =>
        Ok(await dashboard.ProvidersAsync(query, cancellationToken));

    /// <summary>The licenses behind a dashboard number. Omit validity to list every license.</summary>
    [HttpGet("licenses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<DashboardLicenseRow>>> Licenses(
        [FromQuery] LicenseValidity? validity, CancellationToken cancellationToken) =>
        Ok(await dashboard.LicensesAsync(validity, cancellationToken));
}
