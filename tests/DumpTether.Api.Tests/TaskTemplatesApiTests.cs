using System.Net;
using System.Net.Http.Json;
using DumpTether.App.Tasks;
using DumpTether.App.Templates;
using Xunit;

namespace DumpTether.Api.Tests;

public sealed class TaskTemplatesApiTests
{
    [Fact]
    public async Task PostTemplates_CreatesTemplate()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var template = await CreateTemplateAsync(
            client,
            "Research Note",
            []);

        Assert.NotEqual(Guid.Empty, template.Id);
        Assert.Equal("Research Note", template.Name);
    }

    [Fact]
    public async Task PatchTemplate_AddsFields()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var template = await CreateTemplateAsync(client, "Field Starter", []);

        var updated = await PatchTemplateAsync(
            client,
            template.Id,
            new
            {
                fields = new[]
                {
                    new
                    {
                        name = "Reference",
                        type = "Text",
                        required = true,
                        sortOrder = 0,
                        options = Array.Empty<string>()
                    },
                    new
                    {
                        name = "Stage",
                        type = "Select",
                        required = false,
                        sortOrder = 1,
                        options = new[] { "New", "Validated" }
                    }
                }
            });

        Assert.Equal(2, updated.Fields.Count);
        Assert.Contains(updated.Fields, field => field.Name == "Reference" && field.Required);
        Assert.Contains(updated.Fields, field =>
            field.Name == "Stage" &&
            field.Type == "Select" &&
            field.Options.Contains("Validated"));
    }

    [Fact]
    public async Task PostTaskItems_CreatesTaskFromTemplate()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var template = await CreateCaseTemplateAsync(client);
        var customerField = template.Fields.Single(field => field.Name == "Customer");
        var severityField = template.Fields.Single(field => field.Name == "Severity");

        var created = await CreateTaskItemAsync(
            client,
            "Case 100",
            template.Id,
            new Dictionary<Guid, object?>
            {
                [customerField.Id] = "Northwind",
                [severityField.Id] = "High"
            });

        Assert.Equal(template.Id, created.TaskTemplateId);
        Assert.NotNull(created.Template);
        Assert.Equal("Service Desk Test", created.Template.Name);
        Assert.Equal(2, created.FieldValues.Count);
    }

    [Fact]
    public async Task PatchTaskItem_UpdatesFieldValues()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var template = await CreateCaseTemplateAsync(client);
        var customerField = template.Fields.Single(field => field.Name == "Customer");
        var severityField = template.Fields.Single(field => field.Name == "Severity");
        var created = await CreateTaskItemAsync(
            client,
            "Case 101",
            template.Id,
            new Dictionary<Guid, object?>
            {
                [customerField.Id] = "Northwind",
                [severityField.Id] = "Low"
            });

        var updated = await PatchTaskItemAsync(
            client,
            created.Id,
            new
            {
                fieldValues = new Dictionary<Guid, object?>
                {
                    [severityField.Id] = "High"
                }
            });

        var severityValue = updated.FieldValues.Single(value =>
            value.FieldDefinitionId == severityField.Id);
        Assert.Equal("\"High\"", severityValue.ValueJson);
    }

    [Fact]
    public async Task PostTaskItems_RejectsMissingRequiredFieldValue()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var template = await CreateCaseTemplateAsync(client);
        var severityField = template.Fields.Single(field => field.Name == "Severity");

        var response = await client.PostAsJsonAsync(
            "/api/tasks",
            new
            {
                title = "Case without customer",
                taskTemplateId = template.Id,
                fieldValues = new Dictionary<Guid, object?>
                {
                    [severityField.Id] = "Low"
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchTaskItem_SelectFieldRejectsValueOutsideOptions()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var template = await CreateCaseTemplateAsync(client);
        var customerField = template.Fields.Single(field => field.Name == "Customer");
        var severityField = template.Fields.Single(field => field.Name == "Severity");
        var created = await CreateTaskItemAsync(
            client,
            "Case 102",
            template.Id,
            new Dictionary<Guid, object?>
            {
                [customerField.Id] = "Northwind",
                [severityField.Id] = "Low"
            });

        var response = await SendPatchAsync(
            client,
            created.Id,
            new
            {
                fieldValues = new Dictionary<Guid, object?>
                {
                    [severityField.Id] = "Emergency"
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchTaskItem_FieldUpdatesUpdateLastTouchedAt()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var template = await CreateCaseTemplateAsync(client);
        var customerField = template.Fields.Single(field => field.Name == "Customer");
        var severityField = template.Fields.Single(field => field.Name == "Severity");
        var created = await CreateTaskItemAsync(
            client,
            "Case 103",
            template.Id,
            new Dictionary<Guid, object?>
            {
                [customerField.Id] = "Northwind",
                [severityField.Id] = "Low"
            });

        await Task.Delay(10);

        var updated = await PatchTaskItemAsync(
            client,
            created.Id,
            new
            {
                fieldValues = new Dictionary<Guid, object?>
                {
                    [severityField.Id] = "High"
                }
            });

        Assert.True(updated.LastTouchedAt > created.LastTouchedAt);
    }

    [Fact]
    public async Task PatchTaskItem_FieldUpdatesCreateTimelineEvidence()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var template = await CreateCaseTemplateAsync(client);
        var customerField = template.Fields.Single(field => field.Name == "Customer");
        var severityField = template.Fields.Single(field => field.Name == "Severity");
        var created = await CreateTaskItemAsync(
            client,
            "Case 104",
            template.Id,
            new Dictionary<Guid, object?>
            {
                [customerField.Id] = "Northwind",
                [severityField.Id] = "Low"
            });

        var updated = await PatchTaskItemAsync(
            client,
            created.Id,
            new
            {
                fieldValues = new Dictionary<Guid, object?>
                {
                    [severityField.Id] = "High"
                }
            });

        Assert.Contains(updated.TimelineEntries, entry =>
            entry.Kind == "FieldValueChanged" &&
            entry.Summary == "Field value changed: Severity");
    }

    private static async Task<TaskTemplateDetailResponse> CreateCaseTemplateAsync(
        HttpClient client)
    {
        return await CreateTemplateAsync(
            client,
            "Service Desk Test",
            new object[]
            {
                new
                {
                    name = "Customer",
                    type = "Text",
                    required = true,
                    sortOrder = 0,
                    options = Array.Empty<string>()
                },
                new
                {
                    name = "Severity",
                    type = "Select",
                    required = false,
                    sortOrder = 1,
                    options = new[] { "Low", "High" }
                }
            });
    }

    private static async Task<TaskTemplateDetailResponse> CreateTemplateAsync(
        HttpClient client,
        string name,
        IReadOnlyList<object> fields)
    {
        var response = await client.PostAsJsonAsync(
            "/api/templates",
            new
            {
                name,
                fields
            });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created, got {response.StatusCode}. Body: {body}");

        var created = await response.Content.ReadFromJsonAsync<TaskTemplateDetailResponse>();
        Assert.NotNull(created);

        return created;
    }

    private static async Task<TaskTemplateDetailResponse> PatchTemplateAsync(
        HttpClient client,
        Guid id,
        object request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/templates/{id}")
        {
            Content = JsonContent.Create(request)
        };

        var response = await client.SendAsync(message);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected success, got {response.StatusCode}. Body: {body}");

        var updated = await response.Content.ReadFromJsonAsync<TaskTemplateDetailResponse>();
        Assert.NotNull(updated);

        return updated;
    }

    private static async Task<TaskItemDetailResponse> CreateTaskItemAsync(
        HttpClient client,
        string title,
        Guid taskTemplateId,
        IReadOnlyDictionary<Guid, object?> fieldValues)
    {
        var response = await client.PostAsJsonAsync(
            "/api/tasks",
            new
            {
                title,
                taskTemplateId,
                fieldValues
            });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created, got {response.StatusCode}. Body: {body}");

        var created = await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>();
        Assert.NotNull(created);

        return created;
    }

    private static async Task<TaskItemDetailResponse> PatchTaskItemAsync(
        HttpClient client,
        Guid id,
        object request)
    {
        using var response = await SendPatchAsync(client, id, request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected success, got {response.StatusCode}. Body: {body}");

        var updated = await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>();
        Assert.NotNull(updated);

        return updated;
    }

    private static async Task<HttpResponseMessage> SendPatchAsync(
        HttpClient client,
        Guid id,
        object request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/tasks/{id}")
        {
            Content = JsonContent.Create(request)
        };

        return await client.SendAsync(message);
    }
}
