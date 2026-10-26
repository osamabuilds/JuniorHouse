using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Domain.Mfa;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Tests.Roles;

namespace Romp.Modules.Identity.Tests.Mfa;

/// <summary>MFA task 5 (AC-1, AC-2, AC-6, AC-7): what an account remembers about its second factor, and where it is stored.</summary>
public sealed class StaffUserMfaTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    private static StaffUser NewUser() =>
        StaffUser.CreateWithTemporaryPassword(UserName.Create("amna.khan"), "Amna Khan", "hash-0", [4], Now);

    [Fact]
    [Trait("Spec", "AC-1")]
    public void ANewAccount_HasNoSecondFactor()
    {
        var user = NewUser();

        Assert.False(user.IsMfaEnrolled);
        Assert.Null(user.MfaSecretCipher);
        Assert.Null(user.MfaKeyId);
        Assert.Null(user.MfaEnrolledAt);
        Assert.Equal(0, user.MfaLastStep);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public void Enrolling_StoresTheSealedSecret_AndTheStepOfTheConfirmingCode()
    {
        var user = NewUser();

        user.EnrolMfa([1, 2, 3], "k1", Now, firstStep: 55_000_000);

        Assert.True(user.IsMfaEnrolled);
        Assert.Equal(new byte[] { 1, 2, 3 }, user.MfaSecretCipher);
        Assert.Equal("k1", user.MfaKeyId);
        Assert.Equal(Now, user.MfaEnrolledAt);
        Assert.Equal(55_000_000, user.MfaLastStep); // the code that confirmed enrolment cannot be used again to sign in
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public void AnEnrolledAccount_CannotEnrolAgainWithoutBeingReset()
    {
        var user = NewUser();
        user.EnrolMfa([1], "k1", Now, 1);

        Assert.Throws<DomainException>(() => user.EnrolMfa([2], "k1", Now, 2));
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public void Clearing_ForgetsEverything_SoTheAccountMustEnrolAgain()
    {
        var user = NewUser();
        user.EnrolMfa([1, 2], "k1", Now, 99);

        user.ClearMfa();

        Assert.False(user.IsMfaEnrolled);
        Assert.Null(user.MfaSecretCipher);
        Assert.Null(user.MfaKeyId);
        Assert.Null(user.MfaEnrolledAt);
        Assert.Equal(0, user.MfaLastStep);
        user.EnrolMfa([3], "k1", Now, 5); // and it can enrol again
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public void AcceptingAStep_NeverGoesBackwards()
    {
        var user = NewUser();
        user.EnrolMfa([1], "k1", Now, 100);

        Assert.True(user.TryAcceptMfaStep(101));
        Assert.False(user.TryAcceptMfaStep(101));
        Assert.False(user.TryAcceptMfaStep(100));
        Assert.Equal(101, user.MfaLastStep);
        Assert.True(user.TryAcceptMfaStep(102));
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public void Resealing_ReplacesTheCiphertext_WithoutTouchingTheStep()
    {
        var user = NewUser();
        user.EnrolMfa([1], "k1", Now, 100);

        user.ReplaceSealedSecret([9, 9], "k2");

        Assert.Equal(new byte[] { 9, 9 }, user.MfaSecretCipher);
        Assert.Equal("k2", user.MfaKeyId);
        Assert.Equal(100, user.MfaLastStep);
    }
}

public sealed class MfaMigrationTests(IdentityPostgres postgres) : IClassFixture<IdentityPostgres>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task Applies_OnACleanDatabase()
    {
        await using var context = postgres.NewContext();

        var tables = await context.Database
            .SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'IDNT'").ToListAsync();
        Assert.Contains("USR_RCVR_CODE", tables);
        Assert.Contains("MFA_ENRL_PEND", tables);

        var columns = await context.Database
            .SqlQueryRaw<string>("SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = 'IDNT' AND table_name = 'USR_MAIN'")
            .ToListAsync();
        Assert.Contains("MFA_SECR_ENC", columns);
        Assert.Contains("MFA_KEY_ID", columns);
        Assert.Contains("MFA_ENRL_DTE", columns);
        Assert.Contains("MFA_LAST_STEP", columns);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    private async Task<long> NewUserAsync(string name)
    {
        await using var context = postgres.NewContext();
        var user = StaffUser.CreateWithTemporaryPassword(UserName.Create(name), "Test User", "hash-0", [], Now);
        context.Staff.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task TheSecondFactorState_AndRecoveryCodes_RoundTrip_AsBytesNotText()
    {
        var userId = await NewUserAsync("mfa.round.trip");
        var pepper = new byte[32];
        var (rows, codes) = RecoveryCodes.Issue(userId, pepper, Now);
        await using (var context = postgres.NewContext())
        {
            var user = await context.Staff.SingleAsync(u => u.Id == userId);
            user.EnrolMfa([1, 2, 3, 4], "k1", Now, 77);
            context.RecoveryCodes.AddRange(rows);
            await context.SaveChangesAsync();
        }

        await using var read = postgres.NewContext();
        var loaded = await read.Staff.AsNoTracking().SingleAsync(u => u.Id == userId);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, loaded.MfaSecretCipher);
        Assert.Equal("k1", loaded.MfaKeyId);
        Assert.Equal(77, loaded.MfaLastStep);
        Assert.True(loaded.IsMfaEnrolled);

        var stored = await read.RecoveryCodes.AsNoTracking().Where(r => r.UserId == userId).ToListAsync();
        Assert.Equal(10, stored.Count);
        Assert.NotNull(RecoveryCodes.TryUse(stored, userId, pepper, codes[0], Now));

        var length = await read.Database
            .SqlQueryRaw<int>("SELECT octet_length(\"CODE_HASH\") AS \"Value\" FROM \"IDNT\".\"USR_RCVR_CODE\" WHERE \"USR_ID\" = {0} LIMIT 1", userId)
            .SingleAsync();
        Assert.Equal(32, length);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task AnUnconfirmedEnrolment_IsOnePerAccount_AndNeedsARealAccount()
    {
        var userId = await NewUserAsync("mfa.pending");
        await using (var context = postgres.NewContext())
        {
            context.MfaEnrolmentsPending.Add(MfaEnrolmentPending.Start(userId, [1, 2], "k1", Now));
            await context.SaveChangesAsync();
        }

        await using var duplicate = postgres.NewContext();
        duplicate.MfaEnrolmentsPending.Add(MfaEnrolmentPending.Start(userId, [3], "k1", Now));
        var clash = await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)clash.InnerException!).SqlState);

        await using var orphan = postgres.NewContext();
        orphan.MfaEnrolmentsPending.Add(MfaEnrolmentPending.Start(long.MaxValue, [1], "k1", Now));
        var missing = await Assert.ThrowsAsync<DbUpdateException>(() => orphan.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ((PostgresException)missing.InnerException!).SqlState);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public void AnUnconfirmedEnrolment_LivesFifteenMinutes()
    {
        var pending = MfaEnrolmentPending.Start(1, [1], "k1", Now);

        Assert.Equal(Now.AddMinutes(15), pending.ExpiresAt);
        Assert.False(pending.IsExpired(Now.AddMinutes(14)));
        Assert.True(pending.IsExpired(Now.AddMinutes(15)));
    }
}
