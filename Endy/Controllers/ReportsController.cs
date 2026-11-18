using Endy.Data;
using Endy.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Endy.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public sealed class ReportsController(ApplicationDbContext db) : AppController
{
    public async Task<IActionResult> Index(RequestStatus? status, int? departmentId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        SetNavigation();
        var requests = await Filter(status, departmentId, from, to).Include(x => x.Department).Include(x => x.Visit).OrderByDescending(x => x.RequestDate).ToListAsync(cancellationToken);
        ViewBag.Requests = requests; ViewBag.Status = status; ViewBag.DepartmentId = departmentId;
        ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.Departments = await db.Departments.Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync(cancellationToken);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(RequestStatus? status, int? departmentId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var rows = await Filter(status, departmentId, from, to).Include(x => x.Department).Include(x => x.Visit).OrderByDescending(x => x.RequestDate).ToListAsync(cancellationToken);
        static string Csv(string? value)
        {
            var safe = value ?? string.Empty;
            var firstNonWhitespace = safe.FirstOrDefault(character => !char.IsWhiteSpace(character));
            if ("=+-@".Contains(firstNonWhitespace) || safe.StartsWith('\t') || safe.StartsWith('\r') || safe.StartsWith('\n')) safe = "'" + safe;
            return "\"" + safe.Replace("\"", "\"\"") + "\"";
        }
        var csv = new StringBuilder("Request ID,Full Name,Vehicle Type,Registration Number,Company,Visit Duration,Department,Status,Request Date,Check-in,Check-out\r\n");
        foreach (var row in rows)
            csv.AppendLine(string.Join(',', new[] { Csv(row.Id.ToString()), Csv(row.FullName), Csv(row.VehicleType), Csv(row.RegistrationNumber), Csv(row.CompanyName), Csv(row.VisitDurationHours.ToString()), Csv(row.Department.Name), Csv(row.Status.ToString()), Csv(DateTimeDisplay.ToLocal(row.RequestDate).ToString("yyyy-MM-dd HH:mm")), Csv(DateTimeDisplay.ToLocal(row.Visit?.CheckInTime)?.ToString("yyyy-MM-dd HH:mm")), Csv(DateTimeDisplay.ToLocal(row.Visit?.CheckOutTime)?.ToString("yyyy-MM-dd HH:mm")) }));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv; charset=utf-8", "access-requests.csv");
    }

    private IQueryable<AccessRequest> Filter(RequestStatus? status, int? departmentId, DateTime? from, DateTime? to)
    {
        var query = db.AccessRequests.AsQueryable();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (departmentId.HasValue) query = query.Where(x => x.DepartmentId == departmentId.Value);
        if (from.HasValue) { var startUtc = DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Local).ToUniversalTime(); query = query.Where(x => x.RequestDate >= startUtc); }
        if (to.HasValue) { var endUtc = DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime(); query = query.Where(x => x.RequestDate < endUtc); }
        return query;
    }
}