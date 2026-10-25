using System.Text;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Tests.Support;

/// <summary>An <see cref="IImageResizer"/> that returns recognisable bytes and records its calls, so worker tests need no image library.</summary>
internal sealed class FakeImageResizer : IImageResizer
{
    public ImageProbe Info { get; set; } = new(1200, 1600);

    /// <summary>When set, every call throws this rejection.</summary>
    public ImageRejectedException? Rejection { get; set; }

    public List<(int Width, DerivativeFormat Format)> Resized { get; } = [];

    public ImageProbe Probe(ReadOnlySpan<byte> original, long maxPixels) => Rejection is null ? Info : throw Rejection;

    public byte[] Resize(ReadOnlySpan<byte> original, int width, DerivativeFormat format, int quality, long maxPixels)
    {
        if (Rejection is not null)
        {
            throw Rejection;
        }

        Resized.Add((width, format));
        return Encoding.ASCII.GetBytes($"{format}-{width}-{original.Length}");
    }
}
