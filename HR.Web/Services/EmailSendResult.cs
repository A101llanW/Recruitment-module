namespace HR.Web.Services
{
    public sealed class EmailSendResult
    {
        public bool Attempted { get; private set; }
        public bool Success { get; private set; }
        public string ErrorMessage { get; private set; }

        public static EmailSendResult Skipped()
        {
            return new EmailSendResult
            {
                Attempted = false,
                Success = true
            };
        }

        public static EmailSendResult Succeeded()
        {
            return new EmailSendResult
            {
                Attempted = true,
                Success = true
            };
        }

        public static EmailSendResult Failed(string message)
        {
            return new EmailSendResult
            {
                Attempted = true,
                Success = false,
                ErrorMessage = message ?? "Email could not be sent."
            };
        }
    }
}
