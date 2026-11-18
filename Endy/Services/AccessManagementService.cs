using System.Text.Json;
using Endy.Data;
using Endy.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Endy.Services;

public sealed class AccessManagementService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
{
    public IQueryable<AccessRequest> RequestQuery() => db.AccessRequests.Include(x => x.Department).Include(x => x.Visit).Include(x => x.History).Include(x => x.CreatedByUser);

    public Task<AccessRequest?> FindAsync(int id, CancellationToken cancellationToken = default) =>
        RequestQuery().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<AccessRequest> CreateAsync(AccessRequestInput input, ApplicationUser user, CancellationToken cancellationToken = default)
    {
        if (user.DepartmentId is not int departmentId) throw new InvalidOperationException("Your account must be assigned to a department before submitting requests.");
        var department = await db.Departments.SingleOrDefaultAsync(x => x.Id == departmentId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Your assigned department is unavailable.");
        var request = new AccessRequest
        {
            FullName = input.FullName.Trim(), VehicleType = input.VehicleType.Trim(), RegistrationNumber = input.RegistrationNumber.Trim(),
            CompanyName = input.CompanyName.Trim(), VisitDurationHours = input.VisitDurationHours, DepartmentId = department.Id,
            Department = department, SecurityDepartment = input.SecurityDepartment.Trim(), CreatedByUserId = user.Id,
            CreatedBy = user.FullName, RequestDate = DateTime.UtcNow, Status = RequestStatus.PendingReview
        };
        db.AccessRequests.Add(request);
        AddHistory(request, "Request Submitted", user, null);
        await NotifyRoleAsync(AppRoles.Admin, "New customer created", $"{request.FullName} was added by {request.CreatedBy} and is ready for review.", cancellationToken, request);
        await db.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<bool> DecideAsync(int id, RequestStatus status, string? comment, ApplicationUser actor, CancellationToken cancellationToken = default)
    {
        if (status is not (RequestStatus.Approved or RequestStatus.Rejected or RequestStatus.ModificationRequired)) return false;
        var request = await RequestQuery().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null || request.Status != RequestStatus.PendingReview || request.CreatedByUserId == actor.Id) return false;
        if (status != RequestStatus.Approved && string.IsNullOrWhiteSpace(comment)) return false;
        request.Status = status;
        request.DecisionComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        request.ApprovalDate = status == RequestStatus.Approved ? DateTime.UtcNow : null;
        var action = status switch { RequestStatus.Approved => "Approved", RequestStatus.Rejected => "Rejected", _ => "Modification Required" };
        AddHistory(request, action, actor, request.DecisionComment);
        var message = status switch
        {
            RequestStatus.Approved => "Your access request has been approved.",
            RequestStatus.Rejected => $"Your request was rejected: {request.DecisionComment}",
            _ => $"Please update your request: {request.DecisionComment}"
        };
        db.Notifications.Add(new AppNotification { RecipientUserId = request.CreatedByUserId, Title = action, Message = message });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ResubmitAsync(int id, AccessRequestInput input, ApplicationUser actor, CancellationToken cancellationToken = default)
    {
        var request = await RequestQuery().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null || request.CreatedByUserId != actor.Id || request.Status != RequestStatus.ModificationRequired) return false;
        AddHistory(request, "Previous Version", actor, "Submitted values before correction.");
        request.FullName = input.FullName.Trim(); request.VehicleType = input.VehicleType.Trim(); request.RegistrationNumber = input.RegistrationNumber.Trim();
        request.CompanyName = input.CompanyName.Trim(); request.VisitDurationHours = input.VisitDurationHours; request.SecurityDepartment = input.SecurityDepartment.Trim();
        request.Status = RequestStatus.PendingReview; request.DecisionComment = null; request.ApprovalDate = null;
        AddHistory(request, "Request Resubmitted", actor, null);
        await NotifyRoleAsync(AppRoles.Admin, "Request resubmitted", $"{request.FullName}'s corrected request needs review.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CheckInAsync(int id, ApplicationUser actor, CancellationToken cancellationToken = default)
    {
        var request = await RequestQuery().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null || request.Status != RequestStatus.Approved || request.Visit is not null) return false;
        request.Visit = new VisitTracking { AccessRequestId = id, CheckInTime = DateTime.UtcNow, CheckedInByUserId = actor.Id };
        AddHistory(request, "Customer Checked In", actor, null);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CheckOutAsync(int id, ApplicationUser actor, CancellationToken cancellationToken = default)
    {
        var request = await RequestQuery().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request?.Status != RequestStatus.Approved || request.Visit is null || request.Visit.CheckOutTime.HasValue) return false;
        request.Visit.CheckOutTime = DateTime.UtcNow; request.Visit.CheckedOutByUserId = actor.Id;
        AddHistory(request, "Customer Checked Out", actor, null);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task NotifyRoleAsync(string role, string title, string message, CancellationToken cancellationToken = default, AccessRequest? accessRequest = null)
    {
        var recipients = await userManager.GetUsersInRoleAsync(role);
        foreach (var recipient in recipients.Where(x => x.IsActive))
            db.Notifications.Add(new AppNotification { RecipientUserId = recipient.Id, Title = title, Message = message, AccessRequest = accessRequest });
    }

    public static void AddHistory(AccessRequest request, string action, ApplicationUser actor, string? comment)
    {
        request.History.Add(new RequestEvent
        {
            Action = action, UserId = actor.Id, User = actor.FullName.Length == 0 ? actor.UserName ?? actor.Id : actor.FullName,
            Comment = comment, OccurredAt = DateTime.UtcNow,
            SnapshotJson = JsonSerializer.Serialize(new { request.FullName, request.VehicleType, request.RegistrationNumber, request.CompanyName, request.VisitDurationHours, DepartmentId = request.DepartmentId, request.SecurityDepartment, Status = request.Status.ToString() })
        });
    }
}
