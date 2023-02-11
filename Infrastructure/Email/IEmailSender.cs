namespace Infrastructure.Email
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string userEmail, string emailSubject, string msg);
    }
}
