$dir = "C:\Users\jonam\.gemini\antigravity\scratch\Tarkov-Item-Helper-main\TarkovHelper"
$files = Get-ChildItem -Path $dir -Filter "*.cs" -Recurse

foreach ($file in $files) {
    if ($file.Name -eq "UserDataDbService.cs") { continue } # Already fixed
    
    $content = Get-Content $file.FullName -Raw
    $original = $content
    
    # 1. Fix MessageBox.Show signature issues
    # Matches: MessageBox.Show(message, MessageBoxButton.OK, MessageBoxImage.Information) 
    # Inserts "알림" as the missing second argument (caption)
    $content = [Regex]::Replace($content, 'MessageBox\.Show\(\s*((".*?")|([^(),]*?))\s*,\s*(MessageBoxButton\.[a-zA-Z]+)\s*,\s*(MessageBoxImage\.[a-zA-Z]+)\s*\)', 'MessageBox.Show($1, "알림", $4, $5)')

    # 2. Simplify AppLanguage switches (CurrentLanguage switch { ... })
    # This is broad, so we use a more specific pattern for language switches
    # lang switch { AppLanguage.KO => "A", _ => "B" } -> "A"
    $content = [Regex]::Replace($content, '(?s)(CurrentLanguage|lang|_loc\.CurrentLanguage)\s*switch\s*\{.*?AppLanguage\.KO\s*=>\s*(.*?)\s*,.*?\}', '$2')

    # 3. Simplify Bilingual Strings like "Reset / 초기화" -> "초기화"
    # Only if it's in a string literal
    $content = [Regex]::Replace($content, '"([^"/]+)\s*/\s*([^"/]+)"', '"$2"')

    # 4. Remove remaining JA/EN refs from static switch expressions
    # static string GetName(AppLanguage lang) => lang switch { AppLanguage.KO => "A", _ => "B" }
    $content = [Regex]::Replace($content, '(?s)\b(AppLanguage\s+[\w\d]+)\s*=>\s*\1\s*switch\s*\{.*?AppLanguage\.KO\s*=>\s*(.*?)\s*,.*?\}', '$1 => $2')

    if ($content -ne $original) {
        Set-Content $file.FullName $content -Encoding UTF8
        Write-Host "Refactored: $($file.FullName)"
    }
}
