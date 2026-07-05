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
    public async Task PostTemplates_CanDefineHeaderAndEntryFields()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var template = await CreateTemplateAsync(
            client,
            "Todo Evidence",
            new object[]
            {
                new
                {
                    name = "Context",
                    type = "LongText",
                    scope = "Header",
                    required = false,
                    sortOrder = 0,
                    layoutRow = 1,
                    layoutColumn = 1,
                    layoutColumnSpan = 2,
                    options = Array.Empty<string>()
                },
                new
                {
                    name = "Done",
                    type = "Checkbox",
                    scope = "Entry",
                    required = true,
                    sortOrder = 0,
                    layoutRow = 1,
                    layoutColumn = 1,
                    options = Array.Empty<string>()
                }
            });

        Assert.Contains(template.Fields, field =>
            field.Name == "Context" &&
            field.Scope == "Header" &&
            field.LayoutColumnSpan == 2);
        Assert.Contains(template.Fields, field =>
            field.Name == "Done" &&
            field.Scope == "Entry" &&
            field.LayoutColumn == 1);
    }

    [Fact]
    public async Task PostAndPatchTemplates_PersistLayoutRows()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/templates",
            new
            {
                name = "Layout Test",
                layout = new
                {
                    header = new[]
                    {
                        new
                        {
                            row = 1,
                            columnWeights = new[] { 3.0, 1.0 },
                            height = 240.0
                        }
                    },
                    entry = new[]
                    {
                        new
                        {
                            row = 1,
                            columnWeights = new[] { 4.0, 0.75 },
                            height = 150.0
                        },
                        new
                        {
                            row = 2,
                            columnWeights = new[] { 1.0 },
                            height = 220.0
                        }
                    }
                },
                fields = new object[]
                {
                    new
                    {
                        name = "Description",
                        type = "LongText",
                        scope = "Header",
                        required = false,
                        sortOrder = 0,
                        layoutRow = 1,
                        layoutColumn = 1,
                        options = Array.Empty<string>()
                    },
                    new
                    {
                        name = "Done",
                        type = "Checkbox",
                        scope = "Entry",
                        required = false,
                        sortOrder = 0,
                        layoutRow = 1,
                        layoutColumn = 2,
                        options = Array.Empty<string>()
                    }
                }
            });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created, got {response.StatusCode}. Body: {body}");

        var created = await response.Content.ReadFromJsonAsync<TaskTemplateDetailResponse>();
        Assert.NotNull(created);
        Assert.Equal(240.0, created.Layout.Header.Single().Height, 2);
        Assert.Collection(
            created.Layout.Header.Single().ColumnWeights,
            first => Assert.Equal(3.0, first, 2),
            second => Assert.Equal(1.0, second, 2));
        Assert.Equal(0.75, created.Layout.Entry.First().ColumnWeights[1], 2);
        Assert.Equal(220.0, created.Layout.Entry.Last().Height, 2);

        var updated = await PatchTemplateAsync(
            client,
            created.Id,
            new
            {
                layout = new
                {
                    header = new[]
                    {
                        new
                        {
                            row = 1,
                            columnWeights = new[] { 1.0 },
                            height = 180.0
                        }
                    },
                    entry = new[]
                    {
                        new
                        {
                            row = 1,
                            columnWeights = new[] { 2.5, 1.5 },
                            height = 260.0
                        }
                    }
                }
            });

        Assert.Equal(180.0, updated.Layout.Header.Single().Height, 2);
        Assert.Equal(1.0, Assert.Single(updated.Layout.Header.Single().ColumnWeights), 2);
        Assert.Equal(2.5, updated.Layout.Entry.Single().ColumnWeights[0], 2);
        Assert.Equal(260.0, updated.Layout.Entry.Single().Height, 2);
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
    public async Task DeleteTemplate_HidesTemplateButPreservesExistingTaskStructure()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var template = await CreateTodoTemplateAsync(client);
        var doneField = template.Fields.Single(field => field.Name == "Done");
        var created = await CreateTaskItemAsync(
            client,
            "Todo survives template delete",
            template.Id,
            new Dictionary<Guid, object?>());
        var withEntry = await PostTimelineEntryAsync(
            client,
            created.Id,
            new
            {
                fieldValues = new Dictionary<Guid, object?>
                {
                    [doneField.Id] = true
                }
            });

        var deleteResponse = await client.DeleteAsync($"/api/templates/{template.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var templates = await client.GetFromJsonAsync<List<TaskTemplateSummaryResponse>>(
            "/api/templates");
        Assert.NotNull(templates);
        Assert.DoesNotContain(templates, candidate => candidate.Id == template.Id);

        var fetched = await client.GetFromJsonAsync<TaskItemDetailResponse>(
            $"/api/tasks/{created.Id}");

        Assert.NotNull(fetched);
        Assert.Equal(template.Id, fetched.TaskTemplateId);
        Assert.NotNull(fetched.Template);
        Assert.Equal(template.Id, fetched.Template.Id);
        Assert.Contains(fetched.Template.Fields, field => field.Id == doneField.Id);
        Assert.Contains(
            fetched.TimelineEntries,
            entry => entry.Id == withEntry.TimelineEntries.Last().Id &&
                entry.FieldValues.Any(value => value.FieldDefinitionId == doneField.Id));
    }

    [Fact]
    public async Task ImportTemplateFromTask_RestoresDeletedTemplateToLibrary()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var template = await CreateTodoTemplateAsync(client);
        var doneField = template.Fields.Single(field => field.Name == "Done");
        var created = await CreateTaskItemAsync(
            client,
            "Todo imports deleted template",
            template.Id,
            new Dictionary<Guid, object?>());
        _ = await PostTimelineEntryAsync(
            client,
            created.Id,
            new
            {
                fieldValues = new Dictionary<Guid, object?>
                {
                    [doneField.Id] = true
                }
            });

        var deleteResponse = await client.DeleteAsync($"/api/templates/{template.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var importResponse = await client.PostAsync(
            $"/api/tasks/{created.Id}/template/import",
            content: null);
        var importBody = await importResponse.Content.ReadAsStringAsync();
        Assert.True(
            importResponse.IsSuccessStatusCode,
            $"Expected success, got {importResponse.StatusCode}. Body: {importBody}");
        var imported = await importResponse.Content.ReadFromJsonAsync<TaskTemplateImportResponse>();
        Assert.NotNull(imported);

        var importedTemplate = imported.Template;
        var importedDoneField = importedTemplate.Fields.Single(field => field.Name == "Done");
        var templates = await client.GetFromJsonAsync<List<TaskTemplateSummaryResponse>>(
            "/api/templates");

        Assert.Equal(template.Id, imported.SourceTemplateId);
        Assert.NotEqual(template.Id, importedTemplate.Id);
        Assert.Contains("Todo Test", importedTemplate.Name);
        Assert.NotEqual(doneField.Id, importedDoneField.Id);
        Assert.Equal(doneField.Type, importedDoneField.Type);
        Assert.Contains(templates!, candidate => candidate.Id == importedTemplate.Id);
        Assert.DoesNotContain(templates!, candidate => candidate.Id == template.Id);
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

    [Fact]
    public async Task PatchTaskItem_RejectsEntryFieldValuesOnTaskHeader()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var template = await CreateTodoTemplateAsync(client);
        var doneField = template.Fields.Single(field => field.Name == "Done");
        var created = await CreateTaskItemAsync(
            client,
            "Todo container",
            template.Id,
            new Dictionary<Guid, object?>());

        var response = await SendPatchAsync(
            client,
            created.Id,
            new
            {
                fieldValues = new Dictionary<Guid, object?>
                {
                    [doneField.Id] = true
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTaskTimeline_StoresEntryFieldValues()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var template = await CreateTodoTemplateAsync(client);
        var doneField = template.Fields.Single(field => field.Name == "Done");
        var created = await CreateTaskItemAsync(
            client,
            "Checklist",
            template.Id,
            new Dictionary<Guid, object?>());

        var updated = await PostTimelineEntryAsync(
            client,
            created.Id,
            new
            {
                note = "Confirm upgrade notes",
                fieldValues = new Dictionary<Guid, object?>
                {
                    [doneField.Id] = true
                }
            });

        var note = updated.TimelineEntries.Last();
        Assert.Equal("NoteAdded", note.Kind);
        var value = Assert.Single(note.FieldValues);
        Assert.Equal(doneField.Id, value.FieldDefinitionId);
        Assert.Equal("true", value.ValueJson);
    }

    [Fact]
    public async Task PatchTaskTimeline_UpdatesExistingEntryFieldValue()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var template = await CreateTodoTemplateAsync(client);
        var doneField = template.Fields.Single(field => field.Name == "Done");
        var created = await CreateTaskItemAsync(
            client,
            "Checklist edit",
            template.Id,
            new Dictionary<Guid, object?>());
        var withEntry = await PostTimelineEntryAsync(
            client,
            created.Id,
            new
            {
                note = "Confirm upgrade notes",
                fieldValues = new Dictionary<Guid, object?>
                {
                    [doneField.Id] = true
                }
            });
        var entry = withEntry.TimelineEntries.Last();

        var updated = await PatchTimelineEntryAsync(
            client,
            created.Id,
            entry.Id,
            new
            {
                note = "Confirm upgrade notes",
                fieldValues = new Dictionary<Guid, object?>
                {
                    [doneField.Id] = false
                }
            });

        var updatedEntry = updated.TimelineEntries.Single(candidate => candidate.Id == entry.Id);
        var value = Assert.Single(updatedEntry.FieldValues);
        Assert.Equal(doneField.Id, value.FieldDefinitionId);
        Assert.Equal("false", value.ValueJson);
    }


    [Fact]
    public async Task PostTaskTimeline_RejectsHeaderFieldValuesOnEntry()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var template = await CreateTodoTemplateAsync(client);
        var contextField = template.Fields.Single(field => field.Name == "Context");
        var created = await CreateTaskItemAsync(
            client,
            "Checklist rejection",
            template.Id,
            new Dictionary<Guid, object?>());

        var response = await client.PostAsJsonAsync(
            $"/api/tasks/{created.Id}/timeline",
            new
            {
                note = "Wrong scope",
                fieldValues = new Dictionary<Guid, object?>
                {
                    [contextField.Id] = "Header value"
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private static async Task<TaskTemplateDetailResponse> CreateTodoTemplateAsync(
        HttpClient client)
    {
        return await CreateTemplateAsync(
            client,
            "Todo Test",
            new object[]
            {
                new
                {
                    name = "Context",
                    type = "LongText",
                    scope = "Header",
                    required = false,
                    sortOrder = 0,
                    options = Array.Empty<string>()
                },
                new
                {
                    name = "Done",
                    type = "Checkbox",
                    scope = "Entry",
                    required = true,
                    sortOrder = 0,
                    options = Array.Empty<string>()
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

    private static async Task<TaskItemDetailResponse> PostTimelineEntryAsync(
        HttpClient client,
        Guid id,
        object request)
    {
        var response = await client.PostAsJsonAsync($"/api/tasks/{id}/timeline", request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected success, got {response.StatusCode}. Body: {body}");

        var updated = await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>();
        Assert.NotNull(updated);

        return updated;
    }

    private static async Task<TaskItemDetailResponse> PatchTimelineEntryAsync(
        HttpClient client,
        Guid id,
        Guid entryId,
        object request)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/tasks/{id}/timeline/{entryId}")
        {
            Content = JsonContent.Create(request)
        };

        var response = await client.SendAsync(message);
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
