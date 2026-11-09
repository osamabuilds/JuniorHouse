using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Api.HostVerbs;

/// <summary>
/// The operator verbs <c>create-owner</c> and <c>reset-owner [--password] [--mfa]</c> (staff-accounts AC-1, AC-12, AC-13). Values come from
/// environment variables or a no-echo prompt, never from arguments. The runner has already set the audit actor to <c>operator-cli</c>.
/// What is printed is only what is safe: messages from the rules, never a password.
/// </summary>
public static class OwnerVerbs
{
    public const string UserNameVariable = "ROMP_OWNER_USERNAME";
    public const string DisplayNameVariable = "ROMP_OWNER_DISPLAY_NAME";
    public const string PasswordVariable = "ROMP_OWNER_PASSWORD";

    public const string CreateVerb = "create-owner";
    public const string ResetVerb = "reset-owner";

    public static Task<int> CreateAsync(
        IServiceProvider services, OperatorSecretReader reader, TextWriter output, CancellationToken cancellationToken) =>
        RunAsync(output, async () =>
        {
            var userName = reader.Read(UserNameVariable, "Owner user name: ");
            var displayName = reader.Read(DisplayNameVariable, "Owner display name: ");
            var password = new SecretString(reader.Read(PasswordVariable, "Owner password: "));

            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new CreateOwnerCommand(userName, displayName, password), cancellationToken);
            await output.WriteLineAsync("Owner account created. The Owner must change the password at the first sign-in.");
        });

    public static Task<int> ResetAsync(
        IServiceProvider services, OperatorSecretReader reader, HostVerbRequest request, TextWriter output, CancellationToken cancellationToken) =>
        RunAsync(output, async () =>
        {
            var wantsPassword = request.Has("--password");
            var clearMfa = request.Has("--mfa");
            if (!wantsPassword && !clearMfa)
            {
                throw new InvalidOperationException("Nothing to do: give --password, --mfa, or both.");
            }

            var userName = reader.Read(UserNameVariable, "Owner user name: ");
            SecretString? password = wantsPassword ? new SecretString(reader.Read(PasswordVariable, "New password: ")) : null;

            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new ResetOwnerCommand(userName, password, clearMfa), cancellationToken);
            await output.WriteLineAsync("Owner account reset. Its sessions have ended and the Owner must change the password at the next sign-in.");
        });

    private static async Task<int> RunAsync(TextWriter output, Func<Task> action)
    {
        try
        {
            await action();
            return 0;
        }
        catch (Exception ex) when (ex is DomainException or KeyNotFoundException)
        {
            await output.WriteLineAsync(ex.Message);
            return HostVerbRunner.Failed;
        }
        catch (ValidationException ex)
        {
            foreach (var message in ex.Errors.Values.SelectMany(m => m))
            {
                await output.WriteLineAsync(message);
            }

            return HostVerbRunner.Failed;
        }
        catch (InvalidOperationException ex)
        {
            await output.WriteLineAsync(ex.Message);
            return HostVerbRunner.Failed;
        }
    }
}
