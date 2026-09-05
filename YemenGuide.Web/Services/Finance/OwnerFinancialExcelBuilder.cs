using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using YemenGuide.Core.DTOs.Owner;

namespace YemenGuide.Web.Services.Finance
{
    /// <summary>
    /// منشئ Excel حقيقي بصيغة XLSX بدون أي مكتبات خارجية.
    ///
    /// الملف الناتج:
    /// - يدعم العربية وRTL.
    /// - يحتوي ورقة ملخص.
    /// - ينشئ ورقة مستقلة لكل قسم من أقسام التقرير.
    /// - يثبت صف العناوين ويضيف AutoFilter.
    /// - يطبق تنسيقات الأرقام والمبالغ والتواريخ.
    /// </summary>
    public sealed class OwnerFinancialExcelBuilder
    {
        private const string SpreadsheetNamespace =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        private const string RelationshipNamespace =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        private const string PackageRelationshipNamespace =
            "http://schemas.openxmlformats.org/package/2006/relationships";

        private const string ContentTypesNamespace =
            "http://schemas.openxmlformats.org/package/2006/content-types";

        private const string ExtendedPropertiesNamespace =
            "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";

        private const string DocumentPropertiesVTypesNamespace =
            "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";

        private sealed class ExcelSheetDefinition
        {
            public string Name { get; set; } = "التقرير";
            public string Title { get; set; } = "التقرير المالي";
            public string Description { get; set; } = string.Empty;
            public List<FinancialReportExportColumnDto> Columns { get; set; } =
                new List<FinancialReportExportColumnDto>();
            public List<FinancialReportExportRowDto> Rows { get; set; } =
                new List<FinancialReportExportRowDto>();
        }

        public byte[] Build(
            FinancialReportExportDto report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var sheets = BuildSheetDefinitions(report);

            using var output = new MemoryStream();

            using (
                var archive = new ZipArchive(
                    output,
                    ZipArchiveMode.Create,
                    leaveOpen: true))
            {
                WriteContentTypes(archive, sheets.Count);
                WriteRootRelationships(archive);
                WriteCoreProperties(archive, report);
                WriteAppProperties(archive, sheets);
                WriteWorkbook(archive, sheets);
                WriteWorkbookRelationships(archive, sheets.Count);
                WriteStyles(archive);

                for (var index = 0; index < sheets.Count; index++)
                {
                    WriteWorksheet(
                        archive,
                        sheets[index],
                        index + 1,
                        report);
                }
            }

            return output.ToArray();
        }

        private static List<ExcelSheetDefinition> BuildSheetDefinitions(
            FinancialReportExportDto report)
        {
            var definitions = new List<ExcelSheetDefinition>
            {
                CreateSummarySheet(report)
            };

            var sections = report.Sections ??
                new List<FinancialReportExportSectionDto>();

            if (sections.Count == 0)
            {
                sections = new List<FinancialReportExportSectionDto>
                {
                    new FinancialReportExportSectionDto
                    {
                        Key = report.ReportType,
                        Title = report.Title,
                        Description = report.Subtitle,
                        Columns = report.Columns ??
                            new List<FinancialReportExportColumnDto>(),
                        Rows = report.Rows ??
                            new List<FinancialReportExportRowDto>()
                    }
                };
            }

            var usedNames = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                "الملخص"
            };

            foreach (var section in sections)
            {
                var baseName = string.IsNullOrWhiteSpace(section.Title)
                    ? "البيانات"
                    : section.Title;

                var name = MakeUniqueSheetName(
                    baseName,
                    usedNames);

                definitions.Add(new ExcelSheetDefinition
                {
                    Name = name,
                    Title = string.IsNullOrWhiteSpace(section.Title)
                        ? report.Title
                        : section.Title,
                    Description = section.Description ?? string.Empty,
                    Columns = section.Columns ??
                        new List<FinancialReportExportColumnDto>(),
                    Rows = section.Rows ??
                        new List<FinancialReportExportRowDto>()
                });
            }

            if (report.PlaceBreakdown?.Count > 0)
            {
                definitions.Add(CreateBreakdownSheet(
                    "حسب المنشأة",
                    "توزيع النتائج حسب المنشأة",
                    report.PlaceBreakdown,
                    usedNames));
            }

            if (report.StatusBreakdown?.Count > 0)
            {
                definitions.Add(CreateBreakdownSheet(
                    "حسب الحالة",
                    "توزيع النتائج حسب الحالة",
                    report.StatusBreakdown,
                    usedNames));
            }

            return definitions;
        }

        private static ExcelSheetDefinition CreateSummarySheet(
            FinancialReportExportDto report)
        {
            var columns = new List<FinancialReportExportColumnDto>
            {
                new FinancialReportExportColumnDto
                {
                    Key = "Label",
                    Label = "المؤشر",
                    DataType = "Text",
                    Width = 34d
                },
                new FinancialReportExportColumnDto
                {
                    Key = "Value",
                    Label = "القيمة",
                    DataType = "Text",
                    Alignment = "Left",
                    Width = 28d
                },
                new FinancialReportExportColumnDto
                {
                    Key = "Hint",
                    Label = "التوضيح",
                    DataType = "Text",
                    Width = 46d
                }
            };

            var rows = new List<FinancialReportExportRowDto>();

            void AddRow(
                string label,
                string value,
                string? hint = null)
            {
                rows.Add(new FinancialReportExportRowDto
                {
                    Cells = new List<string>
                    {
                        label,
                        value,
                        hint ?? string.Empty
                    },
                    TypedCells = new List<FinancialReportExportCellDto>
                    {
                        TextCell(label),
                        TextCell(value),
                        TextCell(hint)
                    }
                });
            }

            AddRow("نوع التقرير", report.Title, report.Subtitle);
            AddRow("المنشأة", report.SelectedPlaceName);
            AddRow("الفترة", report.PeriodLabel, report.DateScopeLabel);
            AddRow("الفلاتر", report.FilterDescription);
            AddRow(
                "وقت الإنشاء",
                report.GeneratedAt.ToString(
                    "yyyy-MM-dd HH:mm",
                    CultureInfo.InvariantCulture));
            AddRow(
                "عدد السجلات",
                report.TotalAvailableRows.ToString(
                    "N0",
                    CultureInfo.GetCultureInfo("en-US")));

            foreach (var metric in report.Metrics ??
                new List<FinancialReportMetricDto>())
            {
                AddRow(metric.Label, metric.Value, metric.Hint);
            }

            foreach (var insight in report.Insights ??
                new List<FinancialReportInsightDto>())
            {
                AddRow(
                    string.Concat("تحليل: ", insight.Title),
                    insight.Value,
                    insight.Description);
            }

            return new ExcelSheetDefinition
            {
                Name = "الملخص",
                Title = report.Title,
                Description = report.Subtitle,
                Columns = columns,
                Rows = rows
            };
        }

        private static ExcelSheetDefinition CreateBreakdownSheet(
            string requestedName,
            string title,
            IReadOnlyCollection<FinancialReportBreakdownItemDto> items,
            ISet<string> usedNames)
        {
            var name = MakeUniqueSheetName(
                requestedName,
                usedNames);

            var columns = new List<FinancialReportExportColumnDto>
            {
                new FinancialReportExportColumnDto
                {
                    Key = "Label",
                    Label = "البيان",
                    DataType = "Text",
                    Width = 34d
                },
                new FinancialReportExportColumnDto
                {
                    Key = "Count",
                    Label = "العدد",
                    DataType = "Number",
                    Alignment = "Center",
                    Width = 14d
                },
                new FinancialReportExportColumnDto
                {
                    Key = "Amount",
                    Label = "المبلغ",
                    DataType = "Money",
                    Alignment = "Left",
                    Width = 20d
                },
                new FinancialReportExportColumnDto
                {
                    Key = "Percentage",
                    Label = "النسبة",
                    DataType = "Percentage",
                    Alignment = "Center",
                    Width = 14d
                }
            };

            var rows = items
                .Select(item =>
                    new FinancialReportExportRowDto
                    {
                        Cells = new List<string>
                        {
                            item.Label,
                            item.Count.ToString(CultureInfo.InvariantCulture),
                            item.Amount.ToString("0.##", CultureInfo.InvariantCulture),
                            item.Percentage.ToString("0.##", CultureInfo.InvariantCulture)
                        },
                        TypedCells = new List<FinancialReportExportCellDto>
                        {
                            TextCell(item.Label),
                            IntegerCell(item.Count),
                            DecimalCell(item.Amount, "Money"),
                            DecimalCell(item.Percentage / 100m, "Percentage")
                        }
                    })
                .ToList();

            return new ExcelSheetDefinition
            {
                Name = name,
                Title = title,
                Description = "القيم محسوبة من نفس النتائج المستخدمة في الشاشة والطباعة.",
                Columns = columns,
                Rows = rows
            };
        }

        private static FinancialReportExportCellDto TextCell(
            string? value) =>
                new FinancialReportExportCellDto
                {
                    Text = value?.Trim() ?? string.Empty,
                    DataType = "Text"
                };

        private static FinancialReportExportCellDto IntegerCell(
            long value) =>
                new FinancialReportExportCellDto
                {
                    Text = value.ToString(CultureInfo.InvariantCulture),
                    DataType = "Number",
                    IntegerValue = value
                };

        private static FinancialReportExportCellDto DecimalCell(
            decimal value,
            string dataType) =>
                new FinancialReportExportCellDto
                {
                    Text = value.ToString("0.####", CultureInfo.InvariantCulture),
                    DataType = dataType,
                    DecimalValue = value
                };

        private static string MakeUniqueSheetName(
            string requestedName,
            ISet<string> usedNames)
        {
            var sanitized = SanitizeSheetName(requestedName);
            var candidate = sanitized;
            var suffix = 2;

            while (usedNames.Contains(candidate))
            {
                var suffixText = string.Concat(" (", suffix, ")");
                var maximumBaseLength = Math.Max(1, 31 - suffixText.Length);
                var baseName = sanitized.Length > maximumBaseLength
                    ? sanitized.Substring(0, maximumBaseLength)
                    : sanitized;

                candidate = string.Concat(baseName, suffixText);
                suffix += 1;
            }

            usedNames.Add(candidate);
            return candidate;
        }

        private static string SanitizeSheetName(
            string? value)
        {
            var name = string.IsNullOrWhiteSpace(value)
                ? "التقرير"
                : value.Trim();

            foreach (var invalidCharacter in new[]
            {
                ':', '\\', '/', '?', '*', '[', ']'
            })
            {
                name = name.Replace(invalidCharacter, '-');
            }

            if (name.Length > 31)
            {
                name = name.Substring(0, 31);
            }

            return string.IsNullOrWhiteSpace(name)
                ? "التقرير"
                : name;
        }

        private static void WriteContentTypes(
            ZipArchive archive,
            int sheetCount)
        {
            WriteXmlEntry(
                archive,
                "[Content_Types].xml",
                writer =>
                {
                    writer.WriteStartElement("Types", ContentTypesNamespace);

                    WriteDefaultContentType(
                        writer,
                        "rels",
                        "application/vnd.openxmlformats-package.relationships+xml");

                    WriteDefaultContentType(
                        writer,
                        "xml",
                        "application/xml");

                    WriteOverrideContentType(
                        writer,
                        "/xl/workbook.xml",
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");

                    WriteOverrideContentType(
                        writer,
                        "/xl/styles.xml",
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");

                    WriteOverrideContentType(
                        writer,
                        "/docProps/core.xml",
                        "application/vnd.openxmlformats-package.core-properties+xml");

                    WriteOverrideContentType(
                        writer,
                        "/docProps/app.xml",
                        "application/vnd.openxmlformats-officedocument.extended-properties+xml");

                    for (var index = 1; index <= sheetCount; index++)
                    {
                        WriteOverrideContentType(
                            writer,
                            string.Concat("/xl/worksheets/sheet", index, ".xml"),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                    }

                    writer.WriteEndElement();
                });
        }

        private static void WriteDefaultContentType(
            XmlWriter writer,
            string extension,
            string contentType)
        {
            writer.WriteStartElement("Default", ContentTypesNamespace);
            writer.WriteAttributeString("Extension", extension);
            writer.WriteAttributeString("ContentType", contentType);
            writer.WriteEndElement();
        }

        private static void WriteOverrideContentType(
            XmlWriter writer,
            string partName,
            string contentType)
        {
            writer.WriteStartElement("Override", ContentTypesNamespace);
            writer.WriteAttributeString("PartName", partName);
            writer.WriteAttributeString("ContentType", contentType);
            writer.WriteEndElement();
        }

        private static void WriteRootRelationships(
            ZipArchive archive)
        {
            WriteXmlEntry(
                archive,
                "_rels/.rels",
                writer =>
                {
                    writer.WriteStartElement("Relationships", PackageRelationshipNamespace);

                    WriteRelationship(
                        writer,
                        "rId1",
                        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument",
                        "xl/workbook.xml");

                    WriteRelationship(
                        writer,
                        "rId2",
                        "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties",
                        "docProps/core.xml");

                    WriteRelationship(
                        writer,
                        "rId3",
                        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties",
                        "docProps/app.xml");

                    writer.WriteEndElement();
                });
        }

        private static void WriteRelationship(
            XmlWriter writer,
            string id,
            string type,
            string target)
        {
            writer.WriteStartElement("Relationship", PackageRelationshipNamespace);
            writer.WriteAttributeString("Id", id);
            writer.WriteAttributeString("Type", type);
            writer.WriteAttributeString("Target", target);
            writer.WriteEndElement();
        }

        private static void WriteCoreProperties(
            ZipArchive archive,
            FinancialReportExportDto report)
        {
            WriteXmlEntry(
                archive,
                "docProps/core.xml",
                writer =>
                {
                    writer.WriteStartElement(
                        "cp",
                        "coreProperties",
                        "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");

                    writer.WriteAttributeString(
                        "xmlns",
                        "dc",
                        null,
                        "http://purl.org/dc/elements/1.1/");

                    writer.WriteAttributeString(
                        "xmlns",
                        "dcterms",
                        null,
                        "http://purl.org/dc/terms/");

                    writer.WriteAttributeString(
                        "xmlns",
                        "xsi",
                        null,
                        "http://www.w3.org/2001/XMLSchema-instance");

                    writer.WriteElementString(
                        "dc",
                        "creator",
                        "http://purl.org/dc/elements/1.1/",
                        report.PlatformName);

                    writer.WriteElementString(
                        "dc",
                        "title",
                        "http://purl.org/dc/elements/1.1/",
                        report.Title);

                    writer.WriteStartElement(
                        "dcterms",
                        "created",
                        "http://purl.org/dc/terms/");
                    writer.WriteAttributeString(
                        "xsi",
                        "type",
                        "http://www.w3.org/2001/XMLSchema-instance",
                        "dcterms:W3CDTF");
                    writer.WriteString(
                        report.GeneratedAt.ToUniversalTime().ToString(
                            "yyyy-MM-ddTHH:mm:ssZ",
                            CultureInfo.InvariantCulture));
                    writer.WriteEndElement();

                    writer.WriteEndElement();
                });
        }

        private static void WriteAppProperties(
            ZipArchive archive,
            IReadOnlyCollection<ExcelSheetDefinition> sheets)
        {
            WriteXmlEntry(
                archive,
                "docProps/app.xml",
                writer =>
                {
                    writer.WriteStartElement(
                        "Properties",
                        ExtendedPropertiesNamespace);
                    writer.WriteAttributeString(
                        "xmlns",
                        "vt",
                        null,
                        DocumentPropertiesVTypesNamespace);

                    writer.WriteElementString("Application", ExtendedPropertiesNamespace, "دليلك اليمني");

                    writer.WriteStartElement("HeadingPairs", ExtendedPropertiesNamespace);
                    writer.WriteStartElement(
                        "vt",
                        "vector",
                        DocumentPropertiesVTypesNamespace);
                    writer.WriteAttributeString("size", "2");
                    writer.WriteAttributeString("baseType", "variant");
                    writer.WriteStartElement("vt", "variant", DocumentPropertiesVTypesNamespace);
                    writer.WriteElementString("vt", "lpstr", DocumentPropertiesVTypesNamespace, "Worksheets");
                    writer.WriteEndElement();
                    writer.WriteStartElement("vt", "variant", DocumentPropertiesVTypesNamespace);
                    writer.WriteElementString(
                        "vt",
                        "i4",
                        DocumentPropertiesVTypesNamespace,
                        sheets.Count.ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteEndElement();

                    writer.WriteStartElement("TitlesOfParts", ExtendedPropertiesNamespace);
                    writer.WriteStartElement(
                        "vt",
                        "vector",
                        DocumentPropertiesVTypesNamespace);
                    writer.WriteAttributeString(
                        "size",
                        sheets.Count.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("baseType", "lpstr");

                    foreach (var sheet in sheets)
                    {
                        writer.WriteElementString("vt", "lpstr", DocumentPropertiesVTypesNamespace, sheet.Name);
                    }

                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                });
        }

        private static void WriteWorkbook(
            ZipArchive archive,
            IReadOnlyCollection<ExcelSheetDefinition> sheets)
        {
            WriteXmlEntry(
                archive,
                "xl/workbook.xml",
                writer =>
                {
                    writer.WriteStartElement("workbook", SpreadsheetNamespace);
                    writer.WriteAttributeString("xmlns", "r", null, RelationshipNamespace);

                    writer.WriteStartElement("bookViews", SpreadsheetNamespace);
                    writer.WriteStartElement("workbookView", SpreadsheetNamespace);
                    writer.WriteAttributeString("activeTab", "0");
                    writer.WriteEndElement();
                    writer.WriteEndElement();

                    writer.WriteStartElement("sheets", SpreadsheetNamespace);

                    var index = 1;
                    foreach (var sheet in sheets)
                    {
                        writer.WriteStartElement("sheet", SpreadsheetNamespace);
                        writer.WriteAttributeString("name", sheet.Name);
                        writer.WriteAttributeString(
                            "sheetId",
                            index.ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString(
                            "r",
                            "id",
                            RelationshipNamespace,
                            string.Concat("rId", index + 1));
                        writer.WriteEndElement();
                        index += 1;
                    }

                    writer.WriteEndElement();
                    writer.WriteStartElement("calcPr", SpreadsheetNamespace);
                    writer.WriteAttributeString("calcId", "191029");
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                });
        }

        private static void WriteWorkbookRelationships(
            ZipArchive archive,
            int sheetCount)
        {
            WriteXmlEntry(
                archive,
                "xl/_rels/workbook.xml.rels",
                writer =>
                {
                    writer.WriteStartElement("Relationships", PackageRelationshipNamespace);

                    WriteRelationship(
                        writer,
                        "rId1",
                        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles",
                        "styles.xml");

                    for (var index = 1; index <= sheetCount; index++)
                    {
                        WriteRelationship(
                            writer,
                            string.Concat("rId", index + 1),
                            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet",
                            string.Concat("worksheets/sheet", index, ".xml"));
                    }

                    writer.WriteEndElement();
                });
        }

        private static void WriteStyles(
            ZipArchive archive)
        {
            WriteXmlEntry(
                archive,
                "xl/styles.xml",
                writer =>
                {
                    writer.WriteStartElement("styleSheet", SpreadsheetNamespace);

                    writer.WriteStartElement("numFmts", SpreadsheetNamespace);
                    writer.WriteAttributeString("count", "4");
                    WriteNumberFormat(writer, 164, "#,##0.00;[Red]-#,##0.00");
                    WriteNumberFormat(writer, 165, "yyyy-mm-dd");
                    WriteNumberFormat(writer, 166, "yyyy-mm-dd hh:mm");
                    WriteNumberFormat(writer, 167, "0.00%");
                    writer.WriteEndElement();

                    writer.WriteStartElement("fonts", SpreadsheetNamespace);
                    writer.WriteAttributeString("count", "4");
                    WriteFont(writer, "Calibri", 11d, "1F344D", false, false);
                    WriteFont(writer, "Calibri", 16d, "FFFFFF", true, false);
                    WriteFont(writer, "Calibri", 11d, "FFFFFF", true, false);
                    WriteFont(writer, "Calibri", 10d, "05203C", true, false);
                    writer.WriteEndElement();

                    writer.WriteStartElement("fills", SpreadsheetNamespace);
                    writer.WriteAttributeString("count", "6");
                    WritePatternFill(writer, "none", null);
                    WritePatternFill(writer, "gray125", null);
                    WritePatternFill(writer, "solid", "05203C");
                    WritePatternFill(writer, "solid", "00A6DA");
                    WritePatternFill(writer, "solid", "EAF9FD");
                    WritePatternFill(writer, "solid", "F7FAFC");
                    writer.WriteEndElement();

                    writer.WriteStartElement("borders", SpreadsheetNamespace);
                    writer.WriteAttributeString("count", "2");
                    WriteBorder(writer, false);
                    WriteBorder(writer, true);
                    writer.WriteEndElement();

                    writer.WriteStartElement("cellStyleXfs", SpreadsheetNamespace);
                    writer.WriteAttributeString("count", "1");
                    WriteCellXf(writer, 0, 0, 0, 0, false, null, null);
                    writer.WriteEndElement();

                    writer.WriteStartElement("cellXfs", SpreadsheetNamespace);
                    writer.WriteAttributeString("count", "11");
                    WriteCellXf(writer, 0, 0, 0, 0, false, null, "right");
                    WriteCellXf(writer, 1, 2, 0, 0, true, "center", "center");
                    WriteCellXf(writer, 3, 4, 1, 0, true, "right", "center");
                    WriteCellXf(writer, 0, 5, 1, 0, false, "right", "center");
                    WriteCellXf(writer, 2, 3, 1, 0, true, "center", "center");
                    WriteCellXf(writer, 0, 0, 1, 0, false, "right", "center");
                    WriteCellXf(writer, 0, 0, 1, 0, false, "center", "center");
                    WriteCellXf(writer, 0, 0, 1, 164, false, "left", "center");
                    WriteCellXf(writer, 0, 0, 1, 165, false, "center", "center");
                    WriteCellXf(writer, 0, 0, 1, 166, false, "center", "center");
                    WriteCellXf(writer, 0, 0, 1, 167, false, "center", "center");
                    writer.WriteEndElement();

                    writer.WriteStartElement("cellStyles", SpreadsheetNamespace);
                    writer.WriteAttributeString("count", "1");
                    writer.WriteStartElement("cellStyle", SpreadsheetNamespace);
                    writer.WriteAttributeString("name", "Normal");
                    writer.WriteAttributeString("xfId", "0");
                    writer.WriteAttributeString("builtinId", "0");
                    writer.WriteEndElement();
                    writer.WriteEndElement();

                    writer.WriteEndElement();
                });
        }

        private static void WriteNumberFormat(
            XmlWriter writer,
            int id,
            string formatCode)
        {
            writer.WriteStartElement("numFmt", SpreadsheetNamespace);
            writer.WriteAttributeString("numFmtId", id.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("formatCode", formatCode);
            writer.WriteEndElement();
        }

        private static void WriteFont(
            XmlWriter writer,
            string name,
            double size,
            string color,
            bool bold,
            bool italic)
        {
            writer.WriteStartElement("font", SpreadsheetNamespace);
            if (bold) writer.WriteElementString("b", SpreadsheetNamespace, string.Empty);
            if (italic) writer.WriteElementString("i", SpreadsheetNamespace, string.Empty);
            writer.WriteStartElement("sz", SpreadsheetNamespace);
            writer.WriteAttributeString("val", size.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
            writer.WriteStartElement("color", SpreadsheetNamespace);
            writer.WriteAttributeString("rgb", string.Concat("FF", color));
            writer.WriteEndElement();
            writer.WriteStartElement("name", SpreadsheetNamespace);
            writer.WriteAttributeString("val", name);
            writer.WriteEndElement();
            writer.WriteStartElement("family", SpreadsheetNamespace);
            writer.WriteAttributeString("val", "2");
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WritePatternFill(
            XmlWriter writer,
            string patternType,
            string? foregroundColor)
        {
            writer.WriteStartElement("fill", SpreadsheetNamespace);
            writer.WriteStartElement("patternFill", SpreadsheetNamespace);
            writer.WriteAttributeString("patternType", patternType);

            if (!string.IsNullOrWhiteSpace(foregroundColor))
            {
                writer.WriteStartElement("fgColor", SpreadsheetNamespace);
                writer.WriteAttributeString("rgb", string.Concat("FF", foregroundColor));
                writer.WriteEndElement();
                writer.WriteStartElement("bgColor", SpreadsheetNamespace);
                writer.WriteAttributeString("indexed", "64");
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteBorder(
            XmlWriter writer,
            bool visible)
        {
            writer.WriteStartElement("border", SpreadsheetNamespace);

            foreach (var side in new[] { "left", "right", "top", "bottom" })
            {
                writer.WriteStartElement(side, SpreadsheetNamespace);
                if (visible)
                {
                    writer.WriteAttributeString("style", "thin");
                    writer.WriteStartElement("color", SpreadsheetNamespace);
                    writer.WriteAttributeString("rgb", "FFDCE8EE");
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }

            writer.WriteElementString("diagonal", SpreadsheetNamespace, string.Empty);
            writer.WriteEndElement();
        }

        private static void WriteCellXf(
            XmlWriter writer,
            int fontId,
            int fillId,
            int borderId,
            int numberFormatId,
            bool applyFont,
            string? horizontalAlignment,
            string? verticalAlignment)
        {
            writer.WriteStartElement("xf", SpreadsheetNamespace);
            writer.WriteAttributeString("numFmtId", numberFormatId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("fontId", fontId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("fillId", fillId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("borderId", borderId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("xfId", "0");
            writer.WriteAttributeString("applyAlignment", "1");
            writer.WriteAttributeString("applyBorder", borderId > 0 ? "1" : "0");
            writer.WriteAttributeString("applyFill", fillId > 0 ? "1" : "0");
            writer.WriteAttributeString("applyFont", applyFont ? "1" : "0");
            writer.WriteAttributeString("applyNumberFormat", numberFormatId > 0 ? "1" : "0");

            writer.WriteStartElement("alignment", SpreadsheetNamespace);
            if (!string.IsNullOrWhiteSpace(horizontalAlignment))
            {
                writer.WriteAttributeString("horizontal", horizontalAlignment);
            }
            if (!string.IsNullOrWhiteSpace(verticalAlignment))
            {
                writer.WriteAttributeString("vertical", verticalAlignment);
            }
            writer.WriteAttributeString("wrapText", "1");
            writer.WriteAttributeString("readingOrder", "2");
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteWorksheet(
            ZipArchive archive,
            ExcelSheetDefinition sheet,
            int sheetIndex,
            FinancialReportExportDto report)
        {
            WriteXmlEntry(
                archive,
                string.Concat("xl/worksheets/sheet", sheetIndex, ".xml"),
                writer =>
                {
                    writer.WriteStartElement("worksheet", SpreadsheetNamespace);

                    var columnCount = Math.Max(1, sheet.Columns.Count);
                    var lastColumn = GetExcelColumnName(columnCount);
                    var headerRow = 7;
                    var firstDataRow = headerRow + 1;
                    var lastDataRow = headerRow + Math.Max(1, sheet.Rows.Count);

                    writer.WriteStartElement("sheetViews", SpreadsheetNamespace);
                    writer.WriteStartElement("sheetView", SpreadsheetNamespace);
                    writer.WriteAttributeString("workbookViewId", "0");
                    writer.WriteAttributeString("rightToLeft", "1");
                    writer.WriteAttributeString("showGridLines", "0");
                    writer.WriteStartElement("pane", SpreadsheetNamespace);
                    writer.WriteAttributeString("ySplit", headerRow.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("topLeftCell", string.Concat("A", firstDataRow));
                    writer.WriteAttributeString("activePane", "bottomLeft");
                    writer.WriteAttributeString("state", "frozen");
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteEndElement();

                    writer.WriteStartElement("sheetFormatPr", SpreadsheetNamespace);
                    writer.WriteAttributeString("defaultRowHeight", "18");
                    writer.WriteEndElement();

                    writer.WriteStartElement("cols", SpreadsheetNamespace);
                    for (var index = 0; index < columnCount; index++)
                    {
                        var width = index < sheet.Columns.Count
                            ? Math.Clamp(sheet.Columns[index].Width, 10d, 60d)
                            : 18d;

                        writer.WriteStartElement("col", SpreadsheetNamespace);
                        writer.WriteAttributeString("min", (index + 1).ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("max", (index + 1).ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("width", width.ToString("0.##", CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("customWidth", "1");
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();

                    writer.WriteStartElement("sheetData", SpreadsheetNamespace);

                    WriteMergedTextRow(
                        writer,
                        1,
                        columnCount,
                        sheet.Title,
                        styleIndex: 1,
                        height: 30d);

                    WriteMergedTextRow(
                        writer,
                        2,
                        columnCount,
                        sheet.Description,
                        styleIndex: 3,
                        height: 26d);

                    WriteMetadataRow(
                        writer,
                        3,
                        columnCount,
                        "المنشأة",
                        report.SelectedPlaceName);

                    WriteMetadataRow(
                        writer,
                        4,
                        columnCount,
                        "الفترة",
                        report.PeriodLabel);

                    WriteMetadataRow(
                        writer,
                        5,
                        columnCount,
                        "الفلاتر",
                        report.FilterDescription);

                    WriteMetadataRow(
                        writer,
                        6,
                        columnCount,
                        "تم الإنشاء",
                        report.GeneratedAt.ToString(
                            "yyyy-MM-dd HH:mm",
                            CultureInfo.InvariantCulture));

                    writer.WriteStartElement("row", SpreadsheetNamespace);
                    writer.WriteAttributeString("r", headerRow.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("ht", "24");
                    writer.WriteAttributeString("customHeight", "1");

                    for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                    {
                        var label = columnIndex < sheet.Columns.Count
                            ? sheet.Columns[columnIndex].Label
                            : "البيان";

                        WriteInlineStringCell(
                            writer,
                            GetCellReference(columnIndex + 1, headerRow),
                            label,
                            4);
                    }

                    writer.WriteEndElement();

                    if (sheet.Rows.Count == 0)
                    {
                        writer.WriteStartElement("row", SpreadsheetNamespace);
                        writer.WriteAttributeString("r", firstDataRow.ToString(CultureInfo.InvariantCulture));
                        WriteInlineStringCell(
                            writer,
                            string.Concat("A", firstDataRow),
                            "لا توجد نتائج مطابقة للفلاتر الحالية.",
                            5);
                        writer.WriteEndElement();
                    }
                    else
                    {
                        var rowIndex = firstDataRow;

                        foreach (var row in sheet.Rows)
                        {
                            writer.WriteStartElement("row", SpreadsheetNamespace);
                            writer.WriteAttributeString("r", rowIndex.ToString(CultureInfo.InvariantCulture));

                            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                            {
                                var column = columnIndex < sheet.Columns.Count
                                    ? sheet.Columns[columnIndex]
                                    : new FinancialReportExportColumnDto();

                                var cell = GetTypedCell(
                                    row,
                                    columnIndex,
                                    column.DataType);

                                WriteDataCell(
                                    writer,
                                    GetCellReference(columnIndex + 1, rowIndex),
                                    cell,
                                    column.DataType);
                            }

                            writer.WriteEndElement();
                            rowIndex += 1;
                        }
                    }

                    writer.WriteEndElement();

                    writer.WriteStartElement("mergeCells", SpreadsheetNamespace);
                    writer.WriteAttributeString("count", "2");
                    WriteMergeCell(writer, string.Concat("A1:", lastColumn, "1"));
                    WriteMergeCell(writer, string.Concat("A2:", lastColumn, "2"));
                    writer.WriteEndElement();

                    writer.WriteStartElement("autoFilter", SpreadsheetNamespace);
                    writer.WriteAttributeString(
                        "ref",
                        string.Concat("A", headerRow, ":", lastColumn, lastDataRow));
                    writer.WriteEndElement();

                    writer.WriteStartElement("pageMargins", SpreadsheetNamespace);
                    writer.WriteAttributeString("left", "0.3");
                    writer.WriteAttributeString("right", "0.3");
                    writer.WriteAttributeString("top", "0.5");
                    writer.WriteAttributeString("bottom", "0.5");
                    writer.WriteAttributeString("header", "0.2");
                    writer.WriteAttributeString("footer", "0.2");
                    writer.WriteEndElement();

                    writer.WriteStartElement("pageSetup", SpreadsheetNamespace);
                    writer.WriteAttributeString("orientation", columnCount > 8 ? "landscape" : "portrait");
                    writer.WriteAttributeString("paperSize", "9");
                    writer.WriteAttributeString("fitToWidth", "1");
                    writer.WriteAttributeString("fitToHeight", "0");
                    writer.WriteEndElement();

                    writer.WriteEndElement();
                });
        }

        private static FinancialReportExportCellDto GetTypedCell(
            FinancialReportExportRowDto row,
            int index,
            string? fallbackDataType)
        {
            if (row.TypedCells != null && index < row.TypedCells.Count)
            {
                return row.TypedCells[index] ??
                    new FinancialReportExportCellDto();
            }

            var text = row.Cells != null && index < row.Cells.Count
                ? row.Cells[index]
                : string.Empty;

            return new FinancialReportExportCellDto
            {
                Text = text ?? string.Empty,
                DataType = string.IsNullOrWhiteSpace(fallbackDataType)
                    ? "Text"
                    : fallbackDataType
            };
        }

        private static void WriteDataCell(
            XmlWriter writer,
            string reference,
            FinancialReportExportCellDto cell,
            string? columnDataType)
        {
            var dataType = string.IsNullOrWhiteSpace(cell.DataType)
                ? columnDataType ?? "Text"
                : cell.DataType;

            if (
                string.Equals(dataType, "Money", StringComparison.OrdinalIgnoreCase) &&
                cell.DecimalValue.HasValue)
            {
                WriteNumericCell(writer, reference, cell.DecimalValue.Value, 7);
                return;
            }

            if (
                string.Equals(dataType, "Percentage", StringComparison.OrdinalIgnoreCase) &&
                cell.DecimalValue.HasValue)
            {
                WriteNumericCell(writer, reference, cell.DecimalValue.Value, 10);
                return;
            }

            if (
                string.Equals(dataType, "Number", StringComparison.OrdinalIgnoreCase) &&
                cell.IntegerValue.HasValue)
            {
                WriteNumericCell(writer, reference, cell.IntegerValue.Value, 6);
                return;
            }

            if (
                string.Equals(dataType, "Date", StringComparison.OrdinalIgnoreCase) &&
                cell.DateValue.HasValue)
            {
                WriteNumericCell(writer, reference, cell.DateValue.Value.ToOADate(), 8);
                return;
            }

            if (
                string.Equals(dataType, "DateTime", StringComparison.OrdinalIgnoreCase) &&
                cell.DateValue.HasValue)
            {
                WriteNumericCell(writer, reference, cell.DateValue.Value.ToOADate(), 9);
                return;
            }

            WriteInlineStringCell(writer, reference, cell.Text, 5);
        }

        private static void WriteMergedTextRow(
            XmlWriter writer,
            int rowIndex,
            int columnCount,
            string? text,
            int styleIndex,
            double height)
        {
            writer.WriteStartElement("row", SpreadsheetNamespace);
            writer.WriteAttributeString("r", rowIndex.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("ht", height.ToString("0.##", CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customHeight", "1");

            WriteInlineStringCell(
                writer,
                GetCellReference(1, rowIndex),
                text,
                styleIndex);

            for (var index = 2; index <= columnCount; index++)
            {
                WriteInlineStringCell(
                    writer,
                    GetCellReference(index, rowIndex),
                    string.Empty,
                    styleIndex);
            }

            writer.WriteEndElement();
        }

        private static void WriteMetadataRow(
            XmlWriter writer,
            int rowIndex,
            int columnCount,
            string label,
            string? value)
        {
            writer.WriteStartElement("row", SpreadsheetNamespace);
            writer.WriteAttributeString("r", rowIndex.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("ht", "22");
            writer.WriteAttributeString("customHeight", "1");

            WriteInlineStringCell(
                writer,
                GetCellReference(1, rowIndex),
                label,
                2);

            WriteInlineStringCell(
                writer,
                GetCellReference(2, rowIndex),
                value,
                3);

            for (var index = 3; index <= columnCount; index++)
            {
                WriteInlineStringCell(
                    writer,
                    GetCellReference(index, rowIndex),
                    string.Empty,
                    3);
            }

            writer.WriteEndElement();
        }

        private static void WriteMergeCell(
            XmlWriter writer,
            string reference)
        {
            writer.WriteStartElement("mergeCell", SpreadsheetNamespace);
            writer.WriteAttributeString("ref", reference);
            writer.WriteEndElement();
        }

        private static void WriteInlineStringCell(
            XmlWriter writer,
            string reference,
            string? value,
            int styleIndex)
        {
            writer.WriteStartElement("c", SpreadsheetNamespace);
            writer.WriteAttributeString("r", reference);
            writer.WriteAttributeString("s", styleIndex.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("t", "inlineStr");
            writer.WriteStartElement("is", SpreadsheetNamespace);
            writer.WriteStartElement("t", SpreadsheetNamespace);
            writer.WriteAttributeString("xml", "space", null, "preserve");
            writer.WriteString(value ?? string.Empty);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteNumericCell(
            XmlWriter writer,
            string reference,
            decimal value,
            int styleIndex)
        {
            WriteNumericCell(
                writer,
                reference,
                value.ToString("0.################", CultureInfo.InvariantCulture),
                styleIndex);
        }

        private static void WriteNumericCell(
            XmlWriter writer,
            string reference,
            long value,
            int styleIndex)
        {
            WriteNumericCell(
                writer,
                reference,
                value.ToString(CultureInfo.InvariantCulture),
                styleIndex);
        }

        private static void WriteNumericCell(
            XmlWriter writer,
            string reference,
            double value,
            int styleIndex)
        {
            WriteNumericCell(
                writer,
                reference,
                value.ToString("0.################", CultureInfo.InvariantCulture),
                styleIndex);
        }

        private static void WriteNumericCell(
            XmlWriter writer,
            string reference,
            string value,
            int styleIndex)
        {
            writer.WriteStartElement("c", SpreadsheetNamespace);
            writer.WriteAttributeString("r", reference);
            writer.WriteAttributeString("s", styleIndex.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("t", "n");
            writer.WriteElementString("v", SpreadsheetNamespace, value);
            writer.WriteEndElement();
        }

        private static string GetCellReference(
            int columnIndex,
            int rowIndex) =>
                string.Concat(
                    GetExcelColumnName(columnIndex),
                    rowIndex.ToString(CultureInfo.InvariantCulture));

        private static string GetExcelColumnName(
            int columnIndex)
        {
            var name = string.Empty;
            var index = Math.Max(1, columnIndex);

            while (index > 0)
            {
                index -= 1;
                name = string.Concat(
                    (char)('A' + (index % 26)),
                    name);
                index /= 26;
            }

            return name;
        }

        private static void WriteXmlEntry(
            ZipArchive archive,
            string entryName,
            Action<XmlWriter> writeAction)
        {
            var entry = archive.CreateEntry(
                entryName,
                CompressionLevel.Optimal);

            using var stream = entry.Open();
            using var writer = XmlWriter.Create(
                stream,
                new XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(false),
                    Indent = true,
                    CloseOutput = false
                });

            writer.WriteStartDocument();
            writeAction(writer);
            writer.WriteEndDocument();
        }
    }
}
    