using Microsoft.Data.Sqlite;

namespace Candidate.API.Tests;

/// <summary>Business rules for creating, editing, and listing providers.</summary>
public sealed class ProviderServiceTests : IDisposable
{
    private static readonly int Fulton = TestDatabase.CountyId("Fulton");
    private static readonly int DeKalb = TestDatabase.CountyId("DeKalb");
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private static CreateProviderRequest NewRequest(string name, int? countyId = null, string status = "Active") =>
        new() { ProviderName = name, CountyId = countyId ?? Fulton, Status = status };

    private static UpdateProviderRequest EditRequest(ProviderDetail current, string? name = null, string? stamp = null) => new()
    {
        ProviderName = name ?? current.ProviderName,
        CountyId = current.CountyId,
        Status = current.Status.ToString(),
        ConcurrencyStamp = stamp ?? current.ConcurrencyStamp
    };

    private async Task<ServiceResult<ProviderDetail>> CreateAsync(CreateProviderRequest request)
    {
        await using var db = _database.CreateContext();
        return await new ProviderService(db, _database.Clock).CreateAsync(request);
    }

    private async Task<ServiceResult<ProviderDetail>> UpdateAsync(int providerId, UpdateProviderRequest request)
    {
        await using var db = _database.CreateContext();
        return await new ProviderService(db, _database.Clock).UpdateAsync(providerId, request);
    }

    private async Task<PagedResult<ProviderListItem>> ListAsync(ProviderQuery query)
    {
        await using var db = _database.CreateContext();
        return await new ProviderService(db, _database.Clock).ListAsync(query);
    }

    // Create

    [Fact]
    public async Task Create_trims_the_name_and_stamps_the_audit_columns()
    {
        var result = await CreateAsync(NewRequest("  Sunrise Academy  ", status: "pending"));

        Assert.True(result.IsSuccess);
        var created = result.Value!;
        Assert.Equal("Sunrise Academy", created.ProviderName);
        Assert.Equal("Fulton", created.County);
        Assert.Equal(ProviderStatus.Pending, created.Status);
        Assert.Equal(_database.Clock.UtcNow, created.CreatedUtc);
        Assert.Equal(DateTimeKind.Utc, created.CreatedUtc.Kind);
        Assert.Equal(TestUser.UserName, created.CreatedBy);
        Assert.Null(created.ModifiedUtc);
        Assert.True(Guid.TryParse(created.ConcurrencyStamp, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_rejects_a_missing_name(string? name)
    {
        var result = await CreateAsync(new CreateProviderRequest { ProviderName = name, CountyId = Fulton, Status = "Active" });

        Assert.Equal(ServiceErrorKind.Validation, result.Error?.Kind);
        Assert.Contains("providerName", result.Error!.FieldErrors!.Keys);
    }

    [Fact]
    public async Task Create_rejects_a_name_longer_than_200_characters()
    {
        var result = await CreateAsync(NewRequest(new string('x', 201)));

        Assert.Equal(ServiceErrorKind.Validation, result.Error?.Kind);
        Assert.Contains("providerName", result.Error!.FieldErrors!.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(160)]
    public async Task Create_rejects_a_county_that_is_not_in_the_lookup(int countyId)
    {
        var result = await CreateAsync(NewRequest("Nowhere Child Care", countyId));

        Assert.Equal(ServiceErrorKind.Validation, result.Error?.Kind);
        Assert.Contains("countyId", result.Error!.FieldErrors!.Keys);
    }

    [Theory]
    [InlineData("Deleted")]
    [InlineData("Expired")]
    [InlineData("1")]
    [InlineData("")]
    public async Task Create_rejects_a_status_that_is_not_a_provider_status(string status)
    {
        var result = await CreateAsync(NewRequest("Odd Status Child Care", status: status));

        Assert.Equal(ServiceErrorKind.Validation, result.Error?.Kind);
        Assert.Contains("status", result.Error!.FieldErrors!.Keys);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_name_in_the_same_county_ignoring_case()
    {
        Assert.True((await CreateAsync(NewRequest("Sunrise Academy"))).IsSuccess);

        var duplicate = await CreateAsync(NewRequest("SUNRISE academy"));

        Assert.Equal(ServiceErrorKind.Conflict, duplicate.Error?.Kind);
    }

    [Fact]
    public async Task Create_allows_the_same_name_in_a_different_county()
    {
        Assert.True((await CreateAsync(NewRequest("Sunrise Academy", Fulton))).IsSuccess);

        Assert.True((await CreateAsync(NewRequest("Sunrise Academy", DeKalb))).IsSuccess);
    }

    [Fact]
    public async Task Create_allows_reusing_the_name_of_a_soft_deleted_provider()
    {
        var first = (await CreateAsync(NewRequest("Sunrise Academy"))).Value!;
        await _database.SoftDeleteProviderAsync(first.ProviderId);

        var second = await CreateAsync(NewRequest("Sunrise Academy"));

        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.ProviderId, second.Value!.ProviderId);

        await using var db = _database.CreateContext();
        Assert.Equal(2, await db.Providers.IgnoreQueryFilters().CountAsync(p => p.ProviderName == "Sunrise Academy"));
    }

    // Update

    [Fact]
    public async Task Update_saves_changes_stamps_the_modifier_and_rotates_the_concurrency_stamp()
    {
        var created = (await CreateAsync(NewRequest("Before"))).Value!;
        _database.Clock.UtcNow = _database.Clock.UtcNow.AddHours(2);

        var result = await UpdateAsync(created.ProviderId, new UpdateProviderRequest
        {
            ProviderName = "After",
            CountyId = DeKalb,
            Status = "Inactive",
            ConcurrencyStamp = created.ConcurrencyStamp
        });

        Assert.True(result.IsSuccess);
        var updated = result.Value!;
        Assert.Equal("After", updated.ProviderName);
        Assert.Equal("DeKalb", updated.County);
        Assert.Equal(ProviderStatus.Inactive, updated.Status);
        Assert.Equal(_database.Clock.UtcNow, updated.ModifiedUtc);
        Assert.Equal(TestUser.UserName, updated.ModifiedBy);
        Assert.Equal(created.CreatedUtc, updated.CreatedUtc);
        Assert.NotEqual(created.ConcurrencyStamp, updated.ConcurrencyStamp);
    }

    [Fact]
    public async Task Update_with_a_stale_concurrency_stamp_is_a_conflict_and_changes_nothing()
    {
        var created = (await CreateAsync(NewRequest("Original"))).Value!;
        var firstEdit = await UpdateAsync(created.ProviderId, EditRequest(created, name: "First Editor"));
        Assert.True(firstEdit.IsSuccess);

        // The second editor still holds the stamp from before the first edit.
        var secondEdit = await UpdateAsync(created.ProviderId, EditRequest(created, name: "Second Editor"));

        Assert.Equal(ServiceErrorKind.Conflict, secondEdit.Error?.Kind);
        await using var db = _database.CreateContext();
        Assert.Equal("First Editor", (await db.Providers.SingleAsync()).ProviderName);
    }

    [Fact]
    public async Task Update_with_a_stale_stamp_is_a_conflict_even_when_no_value_changed()
    {
        var created = (await CreateAsync(NewRequest("Unchanged"))).Value!;

        var result = await UpdateAsync(created.ProviderId, EditRequest(created, stamp: "not-the-current-stamp"));

        Assert.Equal(ServiceErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task Update_rejects_renaming_onto_another_provider_in_the_same_county()
    {
        await CreateAsync(NewRequest("Taken Name"));
        var other = (await CreateAsync(NewRequest("Other Name"))).Value!;

        var result = await UpdateAsync(other.ProviderId, EditRequest(other, name: "taken name"));

        Assert.Equal(ServiceErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task Update_can_keep_its_own_name()
    {
        var created = (await CreateAsync(NewRequest("Same Name"))).Value!;

        var result = await UpdateAsync(created.ProviderId, EditRequest(created));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Update_validates_input_like_create()
    {
        var created = (await CreateAsync(NewRequest("Valid"))).Value!;
        var request = EditRequest(created);
        request.CountyId = 9999;

        var result = await UpdateAsync(created.ProviderId, request);

        Assert.Equal(ServiceErrorKind.Validation, result.Error?.Kind);
        Assert.Contains("countyId", result.Error!.FieldErrors!.Keys);
    }

    [Fact]
    public async Task Update_of_an_unknown_provider_is_not_found()
    {
        var result = await UpdateAsync(12345, new UpdateProviderRequest
        {
            ProviderName = "Ghost", CountyId = Fulton, Status = "Active", ConcurrencyStamp = "x"
        });

        Assert.Equal(ServiceErrorKind.NotFound, result.Error?.Kind);
    }

    // List

    [Fact]
    public async Task List_search_is_a_case_insensitive_contains_match()
    {
        await _database.AddProviderAsync("Peachtree Early Learning");
        await _database.AddProviderAsync("Decatur Family Child Care", county: "DeKalb");

        var result = await ListAsync(new ProviderQuery { Search = "  EARLY  " });

        Assert.Equal("Peachtree Early Learning", Assert.Single(result.Items).ProviderName);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("100% Kids")]
    public async Task List_search_treats_wildcard_characters_literally(string search)
    {
        await _database.AddProviderAsync("Plain Name");
        await _database.AddProviderAsync("100% Kids_Club", county: "DeKalb");

        var result = await ListAsync(new ProviderQuery { Search = search });

        Assert.Equal("100% Kids_Club", Assert.Single(result.Items).ProviderName);
    }

    [Fact]
    public async Task List_filters_by_status_and_county()
    {
        await _database.AddProviderAsync("A", "Fulton", ProviderStatus.Active);
        await _database.AddProviderAsync("B", "Fulton", ProviderStatus.Inactive);
        await _database.AddProviderAsync("C", "DeKalb", ProviderStatus.Inactive);

        var result = await ListAsync(new ProviderQuery { Status = ProviderStatus.Inactive, CountyId = Fulton });

        Assert.Equal("B", Assert.Single(result.Items).ProviderName);
    }

    [Fact]
    public async Task List_pages_in_name_order_and_reports_the_total()
    {
        foreach (var name in new[] { "delta", "Alpha", "charlie", "Bravo", "Echo" })
        {
            await _database.AddProviderAsync(name);
        }

        var page2 = await ListAsync(new ProviderQuery { Page = 2, PageSize = 2 });

        Assert.Equal(["charlie", "delta"], page2.Items.Select(p => p.ProviderName));
        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
    }

    [Fact]
    public async Task List_counts_valid_and_expired_licenses_by_date_not_by_stored_status()
    {
        var today = _database.Clock.Today;
        var provider = await _database.AddProviderAsync("Mixed Licenses");
        await _database.AddLicenseAsync(provider.ProviderId, "VALID", LicenseStatus.Active, today.AddYears(1));
        await _database.AddLicenseAsync(provider.ProviderId, "DRIFT", LicenseStatus.Active, today.AddDays(-1));
        await _database.AddLicenseAsync(provider.ProviderId, "SUSPENDED", LicenseStatus.Suspended, today.AddYears(1));

        var item = Assert.Single((await ListAsync(new ProviderQuery())).Items);

        Assert.Equal(3, item.LicenseCount);
        Assert.Equal(1, item.ValidLicenseCount);
        Assert.Equal(1, item.ExpiredLicenseCount);
    }

    // Detail

    [Fact]
    public async Task Get_returns_licenses_with_validity_computed_independently_of_provider_status()
    {
        var today = _database.Clock.Today;
        var provider = await _database.AddProviderAsync("Active Provider", status: ProviderStatus.Active);
        await _database.AddLicenseAsync(provider.ProviderId, "DRIFT", LicenseStatus.Active, today.AddDays(-30));

        await using var db = _database.CreateContext();
        var detail = (await new ProviderService(db, _database.Clock).GetAsync(provider.ProviderId)).Value!;

        Assert.Equal(ProviderStatus.Active, detail.Status);
        Assert.False(detail.HasValidLicense);
        Assert.Equal(today, detail.AsOfDate);
        var license = Assert.Single(detail.Licenses);
        Assert.Equal(LicenseStatus.Active, license.LicenseStatus);
        Assert.True(license.IsExpired);
        Assert.False(license.IsCurrentlyValid);
        Assert.Equal(LicenseStatus.Expired, license.EffectiveStatus);
    }

    // Integrity enforced by the database itself, for writers that bypass the service

    [Fact]
    public async Task Database_rejects_a_duplicate_live_provider_even_without_the_service_check()
    {
        await _database.AddProviderAsync("Race Condition");

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => _database.AddProviderAsync("race condition"));

        Assert.Contains("UNIQUE constraint failed", exception.InnerException!.Message);
    }

    [Theory]
    [InlineData(
        "INSERT INTO License (ProviderId, LicenseNumber, LicenseStatus, ExpirationDate, CreatedUtc, CreatedBy, ConcurrencyStamp) " +
        "VALUES (999, 'ORPHAN', 'Active', '2030-01-01', '2026-01-01 00:00:00', 'test', 'x')",
        "FOREIGN KEY constraint failed")]
    [InlineData(
        "INSERT INTO Provider (ProviderName, CountyId, Status, CreatedUtc, CreatedBy, ConcurrencyStamp) " +
        "VALUES ('Bad Status', 1, 'Archived', '2026-01-01 00:00:00', 'test', 'x')",
        "CHECK constraint failed")]
    [InlineData(
        "INSERT INTO Provider (ProviderName, CountyId, Status, CreatedUtc, CreatedBy, ConcurrencyStamp) " +
        "VALUES ('   ', 1, 'Active', '2026-01-01 00:00:00', 'test', 'x')",
        "CHECK constraint failed")]
    [InlineData(
        "INSERT INTO Provider (ProviderName, CountyId, Status, CreatedUtc, CreatedBy, ConcurrencyStamp) " +
        "VALUES ('No Such County', 999, 'Active', '2026-01-01 00:00:00', 'test', 'x')",
        "FOREIGN KEY constraint failed")]
    public async Task Database_rejects_invalid_rows_written_with_raw_sql(string sql, string expectedMessage)
    {
        await using var db = _database.CreateContext();

        var exception = await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(sql));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public async Task Database_does_not_allow_a_license_number_twice_for_one_provider()
    {
        var provider = await _database.AddProviderAsync("One Number Each");
        await _database.AddLicenseAsync(provider.ProviderId, "LIC-1", LicenseStatus.Active, new DateOnly(2030, 1, 1));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => _database.AddLicenseAsync(provider.ProviderId, "lic-1", LicenseStatus.Expired, new DateOnly(2024, 1, 1)));
    }
}
