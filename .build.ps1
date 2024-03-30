<#
.Synopsis
	Build script

.Description
	TASKS AND REQUIREMENTS
	Initialize and Clean repository
#>

param(
    # Build Version
    [Parameter()]
    [string]
    $Version
)

Set-StrictMode -Version Latest

# Synopsis: Test
Task Test Build, {
    Exec { dotnet test '.\TIKSN.Framework.Core.Tests\TIKSN.Framework.Core.Tests.csproj' }

    Exec { dotnet test '.\TIKSN.Framework.IntegrationTests\TIKSN.Framework.IntegrationTests.csproj' }
}

# Synopsis: Build
Task Build Format, BuildLanguageLocalization, BuildRegionLocalization, BuildCore, BuildMaui, {
    Exec { dotnet build $script:solution }
}

# Synopsis: Build Language Localization
Task BuildLanguageLocalization EstimateVersion, {
    $project = Resolve-Path -Path 'TIKSN.LanguageLocalization/TIKSN.LanguageLocalization.csproj'

    Exec { dotnet build $project /v:m /p:Configuration=Release /p:version=$Script:NextVersion /p:OutDir=$script:anyBuildArtifactsFolder }
}

# Synopsis: Build Region Localization
Task BuildRegionLocalization EstimateVersion, {
    $project = Resolve-Path -Path 'TIKSN.RegionLocalization/TIKSN.RegionLocalization.csproj'

    Exec { dotnet build $project /v:m /p:Configuration=Release /p:version=$Script:NextVersion /p:OutDir=$script:anyBuildArtifactsFolder }
}

# Synopsis: Build Core
Task BuildCore EstimateVersion, {
    $project = Resolve-Path -Path 'TIKSN.Framework.Core/TIKSN.Framework.Core.csproj'

    Exec { dotnet build $project /v:m /p:Configuration=Release /p:version=$Script:NextVersion /p:OutDir=$script:anyBuildArtifactsFolder }
}

# Synopsis: Build MAUI
Task BuildMaui EstimateVersion, {
    $project = Resolve-Path -Path 'TIKSN.Framework.Maui/TIKSN.Framework.Maui.csproj'

    Exec { dotnet build $project /v:m /p:Configuration=Release /p:version=$Script:NextVersion /p:OutDir=$script:anyBuildArtifactsFolder }

    Exec { dotnet build $project --framework net8.0-ios /v:m /p:Configuration=Release /p:version=$Script:NextVersion /p:OutDir=$script:anyIosBuildArtifactsFolder }
    Exec { dotnet build $project --framework net8.0-maccatalyst /v:m /p:Configuration=Release /p:version=$Script:NextVersion /p:OutDir=$script:anyMaccatalystBuildArtifactsFolder }
    Exec { dotnet build $project --framework net8.0-android /v:m /p:Configuration=Release /p:version=$Script:NextVersion /p:OutDir=$script:anyAndroidBuildArtifactsFolder }
    Exec { dotnet build $project --framework net8.0-windows10.0.19041.0 /v:m /p:Configuration=Release /p:version=$Script:NextVersion /p:OutDir=$script:anyWindowsBuildArtifactsFolder }
}

# Synopsis: Estimate Next Version
Task EstimateVersion Restore, {
    if ($Version) {
        $Script:NextVersion = $Version
    }
    else {
        $foundPackages = Find-Package -Name $PackageId -AllVersions -AllowPrereleaseVersions -ProviderName NuGet -Source nuget.org

        $foundPackages = $foundPackages | Where-Object { $_.Name -eq $PackageId }

        $value = [System.Management.Automation.SemanticVersion]::New(0)
        $foundPackageVersions = $foundPackages | Select-Object -ExpandProperty Version
        $foundPackageVersions = $foundPackageVersions | Where-Object { [System.Management.Automation.SemanticVersion]::TryParse($_, [ref][System.Management.Automation.SemanticVersion]$value) }
        $foundPackageVersions = $foundPackageVersions | ForEach-Object { [System.Management.Automation.SemanticVersion]$_ }
        $foundPackageVersions = $foundPackageVersions | Sort-Object -Descending
        $latestPackageVersion = $foundPackageVersions | Select-Object -First 1

        $currentCommit = git rev-parse HEAD

        if ($null -eq $latestPackageVersion.PreReleaseLabel) {
            $nextPreReleaseLabel = 'alpha.1'

            $Script:NextVersion = [System.Management.Automation.SemanticVersion]::New($latestPackageVersion.Major, $latestPackageVersion.Minor, $latestPackageVersion.Patch + 1, $nextPreReleaseLabel, $currentCommit)
        }
        else {
            $nextPreReleaseLabel = $latestPackageVersion.PreReleaseLabel.Split('.')[0] + '.' + (([int]$latestPackageVersion.PreReleaseLabel.Split('.')[1]) + 1)

            $Script:NextVersion = [System.Management.Automation.SemanticVersion]::New($latestPackageVersion.Major, $latestPackageVersion.Minor, $latestPackageVersion.Patch, $nextPreReleaseLabel, $currentCommit)
        }
    }

    Write-Output "Next version estimated to be $Script:NextVersion"
}

# Synopsis: Format
Task Format Restore, FormatWhitespace, FormatStyle, FormatAnalyzers

# Synopsis: Format Analyzers
Task FormatAnalyzers Restore, FormatAnalyzersLanguageLocalization, FormatAnalyzersRegionLocalization, FormatAnalyzersCore, FormatAnalyzersMaui, FormatAnalyzersSolution

# Synopsis: Format Analyzers Solution
Task FormatAnalyzersSolution Restore, {
    # Exec { dotnet format analyzers --severity info --verbosity diagnostic $script:solution }
}

# Synopsis: Format Analyzers Language Localization
Task FormatAnalyzersLanguageLocalization Restore, {
    $project = Resolve-Path -Path 'TIKSN.LanguageLocalization/TIKSN.LanguageLocalization.csproj'

    Exec { dotnet format analyzers --severity info --verbosity diagnostic $project }
}

# Synopsis: Format Analyzers Region Localization
Task FormatAnalyzersRegionLocalization Restore, {
    $project = Resolve-Path -Path 'TIKSN.RegionLocalization/TIKSN.RegionLocalization.csproj'

    Exec { dotnet format analyzers --severity info --verbosity diagnostic $project }
}

# Synopsis: Format Analyzers Core
Task FormatAnalyzersCore Restore, {
    $project = Resolve-Path -Path 'TIKSN.Framework.Core/TIKSN.Framework.Core.csproj'

    Exec { dotnet format analyzers --severity info --verbosity diagnostic $project }
}

# Synopsis: Format Analyzers MAUI
Task FormatAnalyzersMaui Restore, {
    $project = Resolve-Path -Path 'TIKSN.Framework.Maui/TIKSN.Framework.Maui.csproj'

    Exec { dotnet format analyzers --severity info --verbosity diagnostic $project }
}

# Synopsis: Format Style
Task FormatStyle Restore, FormatStyleLanguageLocalization, FormatStyleRegionLocalization, FormatStyleCore, FormatStyleMaui, FormatStyleSolution

# Synopsis: Format Style Solution
Task FormatStyleSolution Restore, {
    # Exec { dotnet format style --severity info --verbosity diagnostic $script:solution }
}

# Synopsis: Format Style Language Localization
Task FormatStyleLanguageLocalization Restore, {
    $project = Resolve-Path -Path 'TIKSN.LanguageLocalization/TIKSN.LanguageLocalization.csproj'

    Exec { dotnet format style --severity info --verbosity diagnostic $project }
}

# Synopsis: Format Style Region Localization
Task FormatStyleRegionLocalization Restore, {
    $project = Resolve-Path -Path 'TIKSN.RegionLocalization/TIKSN.RegionLocalization.csproj'

    Exec { dotnet format style --severity info --verbosity diagnostic $project }
}

# Synopsis: Format Style Core
Task FormatStyleCore Restore, {
    $project = Resolve-Path -Path 'TIKSN.Framework.Core/TIKSN.Framework.Core.csproj'

    Exec { dotnet format style --severity info --verbosity diagnostic $project }
}

# Synopsis: Format Style MAUI
Task FormatStyleMaui Restore, {
    $project = Resolve-Path -Path 'TIKSN.Framework.Maui/TIKSN.Framework.Maui.csproj'

    Exec { dotnet format style --severity info --verbosity diagnostic $project }
}

# Synopsis: Format Whitespace
Task FormatWhitespace Restore, {
    Exec { dotnet format whitespace --verbosity diagnostic $script:solution }
}

# Synopsis: Download Currency Codes
Task DownloadCurrencyCodes Clean, {
    Invoke-WebRequest -Uri 'https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-one.xml' -OutFile 'TIKSN.Framework.Core/Finance/Resources/TableA1.xml'
    Invoke-WebRequest -Uri 'https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-three.xml' -OutFile 'TIKSN.Framework.Core/Finance/Resources/TableA3.xml'
}

# Synopsis: Scan with DevSkim for security issues
Task DevSkim Restore, {
    $sarifFile = Join-Path -Path $script:trashFolder -ChildPath 'DevSkim.sarif'
    Exec { dotnet tool run devskim analyze --source-code . --output-file $sarifFile }
    Exec { dotnet tool run devskim fix --source-code . --sarif-result $sarifFile --all }
}

# Synopsis: Restore
Task Restore RestoreWorkloads, RestoreTools, RestorePackages

# Synopsis: Restore workloads
Task RestoreWorkloads Clean, {
    Exec { dotnet workload restore }
}

# Synopsis: Restore tools
Task RestoreTools Clean, {
    Exec { dotnet tool restore }
}

# Synopsis: Restore packages
Task RestorePackages Clean, {
    Exec { dotnet restore $script:solution }
}

# Synopsis: Clean previous build leftovers
Task Clean Init, {
    Get-ChildItem -Directory
    | Where-Object { -not $_.Name.StartsWith('.') }
    | ForEach-Object { Get-ChildItem -Path $_ -Recurse -Directory }
    | Where-Object { ( $_.Name -eq 'bin') -or ( $_.Name -eq 'obj') }
    | ForEach-Object { Remove-Item -Path $_ -Recurse -Force }
}

# Synopsis: Initialize folders and variables
Task Init {
    $date = Get-Date -Format 'yyyyMMddHHmmss'
    $trashFolder = Join-Path -Path . -ChildPath '.trash'
    $script:trashFolder = Join-Path -Path $trashFolder -ChildPath $date
    New-Item -Path $script:trashFolder -ItemType Directory | Out-Null
    $script:trashFolder = Resolve-Path -Path $script:trashFolder

    $script:buildArtifactsFolder = Join-Path -Path $script:trashFolder -ChildPath 'artifacts'
    New-Item -Path $script:buildArtifactsFolder -ItemType Directory | Out-Null

    $script:anyBuildArtifactsFolder = Join-Path -Path $script:buildArtifactsFolder -ChildPath 'any'
    New-Item -Path $script:anyBuildArtifactsFolder -ItemType Directory | Out-Null

    $script:anyIosBuildArtifactsFolder = Join-Path -Path $script:buildArtifactsFolder -ChildPath 'any-ios'
    New-Item -Path $script:anyIosBuildArtifactsFolder -ItemType Directory | Out-Null

    $script:anyMaccatalystBuildArtifactsFolder = Join-Path -Path $script:buildArtifactsFolder -ChildPath 'any-maccatalyst'
    New-Item -Path $script:anyMaccatalystBuildArtifactsFolder -ItemType Directory | Out-Null

    $script:anyAndroidBuildArtifactsFolder = Join-Path -Path $script:buildArtifactsFolder -ChildPath 'any-android'
    New-Item -Path $script:anyAndroidBuildArtifactsFolder -ItemType Directory | Out-Null

    $script:anyWindowsBuildArtifactsFolder = Join-Path -Path $script:buildArtifactsFolder -ChildPath 'any-windows'
    New-Item -Path $script:anyWindowsBuildArtifactsFolder -ItemType Directory | Out-Null

    $script:armBuildArtifactsFolder = Join-Path -Path $script:buildArtifactsFolder -ChildPath 'arm'
    New-Item -Path $script:armBuildArtifactsFolder -ItemType Directory | Out-Null

    $script:x64BuildArtifactsFolder = Join-Path -Path $script:buildArtifactsFolder -ChildPath 'x64'
    New-Item -Path $script:x64BuildArtifactsFolder -ItemType Directory | Out-Null

    $script:x86BuildArtifactsFolder = Join-Path -Path $script:buildArtifactsFolder -ChildPath 'x86'
    New-Item -Path $script:x86BuildArtifactsFolder -ItemType Directory | Out-Null

    $script:solution = Resolve-Path -Path 'TIKSN Framework.sln'
    $script:PackageId = 'TIKSN-Framework'
}
