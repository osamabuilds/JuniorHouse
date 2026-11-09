using System.Collections.Concurrent;
using Polly;
using Polly.CircuitBreaker;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;

namespace Romp.Modules.Notifications.Infrastructure.Channels;

/// <summary>Circuit breaker settings (<c>Notifications:Circuit</c>). Defaults: open after half of at least five sends in a minute fail, for 30 seconds.</summary>
public sealed class ChannelCircuitOptions
{
    public const string SectionName = "Notifications:Circuit";

    public double FailureRatio { get; set; } = 0.5;

    public int MinimumThroughput { get; set; } = 5;

    public int SamplingSeconds { get; set; } = 60;

    public int BreakSeconds { get; set; } = 30;
}

/// <summary>
/// One circuit breaker per channel (CHN-4), built with Polly's strategy from <c>Microsoft.Extensions.Http.Resilience</c>. WhatsApp failing
/// opens only the WhatsApp circuit; email keeps running. Only temporary failures count: a bad address says nothing about the provider.
/// </summary>
public sealed class ChannelCircuits(ChannelCircuitOptions options, TimeProvider clock)
{
    private readonly ConcurrentDictionary<NotificationChannel, ResiliencePipeline> _pipelines = new();

    public ResiliencePipeline For(NotificationChannel channel) => _pipelines.GetOrAdd(channel, _ => new ResiliencePipelineBuilder { TimeProvider = clock }
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = options.FailureRatio,
            MinimumThroughput = options.MinimumThroughput,
            SamplingDuration = TimeSpan.FromSeconds(options.SamplingSeconds),
            BreakDuration = TimeSpan.FromSeconds(options.BreakSeconds),
            ShouldHandle = new PredicateBuilder()
                .Handle<ChannelSendException>(e => e.Category == ErrorCategory.Temporary)
                .Handle<TimeoutException>(),
        })
        .Build());
}

/// <summary>Wraps a channel sender in its channel's circuit. An open circuit fails fast as <see cref="ErrorCategory.CircuitOpen"/>, which waits without using an attempt.</summary>
internal sealed class CircuitBreakingChannelSender(IChannelSender inner, ChannelCircuits circuits) : IChannelSender
{
    public NotificationChannel Channel => inner.Channel;

    public async Task<string?> SendAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        try
        {
            return await circuits.For(inner.Channel).ExecuteAsync(async ct => await inner.SendAsync(message, ct), cancellationToken);
        }
        catch (BrokenCircuitException)
        {
            throw new ChannelSendException(ErrorCategory.CircuitOpen, "The channel's circuit is open.");
        }
    }
}
