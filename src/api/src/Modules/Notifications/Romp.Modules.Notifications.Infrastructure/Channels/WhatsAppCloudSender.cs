using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;

namespace Romp.Modules.Notifications.Infrastructure.Channels;

/// <summary>
/// The WhatsApp Cloud API adapter (Meta, called directly, ADR 0026) behind <see cref="IWhatsAppSender"/>. Off unless <c>Notifications:WhatsApp:Enabled</c>
/// is true. Only approved template messages are ever sent (CHN-13): a message with no template is refused here, so no free-text business-started
/// message can leave the system. Failures are mapped to the categories the worker understands; the provider's reply body is never kept or logged, because
/// it can echo the customer's number (SEC-25).
/// </summary>
internal sealed class WhatsAppCloudSender(HttpClient http, WhatsAppSettings settings) : IWhatsAppSender
{
    public const string LanguageCode = "en";

    public async Task<string> SendAsync(WhatsAppMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(message.TemplateName))
        {
            throw new ChannelSendException(ErrorCategory.Permanent, "A business-started WhatsApp message needs an approved template.");
        }

        var to = NumberHasher.Normalise(message.RecipientNumber);
        if (to.Length == 0)
        {
            throw new ChannelSendException(ErrorCategory.NoAddress, "There is no number to send to.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.PhoneNumberId}/messages")
        {
            Content = new StringContent(BuildBody(message, to), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new ChannelSendException(ErrorCategory.Temporary, "The WhatsApp provider could not be reached.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new ChannelSendException(Classify(response.StatusCode), $"The WhatsApp provider answered {(int)response.StatusCode}.");
            }

            return await ReadMessageIdAsync(response, cancellationToken);
        }
    }

    /// <summary>
    /// 5xx, 429 and 408 are the provider's trouble; 401 and 403 mean our token or permissions are wrong, which retrying and the dead-letter alert will
    /// surface; any other 4xx means this message can never be sent (invalid or unreachable number, template mismatch).
    /// </summary>
    internal static ErrorCategory Classify(HttpStatusCode status) => status switch
    {
        HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ErrorCategory.Temporary,
        >= HttpStatusCode.InternalServerError => ErrorCategory.Temporary,
        _ => ErrorCategory.Permanent,
    };

    private static string BuildBody(WhatsAppMessage message, string to)
    {
        var components = new JsonArray();
        if (message.Parameters is { Count: > 0 })
        {
            components.Add(new JsonObject
            {
                ["type"] = "body",
                ["parameters"] = new JsonArray(message.Parameters.Select(p => (JsonNode)new JsonObject { ["type"] = "text", ["text"] = p }).ToArray()),
            });
        }

        for (var i = 0; i < message.Buttons.Count; i++)
        {
            components.Add(new JsonObject
            {
                ["type"] = "button",
                ["sub_type"] = "quick_reply",
                ["index"] = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["parameters"] = new JsonArray(new JsonObject { ["type"] = "payload", ["payload"] = message.Buttons[i].Id }),
            });
        }

        return new JsonObject
        {
            ["messaging_product"] = "whatsapp",
            ["to"] = to,
            ["type"] = "template",
            ["template"] = new JsonObject
            {
                ["name"] = message.TemplateName,
                ["language"] = new JsonObject { ["code"] = LanguageCode },
                ["components"] = components,
            },
        }.ToJsonString();
    }

    private static async Task<string> ReadMessageIdAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return doc.RootElement.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                    ? id.GetString() ?? string.Empty
                    : string.Empty;
        }
        catch (JsonException)
        {
            // Accepted (2xx) but unreadable: the message is on its way, so do not send it again. Receipts cannot be matched without the id.
            return string.Empty;
        }
    }
}

/// <summary>
/// Refuses to start in a way that would silently lose or fake customer messages (plan, "Policies, configuration"): WhatsApp enabled but missing a
/// secret outside Development, or the fake sender outside Development and Testing. Names the missing keys, never a value.
/// </summary>
internal sealed class WhatsAppStartupCheck(WhatsAppSettings settings, IHostEnvironment environment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Validate(settings, environment.EnvironmentName) is { } problem)
        {
            throw new InvalidOperationException(problem);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static string? Validate(WhatsAppSettings settings, string environmentName)
    {
        var development = string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase);
        var testing = string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
        if (!settings.Enabled)
        {
            return development || testing
                ? null
                : "WhatsApp is not enabled, and the fake WhatsApp sender is refused outside Development and Testing. Set Notifications:WhatsApp:Enabled and its secrets.";
        }

        var missing = new[]
            {
                (Key: "PhoneNumberId", Value: settings.PhoneNumberId),
                (Key: "AccessToken", Value: settings.AccessToken),
                (Key: "AppSecret", Value: settings.AppSecret),
                (Key: "VerifyToken", Value: settings.VerifyToken),
                (Key: "NumberSalt", Value: settings.NumberSalt),
            }
            .Where(s => string.IsNullOrWhiteSpace(s.Value))
            .Select(s => s.Key)
            .ToList();
        return missing.Count == 0 || development
            ? null
            : $"WhatsApp is enabled but these settings under {WhatsAppSettings.SectionName} are missing: {string.Join(", ", missing)}.";
    }
}
