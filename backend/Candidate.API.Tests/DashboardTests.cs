using System.Net;
using System.Net.Http.Json;

namespace Candidate.API.Tests;

/// <summary>
/// The dashboard's record lists: every number on the summary must open onto exactly the
/// records it counted.
/// </summary>
public sealed class DashboardServiceTests : IDisposable
{
    private readonly TestDatabase _database = new(seedSampleData: true);
    private DateOnly Today => _database.Clock.Today;

    public void Dispose() => _database.Dispose();

    private async Task<T> WithServiceAsync<T>(Func<DashboardService, Task<T>> action)
    {
        await using var db = _database.CreateContext();
        return await action(new DashboardService(db, _database.Clock));
    }

    private Task<DashboardSummary> SummaryAsync() => WithServiceAsync(s => s.SummaryAsync());

    private Task<IReadOnlyList<ProviderListItem>> ProvidersAsync(DashboardProviderQuery query) =>
        WithServiceAsync(s => s.ProvidersAsync(query));

    private Task<IReadOnlyList<DashboardLicenseRow>> LicensesAsync(LicenseValidity? validity) =>
        WithServiceAsync(s => s.LicensesAsync(validity));

    [Fact]
    public async Task Total_cards_match_the_full_record_lists()
    {
        var summary = await SummaryAsync();

        Assert.Equal(summary.TotalProviders, (await ProvidersAsync(new DashboardProviderQuery())).Count);
        Assert.Equal(summary.TotalLicenses, (await LicensesAsync(null)).Count);
    }

    [Fact]
    public async Task Compliance_cards_match_the_providers_listed_for_each_gap()
    {
        var summary = await SummaryAsync();

        var noValid = await ProvidersAsync(new DashboardProviderQuery { LicenseGap = LicenseGap.NoValidLicense });
        var expired = await ProvidersAsync(new DashboardProviderQuery { LicenseGap = LicenseGap.ExpiredLicense });

        Assert.Equal(summary.ActiveProvidersWithoutValidLicense, noValid.Count);
        Assert.Equal(summary.ActiveProvidersWithExpiredLicense, expired.Count);

        Assert.Equal(
            ["Athens Bright Beginnings", "Marietta Montessori House", "Savannah Coastal Kids Academy"],
            noValid.Select(p => p.ProviderName));
        Assert.All(noValid, p => Assert.Equal(0, p.ValidLicenseCount));
        Assert.All(expired, p => Assert.True(p.ExpiredLicenseCount > 0));
        Assert.All(noValid.Concat(expired), p => Assert.Equal(ProviderStatus.Active, p.Status));
    }

    [Fact]
    public async Task Each_status_bar_matches_the_providers_listed_for_that_status()
    {
        var summary = await SummaryAsync();

        foreach (var bar in summary.ProvidersByStatus)
        {
            var status = Enum.Parse<ProviderStatus>(bar.Key);
            var listed = await ProvidersAsync(new DashboardProviderQuery { Status = status });

            Assert.Equal(bar.Count, listed.Count);
            Assert.All(listed, p => Assert.Equal(status, p.Status));
        }
    }

    [Fact]
    public async Task Each_county_bar_matches_the_providers_listed_for_that_county()
    {
        var summary = await SummaryAsync();
        Assert.NotEmpty(summary.ProvidersByCounty);

        foreach (var bar in summary.ProvidersByCounty)
        {
            var listed = await ProvidersAsync(new DashboardProviderQuery { County = bar.Key.ToUpperInvariant() });

            Assert.Equal(bar.Count, listed.Count);
            Assert.All(listed, p => Assert.Equal(bar.Name, p.County));
        }
    }

    [Fact]
    public async Task Each_license_slice_matches_the_licenses_listed_for_that_bucket()
    {
        // A stored status of Expired with a future date: the only way into the catch-all bucket.
        await _database.AddLicenseAsync(5, "CONTRADICTORY", LicenseStatus.Expired, Today.AddYears(1));
        var summary = await SummaryAsync();

        foreach (var slice in summary.LicensesByEffectiveStatus)
        {
            var listed = await LicensesAsync(Enum.Parse<LicenseValidity>(slice.Key));
            Assert.Equal(slice.Count, listed.Count);
        }

        Assert.Equal(summary.TotalLicenses, summary.LicensesByEffectiveStatus.Sum(s => s.Count));
        Assert.Equal("CONTRADICTORY", Assert.Single(await LicensesAsync(LicenseValidity.OtherNotValid)).LicenseNumber);
    }

    [Fact]
    public async Task License_lists_apply_the_shared_validity_rules()
    {
        var valid = await LicensesAsync(LicenseValidity.Valid);
        var expired = await LicensesAsync(LicenseValidity.Expired);
        var suspended = await LicensesAsync(LicenseValidity.Suspended);

        Assert.All(valid, l => Assert.True(l.IsCurrentlyValid));
        Assert.All(expired, l => Assert.True(l.IsExpired));
        Assert.All(suspended, l => Assert.Equal(LicenseStatus.Suspended, l.LicenseStatus));

        // Includes the license still stored as Active after its date passed.
        Assert.Contains(expired, l => l.LicenseStatus == LicenseStatus.Active);
        Assert.Contains(valid, l => l is { ProviderName: "Peachtree Early Learning Center", County: "Fulton" });
    }

    [Fact]
    public async Task Record_lists_never_include_soft_deleted_providers_or_their_licenses()
    {
        var providers = await ProvidersAsync(new DashboardProviderQuery());
        var licenses = await LicensesAsync(null);

        Assert.DoesNotContain(providers, p => p.ProviderId is 11 or 12);
        Assert.DoesNotContain(licenses, l => l.ProviderId == 11);
    }

    [Fact]
    public async Task Filters_combine()
    {
        var listed = await ProvidersAsync(new DashboardProviderQuery
        {
            Status = ProviderStatus.Active,
            County = "Chatham",
            LicenseGap = LicenseGap.ExpiredLicense
        });

        Assert.Equal("Savannah Coastal Kids Academy", Assert.Single(listed).ProviderName);
    }

    [Fact]
    public async Task Licenses_per_provider_lists_the_providers_with_the_most_licenses_first()
    {
        var summary = await SummaryAsync();
        var perProvider = summary.LicensesPerProvider;

        Assert.Equal(perProvider.OrderByDescending(p => p.LicenseCount).ThenBy(p => p.ProviderName), perProvider);
        Assert.All(perProvider, p => Assert.True(p.LicenseCount > 0));
        Assert.True(perProvider.Count <= DashboardService.TopProvidersByLicenses);
        Assert.DoesNotContain(perProvider, p => p.ProviderId == 11);

        var decatur = Assert.Single(perProvider, p => p.ProviderName == "Decatur Family Child Care");
        Assert.Equal(2, decatur.LicenseCount);
        Assert.Equal(1, decatur.ValidLicenseCount);
    }
}

public sealed class DashboardApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Providers_list_is_filtered_by_query_parameters()
    {
        var providers = await _client.GetFromJsonAsync<List<ProviderListItem>>(
            "/api/dashboard/providers?licenseGap=NoValidLicense", ApiFactory.Json);

        Assert.Contains(providers!, p => p.ProviderName == "Athens Bright Beginnings");
        Assert.DoesNotContain(providers!, p => p.ProviderName == "Peachtree Early Learning Center");
    }

    [Fact]
    public async Task Licenses_list_carries_the_provider_and_computed_validity()
    {
        var licenses = await _client.GetFromJsonAsync<List<DashboardLicenseRow>>(
            "/api/dashboard/licenses?validity=Suspended", ApiFactory.Json);

        var license = Assert.Single(licenses!);
        Assert.Equal("Marietta Montessori House", license.ProviderName);
        Assert.Equal("CCLC-10006", license.LicenseNumber);
        Assert.False(license.IsCurrentlyValid);
    }

    [Fact]
    public async Task Summary_gives_each_license_bucket_a_key_the_list_endpoint_accepts()
    {
        var summary = await _client.GetFromJsonAsync<DashboardSummary>("/api/dashboard/summary", ApiFactory.Json);

        foreach (var slice in summary!.LicensesByEffectiveStatus)
        {
            var response = await _client.GetAsync($"/api/dashboard/licenses?validity={slice.Key}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("/api/dashboard/licenses?validity=Sometimes")]
    [InlineData("/api/dashboard/providers?licenseGap=Unknown")]
    [InlineData("/api/dashboard/providers?status=Archived")]
    public async Task Unknown_filter_values_are_400(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
