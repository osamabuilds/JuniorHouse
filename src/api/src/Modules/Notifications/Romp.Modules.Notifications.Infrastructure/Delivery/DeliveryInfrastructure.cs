using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Romp.BuildingBlocks.Email;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Persistence;

namespace Romp.Modules.Notifications.Infrastructure.Delivery;

/// <summary>
/// Claims due deliveries in one statement: <c>UPDATE … WHERE ID IN (SELECT … FOR UPDATE SKIP LOCKED) RETURNING ID</c>. Rows another worker
/// is claiming are skipped, so two workers never hold one delivery (DLV-9). Order: priority, then due time (DLV-11).
/// </summary>
internal sealed class SkipLockedDeliveryClaimer(NtfyDbContext dbContext) : IDeliveryClaimer
{
    private const string Sql = """
        UPDATE "NTFY"."NTFY_CHNL" AS c
        SET "LEAS_DTE" = @lease, "UPDT_DTE" = @now, "UPDT_BY" = 'delivery-worker'
        WHERE c."ID" IN (
            SELECT ch."ID"
            FROM "NTFY"."NTFY_CHNL" AS ch
            INNER JOIN "NTFY"."NTFY_MAIN" AS m ON m."ID" = ch."NTFY_ID"
            WHERE ch."CHNL_STS_ID" = @queued
              AND ch."NXT_ATMP_DTE" <= @now
              AND (ch."LEAS_DTE" IS NULL OR ch."LEAS_DTE" <= @now)
            ORDER BY m."PRIO", ch."NXT_ATMP_DTE", ch."ID"
            LIMIT @batch
            FOR UPDATE OF ch SKIP LOCKED)
        RETURNING c."ID"
        """;

    public async Task<IReadOnlyList<long>> ClaimAsync(DateTimeOffset now, int batchSize, TimeSpan lease, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = new NpgsqlCommand(Sql, connection);
            command.Parameters.AddWithValue("lease", now + lease);
            command.Parameters.AddWithValue("now", now);
            command.Parameters.AddWithValue("queued", DeliveryConversions.StatusId(DeliveryStatus.Queued));
            command.Parameters.AddWithValue("batch", batchSize);

            var ids = new List<long>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetInt64(0));
            }

            return ids;
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }
}

/// <summary>Sends email through the shared port (ADR 0022). A malformed address is permanent; anything else is worth retrying.</summary>
internal sealed class EmailChannelSender(IEmailSender email) : IChannelSender
{
    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task<string?> SendAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await email.SendAsync(new EmailMessage(message.Recipient, message.Subject, message.Text, message.Html), cancellationToken);
            return null;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new ChannelSendException(ErrorCategory.Permanent, "The email address was refused.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ChannelSendException(ErrorCategory.Temporary, "The email server did not accept the message.");
        }
    }
}

/// <summary>Runs <see cref="DeliveryProcessor"/> until the host stops. Idle passes wait; a busy pass runs again at once.</summary>
internal sealed class DeliveryWorker(IServiceScopeFactory scopes, IOptions<DeliverySettings> settings, ILogger<DeliveryWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> PassFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(1, nameof(PassFailed)), "Delivery pass failed: {ExceptionType}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idle = TimeSpan.FromSeconds(settings.Value.PollSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            var handled = 0;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                handled = await scope.ServiceProvider.GetRequiredService<DeliveryProcessor>().RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Never the message: it can carry an address or a connection string (SEC-25).
                PassFailed(logger, ex.GetType().Name, null);
            }

            if (handled == 0)
            {
                try
                {
                    await Task.Delay(idle, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
