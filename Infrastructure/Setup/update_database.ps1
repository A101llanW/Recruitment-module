# Add password change columns to existing database
try {
    $connectionString = "Data Source=.\SQLEXPRESS;Initial Catalog=HR_Local;Integrated Security=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    # Add RequirePasswordChange column
    $command = $connection.CreateCommand()
    $command.CommandText = "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'RequirePasswordChange') ALTER TABLE Users ADD RequirePasswordChange BIT NOT NULL DEFAULT 0"
    $command.ExecuteNonQuery()
    Write-Host "Added RequirePasswordChange column"
    
    # Add LastPasswordChange column
    $command.CommandText = "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'LastPasswordChange') ALTER TABLE Users ADD LastPasswordChange DATETIME NULL"
    $command.ExecuteNonQuery()
    Write-Host "Added LastPasswordChange column"
    
    # Add PasswordChangeExpiry column
    $command.CommandText = "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'PasswordChangeExpiry') ALTER TABLE Users ADD PasswordChangeExpiry DATETIME NULL"
    $command.ExecuteNonQuery()
    Write-Host "Added PasswordChangeExpiry column"
    
    # Update existing users with weak passwords
    $command.CommandText = "UPDATE Users SET RequirePasswordChange = 1, PasswordChangeExpiry = DATEADD(day, 7, GETDATE()) WHERE RequirePasswordChange = 0 AND (PasswordHash LIKE '%10000%' OR LEN(PasswordHash) < 50 OR LastPasswordChange IS NULL)"
    $command.ExecuteNonQuery()
    Write-Host "Updated existing users with weak passwords"
    
    $connection.Close()
    Write-Host "Database migration completed successfully!"
    
} catch {
    Write-Host "Error: $_"
}
