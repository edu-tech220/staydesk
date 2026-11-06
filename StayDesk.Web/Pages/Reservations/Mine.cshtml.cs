using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StayDesk.Web.ApiClient;

namespace StayDesk.Web.Pages.Reservations;

[Authorize]
public class MineModel : PageModel
{
    private readonly StayDeskApiClient _api;

    public MineModel(StayDeskApiClient api)
    {
        _api = api;
    }

    public IReadOnlyList<ReservationDto> Reservations { get; private set; } = Array.Empty<ReservationDto>();

    public string? ErrorMessage { get; private set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ActionErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetMyReservationsAsync(1, 50, ct);
        if (result.IsSuccess)
        {
            Reservations = result.Data!.Items;
        }
        else
        {
            ErrorMessage = result.ErrorMessage;
        }
    }

    public async Task<IActionResult> OnPostCancelAsync(int id, CancellationToken ct)
    {
        var result = await _api.CancelReservationAsync(id, ct);
        if (result.IsSuccess)
        {
            SuccessMessage = "La reserva fue cancelada.";
        }
        else
        {
            ActionErrorMessage = result.ErrorMessage;
        }

        return RedirectToPage();
    }
}
