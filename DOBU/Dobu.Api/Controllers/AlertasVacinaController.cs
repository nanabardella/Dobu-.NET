using System.Security.Claims;
using Dobu.Api.Services;
using Dobu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dobu.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/alertas-vacina")]
public sealed class AlertasVacinaController(VaccineAlerts alerts, DobuDbContext context,
    IWebHostEnvironment environment) : ControllerBase
{
    private Guid Owner => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get()
        => Ok(await context.Lembretes.AsNoTracking()
            .Where(x => x.Pet.ResponsavelId == Owner && x.Descricao.StartsWith("Vacina "))
            .Select(x => new { x.Id, x.Descricao, x.DataLembrete, x.Status, x.PetId }).ToListAsync());

    // Controlled-clock hook for reproducible QA; never exposed in Production.
    [HttpPost("gerar")]
    public async Task<IActionResult> Generate([FromQuery] DateTime referencia)
    {
        if (!environment.IsDevelopment()) return NotFound();
        if (referencia == default) return BadRequest("Informe referencia para o relogio controlado de QA.");
        return Ok(new { criados = await alerts.GenerateAsync(referencia, Owner) });
    }
}
