namespace RaceDay.Api.Models;

/// <summary>
/// A registered user, either an Organiser or a Participant. Matches the User entity in the Part 1 ERD.
/// </summary>
public class User
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>PBKDF2 password hash. The plain-text password is never stored.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public int RoleId { get; set; }
    public Role? Role { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Events this user organises, when their role is Organiser.</summary>
    public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();

    /// <summary>Enrolments this user has entered, when their role is Participant.</summary>
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
