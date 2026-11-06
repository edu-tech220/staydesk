using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StayDesk.Web.ApiClient;

namespace StayDesk.Web.Pages.Admin;

[Authorize(Roles = "FrontDesk")]
public class ReservationsModel : PageModel
{
    private readonly StayDeskApiClient _api;

    public ReservationsModel(StayDeskApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public IReadOnlyList<ReservationDto> Reservations { get; private set; } = Array.Empty<ReservationDto>();

    public string? ErrorMessage { get; private set; }

    [TempData]
    public string? ActionErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetAllReservationsAsync(Status, 1, 100, ct);
        if (result.IsSuccess)
        {
            Reservations = result.Data!.Items;
        }
        else
        {
            ErrorMessage = result.ErrorMessage;
        }
    }

    public Task<IActionResult> OnPostConfirmAsync(int id, CancellationToken ct) => RunActionAsync(id, ct, _api.ConfirmReservationAsync);

    public Task<IActionResult> OnPostCheckInAsync(int id, CancellationToken ct) => RunActionAsync(id, ct, _api.CheckInReservationAsync);

    public Task<IActionResult> OnPostCheckOutAsync(int id, CancellationToken ct) => RunActionAsync(id, ct, _api.CheckOutReservationAsync);

    public Task<IActionResult> OnPostCancelAsync(int id, CancellationToken ct) => RunActionAsync(id, ct, (resId, token) => _api.CancelReservationAsync(resId, token));

    public Task<IActionResult> OnPostNoShowAsync(int id, CancellationToken ct) => RunActionAsync(id, ct, _api.MarkNoShowAsync);

    private async Task<IActionResult> RunActionAsync(int id, CancellationToken ct, Func<int, CancellationToken, Task<ApiResult<ReservationDto>>> action)
    {
        var result = await action(id, ct);
        if (!result.IsSuccess)
        {
            ActionErrorMessage = result.ErrorMessage;
        }

        return RedirectToPage(new { status = Status });
    }
}
