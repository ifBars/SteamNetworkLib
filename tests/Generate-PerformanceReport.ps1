#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates SteamNetworkLib real-game performance reports.

.DESCRIPTION
    Reads JSONL metrics emitted by SteamNetworkLib.TestMod during isolated real-game
    process testing and writes both a Markdown report for DocFX and a standalone
    HTML report with dense tables, horizontal bars, and a Markdown-style aside.
#>

param(
    [string]$MetricsPath = "tests/performance/latest-metrics.jsonl",
    [string]$OutputPath = "docs/performance-report-full.html",
    [string]$MarkdownOutputPath = "docs/performance-report.md",
    [string]$Title = "SteamNetworkLib Real-Game Behavior Report"
)

$ErrorActionPreference = "Stop"

function ConvertTo-HtmlText($Value) {
    return [System.Net.WebUtility]::HtmlEncode([string]$Value)
}

function ConvertTo-MarkdownCell($Value) {
    return ([string]$Value).Replace("|", "\|").Replace("`r", " ").Replace("`n", " ")
}

function Format-Bytes($Bytes) {
    if ($Bytes -ge 1MB) {
        return "{0:N2} MB" -f ($Bytes / 1MB)
    }

    if ($Bytes -ge 1KB) {
        return "{0:N1} KB" -f ($Bytes / 1KB)
    }

    return "$Bytes B"
}

function Format-Duration($Milliseconds) {
    if ($Milliseconds -ge 1000) {
        return "{0:N2}s" -f ($Milliseconds / 1000)
    }

    return "{0:N0} ms" -f $Milliseconds
}

function Format-Mbps($Value) {
    return "{0:N2} Mbps" -f $Value
}

function New-BarRow($Metric, [double]$MaxDuration, [double]$MaxThroughput) {
    $durationWidth = if ($MaxDuration -gt 0) { [Math]::Max(1, [Math]::Round(($Metric.durationMs / $MaxDuration) * 100, 2)) } else { 0 }
    $throughputWidth = if ($MaxThroughput -gt 0) { [Math]::Max(1, [Math]::Round(($Metric.throughputMbps / $MaxThroughput) * 100, 2)) } else { 0 }

    return @"
          <tr>
            <td>
              <strong>$(ConvertTo-HtmlText $Metric.label)</strong>
              <code>$(ConvertTo-HtmlText $Metric.scenario)</code>
            </td>
            <td>$(ConvertTo-HtmlText $Metric.role)</td>
            <td>$(ConvertTo-HtmlText $Metric.direction)</td>
            <td>$(Format-Bytes $Metric.bytes)</td>
            <td>$($Metric.messages)</td>
            <td>
              <div class="bar-cell">
                <div class="bar duration" style="width: $durationWidth%"></div>
                <span>$(Format-Duration $Metric.durationMs)</span>
              </div>
            </td>
            <td>
              <div class="bar-cell">
                <div class="bar throughput" style="width: $throughputWidth%"></div>
                <span>$(Format-Mbps $Metric.throughputMbps)</span>
              </div>
            </td>
          </tr>
"@
}

function New-MarkdownTableRow($Metric) {
    return "| $(ConvertTo-MarkdownCell $Metric.label) | $(ConvertTo-MarkdownCell $Metric.role) | $(ConvertTo-MarkdownCell $Metric.direction) | $(Format-Bytes $Metric.bytes) | $($Metric.messages) | $(Format-Duration $Metric.durationMs) | $(Format-Mbps $Metric.throughputMbps) |"
}

$metricsFullPath = [System.IO.Path]::GetFullPath($MetricsPath)
if (-not (Test-Path -LiteralPath $metricsFullPath)) {
    throw "Metrics file not found: $metricsFullPath"
}

$metrics = @(Get-Content -LiteralPath $metricsFullPath |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    ForEach-Object { $_ | ConvertFrom-Json })

if ($metrics.Count -eq 0) {
    throw "No metrics were found in $metricsFullPath"
}

$generatedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"
$maxDuration = ($metrics | Measure-Object -Property durationMs -Maximum).Maximum
$maxThroughput = ($metrics | Measure-Object -Property throughputMbps -Maximum).Maximum
$totalBytes = ($metrics | Measure-Object -Property bytes -Sum).Sum
$totalMessages = ($metrics | Measure-Object -Property messages -Sum).Sum
$averageThroughput = ($metrics | Measure-Object -Property throughputMbps -Average).Average
$slowest = $metrics | Sort-Object durationMs -Descending | Select-Object -First 1
$fastestThroughput = $metrics | Sort-Object throughputMbps -Descending | Select-Object -First 1
$environment = ($metrics | Select-Object -First 1).environment
$transport = ($metrics | Select-Object -First 1).transport
$runtime = ($metrics | Select-Object -First 1).runtime
$orderedMetrics = @($metrics | Sort-Object scenario, role, direction)

$chartLabels = @()
$durationValues = @()
$throughputValues = @()
foreach ($metric in $orderedMetrics) {
    $chartLabels += ('"{0}"' -f (($metric.label -replace '"', "'") -replace "Built-in message model suite", "Model suite" -replace "Small direct messages", "Direct"))
    $durationValues += ("{0:N3}" -f ([double]$metric.durationMs))
    $throughputValues += ("{0:N3}" -f ([double]$metric.throughputMbps))
}

$rows = ($orderedMetrics | ForEach-Object {
    New-BarRow $_ $maxDuration $maxThroughput
}) -join "`n"

$markdownRows = ($orderedMetrics | ForEach-Object {
    New-MarkdownTableRow $_
}) -join "`n"

$markdown = @"
# $Title

Generated: $generatedAt

This report summarizes SteamNetworkLib behavior from a real Schedule I process harness. The current run uses two isolated game processes with a Goldberg-compatible local Steam configuration, so it is useful for message-path and chunking behavior. It is not a WAN benchmark.

## Current Status

| Metric | Value |
| --- | ---: |
| Runtime | $(ConvertTo-MarkdownCell $runtime) |
| Transport | $(ConvertTo-MarkdownCell $transport) |
| Environment | $(ConvertTo-MarkdownCell $environment) |
| Measurements | $($metrics.Count) |
| Total payload | $(Format-Bytes $totalBytes) |
| Messages / chunks | $totalMessages |
| Average throughput | $(Format-Mbps $averageThroughput) |
| Slowest measurement | $(ConvertTo-MarkdownCell $slowest.label), $(Format-Duration $slowest.durationMs) |
| Highest throughput | $(ConvertTo-MarkdownCell $fastestThroughput.label), $(Format-Mbps $fastestThroughput.throughputMbps) |

~~~mermaid
xychart-beta
  title "Duration by Scenario"
  x-axis [$($chartLabels -join ", ")]
  y-axis "Milliseconds" 0 --> $([Math]::Ceiling([double]$maxDuration + 5))
  bar [$($durationValues -join ", ")]
~~~

~~~mermaid
xychart-beta
  title "Throughput by Scenario"
  x-axis [$($chartLabels -join ", ")]
  y-axis "Mbps" 0 --> $([Math]::Ceiling([double]$maxThroughput + 25))
  bar [$($throughputValues -join ", ")]
~~~

## Measurement Table

| Scenario | Role | Direction | Payload | Messages | Duration | Throughput |
| --- | --- | --- | ---: | ---: | ---: | ---: |
$markdownRows

## Real-World Interpretation

The most useful case in this run is the 2 MB music-file transfer because it exercises SteamNetworkLib's chunked file path instead of only small direct messages. The 70 KB state blob is a smaller version of the same behavior for mod-state synchronization. The message-model suite keeps coverage on `EventMessage`, `DataSyncMessage`, `HeartbeatMessage`, `StreamMessage`, and `FileTransferMessage`.

Upload speed, download speed, and latency should be treated as properties of the current run's transport path. For public WAN claims, run the same metrics across separate machines or with a controlled network emulator and regenerate this report from the new JSONL.

## Standalone View

The standalone full-page report is available at [performance-report-full.html](performance-report-full.html).
"@

$html = @"
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>$(ConvertTo-HtmlText $Title)</title>
  <style>
    :root {
      color-scheme: light;
      --ink: #20242a;
      --muted: #68717d;
      --line: #d8dee6;
      --soft: #f6f8fa;
      --paper: #ffffff;
      --duration: #346ec9;
      --throughput: #15845f;
      --warn-bg: #fff7df;
      --warn-line: #dfc36d;
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      background: var(--paper);
      color: var(--ink);
      font-family: ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
      line-height: 1.55;
    }
    main {
      max-width: 1260px;
      margin: 0 auto;
      padding: 34px 26px 52px;
    }
    header {
      border-bottom: 1px solid var(--line);
      margin-bottom: 22px;
      padding-bottom: 18px;
    }
    h1 {
      margin: 0 0 8px;
      font-size: 2.25rem;
      line-height: 1.12;
      letter-spacing: 0;
    }
    h2 {
      margin: 30px 0 10px;
      font-size: 1.35rem;
      border-bottom: 1px solid var(--line);
      padding-bottom: 6px;
    }
    p { margin: 0 0 14px; color: #39414b; }
    code {
      border: 1px solid var(--line);
      border-radius: 4px;
      background: var(--soft);
      padding: 1px 5px;
      font-size: .86em;
    }
    .layout {
      display: grid;
      grid-template-columns: minmax(260px, 360px) minmax(0, 1fr);
      gap: 24px;
      align-items: start;
    }
    aside {
      position: sticky;
      top: 18px;
      border: 1px solid var(--line);
      border-radius: 8px;
      background: var(--soft);
      padding: 18px;
    }
    aside h2 {
      margin-top: 0;
      font-size: 1.15rem;
    }
    aside ul {
      margin: 0 0 16px 18px;
      padding: 0;
    }
    aside li { margin: 6px 0; }
    .status-table, .metric-table {
      width: 100%;
      border-collapse: collapse;
      border: 1px solid var(--line);
      border-radius: 8px;
      overflow: hidden;
      background: var(--paper);
    }
    th, td {
      border-bottom: 1px solid var(--line);
      padding: 10px 12px;
      text-align: left;
      vertical-align: middle;
    }
    tr:last-child td { border-bottom: 0; }
    th {
      background: #eef2f6;
      color: #343c46;
      font-size: .78rem;
      letter-spacing: .04em;
      text-transform: uppercase;
    }
    .status-table td:last-child {
      text-align: right;
      font-weight: 700;
    }
    .metric-table strong { display: block; }
    .metric-table code { display: inline-block; margin-top: 3px; }
    .bar-cell {
      min-width: 170px;
      height: 28px;
      position: relative;
      border: 1px solid #d4dbe4;
      background: #edf1f5;
      overflow: hidden;
      border-radius: 5px;
    }
    .bar {
      position: absolute;
      inset: 0 auto 0 0;
      min-width: 2px;
    }
    .bar.duration { background: var(--duration); }
    .bar.throughput { background: var(--throughput); }
    .bar-cell span {
      position: relative;
      display: inline-flex;
      align-items: center;
      height: 100%;
      padding-left: 8px;
      font-weight: 700;
      color: #121820;
      white-space: nowrap;
    }
    .chart-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 14px;
    }
    .chart {
      border: 1px solid var(--line);
      border-radius: 8px;
      padding: 14px;
      background: var(--paper);
    }
    .chart h3 {
      margin: 0 0 12px;
      font-size: 1rem;
    }
    .chart-row {
      display: grid;
      grid-template-columns: minmax(120px, 1fr) minmax(150px, 2fr) 86px;
      gap: 10px;
      align-items: center;
      margin: 8px 0;
      font-size: .9rem;
    }
    .mini-bar {
      height: 18px;
      border-radius: 4px;
      background: #e7ebf0;
      overflow: hidden;
    }
    .mini-bar span {
      display: block;
      height: 100%;
      min-width: 2px;
    }
    .mini-bar .duration { background: var(--duration); }
    .mini-bar .throughput { background: var(--throughput); }
    .note {
      border-left: 4px solid var(--warn-line);
      background: var(--warn-bg);
      padding: 12px 14px;
      margin-top: 16px;
      border-radius: 0 6px 6px 0;
    }
    .table-wrap { overflow-x: auto; }
    footer {
      margin-top: 28px;
      color: var(--muted);
      font-size: .9rem;
    }
    @media (max-width: 900px) {
      .layout, .chart-grid { grid-template-columns: 1fr; }
      aside { position: static; }
    }
  </style>
</head>
<body>
  <main>
    <header>
      <h1>$(ConvertTo-HtmlText $Title)</h1>
      <p>Generated: $(ConvertTo-HtmlText $generatedAt)</p>
    </header>

    <div class="layout">
      <aside aria-label="Markdown summary">
        <h2>Markdown Brief</h2>
        <p>This mirrors the Windows Fast Search report style: short written interpretation, compact status tables, and simple bars that make the important differences scannable.</p>
        <ul>
          <li>Real game-process harness, not serializer-only tests.</li>
          <li>Current transport: <code>$(ConvertTo-HtmlText $transport)</code>.</li>
          <li>2 MB file transfer covers the practical "share a music file" case.</li>
          <li>WAN claims still need separate machines or a network emulator.</li>
        </ul>
        <p>Source metrics: <code>$(ConvertTo-HtmlText $metricsFullPath)</code></p>
      </aside>

      <article>
        <h2>Current Status</h2>
        <table class="status-table">
          <tbody>
            <tr><td>Runtime</td><td>$(ConvertTo-HtmlText $runtime)</td></tr>
            <tr><td>Transport</td><td>$(ConvertTo-HtmlText $transport)</td></tr>
            <tr><td>Environment</td><td>$(ConvertTo-HtmlText $environment)</td></tr>
            <tr><td>Measurements</td><td>$($metrics.Count)</td></tr>
            <tr><td>Total payload</td><td>$(Format-Bytes $totalBytes)</td></tr>
            <tr><td>Messages / chunks</td><td>$totalMessages</td></tr>
            <tr><td>Average throughput</td><td>$(Format-Mbps $averageThroughput)</td></tr>
            <tr><td>Slowest measurement</td><td>$(ConvertTo-HtmlText $slowest.label), $(Format-Duration $slowest.durationMs)</td></tr>
            <tr><td>Highest throughput</td><td>$(ConvertTo-HtmlText $fastestThroughput.label), $(Format-Mbps $fastestThroughput.throughputMbps)</td></tr>
          </tbody>
        </table>

        <h2>Bar Charts</h2>
        <div class="chart-grid">
          <section class="chart">
            <h3>Duration by Scenario</h3>
"@

foreach ($metric in $orderedMetrics) {
    $width = if ($maxDuration -gt 0) { [Math]::Max(1, [Math]::Round(($metric.durationMs / $maxDuration) * 100, 2)) } else { 0 }
    $html += @"
            <div class="chart-row"><span>$(ConvertTo-HtmlText $metric.label)</span><div class="mini-bar"><span class="duration" style="width: $width%"></span></div><strong>$(Format-Duration $metric.durationMs)</strong></div>
"@
}

$html += @"
          </section>
          <section class="chart">
            <h3>Throughput by Scenario</h3>
"@

foreach ($metric in $orderedMetrics) {
    $width = if ($maxThroughput -gt 0) { [Math]::Max(1, [Math]::Round(($metric.throughputMbps / $maxThroughput) * 100, 2)) } else { 0 }
    $html += @"
            <div class="chart-row"><span>$(ConvertTo-HtmlText $metric.label)</span><div class="mini-bar"><span class="throughput" style="width: $width%"></span></div><strong>$(Format-Mbps $metric.throughputMbps)</strong></div>
"@
}

$html += @"
          </section>
        </div>

        <h2>Measurement Table</h2>
        <div class="table-wrap">
          <table class="metric-table">
            <thead>
              <tr>
                <th>Scenario</th>
                <th>Role</th>
                <th>Direction</th>
                <th>Payload</th>
                <th>Messages</th>
                <th>Duration</th>
                <th>Throughput</th>
              </tr>
            </thead>
            <tbody>
$rows
            </tbody>
          </table>
        </div>

        <h2>Interpretation</h2>
        <p>The 2 MB music-file transfer is the strongest practical signal in this run because it exercises SteamNetworkLib's chunked transfer path. The 70 KB state blob is a smaller mod-state synchronization case, while the built-in model suite keeps coverage on EventMessage, DataSyncMessage, HeartbeatMessage, StreamMessage, and FileTransferMessage.</p>
        <div class="note">Upload speed, download speed, and latency are observed from this local transport path. For public WAN claims, run the same metrics across separate machines or under a controlled network emulator and regenerate this page from the resulting JSONL.</div>
      </article>
    </div>

    <footer>Generated from SteamNetworkLib real-game metrics.</footer>
  </main>
</body>
</html>
"@

$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDir = Split-Path -Parent $outputFullPath
if (-not [string]::IsNullOrWhiteSpace($outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$markdownFullPath = [System.IO.Path]::GetFullPath($MarkdownOutputPath)
$markdownDir = Split-Path -Parent $markdownFullPath
if (-not [string]::IsNullOrWhiteSpace($markdownDir)) {
    New-Item -ItemType Directory -Path $markdownDir -Force | Out-Null
}

Set-Content -LiteralPath $outputFullPath -Value $html -Encoding UTF8
Set-Content -LiteralPath $markdownFullPath -Value $markdown -Encoding UTF8
Write-Host "Performance report written to: $outputFullPath" -ForegroundColor Green
Write-Host "Markdown report written to: $markdownFullPath" -ForegroundColor Green
