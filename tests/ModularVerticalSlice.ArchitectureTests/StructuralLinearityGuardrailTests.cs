using System.Text.RegularExpressions;

namespace ModularVerticalSlice.ArchitectureTests;

/// <summary>
/// Protects the ratified slice and public-message linearity convention with
/// focused filesystem-level guardrails.
/// </summary>
public sealed partial class StructuralLinearityGuardrailTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string ModulesRoot = Path.Combine(
        RepositoryRoot,
        "src",
        "ModularVerticalSlice.Application",
        "Modules");

    private static readonly HashSet<string> DisallowedTechnicalContainerNames =
    [
        "Command",
        "Commands",
        "Event",
        "Events",
        "Handler",
        "Handlers",
        "Message",
        "Messages",
        "Queries",
        "Query",
        "Validator",
        "Validators"
    ];

    [Fact]
    public void Feature_Slices_Have_Primary_File_Named_After_Their_Folder()
    {
        var violations = GetFeatureSliceDirectories()
            .Where(sliceDirectory =>
            {
                var expectedPrimaryFile = Path.Combine(
                    sliceDirectory.FullName,
                    $"{sliceDirectory.Name}.cs");

                return !File.Exists(expectedPrimaryFile);
            })
            .Select(sliceDirectory => sliceDirectory.FullName)
            .OrderBy(path => path)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"Missing primary slice file '<SliceName>.cs' in: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Active_Module_Code_Does_Not_Declare_Transitional_Alias_Types()
    {
        var fileNameViolations = Directory.GetFiles(ModulesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => TransitionalAliasMarkerRegex().IsMatch(Path.GetFileNameWithoutExtension(path)))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path));

        var typeViolations = Directory.GetFiles(ModulesRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path =>
            {
                var content = File.ReadAllText(path);
                return TransitionalAliasTypeRegex().Matches(content)
                    .Select(match => $"{Path.GetRelativePath(RepositoryRoot, path)}::{match.Groups["name"].Value}");
            });

        var violations = fileNameViolations
            .Concat(typeViolations)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"Transitional alias markers found in active module code: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Public_Query_Slices_Are_Named_For_Their_Use_Case()
    {
        var violations = new List<string>();

        foreach (var sliceDirectory in GetFeatureSliceDirectories())
        {
            foreach (var filePath in Directory.GetFiles(sliceDirectory.FullName, "*.cs", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(filePath);
                foreach (Match match in PublicQueryRecordRegex().Matches(content))
                {
                    var queryTypeName = match.Groups["name"].Value;
                    var expectedSliceName = queryTypeName[..^"Query".Length];
                    if (!string.Equals(expectedSliceName, sliceDirectory.Name, StringComparison.Ordinal))
                    {
                        violations.Add(
                            $"{Path.GetRelativePath(RepositoryRoot, filePath)}::{queryTypeName} -> expected slice '{expectedSliceName}'");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Public query types are hidden in differently named slices: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Features_And_Messages_Do_Not_Use_Generic_Technical_Container_Filenames()
    {
        var candidateRoots = Directory.GetDirectories(ModulesRoot, "*", SearchOption.TopDirectoryOnly)
            .SelectMany(modulePath => new[]
            {
                Path.Combine(modulePath, "Features"),
                Path.Combine(modulePath, "Messages")
            })
            .Where(Directory.Exists);

        var violations = candidateRoots
            .SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(path => DisallowedTechnicalContainerNames.Contains(Path.GetFileNameWithoutExtension(path)))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .OrderBy(path => path)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"Generic technical container filenames are not allowed here: {string.Join(", ", violations)}");
    }

    private static IEnumerable<DirectoryInfo> GetFeatureSliceDirectories() =>
        Directory.GetDirectories(ModulesRoot, "*", SearchOption.TopDirectoryOnly)
            .Select(modulePath => Path.Combine(modulePath, "Features"))
            .Where(Directory.Exists)
            .SelectMany(featuresPath => new DirectoryInfo(featuresPath).GetDirectories());

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "ModularVerticalSlice.Application");
            if (Directory.Exists(candidate))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved from the test output path.");
    }

    [GeneratedRegex(@"\b(?:Alias|Legacy|Compat|Compatibility|Transitional|Temporary)\b", RegexOptions.CultureInvariant)]
    private static partial Regex TransitionalAliasMarkerRegex();

    [GeneratedRegex(@"\b(?:class|record|interface|enum)\s+(?<name>[A-Za-z0-9_]*(?:Alias|Legacy|Compat|Compatibility|Transitional|Temporary)[A-Za-z0-9_]*)\b", RegexOptions.CultureInvariant)]
    private static partial Regex TransitionalAliasTypeRegex();

    [GeneratedRegex(@"public\s+(?:sealed\s+)?record\s+(?<name>[A-Za-z0-9_]+Query)\b", RegexOptions.CultureInvariant)]
    private static partial Regex PublicQueryRecordRegex();
}
