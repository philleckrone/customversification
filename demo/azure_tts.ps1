<#
.SYNOPSIS
  Azure AI Speech text-to-speech through its REST API (no SDK needed).
  The key and region come from the user's Windows settings (AZURE_SPEECH_KEY,
  AZURE_SPEECH_REGION), read from the registry so a restart is not needed after setting them.
  Dot-source it, then:  Speak-Azure -Text '...' -Voice 'en-US-AvaNeural' -OutFile x.wav

.EXAMPLE
  .\azure_tts.ps1 -Samples     # writes voice_samples\*.wav from narration.json
#>
param([switch]$Samples)

function Get-AzureSpeechSetting($name) {
    $v = [Environment]::GetEnvironmentVariable($name, 'Process')
    if (-not $v) { $v = [Environment]::GetEnvironmentVariable($name, 'User') }
    if (-not $v) { throw "$name is not set (see the steps for storing the Azure key)" }
    $v.Trim()
}

# Words the voice should say differently from how they are written, per language: the subtitles
# keep the written word, the voice says the alias (SSML <sub>). "Paratext" as "PEARatext" is the
# user's request.
$script:SayAs = @{
    # Custom Versification Editor: file names and book codes spelled out.
    'en' = @{ 'Paratext' = 'Pair-uh-text'; 'custom.vrs' = 'custom dot V R S'; 'Settings.xml' = 'Settings dot X M L' }
    'es' = @{ 'custom.vrs' = 'custom punto V R S'; 'Settings.xml' = 'Settings punto X M L' }
    'fr' = @{ 'custom.vrs' = 'custom point V R S'; 'Settings.xml' = 'Settings point X M L' }
}

# Writes a WAV file (24 kHz, 16-bit, mono). $Rate is an SSML prosody rate such as '-5%' or '0%'.
function Speak-Azure([string]$Text, [string]$Voice, [string]$OutFile, [string]$Rate = '0%') {
    $key = Get-AzureSpeechSetting 'AZURE_SPEECH_KEY'
    $region = Get-AzureSpeechSetting 'AZURE_SPEECH_REGION'
    $lang = ($Voice -split '-')[0..1] -join '-'
    $esc = [Security.SecurityElement]::Escape($Text)
    $words = $script:SayAs[($lang -split '-')[0]]
    if ($words) {
        foreach ($w in $words.Keys) {
            $esc = [regex]::Replace($esc, '\b' + [regex]::Escape($w) + '\b', "<sub alias='$($words[$w])'>$w</sub>")
        }
    }
    $ssml = "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='$lang'>" +
        "<voice name='$Voice'><prosody rate='$Rate'>$esc</prosody></voice></speak>"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $headers = @{
        'Ocp-Apim-Subscription-Key' = $key
        'X-Microsoft-OutputFormat'  = 'riff-24khz-16bit-mono-pcm'
        'User-Agent'                = 'ParatextPluginDemo'
    }
    $body = [Text.Encoding]::UTF8.GetBytes($ssml)
    for ($try = 1; $try -le 4; $try++) {
        try {
            Invoke-WebRequest -Uri "https://$region.tts.speech.microsoft.com/cognitiveservices/v1" -Method Post `
                -Headers $headers -ContentType 'application/ssml+xml' -Body $body -OutFile $OutFile -UseBasicParsing
            return
        }
        catch {
            # The free tier allows a limited number of requests per minute: wait and try again.
            if ($try -eq 4) { throw "Azure speech failed for '$Voice': $($_.Exception.Message)" }
            Start-Sleep -Seconds (5 * $try)
        }
    }
}

if ($Samples) {
    $narr = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'narration.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $out = Join-Path $PSScriptRoot 'voice_samples'
    New-Item -ItemType Directory -Force $out | Out-Null
    $ids = @('s01_open', 's04_browser')
    $voices = @{
        'en' = @('en-US-AvaNeural', 'en-US-AndrewNeural', 'en-US-JennyNeural', 'en-US-AriaNeural', 'en-US-GuyNeural')
        'es' = @('es-MX-DaliaNeural', 'es-MX-JorgeNeural')
    }
    foreach ($lang in @('en', 'es')) {
        $text = ($ids | ForEach-Object { $id = $_; ($narr.steps | Where-Object { $_.id -eq $id }).$lang.text }) -join ' '
        foreach ($v in $voices[$lang]) {
            $name = ($v -replace '^\w\w-\w\w-', '' -replace 'Neural$', '')
            $file = Join-Path $out ("{0}_{1}.wav" -f $lang.ToUpper(), $name)
            Speak-Azure -Text $text -Voice $v -OutFile $file
            "written $file"
        }
    }
}
