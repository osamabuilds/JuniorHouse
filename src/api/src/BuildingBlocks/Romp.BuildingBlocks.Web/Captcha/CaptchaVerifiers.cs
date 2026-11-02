using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application.Captcha;

namespace Romp.BuildingBlocks.Web.Captcha;

/// <summary><c>Captcha:Turnstile:*</c>. The secret key comes from secrets, never from the repository.</summary>
public sealed class TurnstileOptions
{
    public const string Section = "Captcha:Turnstile";

    public string SecretKey { get; set; } = string.Empty;
}

/// <summary>Development and Testing: accepts exactly <see cref="PassToken"/>, so e2e tests and the demo need no Cloudflare account.</summary>
public sealed class FakeCaptchaVerifier : ICaptchaVerifier
{
    public const string PassToken = "dev-captcha-pass";

    public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken) =>
        Task.FromResult(string.Equals(token, PassToken, StringComparison.Ordinal));
}

/// <summary>Cloudflare Turnstile server-side check. A fixed host, a short timeout (set on the client), fail closed on any error.</summary>
public sealed class TurnstileCaptchaVerifier(HttpClient http, IOptions<TurnstileOptions> options) : ICaptchaVerifier
{
    public const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("secret", options.Value.SecretKey),
            new("response", token),
        };
        if (!string.IsNullOrEmpty(remoteIp))
        {
            form.Add(new("remoteip", remoteIp));
        }

        try
        {
            using var response = await http.PostAsync(VerifyUrl, new FormUrlEncodedContent(form), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            return json.RootElement.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return false;
        }
    }
}

public static class CaptchaServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ICaptchaVerifier"/>. The fake (accepts only <see cref="FakeCaptchaVerifier.PassToken"/>) is for Development and
    /// Testing only; anywhere else Turnstile needs <c>Captcha:Turnstile:SecretKey</c>, checked when the verifier is first used. Safe to call
    /// from several modules: the first registration wins.
    /// </summary>
    public static IServiceCollection AddCaptcha(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TurnstileOptions>().Bind(configuration.GetSection(TurnstileOptions.Section));
        services.TryAddSingleton<FakeCaptchaVerifier>();
        if (services.Any(d => d.ServiceType == typeof(TurnstileCaptchaVerifier)))
        {
            return services;
        }

        services.AddHttpClient<TurnstileCaptchaVerifier>(client => client.Timeout = TimeSpan.FromSeconds(5));
        services.TryAddScoped<ICaptchaVerifier>(sp =>
        {
            if (RompAuthorizationExtensions.IsDevelopmentLike(sp.GetRequiredService<IHostEnvironment>()))
            {
                return sp.GetRequiredService<FakeCaptchaVerifier>();
            }

            if (string.IsNullOrWhiteSpace(sp.GetRequiredService<IOptions<TurnstileOptions>>().Value.SecretKey))
            {
                throw new InvalidOperationException("Captcha:Turnstile:SecretKey is required outside Development and Testing.");
            }

            return sp.GetRequiredService<TurnstileCaptchaVerifier>();
        });
        return services;
    }
}
