# Run this to create the missing PasswordResets table
try {
    $connectionString = "Data Source=.\SQLEXPRESS;Initial Catalog=HR_Local;Integrated Security=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    $sql = @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PasswordResets')
BEGIN
    CREATE TABLE PasswordResets (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        Token NVARCHAR(255) NOT NULL,
        ExpiryDate DATETIME NOT NULL,
        IsUsed BIT NOT NULL DEFAULT 0,
        CreatedDate DATETIME NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_PasswordResets_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
    PRINT 'Created PasswordResets table';
END
"@
    
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $result = $command.ExecuteNonQuery()
    
    $connection.Close()
    Write-Host "Database table creation script executed successfully!"
    
} catch {
    Write-Host "Error: $_"
}
