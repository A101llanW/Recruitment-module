try {
    $connectionString = "Data Source=.\SQLEXPRESS;Initial Catalog=HR_Local;Integrated Security=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    $sql = @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Applicants' AND COLUMN_NAME = 'Phone')
BEGIN
    ALTER TABLE Applicants ADD Phone NVARCHAR(20) NULL;
    PRINT 'Added Phone column to Applicants table';
END
ELSE
BEGIN
    PRINT 'Phone column already exists in Applicants';
END
"@
    
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.ExecuteNonQuery()
    
    $connection.Close()
    Write-Host "Applicants Phone column check/creation script executed successfully!"
    
}
catch {
    Write-Host "Error: $_"
}
