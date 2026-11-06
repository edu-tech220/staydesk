using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StayDesk.Web.ApiClient;

namespace StayDesk.Web.Pages.Reservations;

[Authorize]
public class NewModel : PageModel
{
    private readonly StayDeskApiClient _api;

    public NewModel(StayDeskApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true)]
    public int RoomTypeId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly CheckIn { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly CheckOut { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Guests { get; set; } = 1;

    public RoomTypeAvailabilityDto? Room { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.SearchAvailabilityAsync(CheckIn, CheckOut, Guests, ct);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return Page();
        }

        Room = result.Data!.Items.FirstOrDefault(r => r.RoomTypeId == RoomTypeId);
        if (Room is null)
        {
            ErrorMessage = "La habitacion seleccionada ya no esta disponible para esas fechas.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var result = await _api.CreateReservationAsync(RoomTypeId, CheckIn, CheckOut, Guests, ct);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return await OnGetAsync(ct);
        }

        TempData["SuccessMessage"] = $"Reserva creada con codigo de confirmacion {result.Data!.ConfirmationCode}.";
        return RedirectToPage("/Reservations/Mine");
    }
}
