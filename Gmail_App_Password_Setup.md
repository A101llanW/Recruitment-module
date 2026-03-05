# Gmail App Password Setup Guide

## Step 1: Enable 2-Factor Authentication (if not already enabled)
1. Go to: https://myaccount.google.com/security
2. Sign in with ntlafrica@gmail.com
3. Find "Signing in to Google" section
4. Click on "2-Step Verification" 
5. Click "Get Started" and follow the setup process
6. You'll need to add a phone number or backup codes

## Step 2: Generate App Password
1. Go to: https://myaccount.google.com/apppasswords
2. Sign in again if prompted
3. Under "Select app", choose:
   - **If "Mail" is not listed**: Choose "Other (Custom name)"
4. In the custom name field, type: "HR System"
5. Click "Generate"
6. Copy the 16-character password (it will look like: xxxx xxxx xxxx xxxx)

## Step 3: Update Configuration
1. Open: C:\Users\allan\Documents\Examples\Recruitment\HR.Web\secrets.config
2. Replace YOUR_16_CHARACTER_GMAIL_APP_PASSWORD with the generated password
3. Save the file
4. Restart the web application

## Alternative: If "Mail" App Not Available
Sometimes Google doesn't show "Mail" as an option. In that case:
1. Select "Other (Custom name)"
2. Name it "Email Client" or "HR System"
3. This will work the same way

## Important Notes:
- App passwords only work if 2FA is enabled on the account
- Each app password is 16 characters with spaces (copy without spaces)
- You can revoke app passwords later if needed
- The app password is different from your regular Gmail password
