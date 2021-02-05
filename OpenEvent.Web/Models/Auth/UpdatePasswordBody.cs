namespace OpenEvent.Web.Models.Auth
{
    /// <summary>
    /// Request body for updating user's password.
    /// </summary>
    public class UpdatePasswordBody
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}