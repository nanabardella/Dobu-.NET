using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Dobu.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dobu.IntegrationTests;

[Collection(ApiCollection.Name)]
public class Sprint4ComplianceTests(DobuApiFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();

    private async Task<JsonElement> Create(string route, object body)
    {
        var response = await client.PostAsJsonAsync(route, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<(Guid pet, Guid vet)> Prepare()
    {
        async Task<JsonElement> Register(string role)
        {
            var response = await client.PostAsJsonAsync("/api/auth/register", new
            { nome = "Tutor QA", email = $"qa-{Guid.NewGuid():N}@example.test", senha = "Senha123", tipoUsuario = role });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }
        var tutor = await Register("Responsavel");
        var vet = await Register("Veterinario");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tutor.GetProperty("token").GetString());
        var species = await Create("/api/especies", new { nome = "Cachorro QA", descricao = "Animal domestico" });
        var breed = await Create("/api/racas", new { nome = "Vira-lata QA", porte = "Medio", expectativaVida = 12, descricao = "Raca de teste", cuidados = "Banho e passeio", especieId = species.GetProperty("id").GetGuid() });
        var pet = await Create("/api/pets", new { nome = "Thor", idade = 4, racaId = breed.GetProperty("id").GetGuid(), responsavelId = tutor.GetProperty("usuarioId").GetGuid() });
        return (pet.GetProperty("id").GetGuid(), vet.GetProperty("usuarioId").GetGuid());
    }

    [Fact]
    public async Task Carteira_VacinaRegistrada_PersisteDadosControlados()
    {
        var (pet, _) = await Prepare();
        var created = await Create("/api/vacinas", new { nome = "V10", dataAplicacao = "2026-10-19T10:00:00Z", dataProximaDose = "2026-10-25T10:00:00Z", petId = pet });
        var id = created.GetProperty("id").GetGuid();
        var response = await client.GetAsync($"/api/vacinas/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("V10", saved.GetProperty("nome").GetString());
        Assert.Equal(pet, saved.GetProperty("petId").GetGuid());
        Assert.Equal(new DateTime(2026, 10, 25, 10, 0, 0), saved.GetProperty("dataProximaDose").GetDateTime());
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/vacinas/pet/{pet}");
        Assert.Contains(list.EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task ProximasDoses_LimiteControlado_IncluiLimiteEExcluiFuturo()
    {
        var (pet, _) = await Prepare();
        var inside = await Create("/api/vacinas", new { nome = "V10", dataAplicacao = "2026-10-19T10:00:00Z", dataProximaDose = "2026-10-25T10:00:00Z", petId = pet });
        var outside = await Create("/api/vacinas", new { nome = "Controle QA", dataAplicacao = "2026-10-19T10:00:00Z", dataProximaDose = "2026-11-15T10:00:00Z", petId = pet });
        var response = await client.GetAsync("/api/vacinas/proxima-dose?ate=2026-10-25T10:00:00Z");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(items.EnumerateArray(), item => item.GetProperty("id").GetGuid() == inside.GetProperty("id").GetGuid());
        Assert.DoesNotContain(items.EnumerateArray(), item => item.GetProperty("id").GetGuid() == outside.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Reagendamento_DataControlada_PersisteHorarioEVinculos()
    {
        var (pet, vet) = await Prepare();
        var created = await Create("/api/consultas", new { dataConsulta = "2026-10-22T14:00:00Z", descricao = "C-100 consulta anual", valor = 150, petId = pet, veterinarioId = vet });
        var id = created.GetProperty("id").GetGuid();
        var response = await client.PutAsJsonAsync($"/api/consultas/{id}", new { dataConsulta = "2026-10-24T10:00:00Z", descricao = "C-100 reagendada", valor = 150, petId = pet, veterinarioId = vet });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await client.GetFromJsonAsync<JsonElement>($"/api/consultas/{id}");
        Assert.Equal(new DateTime(2026, 10, 24, 10, 0, 0), saved.GetProperty("dataConsulta").GetDateTime());
        Assert.Equal(pet, saved.GetProperty("petId").GetGuid());
        Assert.Equal(vet, saved.GetProperty("veterinarioId").GetGuid());
        Assert.Equal(150m, saved.GetProperty("valor").GetDecimal());
    }

    [Fact]
    public async Task Lembrete_DadosControlados_PersistePendenteParaThor()
    {
        var (pet, _) = await Prepare();
        var created = await Create("/api/lembretes", new { descricao = "Lembrete controlado QA", dataLembrete = "2026-10-21T14:00:00Z", status = "PENDENTE", petId = pet });
        var response = await client.GetAsync($"/api/lembretes/{created.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Lembrete controlado QA", saved.GetProperty("descricao").GetString());
        Assert.Equal("PENDENTE", saved.GetProperty("status").GetString());
        Assert.Equal(pet, saved.GetProperty("petId").GetGuid());
        Assert.Equal(new DateTime(2026, 10, 21, 14, 0, 0), saved.GetProperty("dataLembrete").GetDateTime());
    }

    [Fact]
    public async Task Carteira_OutroTutorOuAnonimo_NaoAcessaNemAlteraDados()
    {
        var (pet, _) = await Prepare();
        var created = await Create("/api/vacinas", new { nome = "V10", dataAplicacao = "2026-10-19T10:00:00Z", dataProximaDose = "2026-10-25T10:00:00Z", petId = pet });
        var id = created.GetProperty("id").GetGuid();
        await Prepare(); // A second owner; the client now uses this owner's token.
        foreach (var route in new[] { $"/api/vacinas/{id}", $"/api/vacinas/pet/{pet}" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route)).StatusCode);
        foreach (var route in new[] { "/api/vacinas", "/api/vacinas/proxima-dose?ate=2026-10-30T23:59:59Z" })
        {
            var list = await client.GetFromJsonAsync<JsonElement>(route);
            Assert.DoesNotContain(list.EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/vacinas/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/vacinas", new { nome = "V10", dataAplicacao = "2026-10-19T10:00:00Z", petId = pet })).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/vacinas/{id}")).StatusCode);
    }

    [Fact]
    public async Task Reagendamento_HorarioOcupado_RecusaEPreservaOriginal()
    {
        var (pet, vet) = await Prepare();
        var original = new { dataConsulta = "2026-10-22T14:00:00Z", descricao = "C-100 original", valor = 150, petId = pet, veterinarioId = vet };
        var occupied = new { dataConsulta = "2026-10-24T10:00:00Z", descricao = "C-200 ocupada", valor = 150, petId = pet, veterinarioId = vet };
        var first = await Create("/api/consultas", original);
        await Create("/api/consultas", occupied);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/consultas", occupied)).StatusCode);
        var id = first.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/consultas/{id}", occupied)).StatusCode);
        var saved = await client.GetFromJsonAsync<JsonElement>($"/api/consultas/{id}");
        Assert.Equal(new DateTime(2026, 10, 22, 14, 0, 0), saved.GetProperty("dataConsulta").GetDateTime());
        Assert.Equal("C-100 original", saved.GetProperty("descricao").GetString());
        // Keeping the same slot is allowed; another veterinarian may use that time.
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/consultas/{id}", original)).StatusCode);
    }

    [Fact]
    public async Task Alertas_JanelaControlada_NaoDuplicaENaoExpoeOutroTutor()
    {
        var (pet, _) = await Prepare();
        await Create("/api/vacinas", new { nome = "V10", dataAplicacao = "2026-10-10T10:00:00Z", dataProximaDose = "2026-10-25T10:00:00Z", petId = pet });
        await Create("/api/vacinas", new { nome = "Limite QA", dataAplicacao = "2026-10-10T10:00:00Z", dataProximaDose = "2026-10-26T23:59:59Z", petId = pet });
        await Create("/api/vacinas", new { nome = "Futura QA", dataAplicacao = "2026-10-10T10:00:00Z", dataProximaDose = "2026-10-27T00:00:00Z", petId = pet });
        await Create("/api/vacinas", new { nome = "Vencida QA", dataAplicacao = "2026-10-10T10:00:00Z", dataProximaDose = "2026-10-18T23:59:59Z", petId = pet });
        var response = await client.PostAsync("/api/alertas-vacina/gerar?referencia=2026-10-19T10:00:00Z", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("criados").GetInt32());
        var repeated = await client.PostAsync("/api/alertas-vacina/gerar?referencia=2026-10-19T10:00:00Z", null);
        Assert.Equal(0, (await repeated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("criados").GetInt32());
        var alerts = await client.GetFromJsonAsync<JsonElement>("/api/alertas-vacina");
        Assert.Equal(2, alerts.GetArrayLength());
        Assert.All(alerts.EnumerateArray(), alert => Assert.Equal(pet, alert.GetProperty("petId").GetGuid()));
        var reminderId = alerts[0].GetProperty("id").GetGuid();
        await Prepare();
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/alertas-vacina")).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/lembretes/{reminderId}")).StatusCode);
    }

    [Fact]
    public async Task Alertas_RotinaEmSegundoPlano_GeraSemRequisicaoManual()
    {
        var (pet, _) = await Prepare();
        await Create("/api/vacinas", new { nome = "Worker QA", dataAplicacao = "2026-10-10T10:00:00Z", dataProximaDose = "2026-10-25T10:00:00Z", petId = pet });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["VaccineAlerts:Enabled"] = "true" }).Build();
        using var worker = new VaccineAlertWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(),
            new FixedClock(), configuration, NullLogger<VaccineAlertWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        JsonElement alerts = default;
        try
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                alerts = await client.GetFromJsonAsync<JsonElement>("/api/alertas-vacina");
                if (alerts.GetArrayLength() > 0) break;
                await Task.Delay(50);
            }
            Assert.Contains(alerts.EnumerateArray(), alert => alert.GetProperty("descricao").GetString()!.Contains("Worker QA"));
        }
        finally { await worker.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task DadosInvalidos_VacinaELembrete_RecusaSemPersistir()
    {
        var (pet, _) = await Prepare();
        var badVaccine = await client.PostAsJsonAsync("/api/vacinas", new { nome = "V10", dataAplicacao = "2026-10-19T10:00:00Z", petId = Guid.Parse("00000000-0000-0000-0000-000000000001") });
        Assert.Equal(HttpStatusCode.BadRequest, badVaccine.StatusCode);
        var badReminder = await client.PostAsJsonAsync("/api/lembretes", new { descricao = "abc", dataLembrete = "2026-10-21T14:00:00Z", status = "PENDENTE", petId = pet });
        Assert.Equal(HttpStatusCode.BadRequest, badReminder.StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/lembretes/pet/{pet}");
        Assert.Empty(list.EnumerateArray());
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);
    }
}
