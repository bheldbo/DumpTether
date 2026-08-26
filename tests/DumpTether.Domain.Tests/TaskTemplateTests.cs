using DumpTether.Domain;
using Xunit;

namespace DumpTether.Domain.Tests;

public sealed class TaskTemplateTests
{
    [Fact]
    public void BuiltInTemplate_CannotBeRenamedRelayoutedOrDeleted()
    {
        var createdAt = new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
        var template = TaskTemplate.Create(Guid.NewGuid(), "Basic Task", createdAt);
        template.MarkAsBuiltIn(TaskTemplateBuiltInKind.Basic, createdAt);

        Assert.True(template.IsProtected);
        Assert.Throws<InvalidOperationException>(() =>
            template.Rename("Renamed", createdAt.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() =>
            template.UpdateLayout("[]", "[]", createdAt.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() =>
            template.SoftDelete(createdAt.AddMinutes(1)));
    }

    [Fact]
    public void BuiltInTemplate_KindCannotBeChanged()
    {
        var now = new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
        var template = TaskTemplate.Create(Guid.NewGuid(), "Basic Task", now);
        template.MarkAsBuiltIn(TaskTemplateBuiltInKind.Basic, now);

        Assert.Throws<InvalidOperationException>(() =>
            template.MarkAsBuiltIn(TaskTemplateBuiltInKind.Todo, now.AddMinutes(1)));
    }

    [Fact]
    public void RestoreBuiltInDefinition_RepairsProtectedTemplateWithoutChangingIdentity()
    {
        var now = new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
        var template = TaskTemplate.Create(Guid.NewGuid(), "Old default", now);
        var templateId = template.Id;
        template.MarkAsBuiltIn(TaskTemplateBuiltInKind.Basic, now);

        template.RestoreBuiltInDefinition(
            TaskTemplateBuiltInKind.Basic,
            "Basic Task",
            "[{\"row\":1}]",
            "[{\"row\":1}]",
            now.AddMinutes(1));

        Assert.Equal(templateId, template.Id);
        Assert.Equal(TaskTemplateBuiltInKind.Basic, template.BuiltInKind);
        Assert.Equal("Basic Task", template.Name);
    }

    [Fact]
    public void RetireBuiltIn_HidesLegacyTemplateWithoutDiscardingItsDefinition()
    {
        var now = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
        var template = TaskTemplate.Create(Guid.NewGuid(), "ToDo Task", now);
        var field = template.AddFieldDefinition(
            "item",
            "Item",
            FieldDefinitionType.Text,
            FieldDefinitionScope.Entry,
            isRequired: false,
            sortOrder: 0);
        template.MarkAsBuiltIn(TaskTemplateBuiltInKind.Todo, now);

        template.RetireBuiltIn(TaskTemplateBuiltInKind.Todo, now.AddMinutes(1));

        Assert.Equal(TaskTemplateBuiltInKind.None, template.BuiltInKind);
        Assert.False(template.IsProtected);
        Assert.NotNull(template.DeletedAt);
        Assert.Contains(template.FieldDefinitions, candidate => candidate.Id == field.Id);
    }
}
