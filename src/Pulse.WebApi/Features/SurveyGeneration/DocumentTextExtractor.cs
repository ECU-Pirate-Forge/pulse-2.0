using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Pulse.WebApi.Features.SurveyGeneration;

public sealed class DocumentTextExtractor
{
    public const int MaximumFiles = 5;
    public const int MaximumFileBytes = 5 * 1024 * 1024;
    public const int MaximumDocumentCharacters = 50_000;
    public const int MaximumContextCharacters = 10_000;

    public string Extract(string fileName, byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > MaximumFileBytes)
            throw new GroundingValidationException("Each document must contain between 1 byte and 5 MB.");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        try
        {
            var text = extension switch
            {
                ".txt" or ".md" => new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'),
                ".pdf" => ReadPdf(bytes),
                ".docx" => ReadDocx(bytes),
                _ => throw new GroundingValidationException("Supported document types are TXT, MD, PDF, and DOCX.")
            };
            if (string.IsNullOrWhiteSpace(text))
                throw new GroundingValidationException("The document contains no readable text. Scanned PDFs require OCR before upload.");
            if (text.Length > MaximumDocumentCharacters)
                throw new GroundingValidationException("Each document may contain at most 50,000 extracted characters. Split the document before uploading.");
            if (text.Contains('\0'))
                throw new GroundingValidationException("The document contains invalid text.");
            return text.Trim();
        }
        catch (Exception ex) when (ex is InvalidDataException or DecoderFallbackException or XmlException ||
                                   ex is UglyToad.PdfPig.Core.PdfDocumentFormatException or UglyToad.PdfPig.Exceptions.PdfDocumentEncryptedException)
        {
            throw new GroundingValidationException("The document could not be read. Check its format and try again.", ex);
        }
    }

    private static string ReadPdf(byte[] bytes)
    {
        using var document = PdfDocument.Open(bytes);
        if (document.NumberOfPages > 100)
            throw new GroundingValidationException("PDFs may contain at most 100 pages.");
        var result = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            result.AppendLine(ContentOrderTextExtractor.GetText(page));
            if (result.Length > MaximumDocumentCharacters)
                throw new GroundingValidationException("The PDF exceeds the 50,000-character limit.");
        }
        return result.ToString();
    }

    private static string ReadDocx(byte[] bytes)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var entry = archive.GetEntry("word/document.xml")
            ?? throw new GroundingValidationException("This file is not a readable DOCX document.");
        if (entry.Length > MaximumFileBytes)
            throw new GroundingValidationException("The expanded DOCX document exceeds the size limit.");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumFileBytes
        });
        var xml = XDocument.Load(reader);
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return string.Join("\n", xml.Descendants(word + "p")
            .Select(paragraph => string.Concat(paragraph.Descendants(word + "t").Select(node => node.Value))));
    }
}

public sealed class GroundingValidationException(string message, Exception? inner = null) : Exception(message, inner);
