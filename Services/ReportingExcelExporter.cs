using System.Globalization;
using System.IO.Compression;
using System.Xml;
using TimeTracker.ViewModels;

namespace TimeTracker.Services;

public static class ReportingExcelExporter
{
    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static byte[] Export(ReportingViewModel report)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteXml(archive, "[Content_Types].xml", writer =>
            {
                writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
                WriteDefault(writer, "rels", "application/vnd.openxmlformats-package.relationships+xml");
                WriteDefault(writer, "xml", "application/xml");
                WriteOverride(writer, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
                WriteOverride(writer, "/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                writer.WriteEndElement();
            });

            WriteXml(archive, "_rels/.rels", writer =>
            {
                writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                writer.WriteStartElement("Relationship");
                writer.WriteAttributeString("Id", "rId1");
                writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
                writer.WriteAttributeString("Target", "xl/workbook.xml");
                writer.WriteEndElement();
                writer.WriteEndElement();
            });

            WriteXml(archive, "xl/workbook.xml", writer =>
            {
                writer.WriteStartElement("workbook", SpreadsheetNamespace);
                writer.WriteAttributeString("xmlns", "r", "http://www.w3.org/2000/xmlns/", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                writer.WriteStartElement("sheets");
                writer.WriteStartElement("sheet");
                writer.WriteAttributeString("name", "Time report");
                writer.WriteAttributeString("sheetId", "1");
                writer.WriteAttributeString(
                    "r",
                    "id",
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
                    "rId1");
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
            });

            WriteXml(archive, "xl/_rels/workbook.xml.rels", writer =>
            {
                writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                writer.WriteStartElement("Relationship");
                writer.WriteAttributeString("Id", "rId1");
                writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");
                writer.WriteAttributeString("Target", "worksheets/sheet1.xml");
                writer.WriteEndElement();
                writer.WriteEndElement();
            });

            WriteXml(archive, "xl/worksheets/sheet1.xml", writer =>
            {
                writer.WriteStartElement("worksheet", SpreadsheetNamespace);
                writer.WriteStartElement("sheetData");

                WriteRow(writer, 1, ("A", "Customer"), ("B", "Project"), ("C", "Hours"));
                for (var i = 0; i < report.Projects.Count; i++)
                {
                    var project = report.Projects[i];
                    var rowNumber = i + 2;
                    writer.WriteStartElement("row");
                    writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
                    WriteTextCell(writer, $"A{rowNumber}", project.CustomerName);
                    WriteTextCell(writer, $"B{rowNumber}", project.ProjectName);
                    WriteNumberCell(writer, $"C{rowNumber}", project.Hours);
                    writer.WriteEndElement();
                }

                var totalRow = report.Projects.Count + 2;
                writer.WriteStartElement("row");
                writer.WriteAttributeString("r", totalRow.ToString(CultureInfo.InvariantCulture));
                WriteTextCell(writer, $"A{totalRow}", "Total");
                WriteTextCell(writer, $"B{totalRow}", string.Empty);
                WriteNumberCell(writer, $"C{totalRow}", report.TotalHours);
                writer.WriteEndElement();

                writer.WriteEndElement();
                writer.WriteEndElement();
            });
        }

        return stream.ToArray();
    }

    private static void WriteXml(ZipArchive archive, string path, Action<XmlWriter> write)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true
        });
        write(writer);
    }

    private static void WriteDefault(XmlWriter writer, string extension, string contentType)
    {
        writer.WriteStartElement("Default");
        writer.WriteAttributeString("Extension", extension);
        writer.WriteAttributeString("ContentType", contentType);
        writer.WriteEndElement();
    }

    private static void WriteOverride(XmlWriter writer, string partName, string contentType)
    {
        writer.WriteStartElement("Override");
        writer.WriteAttributeString("PartName", partName);
        writer.WriteAttributeString("ContentType", contentType);
        writer.WriteEndElement();
    }

    private static void WriteRow(XmlWriter writer, int rowNumber, params (string Column, string Value)[] values)
    {
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
        foreach (var (column, value) in values)
        {
            WriteTextCell(writer, $"{column}{rowNumber}", value);
        }
        writer.WriteEndElement();
    }

    private static void WriteTextCell(XmlWriter writer, string reference, string value)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", reference);
        writer.WriteAttributeString("t", "inlineStr");
        writer.WriteStartElement("is");
        writer.WriteElementString("t", SpreadsheetNamespace, value);
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteNumberCell(XmlWriter writer, string reference, decimal value)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", reference);
        writer.WriteElementString("v", SpreadsheetNamespace, value.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }
}
