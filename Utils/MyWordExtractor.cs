//https://github.com/virex-84

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

public class MyWordExtractor
{
    public class Section
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public int Page { get; set; }
    }

    public List<Section> DecodeAsync(string filename)
    {
        var result = new List<Section>();

        StringBuilder currentContent = new StringBuilder();
        string currentHeading = "No Heading";

        using (WordprocessingDocument wordDocument = WordprocessingDocument.Open(filename, false))
        {
            var mainPart = wordDocument.MainDocumentPart;
            if (mainPart == null || mainPart.Document.Body == null)
            {
                return result;
            }

            var styles = GetHeadingStyles(mainPart);

            foreach (var element in mainPart.Document.Body.Elements())
            {
                var level = HeadingLevel(element, styles);
                if (level > -1)
                {
                    // Store the content under the previous heading.
                    if (currentContent.Length > 0 || currentHeading != "No Heading")
                    {
                        var page = GetPageNumberApproximation(element);
                        result.Add(new Section() { Title = currentHeading, Content = currentContent.ToString(), Page = page });
                    }

                    // Start a new section with the new heading.
                    //currentHeading = element.InnerText;

                    if (level > 1)
                    currentHeading = currentHeading + ". " + element.InnerText;
                    else
                    currentHeading = element.InnerText;


                    currentContent = new StringBuilder();
                }
                else
                {
                    // Append content to the current section.
                    if (element is Paragraph paragraph)
                    {
                        /*
                        if (!IsListItem(paragraph))
                        {
                            currentContent.AppendLine(paragraph.InnerText);
                        }
                        else
                        */
                        {
                            // A more sophisticated implementation could handle lists properly.
                            //currentContent.AppendLine(paragraph.InnerText);
                            currentContent.AppendLine(ExtractParagraphText(paragraph));
                        }
                    }
                    else if (element is Table table)
                    {
                        currentContent.AppendLine(ExtractTableText(table));
                    }
                }
            }
        }

        //последний элемент
        if (currentContent.Length > 0)
        {
            var lastpage = result.Max(x => x.Page) + 1;
            result.Add(new Section() { Title = currentHeading, Content = currentContent.ToString(), Page = lastpage });
        }

        return result;
    }

    public static int GetPageNumberApproximation(OpenXmlElement element)
    {
        int pageNumber = 1;

        // The root is the document body
        var root = element.Ancestors<Body>().FirstOrDefault();
        if (root == null)
        {
            return 1;
        }

        var tmpElement = element;
        while (tmpElement != root)
        {
            var sibling = tmpElement.PreviousSibling();
            while (sibling != null)
            {
                // Count all page break indicators before the element
                pageNumber += sibling.Descendants<LastRenderedPageBreak>().Count();
                sibling = sibling.PreviousSibling();
            }
            tmpElement = tmpElement.Parent;
        }
        return pageNumber;
    }

    private string? ExtractParagraphText(Paragraph paragraph)
    {
        var textBuilder = new StringBuilder();

        foreach (var run in paragraph.Elements<Run>())
        {
            bool inComplexFieldCode = false;

            // Проверяем на маркеры поля
            var fieldChar = run.Elements<FieldChar>().FirstOrDefault();
            if (fieldChar != null)
            {
                if (fieldChar.FieldCharType?.Value == FieldCharValues.Begin)
                {
                    inComplexFieldCode = true;
                }
                else if (fieldChar.FieldCharType?.Value == FieldCharValues.Separate)
                {
                    inComplexFieldCode = false;
                }
                else if (fieldChar.FieldCharType?.Value == FieldCharValues.End)
                {
                    inComplexFieldCode = false;
                }
                continue;
            }

            // Проверяем на простой FieldCode
            var fieldCodeElement = run.Elements<FieldCode>().FirstOrDefault();
            if (fieldCodeElement != null)
            {
                //textBuilder.Append(fieldCodeElement.InnerText.Trim());
                continue;
            }

            // Проверяем на простое поле SimpleField
            var simpleField = run.Elements<SimpleField>().FirstOrDefault();
            if (simpleField != null)
            {
                textBuilder.Append(simpleField.InnerText);
                continue;
            }

            // Проверяем на гиперссылку
            var hyperlink = run.Elements<Hyperlink>().FirstOrDefault();
            if (hyperlink != null)
            {
                if (!string.IsNullOrEmpty(hyperlink.InnerText))
                {
                    textBuilder.Append(hyperlink.InnerText);
                }
                continue;
            }

            if (!inComplexFieldCode)
            {
                var runText = run.InnerText;
                textBuilder.Append(runText);
            }
        }

        //если были только ссылки - добавляем текст из них
        if (textBuilder.ToString().Trim().Length == 0)
        {
            foreach (var hyperlink in paragraph.Descendants<Hyperlink>())
            {
                foreach (var text in hyperlink.Descendants<Text>())
                {
                    //paragraphText += " " + text.InnerText;
                    textBuilder.Append(text.InnerText + " ");
                }
            }
        }

        return textBuilder.ToString().Trim();
    }

    private Dictionary<string, int> GetHeadingStyles(MainDocumentPart mainPart)
    {
        var headingStyles = new Dictionary<string, int>();
        var stylesPart = mainPart.StyleDefinitionsPart;
        if (stylesPart != null)
        {
            foreach (var style in stylesPart.Styles.Elements<Style>())
            {
                var styleParagraphProperties = style.StyleParagraphProperties;
                if (styleParagraphProperties != null)
                {
                    var outlineLevel = styleParagraphProperties.OutlineLevel?.Val?.Value;
                    if (outlineLevel != null)
                    {
                        headingStyles[style.StyleId] = (int)outlineLevel + 1;
                    }
                    else
                    {
                        // Проверяем BasedOn или Link
                        var basedOn = style.BasedOn?.Val?.Value;
                        var link = style.LinkedStyle?.Val?.Value;
                        if (basedOn != null)
                        {
                            // Если BasedOn существует, проверяем уровень в базовом стиле
                            if (headingStyles.ContainsKey(basedOn))
                            {
                                headingStyles[style.StyleId] = headingStyles[basedOn];
                            }
                            else
                            {
                                //headingStyles[style.StyleId] = 12; // По умолчанию
                            }
                        }
                        else if (link != null)
                        {
                            // Если Link существует, проверяем уровень в связанном стиле
                            if (headingStyles.ContainsKey(link))
                            {
                                headingStyles[style.StyleId] = headingStyles[link];
                            }
                            else
                            {
                                //headingStyles[style.StyleId] = 12; // По умолчанию
                            }
                        }
                        else
                        {
                            //headingStyles[style.StyleId] = 12; // По умолчанию
                        }
                    }
                }
            }
        }
        return headingStyles;
    }

    private int HeadingLevel(OpenXmlElement element, Dictionary<string, int> styles)
    {
        if (element is Paragraph paragraph)
        {
            var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            if (styleId != null && styles.ContainsKey(styleId))
            {
                return styles[styleId];
            }
        }
        return -1;
    }

    private string? ExtractTableText(Table table)
    {
        var tableData = new List<Dictionary<string, string>>();
        var rows = table.Elements<TableRow>().ToList();

        if (rows.Any())
        {
            var headerCells = rows.First().Elements<TableCell>().ToList();
            bool hasHeader = IsHeaderRow(headerCells);

            // Skip header row in data if detected
            int startRowIndex = hasHeader ? 1 : 0;

            for (int i = startRowIndex; i < rows.Count; i++)
            {
                var rowData = new Dictionary<string, string>();
                var cells = rows[i].Elements<TableCell>().ToList();

                // Process cells and match with headers if they exist.
                for (int j = 0; j < cells.Count; j++)
                {
                    string headerText = hasHeader && j < headerCells.Count ? headerCells[j].InnerText : $"Column_{j + 1}";
                    rowData[headerText] = cells[j].InnerText;
                }
                tableData.Add(rowData);
            }
        }


        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.Create(new TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All)) };
        return System.Text.Json.JsonSerializer.Serialize(tableData, options);
    }

    private bool IsHeaderRow(IEnumerable<TableCell> cells)
    {
        // Simple heuristic: A row is a header if all its cells have bold text.
        foreach (var cell in cells)
        {
            var boldRun = cell.Descendants<Bold>().FirstOrDefault();
            if (boldRun == null)
            {
                return false;
            }
        }
        return true;
    }
}