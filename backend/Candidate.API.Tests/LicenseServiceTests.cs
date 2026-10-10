namespace Candidate.API.Tests;

/// <summary>Issuing a license: who may receive one, the generated number, and the date rules.</summary>
public sealed class LicenseServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private DateOnly Today => _database.Clock.Today;

    public void Dispose() => _database.Dispose();

    private async Task<ServiceResult<LicenseDto>> IssueAsync(int providerId, DateOnly? issueDate = null, DateOnly? expirationDate = null)
    {
        await using var db = _database.CreateContext();
        return await new LicenseService(db, _database.Clock).IssueAsync(
            providerId, new IssueLicenseRequest { IssueDate = issueDate, ExpirationDate = expirationDate });
    }

    [Fact]
    public async Task Issue_creates_an_active_license_with_a_number_generated_from_its_id()
    {
        var provider = await _database.AddProviderAsync("No Licenses Yet");

        var result = await IssueAsync(provider.ProviderId, Today, Today.AddYears(2));

        Assert.True(result.IsSuccess);
        var issued = result.Value!;
        Assert.Equal($"CCLC-{10_000 + issued.LicenseId}", issued.LicenseNumber);
        Assert.Equal(LicenseStatus.Active, issued.LicenseStatus);
        Assert.Equal(Today, issued.IssueDate);
        Assert.Equal(Today.AddYears(2), issued.ExpirationDate);
        Assert.True(issued.IsCurrentlyValid);

        await using var db = _database.CreateContext();
        var stored = await db.Licenses.SingleAsync();
        Assert.Equal(issued.LicenseNumber, stored.LicenseNumber);
        Assert.Equal(provider.ProviderId, stored.ProviderId);
        Assert.Equal(TestUser.UserName, stored.CreatedBy);
        Assert.Equal(_database.Clock.UtcNow, stored.CreatedUtc);
        // Assigning the number is part of creating the row, not a later modification.
        Assert.Null(stored.ModifiedUtc);
    }

    [Fact]
    public async Task Issue_defaults_to_today_and_a_one_year_term()
    {
        var provider = await _database.AddProviderAsync("Defaults");

        var issued = (await IssueAsync(provider.ProviderId)).Value!;

        Assert.Equal(Today, issued.IssueDate);
        Assert.Equal(Today.AddYears(1), issued.ExpirationDate);
    }

    [Fact]
    public async Task Issue_defaults_the_expiration_to_one_year_after_a_backdated_issue_date()
    {
        var provider = await _database.AddProviderAsync("Backdated");
        var issueDate = Today.AddMonths(-3);

        var issued = (await IssueAsync(provider.ProviderId, issueDate)).Value!;

        Assert.Equal(issueDate.AddYears(1), issued.ExpirationDate);
    }

    [Fact]
    public async Task Issued_numbers_are_unique_across_providers()
    {
        var first = await _database.AddProviderAsync("First");
        var second = await _database.AddProviderAsync("Second", county: "DeKalb");

        var a = (await IssueAsync(first.ProviderId)).Value!;
        var b = (await IssueAsync(second.ProviderId)).Value!;

        Assert.NotEqual(a.LicenseNumber, b.LicenseNumber);
    }

    [Theory]
    [InlineData(LicenseStatus.Active, -1)]      // expired by date, status never updated
    [InlineData(LicenseStatus.Expired, -400)]   // expired
    [InlineData(LicenseStatus.Suspended, 365)]  // suspended: not valid
    public async Task Issue_is_allowed_when_the_provider_has_licenses_but_none_is_valid(LicenseStatus status, int daysFromToday)
    {
        var provider = await _database.AddProviderAsync("Needs A License");
        await _database.AddLicenseAsync(provider.ProviderId, "OLD", status, Today.AddDays(daysFromToday));

        var result = await IssueAsync(provider.ProviderId);

        Assert.True(result.IsSuccess);
        await using var db = _database.CreateContext();
        Assert.Equal(2, await db.Licenses.CountAsync());
    }

    [Fact]
    public async Task Issue_is_a_conflict_when_the_provider_already_holds_a_valid_license()
    {
        var provider = await _database.AddProviderAsync("Already Licensed");
        await _database.AddLicenseAsync(provider.ProviderId, "CURRENT", LicenseStatus.Active, Today);

        var result = await IssueAsync(provider.ProviderId);

        Assert.Equal(ServiceErrorKind.Conflict, result.Error?.Kind);
        Assert.Contains("CURRENT", result.Error!.Message);
        await using var db = _database.CreateContext();
        Assert.Equal(1, await db.Licenses.CountAsync());
    }

    [Fact]
    public async Task Issue_twice_in_a_row_is_a_conflict_the_second_time()
    {
        var provider = await _database.AddProviderAsync("Double Click");

        Assert.True((await IssueAsync(provider.ProviderId)).IsSuccess);

        Assert.Equal(ServiceErrorKind.Conflict, (await IssueAsync(provider.ProviderId)).Error?.Kind);
    }

    [Fact]
    public async Task Issue_to_a_soft_deleted_or_unknown_provider_is_not_found()
    {
        var provider = await _database.AddProviderAsync("Deleted");
        await _database.SoftDeleteProviderAsync(provider.ProviderId);

        Assert.Equal(ServiceErrorKind.NotFound, (await IssueAsync(provider.ProviderId)).Error?.Kind);
        Assert.Equal(ServiceErrorKind.NotFound, (await IssueAsync(98765)).Error?.Kind);
    }

    [Theory]
    // issue date offset, expiration offset (days from today), field in error
    [InlineData(1, 400, "issueDate")]            // issued in the future
    [InlineData(0, 0, "expirationDate")]         // expires the day it is issued
    [InlineData(0, 364, "expirationDate")]       // one day short of a one-year term
    [InlineData(-10, -20, "expirationDate")]     // expires before it is issued
    [InlineData(-400, -30, "expirationDate")]    // already expired
    public async Task Issue_rejects_invalid_dates_and_stores_nothing(int issueOffset, int expirationOffset, string field)
    {
        var provider = await _database.AddProviderAsync("Bad Dates");

        var result = await IssueAsync(provider.ProviderId, Today.AddDays(issueOffset), Today.AddDays(expirationOffset));

        Assert.Equal(ServiceErrorKind.Validation, result.Error?.Kind);
        Assert.Equal([field], result.Error!.FieldErrors!.Keys);
        await using var db = _database.CreateContext();
        Assert.Equal(0, await db.Licenses.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Issue_accepts_a_term_of_exactly_one_year_and_longer()
    {
        var exact = await _database.AddProviderAsync("Exactly One Year");
        var longer = await _database.AddProviderAsync("Three Years", county: "DeKalb");

        Assert.True((await IssueAsync(exact.ProviderId, Today, Today.AddYears(1))).IsSuccess);
        Assert.True((await IssueAsync(longer.ProviderId, Today, Today.AddYears(3))).IsSuccess);
    }

    [Fact]
    public async Task Issue_rejection_explains_the_minimum_term()
    {
        var provider = await _database.AddProviderAsync("Short Term");

        var result = await IssueAsync(provider.ProviderId, Today, Today.AddMonths(6));

        Assert.Equal("Expiration date must be at least one year after the issue date.", result.Error!.Message);
    }

    [Fact]
    public async Task Issue_accepts_a_backdated_license_that_expires_today()
    {
        var provider = await _database.AddProviderAsync("Expires Today");

        var result = await IssueAsync(provider.ProviderId, Today.AddYears(-1), Today);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsCurrentlyValid);
    }

    [Fact]
    public async Task Issue_rolls_back_when_the_generated_number_is_already_used_by_this_provider()
    {
        var provider = await _database.AddProviderAsync("Number Taken");
        // The next identity value will be 2, so the generated number will collide with this one.
        await _database.AddLicenseAsync(provider.ProviderId, LicenseService.FormatNumber(2), LicenseStatus.Expired, Today.AddYears(-1));

        var result = await IssueAsync(provider.ProviderId);

        Assert.Equal(ServiceErrorKind.Conflict, result.Error?.Kind);
        await using var db = _database.CreateContext();
        Assert.Equal(1, await db.Licenses.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task An_issued_license_moves_the_provider_out_of_the_expired_report_gap()
    {
        var provider = await _database.AddProviderAsync("Renewed");
        await _database.AddLicenseAsync(provider.ProviderId, "OLD", LicenseStatus.Active, Today.AddDays(-1));

        await IssueAsync(provider.ProviderId);

        await using var db = _database.CreateContext();
        var reports = new ReportService(db, _database.Clock);
        Assert.False(Assert.Single((await reports.ActiveProvidersWithActiveLicensesAsync()).Items).HasNoValidLicense);
        // Still listed for its expired license, but no longer flagged as having nothing valid.
        Assert.False(Assert.Single((await reports.ActiveProvidersWithExpiredLicensesAsync()).Items).HasNoValidLicense);
    }
}
