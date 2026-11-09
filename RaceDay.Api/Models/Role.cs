namespace RaceDay.Api.Models;

/// <summary>
/// A user role: "Organiser" or "Participant". Matches the Role entity in the Part 1 ERD.
/// </summary>
public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
}
