using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using KeyFactorDashboard.Models;
using KeyFactorDashboard.Services;

namespace KeyFactorDashboard.Tests;

public sealed class SmokeAndApiTests : IClassFixture<TestHost>
{
    private readonly HttpClient _client;

    public SmokeAndApiTests(TestHost host)
    {
        _client = host.CreateClient();
    }

    [Fact]
    public async Task Root_returns_200()
    {
        var response = await _client.GetAsync("/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Swagger_json_returns_200_and_lists_v1_paths()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("/api/v1/datasets");
        json.Should().Contain("/api/v1/models/train");
        json.Should().Contain("/api/v1/auth/login");
        json.Should().Contain("/api/v1/models/{id}/approve");
        json.Should().Contain("/api/v1/datasets/sample-okng");
    }

    [Fact]
    public async Task Json_dataset_train_importances_roundtrip_includes_insight_summary()
    {
        await PlantAuth.LoginAsync(_client);
        var created = await _client.PostAsJsonAsync("/api/v1/datasets/json", new JsonDatasetRequest
        {
            Name = "line-A",
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
        dataset!.Overview.Should().NotBeNull();
        dataset.Overview!.Summary.Should().NotBeNullOrWhiteSpace();

        var trained = await _client.PostAsJsonAsync("/api/v1/models/train", new TrainRequest
        {
            DatasetId = dataset.Id,
            TargetColumn = "TargetConductivity"
        });
        trained.StatusCode.Should().Be(HttpStatusCode.OK);
        var model = await trained.Content.ReadFromJsonAsync<ModelResponse>(PythonApiClient.JsonOptions);
        model.Should().NotBeNull();

        var imp = await _client.GetFromJsonAsync<ImportancesResponse>(
            $"/api/v1/models/{model!.Id}/importances",
            PythonApiClient.JsonOptions);
        imp.Should().NotBeNull();
        imp!.Insights.Should().NotBeEmpty();
        imp.Insights[0].Summary.Should().NotBeNullOrWhiteSpace();
        imp.Importances[0].Feature.Should().Be("Temperature");
    }
}

public sealed class PythonClientSerializationTests
{
    [Fact]
    public void Client_deserializes_python_importances_payload_with_insights()
    {
        const string json = """
            {
              "method": "permutation",
              "targetColumn": "TargetConductivity",
              "importances": [
                { "feature": "Temperature", "importance": 0.42, "share": 0.41, "rank": 1, "direction": "up" }
              ],
              "insights": [
                {
                  "feature": "Temperature",
                  "rank": 1,
                  "share": 0.41,
                  "importance": 0.42,
                  "direction": "up",
                  "insightCode": "topFactor",
                  "summary": "Temperature is the strongest driver of TargetConductivity (about 41%)."
                }
              ]
            }
            """;

        var dto = PythonApiClient.DeserializeImportances(json);
        dto.Importances.Should().ContainSingle();
        dto.Importances[0].Feature.Should().Be("Temperature");
        dto.Insights.Should().ContainSingle();
        dto.Insights[0].Summary.Should().NotBeNullOrWhiteSpace();
        dto.Insights[0].InsightCode.Should().Be("topFactor");
    }

    [Fact]
    public void Client_roundtrips_importances_json()
    {
        var raw = JsonSerializer.Serialize(new ImportancesResponse
        {
            Method = "permutation",
            Insights = [new InsightDto { Summary = "ok", InsightCode = "topFactor" }]
        }, PythonApiClient.JsonOptions);

        var again = PythonApiClient.DeserializeImportances(raw);
        again.Insights[0].Summary.Should().Be("ok");
    }
}
