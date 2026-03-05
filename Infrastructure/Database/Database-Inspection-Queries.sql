-- ========================================
-- HR Database Complete Inspection Queries
-- ========================================

-- Use the HR_Local database
USE HR_Local;
GO

-- ========================================
-- 1. TABLE STRUCTURE OVERVIEW
-- ========================================
SELECT 
    t.name AS TableName,
    p.rows AS RowCounts,
    CAST(ROUND(((SUM(a.total_pages) * 8) / 1024.00), 2) AS NUMERIC(36, 2)) AS TotalSpaceMB
FROM sys.tables t
INNER JOIN sys.indexes i ON t.object_id = i.object_id
INNER JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
WHERE t.is_ms_shipped = 0
AND i.object_id > 255
GROUP BY t.name, p.rows
ORDER BY TotalSpaceMB DESC;
GO

-- ========================================
-- 2. DEPARTMENTS
-- ========================================
PRINT '=== DEPARTMENTS ===';
SELECT * FROM Departments ORDER BY Id;
GO

-- Department summary
SELECT 
    COUNT(*) AS TotalDepartments,
    STRING_AGG(Name, ', ') AS DepartmentList
FROM Departments;
GO

-- ========================================
-- 3. POSITIONS
-- ========================================
PRINT '=== POSITIONS ===';
SELECT 
    p.Id,
    p.Title,
    p.Description,
    p.SalaryMin,
    p.SalaryMax,
    p.IsOpen,
    p.PostedOn,
    d.Name AS DepartmentName
FROM Positions p
LEFT JOIN Departments d ON p.DepartmentId = d.Id
ORDER BY p.PostedOn DESC;
GO

-- Position summary
SELECT 
    COUNT(*) AS TotalPositions,
    COUNT(CASE WHEN IsOpen = 1 THEN 1 END) AS OpenPositions,
    COUNT(CASE WHEN IsOpen = 0 THEN 1 END) AS ClosedPositions,
    AVG(SalaryMin) AS AvgMinSalary,
    AVG(SalaryMax) AS AvgMaxSalary
FROM Positions;
GO

-- ========================================
-- 4. APPLICANTS
-- ========================================
PRINT '=== APPLICANTS ===';
SELECT 
    Id,
    FullName,
    Email,
    Phone,
    CreatedAt
FROM Applicants
ORDER BY CreatedAt DESC;
GO

-- Applicant summary
SELECT 
    COUNT(*) AS TotalApplicants,
    COUNT(CASE WHEN Email LIKE '%@%' THEN 1 END) AS ValidEmails,
    MIN(CreatedAt) AS FirstApplicant,
    MAX(CreatedAt) AS LatestApplicant
FROM Applicants;
GO

-- ========================================
-- 5. APPLICATIONS
-- ========================================
PRINT '=== APPLICATIONS ===';
SELECT 
    a.Id,
    a.Status,
    a.AppliedOn,
    a.ResumePath,
    app.FullName AS ApplicantName,
    app.Email AS ApplicantEmail,
    p.Title AS PositionTitle,
    d.Name AS DepartmentName
FROM Applications a
LEFT JOIN Applicants app ON a.ApplicantId = app.Id
LEFT JOIN Positions p ON a.PositionId = p.Id
LEFT JOIN Departments d ON p.DepartmentId = d.Id
ORDER BY a.AppliedOn DESC;
GO

-- Application summary by status
SELECT 
    Status,
    COUNT(*) AS Count,
    CAST(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM Applications) AS DECIMAL(5,2)) AS Percentage
FROM Applications
GROUP BY Status
ORDER BY Count DESC;
GO

-- Applications by position
SELECT 
    p.Title AS PositionTitle,
    COUNT(a.Id) AS ApplicationCount,
    COUNT(CASE WHEN a.Status = 'Screening' THEN 1 END) AS Screening,
    COUNT(CASE WHEN a.Status = 'Interview' THEN 1 END) AS Interview,
    COUNT(CASE WHEN a.Status = 'Offered' THEN 1 END) AS Offered,
    COUNT(CASE WHEN a.Status = 'Rejected' THEN 1 END) AS Rejected
FROM Positions p
LEFT JOIN Applications a ON p.Id = a.PositionId
GROUP BY p.Id, p.Title
ORDER BY ApplicationCount DESC;
GO

-- ========================================
-- 6. QUESTIONS
-- ========================================
PRINT '=== QUESTIONS ===';
SELECT 
    Id,
    Text,
    Type,
    CreatedAt
FROM Questions
ORDER BY CreatedAt DESC;
GO

-- Question summary by type
SELECT 
    Type,
    COUNT(*) AS Count,
    CAST(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM Questions) AS DECIMAL(5,2)) AS Percentage
FROM Questions
GROUP BY Type
ORDER BY Count DESC;
GO

-- ========================================
-- 7. QUESTION OPTIONS (for choice questions)
-- ========================================
PRINT '=== QUESTION OPTIONS ===';
SELECT 
    qo.Id,
    qo.Text AS OptionText,
    qo.Points,
    q.Text AS QuestionText,
    q.Type AS QuestionType
FROM QuestionOptions qo
LEFT JOIN Questions q ON qo.QuestionId = q.Id
ORDER BY q.Id, qo.Points DESC;
GO

-- ========================================
-- 8. POSITION QUESTIONS (questionnaire assignments)
-- ========================================
PRINT '=== POSITION QUESTIONS ===';
SELECT 
    pq.Id,
    pq.PositionId,
    pq.QuestionId,
    pq.Order,
    p.Title AS PositionTitle,
    q.Text AS QuestionText,
    q.Type AS QuestionType
FROM PositionQuestions pq
LEFT JOIN Positions p ON pq.PositionId = p.Id
LEFT JOIN Questions q ON pq.QuestionId = q.Id
ORDER BY pq.PositionId, pq.Order;
GO

-- Questions per position
SELECT 
    p.Title AS PositionTitle,
    COUNT(pq.Id) AS QuestionCount,
    STRING_AGG(q.Type, ', ') AS QuestionTypes
FROM Positions p
LEFT JOIN PositionQuestions pq ON p.Id = pq.PositionId
LEFT JOIN Questions q ON pq.QuestionId = q.Id
GROUP BY p.Id, p.Title
ORDER BY QuestionCount DESC;
GO

-- ========================================
-- 9. APPLICATION ANSWERS
-- ========================================
PRINT '=== APPLICATION ANSWERS ===';
SELECT 
    aa.Id,
    aa.ApplicationId,
    aa.QuestionId,
    aa.AnswerText,
    a.AppliedOn,
    app.FullName AS ApplicantName,
    p.Title AS PositionTitle,
    q.Text AS QuestionText,
    q.Type AS QuestionType
FROM ApplicationAnswers aa
LEFT JOIN Applications a ON aa.ApplicationId = a.Id
LEFT JOIN Applicants app ON a.ApplicantId = app.Id
LEFT JOIN Positions p ON a.PositionId = p.Id
LEFT JOIN Questions q ON aa.QuestionId = q.Id
ORDER BY a.AppliedOn DESC, q.Text;
GO

-- Answer summary by question type
SELECT 
    q.Type AS QuestionType,
    COUNT(aa.Id) AS TotalAnswers,
    COUNT(CASE WHEN aa.AnswerText IS NOT NULL AND aa.AnswerText != '' THEN 1 END) AS NonEmptyAnswers,
    AVG(LEN(aa.AnswerText)) AS AvgAnswerLength
FROM ApplicationAnswers aa
LEFT JOIN Questions q ON aa.QuestionId = q.Id
GROUP BY q.Type
ORDER BY TotalAnswers DESC;
GO

-- ========================================
-- 10. INTERVIEWS
-- ========================================
PRINT '=== INTERVIEWS ===';
SELECT 
    i.Id,
    i.ApplicationId,
    i.InterviewerId,
    i.ScheduledAt,
    i.Mode,
    i.Notes,
    i.Status,
    app.FullName AS ApplicantName,
    p.Title AS PositionTitle,
    interviewer.FullName AS InterviewerName
FROM Interviews i
LEFT JOIN Applications a ON i.ApplicationId = a.Id
LEFT JOIN Applicants app ON a.ApplicantId = app.Id
LEFT JOIN Positions p ON a.PositionId = p.Id
LEFT JOIN Users interviewer ON i.InterviewerId = interviewer.Id
ORDER BY i.ScheduledAt DESC;
GO

-- Interview summary
SELECT 
    Status,
    COUNT(*) AS Count,
    COUNT(CASE WHEN Mode = 'Remote' THEN 1 END) AS Remote,
    COUNT(CASE WHEN Mode = 'In-Person' THEN 1 END) AS InPerson
FROM Interviews
GROUP BY Status;
GO

-- ========================================
-- 11. ONBOARDING
-- ========================================
PRINT '=== ONBOARDING ===';
SELECT 
    o.Id,
    o.ApplicationId,
    o.StartDate,
    o.Tasks,
    o.Status,
    app.FullName AS ApplicantName,
    p.Title AS PositionTitle
FROM Onboarding o
LEFT JOIN Applications a ON o.ApplicationId = a.Id
LEFT JOIN Applicants app ON a.ApplicantId = app.Id
LEFT JOIN Positions p ON a.PositionId = p.Id
ORDER BY o.StartDate DESC;
GO

-- ========================================
-- 12. USERS
-- ========================================
PRINT '=== USERS ===';
SELECT 
    Id,
    UserName,
    Email,
    Role,
    CreatedAt
FROM Users
ORDER BY Role, UserName;
GO

-- User summary by role
SELECT 
    Role,
    COUNT(*) AS Count,
    STRING_AGG(UserName, ', ') AS UserNames
FROM Users
GROUP BY Role;
GO

-- ========================================
-- 13. COMPLETE APPLICATION WORKFLOW
-- ========================================
PRINT '=== COMPLETE APPLICATION WORKFLOW ===';
SELECT 
    a.Id AS ApplicationID,
    app.FullName AS Applicant,
    p.Title AS Position,
    d.Name AS Department,
    a.Status AS ApplicationStatus,
    a.AppliedOn,
    i.ScheduledAt AS InterviewDate,
    i.Status AS InterviewStatus,
    o.StartDate AS OnboardingStart,
    o.Status AS OnboardingStatus,
    CASE 
        WHEN o.Status = 'Completed' THEN 'Hired'
        WHEN i.Status = 'Completed' AND o.Status IS NULL THEN 'Interview Complete'
        WHEN a.Status = 'Interview' THEN 'In Interview Process'
        WHEN a.Status = 'Screening' THEN 'In Screening'
        ELSE a.Status
    END AS CurrentStage
FROM Applications a
LEFT JOIN Applicants app ON a.ApplicantId = app.Id
LEFT JOIN Positions p ON a.PositionId = p.Id
LEFT JOIN Departments d ON p.DepartmentId = d.Id
LEFT JOIN Interviews i ON a.Id = i.ApplicationId
LEFT JOIN Onboarding o ON a.Id = o.ApplicationId
ORDER BY a.AppliedOn DESC;
GO

-- ========================================
-- 14. DATA QUALITY CHECKS
-- ========================================
PRINT '=== DATA QUALITY CHECKS ===';

-- Check for orphaned records
SELECT 'Applications without Applicants' AS Issue, COUNT(*) AS Count
FROM Applications a
LEFT JOIN Applicants app ON a.ApplicantId = app.Id
WHERE app.Id IS NULL;

SELECT 'Applications without Positions' AS Issue, COUNT(*) AS Count
FROM Applications a
LEFT JOIN Positions p ON a.PositionId = p.Id
WHERE p.Id IS NULL;

SELECT 'Position Questions without Questions' AS Issue, COUNT(*) AS Count
FROM PositionQuestions pq
LEFT JOIN Questions q ON pq.QuestionId = q.Id
WHERE q.Id IS NULL;

SELECT 'Application Answers without Applications' AS Issue, COUNT(*) AS Count
FROM ApplicationAnswers aa
LEFT JOIN Applications a ON aa.ApplicationId = a.Id
WHERE a.Id IS NULL;

-- Check for empty critical fields
SELECT 'Applications with no resume' AS Issue, COUNT(*) AS Count
FROM Applications
WHERE ResumePath IS NULL OR ResumePath = '';

SELECT 'Applicants with invalid email' AS Issue, COUNT(*) AS Count
FROM Applicants
WHERE Email NOT LIKE '%@%' OR Email IS NULL;

-- ========================================
-- 15. RECENT ACTIVITY SUMMARY
-- ========================================
PRINT '=== RECENT ACTIVITY (Last 7 Days) ===';

SELECT 
    'New Applicants' AS ActivityType,
    COUNT(*) AS Count,
    MAX(CreatedAt) AS LatestActivity
FROM Applicants
WHERE CreatedAt >= DATEADD(day, -7, GETDATE())

UNION ALL

SELECT 
    'New Applications' AS ActivityType,
    COUNT(*) AS Count,
    MAX(AppliedOn) AS LatestActivity
FROM Applications
WHERE AppliedOn >= DATEADD(day, -7, GETDATE())

UNION ALL

SELECT 
    'Interviews Scheduled' AS ActivityType,
    COUNT(*) AS Count,
    MAX(ScheduledAt) AS LatestActivity
FROM Interviews
WHERE ScheduledAt >= DATEADD(day, -7, GETDATE())

UNION ALL

SELECT 
    'Onboarding Started' AS ActivityType,
    COUNT(*) AS Count,
    MAX(StartDate) AS LatestActivity
FROM Onboarding
WHERE StartDate >= DATEADD(day, -7, GETDATE());

GO

PRINT '=== DATABASE INSPECTION COMPLETE ===';
