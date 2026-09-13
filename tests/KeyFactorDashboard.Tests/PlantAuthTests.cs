using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KeyFactorDashboard.Auth;
using KeyFactorDashboard.Data;
using KeyFactorDashboard.Models;
using KeyFactorDashboard.Services;

namespace KeyFactorDashboard.Tests;

public sealed class PlantAuthTests : IClassFixture<TestHost>
{
    private readonly TestHost _host;

    public PlantAuthTests(TestHost host) => _host = host;

    [Fact]
    public async Task Seed_admin_can_login_and_me_returns_admin()
    {
        var client = _host.CreateClient();
        var login = await PlantAuth.LoginAsync(client);
        login.User.Username.Should().Be("admin");
        login.User.Role.Should().Be(PlantRoles.Admin);

        var me = await client.GetFromJsonAsync<MeResponse>("/api/v1/auth/me", PythonApiClient.JsonOptions);
        me.Should().NotBeNull();
        me!.Username.Should().Be("admin");
        me.Role.Should().Be(PlantRoles.Admin);
    }

    [Fact]
    public async Task Admin_can_create_user_and_that_user_can_login()
    {
        var admin = _host.CreateClient();
        await PlantAuth.LoginAsync(admin);
        var name = $"eng-{Guid.NewGuid():N}"[..16];
        await PlantAuth.CreateUserAsync(admin, name, "EngPass!123", PlantRoles.Engineer);

        var eng = _host.CreateClient();
        var login = await PlantAuth.LoginAsync(eng, name, "EngPass!123");
        login.User.Role.Should().Be(PlantRoles.Engineer);
        login.User.Username.Should().Be(name);
    }

    [Fact]
    public async Task Engineer_can_train()
    {
        var client = await ClientAsAsync(PlantRoles.Engineer);
        var model = await TrainOnceAsync(client);
        model.Status.Should().Be(ModelStatuses.Draft);
        model.VersionNumber.Should().BeGreaterThan(0);
        model.TrainedBy.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Viewer_cannot_train()
    {
        var admin = _host.CreateClient();
        await PlantAuth.LoginAsync(admin);
        var dataset = await IngestAsync(admin);

        var viewer = await ClientAsAsync(PlantRoles.Viewer);
        var trained = await viewer.PostAsJsonAsync("/api/v1/models/train", new TrainRequest
        {
            DatasetId = dataset.Id,
            TargetColumn = "TargetConductivity"
        });
        trained.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Approve_makes_version_approved_and_previous_retired()
    {
        var client = _host.CreateClient();
        await PlantAuth.LoginAsync(client);
        var dataset = await IngestAsync(client);

        var first = await TrainOnAsync(client, dataset.Id);
        var approve1 = await client.PostAsJsonAsync($"/api/v1/models/{first.Id}/approve", new VersionNoteRequest { Note = "first" });
        approve1.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await TrainOnAsync(client, dataset.Id);
        var approve2 = await client.PostAsJsonAsync($"/api/v1/models/{second.Id}/approve", new VersionNoteRequest { Note = "second" });
        approve2.StatusCode.Should().Be(HttpStatusCode.OK);

        var versions = await client.GetFromJsonAsync<List<ModelResponse>>(
            $"/api/v1/datasets/{dataset.Id}/versions",
            PythonApiClient.JsonOptions);
        versions.Should().NotBeNull();
        versions!.Should().Contain(v => v.Id == second.Id && v.Status == ModelStatuses.Approved);
        versions.Should().Contain(v => v.Id == first.Id && v.Status == ModelStatuses.Retired);

        var current = await client.GetFromJsonAsync<ModelResponse>($"/api/v1/models/{second.Id}", PythonApiClient.JsonOptions);
        current!.VersionNumber.Should().Be(2);
        current.Status.Should().Be(ModelStatuses.Approved);
        current.TrainedBy.Should().Be("admin");
    }

    [Fact]
    public async Task Unauthenticated_mutating_call_is_401()
    {
        var client = _host.CreateClient();
        var trained = await client.PostAsJsonAsync("/api/v1/models/train", new TrainRequest
        {
            DatasetId = "missing",
            TargetColumn = "TargetConductivity"
        });
        trained.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var ingest = await client.PostAsJsonAsync("/api/v1/datasets/json", new JsonDatasetRequest
        {
            Name = "blocked",
            Columns = ["Temperature", "TargetConductivity"],
            Rows = [new List<object> { 180, 12.4 }]
        });
        ingest.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> ClientAsAsync(string role)
    {
        var admin = _host.CreateClient();
        await PlantAuth.LoginAsync(admin);
        var name = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}"[..20];
        await PlantAuth.CreateUserAsync(admin, name, "RolePass!123", role);
        var client = _host.CreateClient();
        await PlantAuth.LoginAsync(client, name, "RolePass!123");
        return client;
    }

    private static async Task<DatasetResponse> IngestAsync(HttpClient client)
    {
        var created = await client.PostAsJsonAsync("/api/v1/datasets/json", new JsonDatasetRequest
        {
            Name = "line-auth",
            Columns = ["Temperature", "Pressure", "TargetConductivity"],
            Rows =
            [
                new List<object> { 180, 2.1, 12.4 },
                new List<object> { 176, 2.3, 11.8 }
            ]
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var dataset = await created.Content.ReadFromJsonAsync<DatasetResponse>(PythonApiClient.JsonOptions);
        dataset.Should().NotBeNull();
        return dataset!;
    }

    private static async Task<ModelResponse> TrainOnceAsync(HttpClient client)
    {
        var dataset = await IngestAsync(client);
        return await TrainOnAsync(client, dataset.Id);
    }

    private static async Task<ModelResponse> TrainOnAsync(HttpClient client, string datasetId)
    {
        var trained = await client.PostAsJsonAsync("/api/v1/models/train", new TrainRequest
        {
            DatasetId = datasetId,
            TargetColumn = "TargetConductivity"
        });
        trained.StatusCode.Should().Be(HttpStatusCode.OK);
        var model = await trained.Content.ReadFromJsonAsync<ModelResponse>(PythonApiClient.JsonOptions);
        model.Should().NotBeNull();
        return model!;
    }
}
