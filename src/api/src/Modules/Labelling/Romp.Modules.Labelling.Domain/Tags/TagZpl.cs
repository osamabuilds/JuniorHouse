using System.Globalization;
using System.Text;
using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Labelling.Domain.Tags;

/// <summary>
/// The ZPL for a run of identical tags (D7). Text fields go through <see cref="ZplEscaper"/>; the barcode uses the printer's own
/// EAN-13 command (<c>^BE</c>) with the GTIN's 12-digit payload, so the printer draws the bars and adds the check digit. The default
/// label is 50 mm × 30 mm at 203 dpi (400 × 240 dots) until the owner chooses a label stock.
/// </summary>
public static class TagZpl
{
    public const int MaxCopies = 99_999;

    public static string Render(TagData tag, int copies)
    {
        if (copies is < 1 or > MaxCopies)
        {
            throw new DomainException($"Print between 1 and {MaxCopies:N0} tags at a time.");
        }

        var gtin = Variants.Gtin.Parse(tag.Gtin); // a corrupt barcode can never be sent to a printer

        var zpl = new StringBuilder();
        zpl.Append("^XA^CI28^PW400^LL240^LH0,0\n");
        zpl.Append("^FO24,14^A0N,26,26^FH_^FD").Append(ZplEscaper.Escape(TagData.Brand)).Append("^FS\n");
        zpl.Append("^FO24,46^A0N,22,22^FH_^FD").Append(ZplEscaper.Escape(tag.PrintedName)).Append("^FS\n");
        zpl.Append("^FO24,74^A0N,20,20^FH_^FD").Append(ZplEscaper.Escape(tag.PrintedVariantLine)).Append("^FS\n");
        zpl.Append("^FO24,100^BY2,3,64^BEN,64,Y,N^FD").Append(gtin.Payload).Append("^FS\n");
        zpl.Append("^FO24,190^A0N,30,30^FH_^FD").Append(ZplEscaper.Escape(tag.PrintedPrice)).Append("^FS\n");
        zpl.Append("^FO24,216^A0N,16,16^FH_^FD").Append(ZplEscaper.Escape(TagData.Origin)).Append("^FS\n");
        if (tag.IsConcession)
        {
            zpl.Append("^FO300,188^GB76,40,2^FS\n");
            zpl.Append("^FO312,194^A0N,28,28^FH_^FD").Append(ZplEscaper.Escape(TagData.ConcessionMark)).Append("^FS\n");
        }

        zpl.Append("^PQ").Append(copies.ToString(CultureInfo.InvariantCulture)).Append(",0,1,N\n");
        zpl.Append("^XZ\n");
        return zpl.ToString();
    }
}
