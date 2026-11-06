using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StayDesk.Web.ApiClient;

namespace StayDesk.Web.Pages;

public class IndexModel : PageModel
{
    private readonly StayDeskApiClient _api;

    public IndexModel(StayDeskApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true)]
    public DateOnly? CheckIn { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? CheckOut { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Guests { get; set; } = 2;

    public IReadOnlyList<RoomTypeAvailabilityDto> Results { get; private set; } = Array.Empty<RoomTypeAvailabilityDto>();

    public string? ErrorMessage { get; private set; }

    public bool Searched { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (CheckIn is null || CheckOut is null)
        {
            CheckIn ??= DateOnly.FromDateTime(DateTime.Today.AddDays(7));
            CheckOut ??= DateOnly.FromDateTime(DateTime.Today.AddDays(9));
            return;
        }

        Searched = true;

        var result = await _api.SearchAvailabilityAsync(CheckIn.Value, CheckOut.Value, Guests, ct);
        if (result.IsSuccess)
        {
            Results = result.Data!.Items;
        }
        else
        {
            ErrorMessage = result.ErrorMessage;
        }
    }
}
