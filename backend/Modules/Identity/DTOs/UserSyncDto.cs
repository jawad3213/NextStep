namespace backend.Modules.Identity.DTOs
{
    public class UserSyncDto
    {
        public string KeycloakId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}
