using System.Text;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Labelling.Domain.Tags;

namespace Romp.Modules.Labelling.Tests.Tags;

public sealed class TagRenderingTests
{
    private const string Gtin = "5901234123457";

    private static TagData Tag(string name = "Floral Summer Dress", bool concession = false, decimal price = 1299m) =>
        new("ROMP-STY-DRS-RED-2-3Y", Gtin, name, "Red", "2-3 Years", price, concession);

    // An independent decoder (its own copy of the EAN-13 tables), so the encoder is checked against something other than itself.
    private static readonly string[] L = ["0001101", "0011001", "0010011", "0111101", "0100011", "0110001", "0101111", "0111011", "0110111", "0001011"];
    private static readonly string[] G = ["0100111", "0110011", "0011011", "0100001", "0011101", "0111001", "0000101", "0010001", "0001001", "0010111"];
    private static readonly string[] R = ["1110010", "1100110", "1101100", "1000010", "1011100", "1001110", "1010000", "1000100", "1001000", "1110100"];
    private static readonly string[] Parity = ["LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG", "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL"];

    private static string Decode(string modules)
    {
        Assert.Equal(95, modules.Length);
        Assert.Equal("101", modules[..3]);
        Assert.Equal("01010", modules.Substring(45, 5));
        Assert.Equal("101", modules[92..]);

        var parity = new StringBuilder();
        var digits = new StringBuilder();
        for (var i = 0; i < 6; i++)
        {
            var group = modules.Substring(3 + (i * 7), 7);
            var l = Array.IndexOf(L, group);
            var g = Array.IndexOf(G, group);
            Assert.True(l >= 0 || g >= 0, $"left group {i} is not a valid EAN-13 digit");
            parity.Append(l >= 0 ? 'L' : 'G');
            digits.Append(l >= 0 ? l : g);
        }

        var first = Array.IndexOf(Parity, parity.ToString());
        Assert.True(first >= 0, "left parity pattern is not valid");
        var right = new StringBuilder();
        for (var i = 0; i < 6; i++)
        {
            var index = Array.IndexOf(R, modules.Substring(50 + (i * 7), 7));
            Assert.True(index >= 0, $"right group {i} is not a valid EAN-13 digit");
            right.Append(index);
        }

        return first + digits.ToString() + right;
    }

    [Theory]
    [InlineData("5901234123457")]
    [InlineData("4006381333931")]
    [InlineData("9780201379624")]
    [InlineData("0200000000012")]
    [Trait("Spec", "TAG-2")]
    public void Ean13_RoundTrips_ThroughAnIndependentDecoder(string gtin)
    {
        var valid = gtin.Length == 13 && Romp.Modules.Labelling.Domain.Variants.Gtin.TryParse(gtin, out _)
            ? gtin
            : gtin[..12] + Romp.Modules.Labelling.Domain.Variants.Gtin.CheckDigit(gtin[..12]);

        Assert.Equal(valid, Decode(Ean13.Encode(valid)));
    }

    [Fact]
    [Trait("Spec", "TAG-2")]
    public void Ean13_RefusesAGtinWithABadCheckDigit() =>
        Assert.Throws<DomainException>(() => Ean13.Encode("5901234123456"));

    [Fact]
    [Trait("Spec", "TAG-1")]
    [Trait("Spec", "TAG-3")]
    public void Zpl_ContainsTheRequiredContent_AndTheBarcodeCommandWithTheGtinPayload()
    {
        var zpl = TagZpl.Render(Tag(), 25);

        Assert.StartsWith("^XA", zpl, StringComparison.Ordinal);
        Assert.EndsWith("^XZ\n", zpl, StringComparison.Ordinal);
        Assert.Contains("^FDROMP^FS", zpl, StringComparison.Ordinal);
        Assert.Contains("Floral Summer Dress", zpl, StringComparison.Ordinal);
        Assert.Contains("Red / 2-3 Years", zpl, StringComparison.Ordinal);
        Assert.Contains("^BEN,64,Y,N^FD590123412345^FS", zpl, StringComparison.Ordinal); // the 12-digit payload; the printer adds the check digit
        Assert.Contains("Rs. 1,299", zpl, StringComparison.Ordinal);
        Assert.Contains("Made in Pakistan", zpl, StringComparison.Ordinal);
        Assert.Contains("^PQ25,0,1,N", zpl, StringComparison.Ordinal);
        Assert.DoesNotContain("2ND", zpl, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "TAG-4")]
    public void Zpl_ForConcessionStock_CarriesThe2ndMark_AndTheSamePrice()
    {
        var accepted = TagZpl.Render(Tag(concession: false), 1);
        var concession = TagZpl.Render(Tag(concession: true), 1);

        Assert.Contains("2ND", concession, StringComparison.Ordinal);
        Assert.Contains("Rs. 1,299", concession, StringComparison.Ordinal);
        Assert.Contains("Rs. 1,299", accepted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1299, "Rs. 1,299")]
    [InlineData(1299.5, "Rs. 1,299.50")]
    [InlineData(0, "Rs. 0")]
    [InlineData(1234567.89, "Rs. 1,234,567.89")]
    [Trait("Spec", "TAG-3")]
    public void PrintedPrice_IsFormattedFromADecimal(double price, string expected) =>
        Assert.Equal(expected, Tag(price: (decimal)price).PrintedPrice);

    [Theory]
    [InlineData("^XA^FS~DG000.GRF,1,1,00")]
    [InlineData("Name\\&with_underscore")]
    [InlineData("^FO0,0^GB400,240,240^FS")]
    [InlineData("~JA^XA^MCY^XZ")]
    [InlineData("Ünïcødé 子供 👕")]
    [InlineData("quote\" and ; semicolon")]
    [Trait("Spec", "TAG-6")]
    public void Zpl_HostileProductNames_CannotInjectCommands(string hostile)
    {
        var zpl = TagZpl.Render(Tag(name: hostile), 1);

        // The only '^' and '~' characters are ours: every command in the output is one the template wrote.
        var commandCount = zpl.Count(c => c == '^');
        var templateCommands = TagZpl.Render(Tag(name: "x"), 1).Count(c => c == '^');
        Assert.Equal(templateCommands, commandCount);
        Assert.DoesNotContain('~', zpl);
        Assert.DoesNotContain('\\', zpl);
        // Every underscore is the start of a two-digit hex escape.
        for (var i = 0; i < zpl.Length; i++)
        {
            if (zpl[i] == '_' && !(i >= 3 && zpl.AsSpan(i - 3, 3).SequenceEqual("^FH")))
            {
                Assert.True(i + 2 < zpl.Length && Uri.IsHexDigit(zpl[i + 1]) && Uri.IsHexDigit(zpl[i + 2]), $"bare underscore at {i}");
            }
        }
    }

    [Fact]
    [Trait("Spec", "TAG-6")]
    public void ZplEscaper_HexEscapesUtf8Bytes_AndKeepsPlainTextReadable()
    {
        Assert.Equal("Plain text 123", ZplEscaper.Escape("Plain text 123"));
        Assert.Equal("_5E_7E_5F_5C", ZplEscaper.Escape("^~_\\"));
        Assert.Equal("_C3_9C", ZplEscaper.Escape("Ü"));
        Assert.Equal(string.Empty, ZplEscaper.Escape(null));
    }

    [Fact]
    [Trait("Spec", "TAG-7")]
    public void LongNames_AreShortenedDeterministically_AndControlCharactersRemoved()
    {
        var name = new string('A', 10_000);

        var cleaned = TextSanitizer.Clean(name, TagData.MaxNameLength);
        Assert.Equal(TagData.MaxNameLength, cleaned.Length);
        Assert.EndsWith("...", cleaned, StringComparison.Ordinal);
        Assert.Equal(cleaned, TextSanitizer.Clean(name, TagData.MaxNameLength));

        Assert.Equal("a b c", TextSanitizer.Clean("a\u0000\u0007 b\r\n\tc  ", 40));
        Assert.Equal(string.Empty, TextSanitizer.Clean("   \u0001 ", 10));
        Assert.Equal(TagData.MaxNameLength, Tag(name: name).PrintedName.Length);
    }

    [Fact]
    [Trait("Spec", "TAG-7")]
    public void Truncation_NeverSplitsASurrogatePair()
    {
        var cleaned = TextSanitizer.Clean(new string('A', 24) + "👕👕👕👕", 28);

        for (var i = 0; i < cleaned.Length; i++)
        {
            if (char.IsHighSurrogate(cleaned[i]))
            {
                Assert.True(i + 1 < cleaned.Length && char.IsLowSurrogate(cleaned[i + 1]), "a surrogate pair was split");
                i++;
            }
            else
            {
                Assert.False(char.IsLowSurrogate(cleaned[i]), "a lone low surrogate");
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100_000)]
    public void Zpl_RefusesAnOutOfRangeCopyCount(int copies) =>
        Assert.Throws<DomainException>(() => TagZpl.Render(Tag(), copies));

    [Fact]
    [Trait("Spec", "TAG-2")]
    public void Zpl_NeverRendersACorruptGtin()
    {
        var tag = Tag() with { Gtin = "5901234123450" };

        Assert.Throws<DomainException>(() => TagZpl.Render(tag, 1));
        Assert.Throws<DomainException>(() => TagPdf.Render(tag));
    }

    [Fact]
    [Trait("Spec", "TAG-8")]
    public void Pdf_IsAWellFormedOnePageDocument_WithTheSameTextAsTheZpl()
    {
        var bytes = TagPdf.Render(Tag(concession: true));
        var text = Encoding.ASCII.GetString(bytes);

        Assert.StartsWith("%PDF-1.4", text, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", text, StringComparison.Ordinal);
        Assert.Contains("/Count 1", text, StringComparison.Ordinal);
        Assert.Contains("(Floral Summer Dress)", text, StringComparison.Ordinal);
        Assert.Contains("(Red / 2-3 Years)", text, StringComparison.Ordinal);
        Assert.Contains("(Rs. 1,299)", text, StringComparison.Ordinal);
        Assert.Contains("(Made in Pakistan)", text, StringComparison.Ordinal);
        Assert.Contains("(2ND)", text, StringComparison.Ordinal);
        Assert.Contains("(5 901234 123457)", text, StringComparison.Ordinal);

        // The xref offsets point at the objects they claim to (a viewer relies on them).
        var xrefAt = int.Parse(
            text[(text.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10)..].Split('\n')[0],
            System.Globalization.CultureInfo.InvariantCulture);
        var xref = text[xrefAt..].Split('\n');
        Assert.Equal("xref", xref[0]);
        for (var n = 1; n <= 6; n++)
        {
            var offset = int.Parse(xref[2 + n][..10], System.Globalization.CultureInfo.InvariantCulture);
            Assert.StartsWith($"{n} 0 obj", text[offset..], StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Spec", "TAG-6")]
    public void Pdf_TextIsEscapedAndAscii_SoNameCannotBreakTheDocument()
    {
        var bytes = TagPdf.Render(Tag(name: "Evil) Tj ET /Foo (\\ 子供"));
        var text = Encoding.ASCII.GetString(bytes);

        Assert.DoesNotContain("Evil) Tj", text, StringComparison.Ordinal);
        Assert.Contains("Evil\\) Tj ET /Foo \\(\\\\ ??", text, StringComparison.Ordinal);
        Assert.All(bytes, b => Assert.True(b < 128));
    }
}
