function Get-PBKDF2Hash($password) {
    $salt = [byte[]]::new(16)
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($salt)
    $iterations = 100000
    $keySize = 32
    $deriveBytes = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($password, $salt, $iterations)
    $key = $deriveBytes.GetBytes($keySize)
    $saltBase64 = [Convert]::ToBase64String($salt)
    $keyBase64 = [Convert]::ToBase64String($key)
    return "$iterations.$saltBase64.$keyBase64"
}

$pw1 = "Admin@123"
$pw2 = ";;XobH9tFh{sYm}g"

$hash1 = Get-PBKDF2Hash $pw1
$hash2 = Get-PBKDF2Hash $pw2

Write-Host "Admin@123 Hash: $hash1"
Write-Host ";;XobH9tFh{sYm}g Hash: $hash2"
