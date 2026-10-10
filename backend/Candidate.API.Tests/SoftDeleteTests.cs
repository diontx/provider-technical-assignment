using Microsoft.Data.Sqlite;

namespace Candidate.API.Tests;

/// <summary>The organizational rule: hard deletion is not allowed, and deleted records stay queryable for audit.</summary>
public sealed class SoftDeleteTests : IDisposable
{
    private static readonly DateOnly Future = new(2030, 12, 31);
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Delete_marks_the_row_deleted_and_keeps_it_in_the_table()
    {
        var provider = await _database.AddProviderAsync("Kept For Audit");
        var originalStamp = provider.ConcurrencyStamp;

        await _database.SoftDeleteProviderAsync(provider.ProviderId);

        await using var db = _database.CreateContext();
        var row = await db.Providers.IgnoreQueryFilters().SingleAsync(p => p.ProviderId == provider.ProviderId);
        Assert.True(row.IsDeleted);
        Assert.Equal(_database.Clock.UtcNow, row.DeletedUtc);
        Assert.Equal(TestUser.UserName, row.DeletedBy);
        Assert.Equal(_database.Clock.UtcNow, row.ModifiedUtc);
        Assert.NotEqual(originalStamp, row.ConcurrencyStamp);

        // Everything else about the record is untouched.
        Assert.Equal("Kept For Audit", row.ProviderName);
        Assert.Equal(ProviderStatus.Active, row.Status);
    }

    [Fact]
    public async Task Deleted_provider_is_excluded_from_every_standard_operation()
    {
        var provider = await _database.AddProviderAsync("Gone From Standard Views");
        await _database.SoftDeleteProviderAsync(provider.ProviderId);

        await using var db = _database.CreateContext();
        var service = new ProviderService(db, _database.Clock);

        var list = await service.ListAsync(new ProviderQuery());
        Assert.Empty(list.Items);
        Assert.Equal(0, list.TotalCount);

        Assert.Equal(ServiceErrorKind.NotFound, (await service.GetAsync(provider.ProviderId)).Error?.Kind);
        Assert.Equal(ServiceErrorKind.NotFound, (await service.GetLicensesAsync(provider.ProviderId)).Error?.Kind);

        var update = new UpdateProviderRequest
        {
            ProviderName = "Renamed",
            CountyId = provider.CountyId,
            Status = "Active",
            ConcurrencyStamp = provider.ConcurrencyStamp
        };
        Assert.Equal(ServiceErrorKind.NotFound, (await service.UpdateAsync(provider.ProviderId, update)).Error?.Kind);

        // A second delete is "not found", not a server error.
        Assert.Equal(ServiceErrorKind.NotFound, (await service.DeleteAsync(provider.ProviderId)).Error?.Kind);
    }

    [Fact]
    public async Task Calling_Remove_on_the_DbContext_soft_deletes_instead_of_deleting()
    {
        var provider = await _database.AddProviderAsync("Removed Through EF");
        var license = await _database.AddLicenseAsync(provider.ProviderId, "LIC-1", LicenseStatus.Active, Future);

        await using (var db = _database.CreateContext())
        {
            db.Licenses.Remove(await db.Licenses.SingleAsync(l => l.LicenseId == license.LicenseId));
            db.Providers.Remove(await db.Providers.SingleAsync(p => p.ProviderId == provider.ProviderId));
            await db.SaveChangesAsync();
        }

        await using var verify = _database.CreateContext();
        Assert.True((await verify.Providers.IgnoreQueryFilters().SingleAsync()).IsDeleted);
        Assert.True((await verify.Licenses.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Theory]
    [InlineData("DELETE FROM Provider", "Hard delete is not allowed on Provider")]
    [InlineData("DELETE FROM License", "Hard delete is not allowed on License")]
    public async Task Raw_sql_delete_is_blocked_by_a_trigger(string sql, string expectedMessage)
    {
        var provider = await _database.AddProviderAsync("Protected By Trigger");
        await _database.AddLicenseAsync(provider.ProviderId, "LIC-1", LicenseStatus.Active, Future);

        await using var db = _database.CreateContext();
        var exception = await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(sql));

        Assert.Contains(expectedMessage, exception.Message);
        Assert.Equal(1, await db.Providers.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await db.Licenses.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Licenses_of_a_deleted_provider_are_hidden_but_not_modified()
    {
        var provider = await _database.AddProviderAsync("Deleted With Licenses");
        await _database.AddLicenseAsync(provider.ProviderId, "LIC-1", LicenseStatus.Active, Future);
        await _database.SoftDeleteProviderAsync(provider.ProviderId);

        await using var db = _database.CreateContext();
        Assert.Empty(await db.Licenses.ToListAsync());

        // The license row itself is unchanged, so restoring the provider would bring it back.
        var license = await db.Licenses.IgnoreQueryFilters().SingleAsync();
        Assert.False(license.IsDeleted);
        Assert.Null(license.DeletedUtc);
    }

    [Fact]
    public async Task Deleted_providers_remain_queryable_through_the_audit_service()
    {
        var kept = await _database.AddProviderAsync("Still Here");
        var deleted = await _database.AddProviderAsync("Deleted Provider", county: "DeKalb", status: ProviderStatus.Inactive);
        await _database.AddLicenseAsync(deleted.ProviderId, "LIC-1", LicenseStatus.Active, Future);
        await _database.SoftDeleteProviderAsync(deleted.ProviderId);

        await using var db = _database.CreateContext();
        var audit = await new AuditService(db).DeletedProvidersAsync();

        var row = Assert.Single(audit);
        Assert.Equal(deleted.ProviderId, row.ProviderId);
        Assert.NotEqual(kept.ProviderId, row.ProviderId);
        Assert.Equal("DeKalb", row.County);
        Assert.Equal(ProviderStatus.Inactive, row.Status);
        Assert.Equal(TestUser.UserName, row.DeletedBy);
        Assert.Equal(_database.Clock.UtcNow, row.DeletedUtc);
        Assert.Equal(1, row.LicenseCount);
    }

    [Theory]
    [InlineData("UPDATE Provider SET IsDeleted = 1")]
    [InlineData("UPDATE Provider SET DeletedUtc = '2026-01-01 00:00:00'")]
    [InlineData("UPDATE Provider SET IsDeleted = 2, DeletedUtc = '2026-01-01 00:00:00'")]
    public async Task Database_rejects_a_delete_flag_and_timestamp_that_disagree(string sql)
    {
        await _database.AddProviderAsync("Consistent Flags");

        await using var db = _database.CreateContext();
        var exception = await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(sql));

        Assert.Contains("CHECK constraint failed", exception.Message);
    }

    [Fact]
    public async Task Database_views_apply_the_same_exclusion()
    {
        await _database.AddProviderAsync("Visible In View");
        var deleted = await _database.AddProviderAsync("Hidden From View", county: "Cobb");
        await _database.SoftDeleteProviderAsync(deleted.ProviderId);

        await using var db = _database.CreateContext();
        var standard = await db.Database.SqlQueryRaw<string>("SELECT ProviderName AS Value FROM vw_Providers").ToListAsync();
        var audit = await db.Database.SqlQueryRaw<string>("SELECT ProviderName AS Value FROM vw_DeletedProvidersAudit").ToListAsync();

        Assert.Equal(["Visible In View"], standard);
        Assert.Equal(["Hidden From View"], audit);
    }
}
