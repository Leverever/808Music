param(
    [string]$SourcePath = "C:\808Music\artifacts\zavrsni_v2_uskladjen.docx",
    [string]$OutputPath = "C:\808Music\artifacts\zavrsni_v3_rezultati.docx",
    [string]$PdfPath = "C:\808Music\artifacts\zavrsni_v3_rezultati.pdf",
    [string]$DataPath = "C:\808Music\experiments\thesis_evaluation\results\thesis_document_data.json"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $SourcePath)) {
    throw "Source DOCX was not found: $SourcePath"
}
if (-not (Test-Path -LiteralPath $DataPath)) {
    throw "Document data was not found: $DataPath"
}
if (Test-Path -LiteralPath $OutputPath) {
    throw "Output already exists; refusing to overwrite it: $OutputPath"
}

$data = Get-Content -LiteralPath $DataPath -Raw -Encoding UTF8 | ConvertFrom-Json
$word = $null
$document = $null
$script:ParagraphCache = $null
$script:TableCache = @{}

function Get-ParagraphText {
    param($Paragraph)
    return $Paragraph.Range.Text.Trim([char]13, [char]7, [char]32, [char]9, [char]10)
}

function Get-MatchingParagraphs {
    param($Document, $Rule)
    $matches = [System.Collections.Generic.List[object]]::new()
    foreach ($item in $script:ParagraphCache) {
        if ($item.Removed) {
            continue
        }
        $text = [string]$item.Text
        $matchesRule = $false
        if ($null -ne $Rule.old_exact) {
            $matchesRule = $text -eq [string]$Rule.old_exact
        }
        elseif ($null -ne $Rule.old_starts_with) {
            $matchesRule = $text.StartsWith([string]$Rule.old_starts_with)
        }
        elseif ($null -ne $Rule.instruction_starts_with) {
            $matchesRule = $text.StartsWith([string]$Rule.instruction_starts_with)
        }
        if ($matchesRule) {
            $matches.Add($item)
        }
    }
    return $matches
}

function Set-ParagraphText {
    param($Paragraph, [string]$Text)
    $range = $Paragraph.Range.Duplicate
    if ($range.End -gt $range.Start) {
        $range.End = $range.End - 1
    }
    $range.Text = $Text
}

function Get-TableAfterCaption {
    param($Document, [string]$Caption)
    if (-not $script:TableCache.ContainsKey($Caption)) {
        throw "No cached table was found after caption '$Caption'."
    }
    return $Document.Tables.Item([int]$script:TableCache[$Caption])
}

try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $word.DisplayAlerts = 0
    $document = $word.Documents.Open($SourcePath, $false, $false)
    $document.SaveAs2($OutputPath, 16)

    # Verified against the Word table collection of zavrsni_v2_uskladjen.docx.
    # Layout boxes are also tables, so caption numbers do not equal collection
    # indexes. Keeping this explicit makes the one-off thesis transformation
    # deterministic and lets us fail before modifying content if the source
    # structure changes.
    $verifiedTableIndexes = @{
        6 = 21
        7 = 22
        8 = 25
        9 = 26
        10 = 28
        11 = 30
        12 = 31
        13 = 33
    }
    foreach ($tableSpec in $data.tables) {
        $caption = [string]$tableSpec.caption
        if ($null -ne $tableSpec.word_index) {
            $wordTableIndex = [int]$tableSpec.word_index
        }
        else {
            $numberMatch = [regex]::Match($caption, '^Tablica\s+(\d+)\.')
            if (-not $numberMatch.Success) {
                throw "Could not derive a table number from caption '$caption'."
            }
            $captionNumber = [int]$numberMatch.Groups[1].Value
            if (-not $verifiedTableIndexes.ContainsKey($captionNumber)) {
                throw "No verified Word table index exists for caption '$caption'."
            }
            $wordTableIndex = [int]$verifiedTableIndexes[$captionNumber]
        }
        $sourceTable = $document.Tables.Item($wordTableIndex)
        $expectedRows = $tableSpec.rows.Count
        $expectedColumns = $tableSpec.rows[0].Count
        if ($sourceTable.Rows.Count -ne $expectedRows -or $sourceTable.Columns.Count -ne $expectedColumns) {
            throw "Preflight for '$caption' found $($sourceTable.Rows.Count)x$($sourceTable.Columns.Count) at Word table $wordTableIndex, expected ${expectedRows}x${expectedColumns}."
        }
        $script:TableCache[$caption] = $wordTableIndex
    }

    $script:ParagraphCache = [System.Collections.Generic.List[object]]::new()
    for ($index = 1; $index -le $document.Paragraphs.Count; $index++) {
        $paragraph = $document.Paragraphs.Item($index)
        $script:ParagraphCache.Add([pscustomobject]@{
            Index = $index
            Paragraph = $paragraph
            Text = Get-ParagraphText $paragraph
            Removed = $false
        })
    }

    foreach ($replacement in $data.paragraph_replacements) {
        $matches = @(Get-MatchingParagraphs $document $replacement)
        if ($matches.Count -ne 1) {
            $key = if ($null -ne $replacement.old_exact) { $replacement.old_exact } else { $replacement.old_starts_with }
            throw "Expected one paragraph for '$key', found $($matches.Count)."
        }
        Set-ParagraphText $matches[0].Paragraph ([string]$replacement.new)
        $matches[0].Text = [string]$replacement.new
    }

    foreach ($deletion in $data.delete_paragraphs) {
        $matches = @(Get-MatchingParagraphs $document $deletion)
        if ($matches.Count -ne 1) {
            $key = if ($null -ne $deletion.old_exact) { $deletion.old_exact } else { $deletion.old_starts_with }
            throw "Expected one paragraph to delete for '$key', found $($matches.Count)."
        }
        Set-ParagraphText $matches[0].Paragraph ""
        $matches[0].Text = ""
        $matches[0].Removed = $true
    }

    foreach ($exactText in $data.delete_all_exact_except_first) {
        $rule = [pscustomobject]@{ old_exact = [string]$exactText }
        $matches = @(Get-MatchingParagraphs $document $rule)
        for ($index = $matches.Count - 1; $index -ge 1; $index--) {
            Set-ParagraphText $matches[$index].Paragraph ""
            $matches[$index].Text = ""
            $matches[$index].Removed = $true
        }
    }

    foreach ($tableSpec in $data.tables) {
        $table = Get-TableAfterCaption $document ([string]$tableSpec.caption)
        $expectedRows = $tableSpec.rows.Count
        $expectedColumns = $tableSpec.rows[0].Count
        if ($table.Rows.Count -ne $expectedRows -or $table.Columns.Count -ne $expectedColumns) {
            throw "Table '$($tableSpec.caption)' has $($table.Rows.Count)x$($table.Columns.Count), expected ${expectedRows}x${expectedColumns}."
        }
        for ($rowIndex = 0; $rowIndex -lt $expectedRows; $rowIndex++) {
            for ($columnIndex = 0; $columnIndex -lt $expectedColumns; $columnIndex++) {
                $table.Cell($rowIndex + 1, $columnIndex + 1).Range.Text = [string]$tableSpec.rows[$rowIndex][$columnIndex]
            }
        }
    }

    foreach ($figure in $data.figures) {
        if (-not (Test-Path -LiteralPath $figure.path)) {
            throw "Figure was not found: $($figure.path)"
        }
        $matches = @(Get-MatchingParagraphs $document $figure)
        if ($matches.Count -ne 1) {
            throw "Expected one figure placeholder '$($figure.instruction_starts_with)', found $($matches.Count)."
        }
        $paragraphIndex = $matches[0].Index
        if ($paragraphIndex -gt 1) {
            $previousItem = $script:ParagraphCache | Where-Object { $_.Index -eq ($paragraphIndex - 1) } | Select-Object -First 1
            if ($null -ne $previousItem -and ([string]$previousItem.Text).StartsWith("MJESTO ZA SLIKU / GRAF")) {
                Set-ParagraphText $previousItem.Paragraph ""
                $previousItem.Text = ""
            }
        }
        $paragraph = $matches[0].Paragraph
        $pictureTable = $null
        if ($paragraph.Range.Tables.Count -gt 0) {
            $pictureTable = $paragraph.Range.Tables.Item(1)
        }
        $paragraph.Range.ParagraphFormat.Alignment = 1
        $range = $paragraph.Range.Duplicate
        if ($range.End -gt $range.Start) {
            $range.End = $range.End - 1
        }
        $range.Text = ""
        $matches[0].Text = ""
        $range.Collapse(1)
        $shape = $document.InlineShapes.AddPicture([string]$figure.path, $false, $true, $range)
        $shape.LockAspectRatio = -1
        $maximumWidth = 400.0
        if ($shape.Width -gt $maximumWidth) {
            $shape.Width = $maximumWidth
        }
        if ($null -ne $pictureTable) {
            $pictureTable.Borders.Enable = 0
            $pictureTable.Shading.BackgroundPatternColor = 16777215
            foreach ($pictureCell in $pictureTable.Range.Cells) {
                $pictureCell.Shading.BackgroundPatternColor = 16777215
            }
        }
        $nextCaption = $script:ParagraphCache |
            Where-Object { $_.Index -gt $paragraphIndex -and ([string]$_.Text).StartsWith("Slika ") } |
            Sort-Object Index |
            Select-Object -First 1
        if ($null -ne $nextCaption) {
            $nextCaption.Paragraph.Range.ParagraphFormat.KeepWithNext = 0
        }
    }

    # The source contains two one-cell red instruction boxes whose text was
    # removed after the real results were inserted. Delete the now-empty boxes
    # in descending order so they do not leave large blank areas in the PDF.
    foreach ($emptyInstructionTableIndex in @(36, 20)) {
        $document.Tables.Item($emptyInstructionTableIndex).Delete()
    }

    foreach ($toc in $document.TablesOfContents) {
        $toc.Update() | Out-Null
    }
    $document.Fields.Update() | Out-Null
    $document.Repaginate()
    $document.Save()
    $document.ExportAsFixedFormat($PdfPath, 17)

    $summary = [ordered]@{
        outputPath = $OutputPath
        pdfPath = $PdfPath
        pages = $document.ComputeStatistics(2)
        words = $document.ComputeStatistics(0)
        tables = $document.Tables.Count
        inlineShapes = $document.InlineShapes.Count
    }
    $summary | ConvertTo-Json | Set-Content -LiteralPath "C:\808Music\experiments\thesis_evaluation\results\document_generation_summary.json" -Encoding UTF8
    $summary | ConvertTo-Json
}
finally {
    if ($null -ne $document) {
        $document.Close($false)
    }
    if ($null -ne $word) {
        $word.Quit()
    }
    if ($null -ne $document) {
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($document) | Out-Null
    }
    if ($null -ne $word) {
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($word) | Out-Null
    }
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}
