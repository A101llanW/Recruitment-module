try {
    $connectionString = "Data Source=.\SQLEXPRESS;Initial Catalog=HR_Local;Integrated Security=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    $sql = @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'Phone')
BEGIN
    ALTER TABLE Users ADD Phone NVARCHAR(20) NULL;
    PRINT 'Added Phone column to Users table';
END
ELSE
BEGIN
    PRINT 'Phone column already exists';
END
"@
    
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.ExecuteNonQuery()
    
    $connection.Close()
    Write-Host "Phone column check/creation script executed successfully!"
    
}
catch {
    Write-Host "Error: $_"
}
