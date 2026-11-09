using RaceDay.Web.Models;

namespace RaceDay.Web.ViewModels;

public class EnrolmentRow
{
    public EnrolmentItem Enrolment { get; set; } = null!;
    public DateTime? EventDate { get; set; }
}

public class MyEnrolmentsViewModel
{
    public List<EnrolmentRow> Active { get; set; } = new();
    public List<EnrolmentRow> Past { get; set; } = new();
}
