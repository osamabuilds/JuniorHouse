using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Tests.StaffAccounts;

public sealed class StaffUserTests
{
    private const short OwnerRole = 1;
    private const short SupportRole = 4;

    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    private static readonly OwnerGuard NoOtherOwner = new(OwnerRole, OtherActiveOwnerExists: false);
    private static readonly OwnerGuard AnotherOwner = new(OwnerRole, OtherActiveOwnerExists: true);

    private static StaffUser Support(string hash = "hash-0") =>
        StaffUser.CreateWithTemporaryPassword(UserName.Create("amna.khan"), "Amna Khan", hash, [SupportRole], Now);

    // The test "hasher": a hash matches a password when it is "hash-of:" + the password.
    private static bool Same(string hash, string password) => hash == "hash-of:" + password;

    [Theory]
    [Trait("Spec", "AC-10")]
    [InlineData("ab")]
    [InlineData("a b c")]
    [InlineData("amna@khan")]
    [InlineData("amna/khan")]
    [InlineData("امنہ.khan")]
    [InlineData("")]
    public void UserName_RejectsOtherCharactersAndBadLengths(string raw)
    {
        Assert.Throws<DomainException>(() => UserName.Create(raw));
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public void UserName_AllowsOnlyTheCharsetAndNeverChanges()
    {
        Assert.Equal("amna.khan_2-a", UserName.Create("  Amna.Khan_2-A ").Value);
        Assert.Equal(40, UserName.Create(new string('a', 40)).Value.Length);
        Assert.Throws<DomainException>(() => UserName.Create(new string('a', 41)));

        var property = typeof(StaffUser).GetProperty(nameof(StaffUser.UserName))!;
        Assert.True(property.SetMethod is null || !property.SetMethod.IsPublic);
        Assert.DoesNotContain(typeof(StaffUser).GetMethods(), m => m.Name.Contains("Rename", StringComparison.Ordinal) || m.Name == "SetUserName");
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public void NewAccount_MustChangeItsPassword_AndIsActive()
    {
        var user = Support();

        Assert.True(user.IsActive);
        Assert.True(user.MustChangePassword);
        Assert.True(user.HasTemporaryPassword);
        Assert.Equal(Now.AddHours(48), user.TemporaryPasswordExpiresAt);
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    [Trait("Spec", "AC-6")]
    public void TemporaryPassword_Expires48HoursAndWorksOnce()
    {
        var user = Support();

        Assert.True(user.TryUseTemporaryPassword(Now.AddHours(47).AddMinutes(59)));
        Assert.False(user.TryUseTemporaryPassword(Now.AddHours(1)), "a second use must fail");

        var late = Support();
        Assert.False(late.TryUseTemporaryPassword(Now.AddHours(48).AddSeconds(1)));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public void RegeneratingTheTemporaryPassword_StartsANewWindow_AsOftenAsNeeded()
    {
        var user = Support();
        Assert.False(user.TryUseTemporaryPassword(Now.AddDays(3)));

        var later = Now.AddDays(3);
        user.IssueTemporaryPassword("hash-1", later);

        Assert.Equal("hash-1", user.PasswordHash);
        Assert.True(user.MustChangePassword);
        Assert.True(user.TryUseTemporaryPassword(later.AddHours(1)));

        user.IssueTemporaryPassword("hash-2", later.AddHours(2));
        Assert.True(user.TryUseTemporaryPassword(later.AddHours(3)));
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public void AnAccountWithAnOrdinaryPassword_HasNoTemporaryPasswordToConsume()
    {
        var user = Support();
        user.SetPassword("hash-of:first-long-password", _ => false);

        Assert.False(user.HasTemporaryPassword);
        Assert.False(user.MustChangePassword);
        Assert.False(user.TryUseTemporaryPassword(Now));
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public void NewPassword_CannotRepeatTheLastThree()
    {
        var user = Support("hash-of:initial-password");
        var passwords = new[] { "second-password-aa", "third-password-bb", "fourth-password-cc", "fifth-password-dd" };
        foreach (var password in passwords)
        {
            user.SetPassword("hash-of:" + password, hash => Same(hash, password));
        }

        // current = fifth; history = second, third, fourth
        foreach (var repeated in new[] { "fifth-password-dd", "fourth-password-cc", "third-password-bb", "second-password-aa" })
        {
            var ex = Assert.Throws<DomainException>(() => user.SetPassword("hash-of:" + repeated, hash => Same(hash, repeated)));
            Assert.Contains("recent", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // the initial password has aged out of the history of three, so it is allowed again
        user.SetPassword("hash-of:initial-password", hash => Same(hash, "initial-password"));
        Assert.Equal("hash-of:initial-password", user.PasswordHash);
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public void PasswordHistory_KeepsOnlyThree()
    {
        var user = Support();
        for (var i = 0; i < 6; i++)
        {
            var password = $"password-number-{i}";
            user.SetPassword("hash-of:" + password, hash => Same(hash, password));
        }

        Assert.Equal(3, user.PasswordHistory.Count);
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public void LastOwner_CannotBeDeactivatedOrDemoted()
    {
        var owner = StaffUser.CreateWithPassword(UserName.Create("the.owner"), "Owner", "hash-0", [OwnerRole]);

        Assert.Throws<DomainException>(() => owner.Deactivate(NoOtherOwner));
        Assert.Throws<DomainException>(() => owner.SetRoles([SupportRole], NoOtherOwner));
        Assert.True(owner.IsActive);
        Assert.Contains(OwnerRole, owner.RoleIds);

        owner.SetRoles([OwnerRole, SupportRole], NoOtherOwner); // keeping Owner is fine
        owner.SetRoles([SupportRole], AnotherOwner);
        Assert.DoesNotContain(OwnerRole, owner.RoleIds);
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public void Owner_CanBeDeactivated_WhenAnotherActiveOwnerExists()
    {
        var owner = StaffUser.CreateWithPassword(UserName.Create("the.owner"), "Owner", "hash-0", [OwnerRole]);

        owner.Deactivate(AnotherOwner);

        Assert.False(owner.IsActive);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public void Deactivate_IsIdempotent_AndReactivateRestores()
    {
        var user = Support();

        user.Deactivate(NoOtherOwner);
        user.Deactivate(NoOtherOwner);
        Assert.False(user.IsActive);

        user.Reactivate();
        user.Reactivate();
        Assert.True(user.IsActive);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public void DeactivatedAccount_CannotTakeANewTemporaryPasswordOrPassword()
    {
        var user = Support();
        user.Deactivate(NoOtherOwner);

        Assert.Throws<DomainException>(() => user.IssueTemporaryPassword("hash-1", Now));
        Assert.Throws<DomainException>(() => user.SetPassword("hash-of:x-long-password", _ => false));
        Assert.False(user.TryUseTemporaryPassword(Now));
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public void DisplayName_IsRequired_AndBounded()
    {
        Assert.Throws<DomainException>(() => StaffUser.CreateWithPassword(UserName.Create("amna.khan"), "  ", "h", []));
        Assert.Throws<DomainException>(() => StaffUser.CreateWithPassword(UserName.Create("amna.khan"), new string('x', 101), "h", []));
        Assert.Equal("Amna Khan", StaffUser.CreateWithPassword(UserName.Create("amna.khan"), " Amna Khan ", "h", []).DisplayName);
    }
}
