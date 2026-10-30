namespace monprojet.NET.Models.Admin;

public class AdminUserViewModel
{
    public string UserId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string CurrentRole { get; set; } = string.Empty;

    public bool CanChangeRole { get; set; }

    public IReadOnlyList<string> AllowedNextRoles { get; set; } = Array.Empty<string>();
}
