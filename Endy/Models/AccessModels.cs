using System.ComponentModel.DataAnnotations;

namespace Endy.Models;

public enum RequestStatus { PendingReview, Approved, Rejected, ModificationRequired }
public enum VisitStatus { NotCheckedIn, Active, ExpiringSoon, Expired, CheckedOut }

public static class DateTimeDisplay
{
    public static DateTime ToLocal(DateTime utcValue) => DateTime.SpecifyKind(utcValue, DateTimeKind.Utc).ToLocalTime();
    public static DateTime? ToLocal(DateTime? utcValue) => utcValue.HasValue ? ToLocal(utcValue.Value) : null;
}

public sealed class AccessRequest
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string FullName { get; set; } = string.Empty;
    [Required, MaxLength(60)] public string VehicleType { get; set; } = string.Empty;
    [Required, MaxLength(40)] public string RegistrationNumber { get; set; } = string.Empty;
    [Required, MaxLength(120)] public string CompanyName { get; set; } = string.Empty;
    [Range(1, 72)] public int VisitDurationHours { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    [Required, MaxLength(100)] public string SecurityDepartment { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    public RequestStatus Status { get; set; } = RequestStatus.PendingReview;
    [Required] public string CreatedByUserId { get; set; } = string.Empty;
    [MaxLength(256)] public string CreatedBy { get; set; } = string.Empty;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    [MaxLength(2000)] public string? DecisionComment { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public List<RequestEvent> History { get; set; } = [];
    public VisitTracking? Visit { get; set; }

    public DateTime? CheckInTime => Visit is null ? null : DateTime.SpecifyKind(Visit.CheckInTime, DateTimeKind.Utc);
    public DateTime? CheckOutTime => Visit?.CheckOutTime is DateTime checkOut ? DateTime.SpecifyKind(checkOut, DateTimeKind.Utc) : null;
    public DateTime? ExpectedCheckOutTime => CheckInTime?.AddHours(VisitDurationHours);
    public VisitStatus VisitStatus
    {
        get
        {
            if (Visit?.CheckOutTime.HasValue == true) return VisitStatus.CheckedOut;
            if (Visit?.CheckInTime is not DateTime checkIn) return VisitStatus.NotCheckedIn;
            var remaining = checkIn.AddHours(VisitDurationHours) - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) return VisitStatus.Expired;
            return remaining.TotalMinutes <= 60 ? VisitStatus.ExpiringSoon : VisitStatus.Active;
        }
    }
    public TimeSpan? RemainingTime => ExpectedCheckOutTime is DateTime expected && Visit?.CheckOutTime is null ? expected - DateTime.UtcNow : null;
}

public sealed class RequestEvent
{
    public int Id { get; set; }
    public int AccessRequestId { get; set; }
    public AccessRequest AccessRequest { get; set; } = null!;
    [Required, MaxLength(60)] public string Action { get; set; } = string.Empty;
    [Required] public string UserId { get; set; } = string.Empty;
    [Required, MaxLength(256)] public string User { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    [MaxLength(2000)] public string? Comment { get; set; }
    public string? SnapshotJson { get; set; }
}

public sealed class VisitTracking
{
    public int Id { get; set; }
    public int AccessRequestId { get; set; }
    public AccessRequest AccessRequest { get; set; } = null!;
    public DateTime CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    [Required] public string CheckedInByUserId { get; set; } = string.Empty;
    public string? CheckedOutByUserId { get; set; }
}

public sealed class AppNotification
{
    public int Id { get; set; }
    [Required] public string RecipientUserId { get; set; } = string.Empty;
    public ApplicationUser RecipientUser { get; set; } = null!;
    [Required, MaxLength(120)] public string Title { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string Message { get; set; } = string.Empty;
    public int? AccessRequestId { get; set; }
    public AccessRequest? AccessRequest { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}

public sealed class AccessRequestInput
{
    [Required, StringLength(120), Display(Name = "Full Name")] public string FullName { get; set; } = string.Empty;
    [Required, StringLength(60), Display(Name = "Vehicle Type")] public string VehicleType { get; set; } = string.Empty;
    [Required, StringLength(40), Display(Name = "Registration Number")] public string RegistrationNumber { get; set; } = string.Empty;
    [Required, StringLength(120), Display(Name = "Company Name")] public string CompanyName { get; set; } = string.Empty;
    [Required, Range(1, 72), Display(Name = "Visit Duration (hours)")] public int VisitDurationHours { get; set; } = 1;
    [Required] public string SecurityDepartment { get; set; } = string.Empty;
}

public sealed class DashboardViewModel
{
    public int TotalRequests { get; set; }
    public int PendingRequests { get; set; }
    public int ApprovedRequests { get; set; }
    public int RejectedRequests { get; set; }
    public int ModificationRequired { get; set; }
    public int ActiveVisitors { get; set; }
    public int ExpiredVisits { get; set; }
    public int TotalDepartments { get; set; }
    public List<AccessRequest> RecentRequests { get; set; } = [];
}