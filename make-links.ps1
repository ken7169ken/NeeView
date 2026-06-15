param(
    [Parameter(Mandatory=$true)]
    [string]$Name
)

$repo = "C:\Users\user\Test\NeeView"
$dst  = "C:\Users\user\Test\workspace\NeeView\$Name"

New-Item -ItemType Directory -Force -Path $dst | Out-Null

Set-Location $repo

$files = git diff-tree --no-commit-id --name-only -r HEAD

$shell = New-Object -ComObject WScript.Shell

foreach ($file in $files) {
    $src = Join-Path $repo $file

    if (-not (Test-Path $src)) {
        continue
    }

    $shortcutName = [IO.Path]::GetFileName($file) + ".lnk"
    $shortcutPath = Join-Path $dst $shortcutName

    $lnk = $shell.CreateShortcut($shortcutPath)
    $lnk.TargetPath = $src
    $lnk.WorkingDirectory = Split-Path $src
    $lnk.Save()
}

Write-Host "Created shortcuts in: $dst"