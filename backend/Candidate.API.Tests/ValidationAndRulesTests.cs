using System.ComponentModel.DataAnnotations;

namespace Candidate.API.Tests;

/// <summary>Request validation attributes: what ASP.NET Core rejects with 400 before a controller action runs.</summary>
public sealed class RequestValidationTests
{
    private static IReadOnlyList<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    private static string[] InvalidMembers(object request) =>
        Validate(request).SelectMany(r => r.MemberNames).Distinct().Order().ToArray();

    [Theory]
    [InlineData("Active")]
    [InlineData("inactive")]
    [InlineData("PENDING")]
    public void Valid_create_request_passes(string status)
    {
        var request = new CreateProviderRequest { ProviderName = "Sunrise Academy", CountyId = 60, Status = status };

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void Empty_create_request_reports_every_required_field()
    {
        Assert.Equal(["CountyId", "ProviderName", "Status"], InvalidMembers(new CreateProviderRequest()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void Provider_name_is_required(string? name)
    {
        var request = new CreateProviderRequest { ProviderName = name, CountyId = 60, Status = "Active" };

        var error = Assert.Single(Validate(request));
        Assert.Equal("Provider name is required.", error.ErrorMessage);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void Provider_name_is_limited_to_200_characters(int length, bool valid)
    {
        var request = new CreateProviderRequest { ProviderName = new string('a', length), CountyId = 60, Status = "Active" };

        Assert.Equal(valid, Validate(request).Count == 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void County_must_be_a_positive_id(int countyId)
    {
        var request = new CreateProviderRequest { ProviderName = "Name", CountyId = countyId, Status = "Active" };

        Assert.Equal(["CountyId"], InvalidMembers(request));
    }

    [Theory]
    [InlineData("Deleted")]
    [InlineData("Suspended")]
    [InlineData("0")]
    [InlineData("Active,Pending")]
    public void Status_must_be_a_provider_status_name(string status)
    {
        var request = new CreateProviderRequest { ProviderName = "Name", CountyId = 60, Status = status };

        var error = Assert.Single(Validate(request));
        Assert.Equal("Status must be one of: Active, Inactive, Pending.", error.ErrorMessage);
    }

    [Fact]
    public void Update_request_also_requires_the_concurrency_stamp()
    {
        var request = new UpdateProviderRequest { ProviderName = "Name", CountyId = 60, Status = "Active" };

        Assert.Equal(["ConcurrencyStamp"], InvalidMembers(request));
    }

    [Theory]
    [InlineData(0, 20, "Page")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, 101, "PageSize")]
    public void Paging_values_are_bounded(int page, int pageSize, string invalidMember)
    {
        Assert.Equal([invalidMember], InvalidMembers(new ProviderQuery { Page = page, PageSize = pageSize }));
    }

    [Fact]
    public void Default_query_is_valid()
    {
        Assert.Empty(Validate(new ProviderQuery()));
    }
}

public sealed class LicenseRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 10);

    private static License License(LicenseStatus status, int daysFromToday) =>
        new() { LicenseStatus = status, ExpirationDate = Today.AddDays(daysFromToday) };

    [Theory]
    // stored status, days until expiration, expired?, valid?, effective status
    [InlineData(LicenseStatus.Active, 365, false, true, LicenseStatus.Active)]
    [InlineData(LicenseStatus.Active, 0, false, true, LicenseStatus.Active)]
    [InlineData(LicenseStatus.Active, -1, true, false, LicenseStatus.Expired)]
    [InlineData(LicenseStatus.Suspended, 365, false, false, LicenseStatus.Suspended)]
    [InlineData(LicenseStatus.Suspended, -1, true, false, LicenseStatus.Expired)]
    [InlineData(LicenseStatus.Expired, -1, true, false, LicenseStatus.Expired)]
    [InlineData(LicenseStatus.Expired, 365, false, false, LicenseStatus.Expired)]
    public void Validity_combines_stored_status_and_expiration_date(
        LicenseStatus stored, int daysFromToday, bool expired, bool valid, LicenseStatus effective)
    {
        var license = License(stored, daysFromToday);

        Assert.Equal(expired, LicenseRules.IsExpired(license, Today));
        Assert.Equal(valid, LicenseRules.IsCurrentlyValid(license, Today));
        Assert.Equal(effective, LicenseRules.EffectiveStatus(license, Today));
    }

    [Theory]
    [InlineData(LicenseStatus.Active, 0)]
    [InlineData(LicenseStatus.Active, -1)]
    [InlineData(LicenseStatus.Suspended, 10)]
    [InlineData(LicenseStatus.Expired, 10)]
    public void Sql_expressions_and_in_memory_rules_agree(LicenseStatus stored, int daysFromToday)
    {
        var license = License(stored, daysFromToday);

        Assert.Equal(LicenseRules.IsExpired(license, Today), LicenseRules.ExpiredOn(Today).Compile()(license));
        Assert.Equal(LicenseRules.IsCurrentlyValid(license, Today), LicenseRules.ValidOn(Today).Compile()(license));
    }
}

public sealed class EasternClockTests
{
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    [Theory]
    // 02:30 UTC is still the previous evening in Georgia (UTC-4 in summer, UTC-5 in winter).
    [InlineData("2026-10-11T02:30:00Z", "2026-10-10")]
    [InlineData("2026-10-11T04:00:00Z", "2026-10-11")]
    [InlineData("2026-01-01T04:59:00Z", "2025-12-31")]
    [InlineData("2026-01-01T05:00:00Z", "2026-01-01")]
    public void Business_date_is_the_us_eastern_date_not_the_utc_date(string utcNow, string expectedDate)
    {
        var instant = DateTimeOffset.Parse(utcNow);
        var clock = new EasternClock(new FixedTimeProvider(instant));

        Assert.Equal(DateOnly.Parse(expectedDate), clock.Today);
        Assert.Equal(instant.UtcDateTime, clock.UtcNow);
        Assert.Equal(DateTimeKind.Utc, clock.UtcNow.Kind);
    }
}

public sealed class ConnectionStringTests
{
    [Fact]
    public void Relative_database_path_is_resolved_against_the_content_root()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"provider-tests-{Guid.NewGuid():N}");
        try
        {
            var resolved = DatabaseInitializer.ResolveConnectionString("Data Source=Database/Data/providers.db", contentRoot);

            var expected = Path.Combine(contentRoot, "Database", "Data", "providers.db");
            Assert.Equal($"Data Source={expected}", resolved);
            Assert.True(Directory.Exists(Path.GetDirectoryName(expected)));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=file:shared?mode=memory&cache=shared")]
    public void In_memory_databases_are_left_alone(string connectionString)
    {
        Assert.Equal(connectionString, DatabaseInitializer.ResolveConnectionString(connectionString, Path.GetTempPath()));
    }
}
