param(
  [Parameter(Mandatory=$true)][string[]]$InputPaths,
  [Parameter(Mandatory=$true)][string]$OutputDir
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

function Get-ShapeText {
  param($Shape, [string]$Prefix)
  $lines = New-Object System.Collections.Generic.List[string]
  try {
    if ($Shape.Type -eq 6) {
      for ($i = 1; $i -le $Shape.GroupItems.Count; $i++) {
        $child = $Shape.GroupItems.Item($i)
        foreach ($line in (Get-ShapeText -Shape $child -Prefix "$Prefix/$i")) { $lines.Add($line) }
      }
    } elseif ($Shape.HasTable -eq -1) {
      for ($r = 1; $r -le $Shape.Table.Rows.Count; $r++) {
        $cells = New-Object System.Collections.Generic.List[string]
        for ($c = 1; $c -le $Shape.Table.Columns.Count; $c++) {
          $cellText = $Shape.Table.Cell($r,$c).Shape.TextFrame.TextRange.Text
          $cells.Add(($cellText -replace "`r|`n", ' ').Trim())
        }
        $lines.Add("[$Prefix TABLE $r] " + ($cells -join ' || '))
      }
    } elseif ($Shape.HasTextFrame -eq -1 -and $Shape.TextFrame.HasText -eq -1) {
      $t = ($Shape.TextFrame.TextRange.Text -replace "`r", "`n").Trim()
      if ($t) { $lines.Add("[$Prefix] $t") }
    }
  } catch {}
  return $lines
}

$ppt = New-Object -ComObject PowerPoint.Application
$ppt.Visible = 1
try {
  foreach ($inputPath in $InputPaths) {
    $base = [IO.Path]::GetFileNameWithoutExtension($inputPath)
    $deckDir = Join-Path $OutputDir $base
    $imgDir = Join-Path $deckDir 'slides'
    New-Item -ItemType Directory -Path $imgDir -Force | Out-Null
    $pres = $ppt.Presentations.Open($inputPath, $true, $true, $false)
    try {
      $out = New-Object System.Collections.Generic.List[string]
      $out.Add("FILE: $inputPath")
      $out.Add("SLIDES: $($pres.Slides.Count)")
      $out.Add("SIZE: $($pres.PageSetup.SlideWidth) x $($pres.PageSetup.SlideHeight)")
      foreach ($slide in $pres.Slides) {
        $out.Add("")
        $out.Add("===== SLIDE $($slide.SlideIndex) =====")
        for ($i = 1; $i -le $slide.Shapes.Count; $i++) {
          $shape = $slide.Shapes.Item($i)
          foreach ($line in (Get-ShapeText -Shape $shape -Prefix "S$i")) { $out.Add($line) }
        }
        try {
          $notesText = New-Object System.Collections.Generic.List[string]
          for ($i = 1; $i -le $slide.NotesPage.Shapes.Count; $i++) {
            $ns = $slide.NotesPage.Shapes.Item($i)
            if ($ns.HasTextFrame -eq -1 -and $ns.TextFrame.HasText -eq -1) {
              $nt = ($ns.TextFrame.TextRange.Text -replace "`r", "`n").Trim()
              if ($nt -and $nt -notmatch '^\d+$') { $notesText.Add($nt) }
            }
          }
          if ($notesText.Count -gt 0) { $out.Add('[NOTES] ' + ($notesText -join ' | ')) }
        } catch {}
        $png = Join-Path $imgDir ("slide-{0:D2}.png" -f $slide.SlideIndex)
        $slide.Export($png, 'PNG', 1600, 900)
      }
      [IO.File]::WriteAllLines((Join-Path $deckDir 'content.txt'), $out, [Text.Encoding]::UTF8)
    } finally {
      $pres.Close()
      [Runtime.InteropServices.Marshal]::ReleaseComObject($pres) | Out-Null
    }
  }
} finally {
  $ppt.Quit()
  [Runtime.InteropServices.Marshal]::ReleaseComObject($ppt) | Out-Null
}
