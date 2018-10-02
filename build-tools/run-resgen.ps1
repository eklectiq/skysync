param(
	[string] $baseDir = $null
)

if ($baseDir -eq $null -or $baseDir.Length -eq 0) {
	$scriptDir = (Split-Path $script:MyInvocation.MyCommand.Path)
	$baseDir = "$scriptDir/.."
} else {
	$baseDir = (Resolve-Path $baseDir).Path
}

$sdkDir = (Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Microsoft SDKs\Windows\v8.1A" -ErrorAction SilentlyContinue).InstallationFolder
if ($sdkDir -eq $null) {
	$sdkDir = (Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Microsoft SDKs\Windows").CurrentInstallFolder
}

if ($sdkDir -eq $null) {
	throw ("Windows Sdk could not be located");
}

$resgen = (Join-Path (Get-ChildItem "$sdkDir\bin\*Tools")[0].FullName 'resgen.exe')

Get-ChildItem $baseDir -Directory | Where { $_.Name -notmatch "node_modules" } | ForEach {
	Get-ChildItem "$($_.FullName)/*.resx" -recurse | Where { $_.FullName -notmatch 'Template' } | ForEach {
		$directory = $_.Directory
		$project = $directory.Name
		while (!$project.StartsWith('PortalArchitects.')) {
			$directory = $directory.Parent;
			$project = $directory.Name;
		}

		$csProj = Get-Content (Join-Path $directory.FullName "$($project).csproj") | Out-String
		$customToolNamespaceIndex = $csProj.IndexOf('<CustomToolNamespace>');
		if ($customToolNamespaceIndex -eq -1) {
			$namespace = $project.Replace('.Primitives', '').Replace('.Abstractions', '').Replace('.Configuration', '').Replace('.Diagnostics', '').Replace('.Extensions', '').Replace('.OAuth2', '').Replace('.OAuth', '').Replace('.Prompt', '');
		} else {
			$startIndex = $customToolNamespaceIndex + '<CustomToolNamespace>'.Length;
			$endIndex = $csProj.IndexOf('</CustomToolNamespace>');
			$namespace = $csProj.Substring($startIndex, $endIndex - $startIndex);
		}
		$className = $_.BaseName;
		$outputCode = (Join-Path $_.Directory.FullName "$className.Designer.cs");
		$outputResource = (Join-Path $_.Directory.FullName "$className.resources");

		. $resgen $_.FullName "/str:cs,$namespace,$className,$outputCode"

		rm $outputResource

		$content = (Get-Content $outputCode) |
						Select-String "GeneratedCodeAttribute" -notmatch |
						Select-String "EditorBrowsableAttribute" -notmatch |
						Select-String "SuppressMessageAttribute" -notmatch |
						Select-String "DebuggerNonUserCodeAttribute" -notmatch |
						Select-String "CompilerGeneratedAttribute" -notmatch | ForEach {
							$value = $_.ToString();
							if ($value.Contains("typeof($className).Assembly);")) {
								return $value.Replace("typeof($className).Assembly", "global::System.Reflection.IntrospectionExtensions.GetTypeInfo(typeof($className)).Assembly").Replace("$namespace.$className", "$namespace.$className");
							} elseif ($value.Trim().Length -eq 0) {
								return "";
							} else {
								return $_;
							}
						}
		while ($true) {
			try {
				$content | Out-File $outputCode -Width 1000
				break
			} catch {
			}
		}
	}
}
