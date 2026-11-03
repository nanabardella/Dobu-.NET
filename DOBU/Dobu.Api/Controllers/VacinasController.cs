using Dobu.Domain.Entities;
using Dobu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Dobu.Api.Controllers;

[Produces("application/json")]
[ApiController]
[Route("api/vacinas")]
[Authorize]
public class VacinasController(DobuDbContext context) : ControllerBase
{
    private Guid UsuarioId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private IQueryable<Vacina> Carteira => context.Vacinas.Where(x => x.Pet.ResponsavelId == UsuarioId);
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Vacina>>> GetAll()
    {
        return Ok(await Carteira.AsNoTracking().ToListAsync());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Vacina>> GetById(Guid id)
    {
        var vacina = await Carteira.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return vacina is null ? NotFound() : Ok(vacina);
    }

    [HttpGet("pet/{petId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Vacina>>> GetByPet(Guid petId)
    {
        if (!await context.Pets.AnyAsync(x => x.Id == petId && x.ResponsavelId == UsuarioId))
            return NotFound();
        return Ok(await Carteira.AsNoTracking().Where(x => x.PetId == petId).ToListAsync());
    }

    [HttpGet("proxima-dose")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Vacina>>> GetByProximaDose([FromQuery] DateTime ate)
    {
        return Ok(await Carteira.AsNoTracking().Where(x => x.DataProximaDose <= ate).ToListAsync());
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Vacina>> Create(VacinaRequest request)
    {
        if (await context.Pets.CountAsync(x => x.Id == request.PetId) == 0)
            return BadRequest("Pet informado nao existe.");

        if (!await context.Pets.AnyAsync(x => x.Id == request.PetId && x.ResponsavelId == UsuarioId))
            return Forbid();

        try
        {
            var vacina = new Vacina(request.Nome, request.DataAplicacao, request.DataProximaDose, request.PetId);
            context.Vacinas.Add(vacina);
            await context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = vacina.Id }, vacina);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Vacina>> Update(Guid id, VacinaRequest request)
    {
        var vacina = await Carteira.FirstOrDefaultAsync(x => x.Id == id);
        if (vacina is null)
            return NotFound();

        if (await context.Pets.CountAsync(x => x.Id == request.PetId) == 0)
            return BadRequest("Pet informado nao existe.");

        if (!await context.Pets.AnyAsync(x => x.Id == request.PetId && x.ResponsavelId == UsuarioId))
            return Forbid();

        try
        {
            vacina.Atualizar(request.Nome, request.DataAplicacao, request.DataProximaDose, request.PetId);
            await context.SaveChangesAsync();
            return Ok(vacina);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var vacina = await Carteira.FirstOrDefaultAsync(x => x.Id == id);
        if (vacina is null)
            return NotFound();

        context.Vacinas.Remove(vacina);
        await context.SaveChangesAsync();
        return NoContent();
    }
}

public record VacinaRequest(string Nome, DateTime DataAplicacao, DateTime? DataProximaDose, Guid PetId);
