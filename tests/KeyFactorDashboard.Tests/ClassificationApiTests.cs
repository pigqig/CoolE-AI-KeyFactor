using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KeyFactorDashboard.Models;
using KeyFactorDashboard.Services;

namespace KeyFactorDashboard.Tests;

public sealed class ClassificationApiTests : IClassFixture<TestHost>
{
    private readonly HttpClient _client;

    public ClassificationApiTests(TestHost host)
    {
        _client = host.CreateClient();
    }

    [Fact]
    public async Task Sample_okng_loads_quality_result_column()
    {
        await PlantAuth.LoginAsync(_client);
        var created = await _client.PostAsync("/api/v1/datasets/sample-okng", null);
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var dataset = await created.Content.ReadFromJsonAsync<DatasetResponse>(PythonApiClient.JsonOptions);
        dataset.Should().NotBeNull();
        dataset!.Columns.Should().Contain(c => c.Name == "QualityResult");
    }

    [Fact]
    public async Task Train_binary_returns_confusion_matrix()
    {
        await PlantAuth.LoginAsync(_client);
        var created = await _client.PostAsJsonAsync("/api/v1/datasets/json", new JsonDatasetRequest
        {
            Name = "okng-line",
            Columns = ["Temperature", "QualityResult"],
            Rows = [new List<object> { 150, "OK" }, new List<object> { 210, "NG" }]
        });
        created.EnsureSuccessStatusCode();
        var dataset = await created.Content.ReadFromJsonAsync<DatasetResponse>(PythonApiClient.JsonOptions);

        var trained = await _client.PostAsJsonAsync("/api/v1/models/train", new TrainRequest
        {
            DatasetId = dataset!.Id,
            TargetColumn = "QualityResult",
            Task = "binary"
        });
        trained.StatusCode.Should().Be(HttpStatusCode.OK);
        var model = await trained.Content.ReadFromJsonAsync<ModelResponse>(PythonApiClient.JsonOptions);
        model!.Task.Should().Be("classification");
        model.Metrics.Confusion.Should().NotBeNull();
        model.Metrics.Confusion!.Labels.Should().HaveCount(2);
        model.Metrics.Confusion.Matrix.Should().HaveCount(2);
    }

    [Fact]
    public async Task Explicit_multiclass_on_two_class_target_is_400()
    {
        await PlantAuth.LoginAsync(_client);
        var created = await _client.PostAsJsonAsync("/api/v1/datasets/json", new JsonDatasetRequest
        {
            Name = "two-class",
            Columns = ["Temperature", "QualityResult"],
            Rows = [new List<object> { 150, "OK" }, new List<object> { 210, "NG" }]
        });
        var dataset = await created.Content.ReadFromJsonAsync<DatasetResponse>(PythonApiClient.JsonOptions);

        var trained = await _client.PostAsJsonAsync("/api/v1/models/train", new TrainRequest
        {
            DatasetId = dataset!.Id,
            TargetColumn = "QualityResult",
            Task = "multiclass"
        });
        trained.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
