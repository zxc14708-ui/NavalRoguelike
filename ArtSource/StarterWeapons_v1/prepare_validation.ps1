$ErrorActionPreference = 'Stop'
$starterProject = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$starterReview = Join-Path $starterProject 'ArtSource/StarterWeapons_v1/ValidationProject'
New-Item -ItemType Directory -Force -Path $starterReview | Out-Null
foreach ($folder in @('ProjectSettings','Packages','Assets/_Game/Editor','Assets/_Game/Scripts/Combat','Assets/_Game/Art/Models/Materials','Assets/_Game/Art/Models/StarterWeapons_v1')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $starterReview $folder) | Out-Null
}
Copy-Item -Path "$starterProject/ProjectSettings/*" -Destination "$starterReview/ProjectSettings"
Copy-Item -LiteralPath "$starterProject/Assets/Settings" -Destination "$starterReview/Assets/Settings" -Recurse -Force
Copy-Item -Path "$starterProject/Assets/_Game/Art/Models/StarterWeapons_v1/*" -Destination "$starterReview/Assets/_Game/Art/Models/StarterWeapons_v1" -Force
foreach ($name in @('Deck Grey','Naval Blue Grey','Gunmetal')) {
    Copy-Item -LiteralPath "$starterProject/Assets/_Game/Art/Models/Materials/$name.mat" -Destination "$starterReview/Assets/_Game/Art/Models/Materials" -Force
    Copy-Item -LiteralPath "$starterProject/Assets/_Game/Art/Models/Materials/$name.mat.meta" -Destination "$starterReview/Assets/_Game/Art/Models/Materials" -Force
}
# Reuse already installed packages; no downloads or changes to the original editor.
$starterDeps = [ordered]@{}
$starterManifest = Get-Content "$starterProject/Packages/manifest.json" -Raw | ConvertFrom-Json
foreach ($property in $starterManifest.dependencies.PSObject.Properties) {
    if ($property.Name.StartsWith('com.unity.modules.')) { $starterDeps[$property.Name] = $property.Value }
}
foreach ($dir in Get-ChildItem "$starterProject/Library/PackageCache" -Directory) {
    $info = Get-Content (Join-Path $dir.FullName 'package.json') -Raw | ConvertFrom-Json
    $starterDeps[$info.name] = 'file:' + $dir.FullName.Replace('\','/')
}
@{dependencies=$starterDeps} | ConvertTo-Json -Depth 10 | Set-Content "$starterReview/Packages/manifest.json" -Encoding utf8
Copy-Item -LiteralPath "$starterProject/Assets/_Game/Editor/StarterWeaponArtBuilder.cs" -Destination "$starterReview/Assets/_Game/Editor" -Force

function Extract-Block([string]$text, [string]$marker) {
    $start = $text.IndexOf($marker)
    if ($start -lt 0) { throw "Missing source marker: $marker" }
    $opening = $text.IndexOf('{', $start)
    $depth = 1
    for ($i = $opening + 1; $i -lt $text.Length; $i++) {
        if ($text[$i] -eq '{') { $depth++ }
        if ($text[$i] -eq '}') { $depth--; if ($depth -eq 0) { return $text.Substring($start, $i - $start + 1) } }
    }
    throw "Unbalanced source: $marker"
}
$starterImport = Extract-Block (Get-Content "$starterProject/Assets/_Game/Editor/NavalEditorUtil.cs" -Raw) 'public static GameObject ImportModel('
"using System.Collections.Generic; using UnityEditor; using UnityEngine; namespace Game.EditorTools { public static class NavalEditorUtil { $starterImport } }" | Set-Content "$starterReview/Assets/_Game/Editor/NavalEditorUtil.cs" -Encoding utf8
$starterArc = Extract-Block (Get-Content "$starterProject/Assets/_Game/Scripts/Combat/FireArc.cs" -Raw) 'public readonly struct FireArc'
"using System.Collections.Generic; using UnityEngine; namespace Game.Combat { $starterArc }" | Set-Content "$starterReview/Assets/_Game/Scripts/Combat/FireArc.cs" -Encoding utf8
(Get-Content "$starterProject/Assets/_Game/Scripts/Combat/WeaponController.cs" -Raw).Replace('using Game.Modules;', '') | Set-Content "$starterReview/Assets/_Game/Scripts/Combat/WeaponController.cs" -Encoding utf8
Write-Output $starterReview
