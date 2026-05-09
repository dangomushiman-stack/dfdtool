Remove-Item -Path "HKCU:\Software\Classes\.dfdj" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\Classes\DfdToolWpf.dfdj" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host ".dfdj の関連付けを削除しました。"
