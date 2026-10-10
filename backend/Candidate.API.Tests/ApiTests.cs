using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Candidate.API.Tests;

/// <summary>
/// End-to-end tests through HTTP: routes, status codes, and the JSON the React UI depends on.
/// The database starts with the sample data. Tests that write use their own provider names,
/// so they do not depend on the order they run in.
/// </summary>
public sealed class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly int Fulton = TestDatabase.CountyId("Fulton");
    private readonly HttpClient _client = factory.CreateClient();

    private static object NewProvider(string name, string status = "Active") =>
        new { providerName = name, countyId = Fulton, status };

    private async Task<ProviderDetail> CreateAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/providers", NewProvider(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProviderDetail>(ApiFactory.Json))!;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;
    }

    private static async Task<ValidationProblemDetails> ReadProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(ApiFactory.Json))!;
    }

    // Composition

    [Fact]
    public void Every_service_can_be_constructed_and_scopes_are_respected()
    {
        // ValidateOnBuild makes a missing or mis-scoped registration fail here, at startup,
        // instead of on the first request that happens to need it.
        using var validated = factory.WithWebHostBuilder(builder => builder.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        }));

        using var scope = validated.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProviderService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReportService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAuditService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ILookupService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDashboardService>());
    }

    // Listing and detail

    [Fact]
    public async Task List_returns_live_providers_and_never_a_soft_deleted_one()
    {
        var page = await ReadAsync<PagedResult<ProviderListItem>>(await _client.GetAsync("/api/providers?pageSize=100"));

        Assert.Contains(page.Items, p => p.ProviderName == "Peachtree Early Learning Center");
        Assert.DoesNotContain(page.Items, p => p.ProviderName == "Albany Tiny Tots");
        Assert.DoesNotContain(page.Items, p => p.ProviderId is 11 or 12);
        Assert.Equal(page.Items.Count, page.TotalCount);
    }

    [Fact]
    public async Task List_supports_search_status_and_county_filters_together()
    {
        var floyd = TestDatabase.CountyId("Floyd");

        var page = await ReadAsync<PagedResult<ProviderListItem>>(
            await _client.GetAsync($"/api/providers?search=hilltop&status=Pending&countyId={floyd}"));

        var provider = Assert.Single(page.Items);
        Assert.Equal(13, provider.ProviderId);
        Assert.Equal("Floyd", provider.County);
    }

    [Fact]
    public async Task Enums_and_dates_are_serialized_as_readable_strings()
    {
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/api/providers/2"));
        var root = document.RootElement;

        Assert.Equal("Active", root.GetProperty("status").GetString());
        Assert.Equal("2024-02-01T15:30:00Z", root.GetProperty("createdUtc").GetString());
        var license = root.GetProperty("licenses")[0];
        Assert.Equal("Expired", license.GetProperty("licenseStatus").GetString());
        Assert.Equal("2024-08-31", license.GetProperty("expirationDate").GetString());
    }

    [Fact]
    public async Task Detail_shows_license_validity_computed_from_the_expiration_date()
    {
        // Provider 3 is Active and its only license is stored as Active, but it expired in 2025.
        var detail = await ReadAsync<ProviderDetail>(await _client.GetAsync("/api/providers/3"));

        Assert.Equal(ProviderStatus.Active, detail.Status);
        Assert.False(detail.HasValidLicense);
        var license = Assert.Single(detail.Licenses);
        Assert.Equal(LicenseStatus.Active, license.LicenseStatus);
        Assert.True(license.IsExpired);
        Assert.False(license.IsCurrentlyValid);
    }

    [Fact]
    public async Task Licenses_endpoint_returns_the_same_licenses_as_the_detail()
    {
        var licenses = await ReadAsync<List<LicenseDto>>(await _client.GetAsync("/api/providers/1/licenses"));

        Assert.Equal(["CCLC-10002", "CCLC-10001"], licenses.Select(l => l.LicenseNumber));
        Assert.All(licenses, l => Assert.Equal(1, l.ProviderId));
    }

    [Theory]
    [InlineData("/api/providers/11")]
    [InlineData("/api/providers/11/licenses")]
    [InlineData("/api/providers/99999")]
    public async Task Soft_deleted_and_unknown_providers_are_both_404(string url)
    {
        var problem = await ReadProblemAsync(await _client.GetAsync(url), HttpStatusCode.NotFound);

        Assert.Equal(404, problem.Status);
        Assert.Contains("was not found", problem.Detail);
    }

    [Theory]
    [InlineData("/api/providers?pageSize=101")]
    [InlineData("/api/providers?page=0")]
    [InlineData("/api/providers?status=Archived")]
    [InlineData("/api/providers?countyId=abc")]
    public async Task Invalid_query_parameters_are_400(string url)
    {
        var problem = await ReadProblemAsync(await _client.GetAsync(url), HttpStatusCode.BadRequest);

        Assert.NotEmpty(problem.Errors);
    }

    // Create

    [Fact]
    public async Task Create_returns_201_with_a_location_that_resolves_to_the_new_provider()
    {
        var response = await _client.PostAsJsonAsync("/api/providers", NewProvider("Api Created Provider", "Pending"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ProviderDetail>(ApiFactory.Json))!;
        Assert.Equal("Api Created Provider", created.ProviderName);
        Assert.Equal(ProviderStatus.Pending, created.Status);
        Assert.Equal(AnonymousCurrentUser.Placeholder, created.CreatedBy);
        Assert.Empty(created.Licenses);

        var fetched = await ReadAsync<ProviderDetail>(await _client.GetAsync(response.Headers.Location));
        Assert.Equal(created.ProviderId, fetched.ProviderId);
    }

    [Fact]
    public async Task Create_with_invalid_input_returns_400_with_a_message_per_field()
    {
        var response = await _client.PostAsJsonAsync("/api/providers", new { providerName = "  ", countyId = 0, status = "Deleted" });

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);

        // Keys are the JSON property names the UI sent, so it can show each message beside its input.
        Assert.Equal(["countyId", "providerName", "status"], problem.Errors.Keys.Order());
        Assert.Equal(["Provider name is required."], problem.Errors["providerName"]);
    }

    [Fact]
    public async Task Create_with_an_unknown_county_returns_400_on_the_county_field()
    {
        var response = await _client.PostAsJsonAsync("/api/providers", new { providerName = "Unknown County", countyId = 9999, status = "Active" });

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);

        Assert.Equal(["countyId"], problem.Errors.Keys);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("""{"providerName":"Wrong Type","countyId":"sixty","status":"Active"}""")]
    public async Task Create_with_a_malformed_body_returns_400_not_500(string body)
    {
        var response = await _client.PostAsync("/api/providers", new StringContent(body, Encoding.UTF8, "application/json"));

        await ReadProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_duplicate_returns_409()
    {
        var response = await _client.PostAsJsonAsync("/api/providers", NewProvider("PEACHTREE early learning center"));

        var problem = await ReadProblemAsync(response, HttpStatusCode.Conflict);

        Assert.Contains("already exists in this county", problem.Detail);
    }

    // Update

    [Fact]
    public async Task Update_returns_the_saved_provider_with_a_new_concurrency_stamp()
    {
        var created = await CreateAsync("Api Update Before");

        var response = await _client.PutAsJsonAsync($"/api/providers/{created.ProviderId}", new
        {
            providerName = "Api Update After",
            countyId = TestDatabase.CountyId("Cobb"),
            status = "Inactive",
            concurrencyStamp = created.ConcurrencyStamp
        });

        var updated = await ReadAsync<ProviderDetail>(response);
        Assert.Equal("Api Update After", updated.ProviderName);
        Assert.Equal("Cobb", updated.County);
        Assert.Equal(ProviderStatus.Inactive, updated.Status);
        Assert.NotNull(updated.ModifiedUtc);
        Assert.NotEqual(created.ConcurrencyStamp, updated.ConcurrencyStamp);
    }

    [Fact]
    public async Task Update_with_a_stale_stamp_returns_409()
    {
        var created = await CreateAsync("Api Stale Stamp");
        var body = new { providerName = "Api Stale Stamp Edited", countyId = Fulton, status = "Active", concurrencyStamp = created.ConcurrencyStamp };
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/providers/{created.ProviderId}", body)).StatusCode);

        var second = await _client.PutAsJsonAsync($"/api/providers/{created.ProviderId}", body);

        var problem = await ReadProblemAsync(second, HttpStatusCode.Conflict);
        Assert.Contains("changed by someone else", problem.Detail);
    }

    [Fact]
    public async Task Update_without_a_stamp_or_with_invalid_fields_returns_400()
    {
        var created = await CreateAsync("Api Update Invalid");

        var response = await _client.PutAsJsonAsync(
            $"/api/providers/{created.ProviderId}", new { providerName = "", countyId = Fulton, status = "Active" });

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(["concurrencyStamp", "providerName"], problem.Errors.Keys.Order());
    }

    [Fact]
    public async Task Update_of_a_soft_deleted_provider_returns_404()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/providers/12", new { providerName = "Revived", countyId = Fulton, status = "Active", concurrencyStamp = "seed-provider-12" });

        await ReadProblemAsync(response, HttpStatusCode.NotFound);
    }

    // Delete

    [Fact]
    public async Task Delete_soft_deletes_then_the_provider_is_gone_from_standard_routes_and_present_in_audit()
    {
        var created = await CreateAsync("Api Delete Me");

        var delete = await _client.DeleteAsync($"/api/providers/{created.ProviderId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/providers/{created.ProviderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/providers/{created.ProviderId}")).StatusCode);

        var list = await ReadAsync<PagedResult<ProviderListItem>>(await _client.GetAsync("/api/providers?search=Api%20Delete%20Me"));
        Assert.Empty(list.Items);

        var audit = await ReadAsync<List<DeletedProviderDto>>(await _client.GetAsync("/api/audit/providers/deleted"));
        var row = Assert.Single(audit, d => d.ProviderId == created.ProviderId);
        Assert.Equal("Api Delete Me", row.ProviderName);
        Assert.Equal(AnonymousCurrentUser.Placeholder, row.DeletedBy);
        Assert.NotNull(row.DeletedUtc);

        // The name is free to use again.
        await CreateAsync("Api Delete Me");
    }

    // Reports, audit, lookups, dashboard

    [Fact]
    public async Task Scenario_1_report_returns_active_providers_with_valid_licenses()
    {
        var report = await ReadAsync<ProviderLicenseReport>(await _client.GetAsync("/api/reports/active-providers-active-licenses"));

        Assert.Equal(factory.Clock.Today, report.AsOfDate);
        var peachtree = Assert.Single(report.Items, row => row.ProviderId == 1);
        Assert.Equal(2, peachtree.Licenses.Count);
        Assert.DoesNotContain(report.Items, row => row.ProviderId is 6 or 10 or 11);
    }

    [Fact]
    public async Task Scenario_2_report_returns_active_providers_with_expired_licenses()
    {
        var report = await ReadAsync<ProviderLicenseReport>(await _client.GetAsync("/api/reports/active-providers-expired-licenses"));

        Assert.Contains(report.Items, row => row.ProviderId == 3 && row.HasNoValidLicense);
        Assert.Contains(report.Items, row => row.ProviderId == 2 && !row.HasNoValidLicense);
        Assert.DoesNotContain(report.Items, row => row.ProviderId is 1 or 6 or 11);
        Assert.All(report.Items.SelectMany(row => row.Licenses), license => Assert.True(license.IsExpired));
    }

    [Fact]
    public async Task Audit_endpoint_returns_the_seeded_soft_deleted_providers()
    {
        var audit = await ReadAsync<List<DeletedProviderDto>>(await _client.GetAsync("/api/audit/providers/deleted"));

        Assert.Contains(audit, d => d.ProviderId == 11 && d.LicenseCount == 1);
        Assert.Contains(audit, d => d.ProviderId == 12);
    }

    [Fact]
    public async Task Lookups_supply_the_dropdown_values()
    {
        var counties = await ReadAsync<List<CountyDto>>(await _client.GetAsync("/api/lookups/counties"));
        var statuses = await ReadAsync<StatusLookups>(await _client.GetAsync("/api/lookups/statuses"));

        Assert.Equal(159, counties.Count);
        Assert.Equal("Appling", counties[0].Name);
        Assert.Equal(["Active", "Inactive", "Pending"], statuses.ProviderStatuses);
        Assert.Equal(["Active", "Expired", "Suspended"], statuses.LicenseStatuses);
    }

    [Fact]
    public async Task Dashboard_summary_is_computed_by_the_api()
    {
        var summary = await ReadAsync<DashboardSummary>(await _client.GetAsync("/api/dashboard/summary"));

        Assert.Equal(summary.TotalProviders, summary.ProvidersByStatus.Sum(c => c.Count));
        Assert.Equal(summary.TotalLicenses, summary.LicensesByEffectiveStatus.Sum(c => c.Count));
        Assert.True(summary.ActiveProvidersWithExpiredLicense >= 3);
        Assert.Equal(DashboardService.ExpiringWindowDays, summary.ExpiringWindowDays);
    }

    [Fact]
    public async Task Unknown_route_returns_a_problem_document()
    {
        await ReadProblemAsync(await _client.GetAsync("/api/does-not-exist"), HttpStatusCode.NotFound);
    }
}
