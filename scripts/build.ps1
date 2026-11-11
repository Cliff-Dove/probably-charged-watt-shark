#requires -Version 7.0

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

# 从仓库根目录执行，确保读取项目的 global.json 和构建配置。
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $projectPath = './src/ProbablyCharged/ProbablyCharged.csproj'
    $manifestPath = './src/ProbablyCharged/manifest.xml'
    $assetsPath = './assets/'

    # 构建失败时停止，不继续部署旧产物。
    dotnet build $projectPath -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed ($LASTEXITCODE)."
    }

    # 由 MSBuild 读取 Directory.Build.props 中实际生效的 GameDir。
    $gameDir = dotnet msbuild $projectPath -nologo -getProperty:GameDir
    if ($LASTEXITCODE -ne 0) {
        throw "GameDir evaluation failed ($LASTEXITCODE)."
    }

    # 使用清单中的模组 ID 作为安装目录名。
    $manifest = [xml](Get-Content $manifestPath -Raw)
    $destination = Join-Path $gameDir "Mods/$($manifest.Manifest.ID)"
    New-Item -ItemType Directory -Path $destination -Force | Out-Null

    # 复制模组 DLL 和清单。
    Copy-Item -LiteralPath @(
        "./src/ProbablyCharged/bin/$Configuration/netstandard2.1/ProbablyCharged.dll"
        $manifestPath
    ) -Destination $destination -Force

    # 将资源子项复制到模组根目录，保留各资源文件夹的层级。
    Get-ChildItem -LiteralPath $assetsPath | Copy-Item -Destination $destination -Recurse -Force
}
finally {
    # 返回调用脚本前所在的目录。
    Pop-Location
}
