bibtex ".\comp61011" > $output;
if ($LASTEXITCODE -eq 1) {
	Write-Output $output;
} else {
	texify --clean --pdf --tex-option=-max-print-line=10000 --quiet ".\comp61011.tex" > $output;
	if ($LASTEXITCODE -eq 1) {
		Write-Output $output;
		Select-String -Path .\comp61011.log -Pattern ":[0-9]+:.*" -AllMatches | % { $_.Matches } | % { $_.Value };
	} else {
		Start-Process ".\comp61011.pdf";
	}
}