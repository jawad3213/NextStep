using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NextStep.Modules.Applications.Infrastructure.Persistence;
using NextStep.Modules.Coaching.Infrastructure.Persistence;
using NextStep.Modules.CvDocuments.Infrastructure.Persistence;
using NextStep.Modules.Messaging.Infrastructure.Persistence;
using NextStep.Modules.Profile.Infrastructure.Persistence;
using NextStep.Modules.Sourcing.Infrastructure.Persistence;
using NextStep.Shared.Events;
using NextStep.Shared.Persistence;
using Xunit;

namespace NextStep.Tests;

/// <summary>
/// Architecture rules of the modular monolith. A module may use another module only
/// through its Contracts, and owns its data alone (own DbContext, own schema).
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly string[] Modules = ["Profile", "Applications", "CvDocuments", "Messaging", "Coaching", "Sourcing"];

    private static readonly Regex ModuleReference = new(@"NextStep\.Modules\.(?<module>\w+)\.(?<area>\w+)", RegexOptions.Compiled);

    public static IEnumerable<object[]> ModuleContexts() =>
    [
        [typeof(ProfileDbContext), "Profile", "profile"],
        [typeof(ApplicationsDbContext), "Applications", "applications"],
        [typeof(CvDocumentsDbContext), "CvDocuments", "cv"],
        [typeof(MessagingDbContext), "Messaging", "messaging"],
        [typeof(CoachingDbContext), "Coaching", "coaching"],
        [typeof(SourcingDbContext), "Sourcing", "sourcing"],
    ];

    [Fact]
    public void Modules_reference_other_modules_only_through_their_contracts()
    {
        var violations = new List<string>();
        var modulesDir = Path.Combine(FindBackendRoot(), "Modules");

        foreach (var module in Modules)
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(modulesDir, module), "*.cs", SearchOption.AllDirectories))
            {
                if (IsBuildOutput(file)) continue;
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    foreach (System.Text.RegularExpressions.Match match in ModuleReference.Matches(lines[i]))
                    {
                        var target = match.Groups["module"].Value;
                        if (target != module && match.Groups["area"].Value != "Contracts")
                            violations.Add($"{Path.GetRelativePath(modulesDir, file)}:{i + 1} uses {match.Value}");
                    }
                }
            }
        }

        violations.Should().BeEmpty("modules must only depend on each other's Contracts");
    }

    [Fact]
    public void Modules_use_the_standard_folder_layout()
    {
        string[] allowed = ["Contracts", "Api", "Application", "Domain", "Infrastructure", "Templates", "bin", "obj"];
        var modulesDir = Path.Combine(FindBackendRoot(), "Modules");

        var unexpected = Modules
            .SelectMany(module => Directory.GetDirectories(Path.Combine(modulesDir, module))
                .Select(Path.GetFileName)
                .Where(folder => !allowed.Contains(folder))
                .Select(folder => $"{module}/{folder}"))
            .ToList();

        unexpected.Should().BeEmpty("every module is organised as Contracts / Api / Application / Domain / Infrastructure");
    }

    [Fact]
    public void Layers_depend_inward()
    {
        // Domain depends on nothing else in the module; Application never depends on Api.
        var rules = new (string Layer, string[] Forbidden)[]
        {
            ("Domain", ["Api", "Application", "Infrastructure"]),
            ("Application", ["Api"]),
        };

        var violations = new List<string>();
        var modulesDir = Path.Combine(FindBackendRoot(), "Modules");
        foreach (var module in Modules)
        foreach (var (layer, forbidden) in rules)
        {
            var layerDir = Path.Combine(modulesDir, module, layer);
            if (!Directory.Exists(layerDir)) continue;

            var pattern = new Regex($@"NextStep\.Modules\.{module}\.({string.Join("|", forbidden)})\b");
            foreach (var file in Directory.EnumerateFiles(layerDir, "*.cs", SearchOption.AllDirectories))
            {
                var match = pattern.Match(File.ReadAllText(file));
                if (match.Success)
                    violations.Add($"{Path.GetRelativePath(modulesDir, file)} uses {match.Value}");
            }
        }

        violations.Should().BeEmpty("dependencies must point inward: Api → Application → Domain");
    }

    [Fact]
    public void Contracts_expose_only_contract_shared_or_framework_types()
    {
        var contractTypes = typeof(ModuleDbContext).Assembly.GetTypes()
            .Where(t => t.IsPublic && t.Namespace?.EndsWith(".Contracts") == true);

        var violations = new List<string>();
        foreach (var type in contractTypes)
        {
            var exposed = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .SelectMany(ExposedTypes)
                .SelectMany(Flatten)
                .Where(t => t.Namespace?.StartsWith("NextStep.Modules.") == true && !t.Namespace.EndsWith(".Contracts"))
                .Distinct();

            violations.AddRange(exposed.Select(t => $"{type.FullName} exposes {t.FullName}"));
        }

        violations.Should().BeEmpty("contracts must not leak a module's internal types");
    }

    [Theory]
    [MemberData(nameof(ModuleContexts))]
    public void Each_DbContext_maps_only_its_own_module_in_its_own_schema(Type contextType, string module, string schema)
    {
        using var context = CreateContext(contextType);

        context.Schema.Should().Be(schema);
        foreach (var entity in context.Model.GetEntityTypes())
        {
            entity.ClrType.Namespace.Should().StartWith($"NextStep.Modules.{module}.",
                $"{contextType.Name} must not map {entity.ClrType.FullName}");
            entity.GetSchema().Should().Be(schema, $"{entity.ClrType.Name} must live in the \"{schema}\" schema");
        }
    }

    [Theory]
    [MemberData(nameof(ModuleContexts))]
    public void Each_DbContext_has_no_model_changes_missing_from_its_migrations(Type contextType, string module, string schema)
    {
        using var context = CreateContext(contextType);

        context.Database.GetMigrations().Should().NotBeEmpty($"{module} must own its migrations");
        context.Database.HasPendingModelChanges().Should().BeFalse(
            $"run: dotnet ef migrations add <Name> --context {contextType.Name} --output-dir Modules/{module}/Data/Migrations");
    }

    [Fact]
    public async Task Event_publisher_calls_every_handler_and_isolates_failures()
    {
        var calls = new List<string>();
        var failing = new Mock<IIntegrationEventHandler<TestEvent>>();
        failing.Setup(h => h.HandleAsync(It.IsAny<TestEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("failing"))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var working = new Mock<IIntegrationEventHandler<TestEvent>>();
        working.Setup(h => h.HandleAsync(It.IsAny<TestEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("working"))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection()
            .AddSingleton(failing.Object)
            .AddSingleton(working.Object)
            .BuildServiceProvider();
        var publisher = new InProcessEventPublisher(services, NullLogger<InProcessEventPublisher>.Instance);

        var publish = () => publisher.PublishAsync(new TestEvent());

        await publish.Should().NotThrowAsync();
        calls.Should().Equal("failing", "working");
    }

    public sealed record TestEvent : IIntegrationEvent;

    private static ModuleDbContext CreateContext(Type contextType)
    {
        var optionsType = typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType);
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(optionsType)!;
        builder.UseNpgsql("Host=localhost;Database=design_time_only");
        return (ModuleDbContext)Activator.CreateInstance(contextType, builder.Options)!;
    }

    private static IEnumerable<Type> ExposedTypes(MemberInfo member) => member switch
    {
        MethodInfo m when !m.IsSpecialName => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType),
        PropertyInfo p => [p.PropertyType],
        ConstructorInfo c => c.GetParameters().Select(p => p.ParameterType),
        FieldInfo f => [f.FieldType],
        _ => [],
    };

    private static IEnumerable<Type> Flatten(Type type)
    {
        if (type.IsByRef || type.IsArray) return Flatten(type.GetElementType()!);
        if (!type.IsGenericType) return [type];
        return type.GetGenericArguments().SelectMany(Flatten).Prepend(type.GetGenericTypeDefinition());
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}");

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NextStep.csproj")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("NextStep.csproj not found above the test output");
    }
}
