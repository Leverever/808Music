param(
    [Parameter(Mandatory = $true)][string]$Username,
    [Parameter(Mandatory = $true)][string]$Password,
    [int]$TrackId = 17,
    [string]$BaseUrl = "http://localhost:7000",
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $PSScriptRoot "results"
}

if (-not (Test-Path -LiteralPath $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
}

$loginBody = @{
    username = $Username
    password = $Password
} | ConvertTo-Json

$login = Invoke-RestMethod `
    -Uri "$BaseUrl/api/UserAuthLoginEndpoint" `
    -Method Post `
    -ContentType "application/json" `
    -Body $loginBody

if ([string]::IsNullOrWhiteSpace($login.token)) {
    throw "Prijava nije vratila pristupni token."
}

$headers = @{ Authorization = "Bearer $($login.token)" }
$latencies = [System.Collections.Generic.List[object]]::new()

function Invoke-MeasuredGet {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Uri,
        [int]$Repetitions = 5
    )

    $lastResponse = $null
    for ($iteration = 1; $iteration -le $Repetitions; $iteration++) {
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        $lastResponse = Invoke-RestMethod -Uri $Uri -Headers $headers -Method Get
        $watch.Stop()
        $latencies.Add([pscustomobject]@{
            Endpoint = $Name
            Iteration = $iteration
            DurationMs = [math]::Round($watch.Elapsed.TotalMilliseconds, 2)
            Successful = $true
        })
    }
    return $lastResponse
}

$homeResponse = Invoke-MeasuredGet `
    -Name "home-recommendations" `
    -Uri "$BaseUrl/api/v2/recommendations/home?dailyPlaylistLimit=6&albumLimit=10&artistLimit=10&playlistLimit=10&trackLimit=20"
$radio = Invoke-MeasuredGet `
    -Name "track-radio" `
    -Uri "$BaseUrl/api/v2/tracks/$TrackId/radio?limit=20"
$stems = Invoke-MeasuredGet `
    -Name "track-stems" `
    -Uri "$BaseUrl/api/v2/tracks/$TrackId/stems"
$masterPlayback = Invoke-MeasuredGet `
    -Name "playback-master" `
    -Uri "$BaseUrl/api/v2/tracks/$TrackId/playback?artistMode=false"
$stemPlayback = Invoke-MeasuredGet `
    -Name "playback-stems" `
    -Uri "$BaseUrl/api/v2/tracks/$TrackId/playback?artistMode=true"

$latencies | Export-Csv `
    -LiteralPath (Join-Path $OutputDirectory "api_latency.csv") `
    -NoTypeInformation `
    -Encoding utf8

$homeTracks = @($homeResponse.recommendedTracks)
$radioTracks = @($radio.tracks)
$stemSets = @($stems.stemSets)
$activeStemSet = $stemSets | Where-Object { $_.isActive -and $_.status -eq "Ready" } | Select-Object -First 1

$masterAssetReachable = $false
$masterAssetStatus = $null
if (-not [string]::IsNullOrWhiteSpace($masterPlayback.stream.master.url)) {
    $status = & curl.exe -s -o NUL -w "%{http_code}" --range 0-0 -- $masterPlayback.stream.master.url
    $masterAssetStatus = [int]$status
    $masterAssetReachable = $masterAssetStatus -ge 200 -and $masterAssetStatus -lt 400
}

$stemAssets = @($stemPlayback.stream.stemSet.stems)
$reachableStemAssets = 0
foreach ($asset in $stemAssets) {
    if ([string]::IsNullOrWhiteSpace($asset.url)) {
        continue
    }
    $status = & curl.exe -s -o NUL -w "%{http_code}" --range 0-0 -- $asset.url
    if ([int]$status -ge 200 -and [int]$status -lt 400) {
        $reachableStemAssets++
    }
}

$unauthorizedStatus = & curl.exe -s -o NUL -w "%{http_code}" "$BaseUrl/api/v2/recommendations/home"

$latencySummary = $latencies |
    Group-Object Endpoint |
    ForEach-Object {
        $values = @($_.Group.DurationMs | Sort-Object)
        if ($values.Count % 2 -eq 1) {
            $median = $values[[math]::Floor($values.Count / 2)]
        }
        else {
            $median = ($values[$values.Count / 2 - 1] + $values[$values.Count / 2]) / 2
        }
        [pscustomobject]@{
            endpoint = $_.Name
            samples = $values.Count
            medianMs = [math]::Round($median, 2)
            meanMs = [math]::Round(($values | Measure-Object -Average).Average, 2)
            minMs = [math]::Round(($values | Measure-Object -Minimum).Minimum, 2)
            maxMs = [math]::Round(($values | Measure-Object -Maximum).Maximum, 2)
        }
    }

$summary = [ordered]@{
    testedAtUtc = [DateTime]::UtcNow.ToString("o")
    trackId = $TrackId
    authentication = [ordered]@{
        loginSucceeded = $true
        unauthenticatedHomeStatus = [int]$unauthorizedStatus
    }
    home = [ordered]@{
        dailyPlaylists = @($homeResponse.dailyPersonalizedPlaylists).Count
        albums = @($homeResponse.recommendedAlbums).Count
        artists = @($homeResponse.recommendedArtists).Count
        playlists = @($homeResponse.recommendedPlaylists).Count
        tracks = $homeTracks.Count
        tracksWithReason = @($homeTracks | Where-Object { -not [string]::IsNullOrWhiteSpace($_.reason) }).Count
        tracksWithSourceSignals = @($homeTracks | Where-Object { $_.sourceSignals.PSObject.Properties.Count -gt 0 }).Count
        tracksWithMatchedTags = @($homeTracks | Where-Object { @($_.matchedTags).Count -gt 0 }).Count
    }
    radio = [ordered]@{
        seedTrackId = $radio.seedTrackId
        tracks = $radioTracks.Count
        tracksWithReason = @($radioTracks | Where-Object { -not [string]::IsNullOrWhiteSpace($_.reason) }).Count
        tracksWithSourceSignals = @($radioTracks | Where-Object { $_.sourceSignals.PSObject.Properties.Count -gt 0 }).Count
        tracksWithMatchedTags = @($radioTracks | Where-Object { @($_.matchedTags).Count -gt 0 }).Count
    }
    stems = [ordered]@{
        stemSets = $stemSets.Count
        activeReadyStemSets = @($stemSets | Where-Object { $_.isActive -and $_.status -eq "Ready" }).Count
        stemsInActiveSet = if ($null -eq $activeStemSet) { 0 } else { @($activeStemSet.stems).Count }
        playbackManifestStemAssets = $stemAssets.Count
        reachableStemAssets = $reachableStemAssets
    }
    playback = [ordered]@{
        masterManifestReturned = $null -ne $masterPlayback.stream.master
        masterAssetHttpStatus = $masterAssetStatus
        masterAssetReachable = $masterAssetReachable
        stemManifestReturned = $null -ne $stemPlayback.stream.stemSet
    }
    latency = @($latencySummary)
}

$summary |
    ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath (Join-Path $OutputDirectory "api_smoke_summary.json") -Encoding utf8

$explanationSamples = [ordered]@{
    home = @($homeTracks | Select-Object -First 3 | ForEach-Object {
        [ordered]@{
            trackId = $_.trackId
            score = $_.score
            reason = $_.reason
            matchedTags = @($_.matchedTags)
            clusterKey = $_.clusterKey
            sourceSignals = $_.sourceSignals
        }
    })
    radio = @($radioTracks | Select-Object -First 3 | ForEach-Object {
        [ordered]@{
            trackId = $_.trackId
            score = $_.score
            reason = $_.reason
            matchedTags = @($_.matchedTags)
            clusterKey = $_.clusterKey
            sourceSignals = $_.sourceSignals
        }
    })
}

$explanationSamples |
    ConvertTo-Json -Depth 10 |
    Set-Content -LiteralPath (Join-Path $OutputDirectory "explanation_samples.json") -Encoding utf8

$summary | ConvertTo-Json -Depth 8
