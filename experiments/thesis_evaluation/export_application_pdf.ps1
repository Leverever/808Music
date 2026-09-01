param(
    [string]$DocxPath = "C:\808Music\artifacts\Prijedlog_teme_zavrsnog_rada_808Music_refaktorirana.docx",
    [string]$PdfPath = "C:\808Music\artifacts\Prijedlog_teme_zavrsnog_rada_808Music_refaktorirana.pdf"
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $DocxPath)) { throw "DOCX not found: $DocxPath" }
if (Test-Path -LiteralPath $PdfPath) { throw "Refusing to overwrite: $PdfPath" }

$word = $null
$document = $null
try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $word.DisplayAlerts = 0
    $document = $word.Documents.Open($DocxPath, $false, $true)
    $document.Repaginate()
    $document.ExportAsFixedFormat($PdfPath, 17)
    [ordered]@{
        docx = $DocxPath
        pdf = $PdfPath
        pages = $document.ComputeStatistics(2)
        words = $document.ComputeStatistics(0)
        tables = $document.Tables.Count
    } | ConvertTo-Json
}
finally {
    if ($null -ne $document) { $document.Close($false) }
    if ($null -ne $word) { $word.Quit() }
}
