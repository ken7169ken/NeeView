<#
.SYNOPSIS
Commit and create shortcut workspace.

.DESCRIPTION
Run git add, git commit, and create .lnk files for files changed in HEAD.

.PARAMETER CommitMessage
Git commit message.

.PARAMETER Name
Workspace folder name.

.EXAMPLE
.\make-links.ps1 "Enable Ctrl+D bookmark making for multiple bookshelf selections" NeeViewP0G0
#>

param(
    [string]$CommitMessage,
    [string]$ProjectName
)

if ([string]::IsNullOrWhiteSpace($CommitMessage) -or [string]::IsNullOrWhiteSpace($ProjectName)) {
    Show-Usage
    exit 1
}

if ($args -contains "--help" -or $args -contains "-h")
{
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  .\make-links.ps1 <CommitMessage> <ProjectName>"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host '  .\make-links.ps1 "Enable Ctrl+D bookmark making for multiple bookshelf selections" NeeViewP0G0'
    Write-Host '  .\make-links.ps1 "Add page thumbnail size option" NeeViewP0H0'
    Write-Host ""
    Write-Host "Behavior:"
    Write-Host "  1. git add ."
    Write-Host "  2. git commit -m <CommitMessage>"
    Write-Host "  3. create .lnk files in workspace"
    Write-Host ""

    exit
}

$repo = "C:\Users\user\Test\NeeView"
$dst  = "C:\Users\user\Test\workspace\NeeView\$ProjectName"
New-Item -ItemType Directory -Force -Path $dst | Out-Null

Set-Location $repo

git add .

git commit -m $CommitMessage

$files = git diff-tree --no-commit-id --name-only -r HEAD |
    Where-Object {
        $_ -match '\.(cs|xaml|json)$'
    }

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

git log --oneline -10