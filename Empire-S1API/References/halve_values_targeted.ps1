$p = "Empire-S1API/References/empire.json"
$j = Get-Content -Raw $p | ConvertFrom-Json

foreach ($dealer in $j.dealers) {
    if ($null -eq $dealer) { continue }

    if ($dealer.PSObject.Properties.Match('deals')) {
        $deals = $dealer.deals
        if ($deals -is [System.Collections.IList]) {
            for ($i = 0; $i -lt $deals.Count; $i++) {
                $arr = $deals[$i]
                if ($arr -is [System.Collections.IList] -and $arr.Count -ge 2) {
                    try {
                        $num = [double]$arr[1]
                        $new = [math]::Round($num / 2, 6)
                        $deals[$i][1] = $new
                    } catch { }
                }
            }
        }
    }

    if ($dealer.PSObject.Properties.Match('drugs')) {
        $drugs = $dealer.drugs
        if ($drugs -is [System.Collections.IList]) {
            foreach ($drug in $drugs) {
                if ($drug -ne $null -and $drug.PSObject.Properties.Match('base_dollar')) {
                    try {
                        $num = [double]$drug.base_dollar
                        $drug.base_dollar = [math]::Round($num / 2, 6)
                    } catch { }
                }
            }
        }
    }
}

# Write back
$j | ConvertTo-Json -Depth 100 | Set-Content -Path $p -Encoding UTF8
Write-Output "done"