using Dobu.Domain.Entities;
using Dobu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dobu.Api.Controllers;

[ApiController]
[Route("api/consultas")]
public class ConsultasController(DobuDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Consulta>>> GetAll()
    {
        return Ok(await context.Consultas.AsNoTracking()
            .Include(x => x.Pet)
            .Include(x => x.Veterinario)
            .ToListAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Consulta>> GetById(Guid id)
    {
        var consulta = await context.Consultas.AsNoTracking()
            .Include(x => x.Pet)
            .Include(x => x.Veterinario)
            .FirstOrDefaultAsync(x => x.Id == id);

        return consulta is null ? NotFound() : Ok(consulta);
    }

    [HttpGet("pet/{petId:guid}")]
    public async Task<ActionResult<IEnumerable<Consulta>>> GetByPet(Guid petId)
    {
        return Ok(await context.Consultas.AsNoTracking().Where(x => x.PetId == petId).ToListAsync());
    }

    [HttpGet("veterinario/{veterinarioId:guid}")]
    public async Task<ActionResult<IEnumerable<Consulta>>> GetByVeterinario(Guid veterinarioId)
    {
        return Ok(await context.Consultas.AsNoTracking().Where(x => x.VeterinarioId == veterinarioId).ToListAsync());
    }

    [HttpGet("periodo")]
    public async Task<ActionResult<IEnumerable<Consulta>>> GetByPeriodo([FromQuery] DateTime inicio, [FromQuery] DateTime fim)
    {
        if (inicio > fim)
            return BadRequest("A data inicial deve ser menor ou igual a data final.");

        return Ok(await context.Consultas.AsNoTracking()
            .Where(x => x.DataConsulta >= inicio && x.DataConsulta <= fim)
            .ToListAsync());
    }

    [HttpPost]
    public async Task<ActionResult<Consulta>> Create(ConsultaRequest request)
    {
        if (await context.Pets.CountAsync(x => x.Id == request.PetId) == 0)
            return BadRequest("Pet informado nao existe.");

        var veterinario = await context.Usuarios.FirstOrDefaultAsync(x => x.Id == request.VeterinarioId);
        if (veterinario is null || veterinario.TipoUsuario != "VETERINARIO")
            return BadRequest("Veterinario informado nao existe ou nao possui perfil VETERINARIO.");

        if (await context.Consultas.AnyAsync(x => x.VeterinarioId == request.VeterinarioId && x.DataConsulta == request.DataConsulta))
            return Conflict("Horario indisponivel para o veterinario informado.");

        try
        {
            var consulta = new Consulta(request.DataConsulta, request.Descricao, request.Valor, request.PetId, request.VeterinarioId);
            context.Consultas.Add(consulta);
            await context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = consulta.Id }, consulta);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Consulta>> Update(Guid id, ConsultaRequest request)
    {
        var consulta = await context.Consultas.FirstOrDefaultAsync(x => x.Id == id);
        if (consulta is null)
            return NotFound();

        if (await context.Pets.CountAsync(x => x.Id == request.PetId) == 0)
            return BadRequest("Pet informado nao existe.");

        var veterinario = await context.Usuarios.FirstOrDefaultAsync(x => x.Id == request.VeterinarioId);
        if (veterinario is null || veterinario.TipoUsuario != "VETERINARIO")
            return BadRequest("Veterinario informado nao existe ou nao possui perfil VETERINARIO.");

        if (await context.Consultas.AnyAsync(x => x.Id != id && x.VeterinarioId == request.VeterinarioId && x.DataConsulta == request.DataConsulta))
            return Conflict("Horario indisponivel para o veterinario informado.");

        try
        {
            consulta.Atualizar(request.DataConsulta, request.Descricao, request.Valor, request.PetId, request.VeterinarioId);
            await context.SaveChangesAsync();
            return Ok(consulta);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var consulta = await context.Consultas.FirstOrDefaultAsync(x => x.Id == id);
        if (consulta is null)
            return NotFound();

        context.Consultas.Remove(consulta);
        await context.SaveChangesAsync();
        return NoContent();
    }
}

public record ConsultaRequest(DateTime DataConsulta, string Descricao, decimal Valor, Guid PetId, Guid VeterinarioId);
