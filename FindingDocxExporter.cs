using DevExpress.XtraRichEdit;
using DevExpress.XtraRichEdit.API.Native;
using TeftisAsistani.Models;

namespace TeftisAsistani.Services;

public interface IFindingDocxExporter
{
    byte[] Export(FindingTable table);
}

/// DevExpress RichEditDocumentServer ile .docx üretimi (500px ≈ 13,2 cm yatay bulgu formu).
public sealed class FindingDocxExporter : IFindingDocxExporter
{
    public byte[] Export(FindingTable model)
    {
        using var server = new RichEditDocumentServer();
        var doc = server.Document;
        doc.BeginUpdate();

        var head = doc.AppendParagraph();
        doc.InsertText(head.Range.Start, $"{model.Title} — {model.FindingNo}");
        var headCp = doc.BeginUpdateCharacters(head.Range);
        headCp.Bold = true;
        headCp.FontSize = 10;
        doc.EndUpdateCharacters(headCp);

        var table = doc.Tables.Create(doc.Range.End, model.Rows.Count, 2, AutoFitBehaviorType.FixedColumnWidth);
        table.TableAlignment = TableRowAlignment.Left;
        table.PreferredWidthType = WidthType.Fixed;
        table.PreferredWidth = DevExpress.Office.Utils.Units.CentimetersToDocumentsF(13.2f);
        table.Borders.InsideHorizontalBorder.LineStyle = TableBorderLineStyle.Single;
        table.Borders.InsideVerticalBorder.LineStyle = TableBorderLineStyle.Single;
        table.Borders.Top.LineStyle = TableBorderLineStyle.Single;
        table.Borders.Bottom.LineStyle = TableBorderLineStyle.Single;
        table.Borders.Left.LineStyle = TableBorderLineStyle.Single;
        table.Borders.Right.LineStyle = TableBorderLineStyle.Single;

        for (var i = 0; i < model.Rows.Count; i++)
        {
            var row = model.Rows[i];

            var keyCell = table.Rows[i].Cells[0];
            keyCell.PreferredWidthType = WidthType.Fixed;
            keyCell.PreferredWidth = DevExpress.Office.Utils.Units.CentimetersToDocumentsF(3.4f);
            keyCell.BackgroundColor = System.Drawing.Color.FromArgb(247, 247, 249);
            doc.InsertText(keyCell.Range.Start, row.Label.ToUpperInvariant());
            var keyCp = doc.BeginUpdateCharacters(keyCell.Range);
            keyCp.Bold = true;
            keyCp.FontSize = 9;
            doc.EndUpdateCharacters(keyCp);

            var valCell = table.Rows[i].Cells[1];
            doc.InsertText(valCell.Range.Start, row.Value);
            var valCp = doc.BeginUpdateCharacters(valCell.Range);
            valCp.FontSize = 9;
            doc.EndUpdateCharacters(valCp);
        }

        doc.EndUpdate();
        using var ms = new MemoryStream();
        server.SaveDocument(ms, DocumentFormat.OpenXml);
        return ms.ToArray();
    }
}
