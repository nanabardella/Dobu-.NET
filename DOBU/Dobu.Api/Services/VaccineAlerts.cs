using Dobu.Domain.Entities;
using Dobu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dobu.Api.Services;

// Uses the existing reminder storage; no schema change is needed.
public sealed class VaccineAlerts(DobuDbContext context, IConfiguration configuration)
{
    public async Task<int> GenerateAsync(DateTime reference, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        var days = Math.Clamp(configuration.GetValue("VaccineAlerts:WindowDays", 7), 1, 365);
        var start = reference.Date;
        var end = start.AddDays(days + 1);
        var vaccines = context.Vacinas.AsNoTracking().Include(x => x.Pet)
            .Where(x => x.DataProximaDose >= start && x.DataProximaDose < end);
        if (owner.HasValue)
            vaccines = vaccines.Where(x => x.Pet.ResponsavelId == owner.Value);
        var created = 0;
        foreach (var vaccine in await vaccines.ToListAsync(cancellationToken))
        {
            var due = vaccine.DataProximaDose!.Value;
            var description = $"Vacina {vaccine.Nome} de {vaccine.Pet.Nome}: proxima dose em {due:yyyy-MM-dd}.";
            if (await context.Lembretes.AnyAsync(x => x.PetId == vaccine.PetId && x.Descricao == description && x.DataLembrete == due, cancellationToken))
                continue;
            context.Lembretes.Add(new Lembrete(description, due, "PENDENTE", vaccine.PetId));
            // Save each item so subsequent duplicate records in this same batch are recognized.
            await context.SaveChangesAsync(cancellationToken);
            created++;
        }
        return created;
    }
}

public sealed class VaccineAlertWorker(IServiceScopeFactory scopes, TimeProvider clock,
    IConfiguration configuration, ILogger<VaccineAlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("VaccineAlerts:Enabled", true)) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<VaccineAlerts>()
                    .GenerateAsync(clock.GetUtcNow().UtcDateTime, cancellationToken: stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(1), clock, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha na rotina de alertas de vacinacao");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
