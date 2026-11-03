using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

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
}
