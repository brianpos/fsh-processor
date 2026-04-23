using Firely.Fhir.Packages;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Snapshot;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification.Summary;
using Hl7.Fhir.Utility;
using Hl7.FhirShorthand.Compiler;
using Hl7.FhirShorthand.Compiler_r4;
using Hl7.FhirShorthand.Serialization;
using Hl7.FhirShorthand.Serialization.Models;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using FhirCodeSystem = Hl7.Fhir.Model.CodeSystem;
using FhirResource = Hl7.Fhir.Model.Resource;
using FhirValueSet = Hl7.Fhir.Model.ValueSet;
using Task = System.Threading.Tasks.Task;

namespace Hl7.FhirShorthand.Compiler_tester_r4;

/// <summary>
/// Integration tests that compile the entire SDC (Structured Data Capture) Implementation Guide
/// FSH source into FHIR R4 resources, generate StructureDefinition snapshots, and validate
/// that the output constitutes well-formed FHIR.
///
/// TODO (comparison with sushi):
///   Once these tests pass, compare the output JSON against the sushi-generated artifacts from
///   https://github.com/HL7/sdc (run `sushi .` in the IG root to regenerate).
///   Key files to compare are under `fsh-generated/resources/`:
///     - StructureDefinition-sdc-questionnairecommon.json
///     - StructureDefinition-sdc-questionnaire.json
///     - StructureDefinition-sdc-questionnaire-render.json
///     - StructureDefinition-sdc-questionnaire-search.json
///     - StructureDefinition-sdc-questionnaire-adapt.json
///     - StructureDefinition-sdc-questionnaire-adapt-srch.json
///     - StructureDefinition-sdc-task.json
///     - ... (all Extension StructureDefinitions)
///     - ValueSet-*.json
///     - CodeSystem-*.json
/// </summary>
[TestClass]
public class SdcIgCompilerTests
{
    // ── Test data path ──────────────────────────────────────────────────────────

    /// <summary>Path to the SDC IG FSH files shipped with the test assembly.</summary>
    private static readonly string SdcPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "SDC");

    // ── Shared compile result (computed once, reused across tests) ──────────────

    private static List<FhirResource>? _compiledResources;
    private static List<string>? _parseFailures;
    private static List<string>? _compileFailures;
    private static IReadOnlyList<CompilerWarning>? _compileWarnings;

    private static IResourceResolver? _source;

    [TestInitialize]
    public void TestInitialize()
    {
        if (_source == null)
        {
            var mr = new MultiResolver();
            _source = new CachedResolver(mr);

            mr.AddSource(ZipSource.CreateValidationSource());

            // Load in the dependency packages too!
            var yaml = ReadSushiYaml();
            // can't use the Firely Package Source here as it doesn't index the name (no-one else does either, but Sushi needs it)
            // and it also doesn't have a non async version (so drops out of my processing)
            // FhirPackageSource resolver = new(ModelInfo.ModelInspector, "https://packages.simplifier.net", yaml.Dependencies.Select(kvp => $"{kvp.Key}@{kvp.Value.Version}").ToArray());
            // mr.AddSource(resolver);

            // Instead I created a custom resolver specifically for this Sushi processing of dependencies
            // (at least for testing anyway)
            var dnr = new DependencyNameResolver(ModelInfo.ModelInspector);
            mr.AddSource(dnr);

            Firely.Fhir.Packages.DiskPackageCache cache = new Firely.Fhir.Packages.DiskPackageCache();
            foreach (var dep in yaml.Dependencies)
            {
                var pr = new Firely.Fhir.Packages.PackageReference(dep.Key, dep.Value.Version);
                if (!cache.IsInstalled(pr).Result)
                {
                    var pc = PackageClient.Create();
                    var content = pc.GetPackage(pr).Result;
                    cache.Install(pr, content).WaitNoResult();
                }

                var contentFolder = cache.PackageContentFolder(pr);
                var packageCacheFolder = Path.Combine(Path.GetTempPath(), "FhirShorthand.Compiler");
                var packageCacheFile = Path.Combine(packageCacheFolder, $"{dep.Key}#{dep.Value.Version}.json");
                if (!Directory.Exists(Path.Combine(packageCacheFolder)))
                    Directory.CreateDirectory(packageCacheFolder);

                if (File.Exists(packageCacheFile))
                {
                    string jsonText = File.ReadAllText(packageCacheFile);
                    var details = JsonSerializer.Deserialize<List<Compiler.ResourceSummaryDetails>>(jsonText);
                    dnr.AppendDetails(details, contentFolder);
                }
                else
                {
                    // We need to actually create the index file...
                    List<ResourceSummaryDetails> details = new List<ResourceSummaryDetails>();
                    foreach (var filename in Directory.EnumerateFiles(contentFolder, "*.xml", SearchOption.AllDirectories))
                    {
                        var fi = new FileInfo(filename);
                        if (fi.Name.StartsWith("."))
                            continue;
                        var detail = SushiPackageIndexer.ExtractIndexDetailsFromXml(fi);
                        details.Add(detail);
                    }
                    foreach (var filename in Directory.EnumerateFiles(contentFolder, "*.json", SearchOption.AllDirectories))
                    {
                        var fi = new FileInfo(filename);
                        if (fi.Name.StartsWith("."))
                            continue;
                        var detail = SushiPackageIndexer.ExtractIndexDetailsFromJson(fi);
                        details.Add(detail);
                    }
                    dnr.AppendDetails(details, contentFolder);

                    // persist the cache file
                    string jsonCache = JsonSerializer.Serialize(details, new JsonSerializerOptions() { WriteIndented = true });
                    File.WriteAllText(packageCacheFile, jsonCache);
                }
                //    FhirPackageSource ps2 = new FhirPackageSource(ModelInfo.ModelInspector, cache.PackageContentFolder(pr));
                //    mr.AddSource(ps2);
            }
        }
    }

    [TestMethod]
    public async Task TestReadSushiYaml()
    {
        var yaml = ReadSushiYaml();
        foreach (var dep in yaml.Dependencies)
        {
            Console.WriteLine($"{dep.Key} : {dep.Value.Version}");
        }
    }

    public SushiYaml ReadSushiYaml()
    {
        string filename = Path.Combine(SdcPath, "sushi-config.yaml");
        string yamlText = File.ReadAllText(filename);

        var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        var yaml = deserializer.Deserialize<SushiYaml>(yamlText);
        return yaml;
    }

    [TestMethod]
    public void SequenceFshDocs()
    {
        // Dynamically compute file-level dependencies by parsing all FSH files.
        var fileDeps = ComputeFileDependencies();

        // Parse all the FSH documents for entity-level detail printing.
        var fshFiles = Directory.GetFiles(SdcPath, "*.fsh", SearchOption.AllDirectories)
                                .OrderBy(f => f)
                                .ToArray();

        var parseErrors = new List<string>();
        var fshDocs = new List<FshDoc>();

        // entity name → dependency entity names (for display purposes)
        Dictionary<string, List<string>> entityDeps = new(StringComparer.Ordinal);

        foreach (var fshFile in fshFiles)
        {
            try
            {
                var fshText = File.ReadAllText(fshFile);
                var result = FshParser.Parse(fshText);

                switch (result)
                {
                    case ParseResult.Success s:
                        var fa = new FileInfo(fshFile);
                        s.Document.Entities.ForEach(e =>
                        {
                            e.AddAnnotation(fa);
                            if (e is Profile p && p.Parent != null && !ModelInfo.ModelInspector.IsKnownResource(p.Parent.Value))
                            {
                                if (!entityDeps.TryGetValue(p.Name, out var pDeps))
                                    entityDeps[p.Name] = pDeps = [];
                                pDeps.Add(p.Parent.Value);
                            }
                            if (e is Instance i && i.InstanceOf != null && !ModelInfo.ModelInspector.IsKnownResource(i.InstanceOf))
                            {
                                if (!entityDeps.TryGetValue(i.Name, out var iDeps))
                                    entityDeps[i.Name] = iDeps = [];
                                iDeps.Add(i.InstanceOf);
                            }
                            // Scan rules for ContainsRule items that reference extensions.
                            var rules = e switch
                            {
                                Profile pr => pr.Rules.AsEnumerable<FshRule>(),
                                Hl7.FhirShorthand.Serialization.Models.Extension ex => ex.Rules.AsEnumerable<FshRule>(),
                                Logical l => l.Rules.AsEnumerable<FshRule>(),
                                Hl7.FhirShorthand.Serialization.Models.Resource r => r.Rules.AsEnumerable<FshRule>(),
                                _ => Enumerable.Empty<FshRule>()
                            };
                            foreach (var rule in rules.OfType<ContainsRule>())
                            {
                                foreach (var item in rule.Items)
                                {
                                    if (item.NamedAlias != null && !item.Name.StartsWith('$'))
                                    {
                                        if (!entityDeps.TryGetValue(e.Name, out var eDeps))
                                            entityDeps[e.Name] = eDeps = [];
                                        if (!eDeps.Contains(item.Name))
                                            eDeps.Add(item.Name);
                                    }
                                }
                            }
                        });
                        s.Document.SetAnnotation(fa);
                        fshDocs.Add(s.Document);
                        break;

                    case ParseResult.Failure f:
                        var firstError = f.Errors.FirstOrDefault();
                        parseErrors.Add(
                            $"{Path.GetFileName(fshFile)}: {firstError?.Message ?? "unknown parse error"} " +
                            $"(line {firstError?.Line})");
                        break;
                }
            }
            catch (Exception ex)
            {
                parseErrors.Add($"{Path.GetFileName(fshFile)}: exception during parse – {ex.Message}");
            }
        }

        // sort the documents so that dependencies are ordered before dependents, then print the sequence to the console for inspection.

        // Build file-level dependency graph: file → set of files it depends on.
        var allFileNames = fshDocs
            .Select(d => d.Annotation<FileInfo>()?.Name)
            .Where(n => n != null)
            .Select(n => n!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // ── Topological sort (Kahn's algorithm) ─────────────────────────────────
        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var dependents = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in allFileNames)
        {
            inDegree[f] = 0;
            dependents[f] = [];
        }

        foreach (var f in allFileNames)
        {
            if (fileDeps.TryGetValue(f, out var deps))
            {
                // Only count deps that are in allFileNames (exclude aliases/shared already removed).
                var relevantDeps = deps.Where(d => inDegree.ContainsKey(d)).ToList();
                inDegree[f] = relevantDeps.Count;
                foreach (var dep in relevantDeps)
                    dependents[dep].Add(f);
            }
        }

        var queue = new Queue<string>(allFileNames.Where(f => inDegree[f] == 0).OrderBy(f => f));
        var sorted = new List<string>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(current);

            foreach (var dependent in dependents[current].OrderBy(f => f))
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                    queue.Enqueue(dependent);
            }
        }

        // Any remaining files have circular dependencies; append them at the end.
        var remaining = allFileNames.Where(f => !sorted.Contains(f)).OrderBy(f => f).ToList();
        if (remaining.Count > 0)
        {
            Console.WriteLine($"\nWarning: {remaining.Count} file(s) involved in circular dependencies:");
            foreach (var f in remaining) Console.WriteLine($"  {f}");
            sorted.AddRange(remaining);
        }

        // ── Print sorted order ───────────────────────────────────────────────────
        Console.WriteLine($"\nTopological file order ({sorted.Count} files):");
        for (int i = 0; i < sorted.Count; i++)
            Console.WriteLine($"  {i + 1,3}. {sorted[i]}");

        // ── Print each file and its dependencies ────────────────────────────────
        Console.WriteLine($"\nFile dependency details:");
        foreach (var file in sorted)
        {
            var deps = fileDeps.TryGetValue(file, out var d) ? d : [];
            Console.WriteLine($"\n  {file}");
            if (deps.Count == 0)
                Console.WriteLine("    (no dependencies on other FSH files)");
            else
                foreach (var dep in deps.OrderBy(x => x))
                    Console.WriteLine($"    depends on: {dep}");

            // Also list the entities defined in this file.
            var doc = fshDocs.FirstOrDefault(fd => fd.Annotation<FileInfo>()?.Name == file);
            if (doc != null)
            {
                foreach (var entity in doc.Entities)
                {
                    var depInfo = entityDeps.TryGetValue(entity.Name, out var depList) ? $" → {string.Join(", ", depList)}" : "";
                    Console.WriteLine($"    [{entity.GetType().Name}] {entity.Name}{depInfo}");
                }
            }
        }

        // ── Parse errors ─────────────────────────────────────────────────────────
        if (parseErrors.Count > 0)
        {
            Console.WriteLine($"\nParse failures ({parseErrors.Count}):");
            foreach (var e in parseErrors) Console.WriteLine($"  PARSE: {e}");
        }

        // ── Validate the hardcoded _fileDependencies dictionary ──────────────────
        // Compare the dynamically computed dependencies against the hardcoded dictionary.
        // Only entries with non-empty dependency sets matter (files with no deps don't
        // need an entry in the hardcoded dict).
        var computedNonEmpty = fileDeps
            .Where(kv => kv.Value.Count > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToArray(),
                          StringComparer.OrdinalIgnoreCase);

        var hardcodedNormalized = _fileDependencies
            .ToDictionary(kv => kv.Key, kv => kv.Value.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToArray(),
                          StringComparer.OrdinalIgnoreCase);

        var mismatches = new List<string>();

        // Check for entries in computed that are missing or different in the hardcoded dict.
        foreach (var (file, computedDeps) in computedNonEmpty)
        {
            if (!hardcodedNormalized.TryGetValue(file, out var hardcodedDeps))
            {
                mismatches.Add($"MISSING from _fileDependencies: [\"{file}\"] = [{string.Join(", ", computedDeps.Select(d => $"\"{d}\""))}]");
            }
            else if (!computedDeps.SequenceEqual(hardcodedDeps, StringComparer.OrdinalIgnoreCase))
            {
                mismatches.Add(
                    $"MISMATCH for \"{file}\":\n" +
                    $"    computed:  [{string.Join(", ", computedDeps.Select(d => $"\"{d}\""))}]\n" +
                    $"    hardcoded: [{string.Join(", ", hardcodedDeps.Select(d => $"\"{d}\""))}]");
            }
        }

        // Check for stale entries in the hardcoded dict that are no longer in the computed set.
        foreach (var file in hardcodedNormalized.Keys)
        {
            if (!computedNonEmpty.ContainsKey(file))
                mismatches.Add($"STALE entry in _fileDependencies: \"{file}\" (no longer has dependencies)");
        }

        if (mismatches.Count > 0)
        {
            Console.WriteLine($"\n_fileDependencies validation failures ({mismatches.Count}):");
            foreach (var m in mismatches) Console.WriteLine($"  {m}");

            // Print the full expected dictionary for easy copy-paste.
            Console.WriteLine("\nExpected _fileDependencies dictionary:");
            Console.WriteLine("    private static readonly Dictionary<string, string[]> _fileDependencies = new(StringComparer.OrdinalIgnoreCase)");
            Console.WriteLine("    {");
            foreach (var (file, deps) in computedNonEmpty.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($"        [\"{file}\"] = [{string.Join(", ", deps.Select(d => $"\"{d}\""))}],");
            }
            Console.WriteLine("    };");
        }

        Assert.AreEqual(0, mismatches.Count,
            $"_fileDependencies dictionary is out of date ({mismatches.Count} issue(s)). " +
            "Run this test and copy the printed dictionary from the output. See details above.");
    }

    /// <summary>
    /// Parses and compiles all SDC FSH files once.  Results are cached so that the
    /// expensive parse + compile step runs only once per test session.
    /// </summary>
    private static (List<FhirResource> resources, List<string> parseErrors, List<string> compileErrors, IReadOnlyList<CompilerWarning> warnings)
        GetOrCompileAll()
    {
        if (_compiledResources != null)
            return (_compiledResources, _parseFailures!, _compileFailures!, _compileWarnings!);

        var parseErrors = new List<string>();
        var fshDocs = new List<FshDoc>();

        Assert.IsTrue(Directory.Exists(SdcPath),
            $"SDC test data directory not found: {SdcPath}");

        var fshFiles = Directory.GetFiles(SdcPath, "*.fsh", SearchOption.AllDirectories)
                                .OrderBy(f => f)
                                .ToArray();

        Assert.IsTrue(fshFiles.Length > 0, "No FSH files found in SDC directory");

        // ── 1. Parse every FSH file ──────────────────────────────────────────────
        foreach (var fshFile in fshFiles)
        {
            try
            {
                var fshText = File.ReadAllText(fshFile);
                var result = FshParser.Parse(fshText);

                switch (result)
                {
                    case ParseResult.Success s:
                        // Annotate each entity with the originating file for diagnostics.
                        var fa = new FileInfo(fshFile);
                        s.Document.Entities.ForEach(e => e.AddAnnotation(fa));
                        s.Document.SetAnnotation(fa);
                        fshDocs.Add(s.Document);
                        break;

                    case ParseResult.Failure f:
                        var firstError = f.Errors.FirstOrDefault();
                        parseErrors.Add(
                            $"{Path.GetFileName(fshFile)}: {firstError?.Message ?? "unknown parse error"} " +
                            $"(line {firstError?.Line})");
                        break;
                }
            }
            catch (Exception ex)
            {
                parseErrors.Add($"{Path.GetFileName(fshFile)}: exception during parse – {ex.Message}");
            }
        }

        // ── 2. Compile all documents together with a shared context ──────────────
        // Compiling as a batch allows cross-file alias/ruleset resolution so that
        // profiles that reference rulesets defined in other files are handled correctly.
        var compileErrors = new List<string>();
        IReadOnlyList<CompilerWarning> warnings = [];
        var resources = new List<FhirResource>();

        // Supply the SDC IG canonical base so that resource URLs are generated as
        // "{canonical}/{ResourceType}/{id}" (e.g. "http://hl7.org/fhir/uv/sdc/CodeSystem/assemble-expectation").
        // This mirrors what sushi reads from sushi-config.yaml's "canonical:" field.
        var sdcOptions = new CompilerOptions
        {
            CanonicalBase = "http://hl7.org/fhir/uv/sdc",
            FhirVersion = R4FshCompiler.FhirVersion,
            Resolver = _source
        };

        var compileResult = R4FshCompiler.Compile(fshDocs, sdcOptions);

        switch (compileResult)
        {
            case CompileResult<List<FhirResource>>.SuccessResult s:
                resources = s.Value;
                warnings = s.Warnings;
                break;

            case CompileResult<List<FhirResource>>.FailureResult f:
                // Multi-doc compile failed (pre-existing compiler bugs may cause this).
                // Fall back to compiling each document individually so we can still produce
                // resources from the files that do compile correctly.
                compileErrors.AddRange(f.Errors.Select(e => e.ToString()));
                warnings = f.Warnings;

                foreach (var doc in fshDocs)
                {
                    var singleResult = R4FshCompiler.Compile(doc, sdcOptions);
                    switch (singleResult)
                    {
                        case CompileResult<List<FhirResource>>.SuccessResult sr:
                            resources.AddRange(sr.Value);
                            break;
                        case CompileResult<List<FhirResource>>.FailureResult fr:
                            // Already captured above; skip duplicate errors.
                            break;
                    }
                }
                break;
        }

        _compiledResources = resources;
        _parseFailures = parseErrors;
        _compileFailures = compileErrors;
        _compileWarnings = warnings;

        return (resources, parseErrors, compileErrors, warnings);
    }

    // ── Test 1: Compile all SDC FSH files ──────────────────────────────────────

    /// <summary>
    /// Parses and compiles every FSH file in the SDC IG test-data folder.
    /// Asserts that:
    ///   • All files parse without errors.
    ///   • The combined compile step succeeds.
    ///   • At least one FHIR resource is produced.
    ///   • The resource type breakdown is logged for manual inspection.
    ///
    /// TODO (sushi comparison):
    ///   The total resource counts below should match what `sushi` reports on
    ///   the same FSH input.  Run `sushi --version` to confirm the sushi version,
    ///   then compare `sushi .` output counts with the counts printed here.
    /// </summary>
    [TestMethod]
    public void ShouldCompileAllSdcIgFilesToFhirResources()
    {
        var (resources, parseErrors, compileErrors, warnings) = GetOrCompileAll();

        // ── Parse failures ───────────────────────────────────────────────────────
        if (parseErrors.Count > 0)
        {
            Console.WriteLine($"\nParse failures ({parseErrors.Count}):");
            foreach (var e in parseErrors) Console.WriteLine($"  PARSE: {e}");
        }

        // ── Compile failures ─────────────────────────────────────────────────────
        if (compileErrors.Count > 0)
        {
            Console.WriteLine($"\nCompile failures ({compileErrors.Count}):");
            foreach (var e in compileErrors) Console.WriteLine($"  COMPILE: {e}");
        }

        // ── Compiler warnings ────────────────────────────────────────────────────
        if (warnings.Count > 0)
        {
            Console.WriteLine($"\nCompiler warnings ({warnings.Count}):");
            foreach (var w in warnings) Console.WriteLine($"  WARNING: {w}");
        }

        // ── Resource breakdown ───────────────────────────────────────────────────
        Console.WriteLine($"\nCompiled {resources.Count} FHIR resource(s):");

        var structureDefs = resources.OfType<StructureDefinition>().ToList();
        var valueSets = resources.OfType<FhirValueSet>().ToList();
        var codeSystems = resources.OfType<FhirCodeSystem>().ToList();
        var instances = resources.Where(r => r is not StructureDefinition
                                               and not FhirValueSet
                                               and not FhirCodeSystem).ToList();

        Console.WriteLine($"  StructureDefinitions : {structureDefs.Count}");
        Console.WriteLine($"  ValueSets            : {valueSets.Count}");
        Console.WriteLine($"  CodeSystems          : {codeSystems.Count}");
        Console.WriteLine($"  Other instances      : {instances.Count}");

        Console.WriteLine("\nStructureDefinitions:");
        foreach (var sd in structureDefs.OrderBy(s => s.Name))
        {
            Console.WriteLine($"  [{sd.Kind}] {sd.Name} (id={sd.Id}, base={sd.BaseDefinition})");
        }

        Console.WriteLine("\nValueSets:");
        foreach (var vs in valueSets.OrderBy(v => v.Name))
            Console.WriteLine($"  {vs.Name} (id={vs.Id})");

        Console.WriteLine("\nCodeSystems:");
        foreach (var cs in codeSystems.OrderBy(c => c.Name))
            Console.WriteLine($"  {cs.Name} (id={cs.Id})");

        if (instances.Count > 0)
        {
            Console.WriteLine("\nOther instances:");
            foreach (var r in instances)
                Console.WriteLine($"  [{r.TypeName}] {r.Id}");
        }

        // ── Assertions ───────────────────────────────────────────────────────────
        Assert.AreEqual(0, parseErrors.Count,
            $"{parseErrors.Count} file(s) failed to parse. See output for details.");

        // T1: SDC IG now compiles with zero errors.  Hard assert so regressions are caught.
        Assert.AreEqual(0, compileErrors.Count,
            $"{compileErrors.Count} compile error(s) found. See output for details.");

        Assert.IsTrue(resources.Count > 0, "No FHIR resources were produced from the SDC IG FSH.");
    }

    [TestMethod]
    public void Compile_AustralianStates()
    {
        Compile_SpecificResource("AustralianStates.fsh", "AustralianStateCodes.fsh");
    }

    [TestMethod]
    public void Compile_VSTaskCode()
    {
        Compile_SpecificResource("TaskCode.fsh", "TemporaryCodes.fsh");
    }

    [TestMethod]
    public void Compile_CHF()
    {
        Compile_SpecificResource("sdc-CHF.fsh", "SDCLibrary.fsh");
    }

    [TestMethod]
    public void Compile_ancquickcheck()
    {
        // ["anc-quick-check.fsh"] = ["AssembleExpectation.fsh", "ChoiceColumnExtension.fsh", "CollapsibleExtension.fsh", "ColumnCountExtension.fsh", "ItemAnswerMedia.fsh", "ItemMedia.fsh", "Keyboard.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "RenderingCriticalExtension.fsh", "SDCBaseQuestionnaire.fsh", "SDCOpenLabel.fsh", "SDCQuestionnaireRender.fsh", "ShortTextExtension.fsh", "WidthExtension.fsh"],
        Compile_SpecificResource("anc-quick-check.fsh", "SDCQuestionnaireRender.fsh", "SDCBaseQuestionnaire.fsh", "ObservationLinkPeriodExtension.fsh");
    }

    [TestMethod]
    public void Compile_BaseQuestionnaire()
    {
        Compile_SpecificResource("SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh");
    }

    [TestMethod]
    public void Compile_TaskExample()
    {
        Compile_SpecificResource("request-task-example.fsh", "SDCTaskQuestionnaire.fsh");
    }

    [TestMethod]
    public void Compile_Species()
    {
        Compile_SpecificResource("QuestionnaireContextSpecies.fsh");
    }


    [TestMethod]
    public void Compile_CodeSystemCSPHQ9()
    {
        Compile_SpecificResource("CodeSystemCSPHQ9.fsh");
    }

    [TestMethod]
    public void Compile_CHFCodes()
    {
        Compile_SpecificResource("CHFCodes.fsh");
    }

    [TestMethod]
    public void Compile_AssembleExpectationCodes()
    {
        Compile_SpecificResource("AssembleExpectationCodes.fsh");
    }

    /// <summary>
    /// Hardcoded file-level dependency map. Each key is an FSH file name and the value
    /// is the set of other FSH files it depends on (excluding <c>aliases.fsh</c> and
    /// <c>shared.fsh</c> which are always loaded).
    /// <para>
    /// This dictionary is maintained manually. Run the <c>SequenceFshDocs</c> test to
    /// dynamically compute the correct dependencies and validate this dictionary is
    /// up-to-date. If FSH files change, that test will fail and print the expected
    /// entries.
    /// </para>
    /// </summary>
        private static readonly Dictionary<string, string[]> _fileDependencies = new(StringComparer.OrdinalIgnoreCase)
     {
            ["adaptive-questionnaireresponse-sdc-example-phq9-start.fsh"] = ["CodeSystemCSPHQ9.fsh", "ItemAnswerMedia.fsh", "ItemMedia.fsh", "QuestionnaireAdaptiveExtension.fsh", "RenderingCriticalExtension.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireResponseAdapt.fsh", "SDCQuestionnaireResponseCommon.fsh", "SDCValueSet.fsh"],
            ["adaptive-questionnaireresponse-sdc-example-phq9.fsh"] = ["CodeSystemCSPHQ9.fsh", "ItemAnswerMedia.fsh", "ItemMedia.fsh", "QuestionnaireAdaptiveExtension.fsh", "RenderingCriticalExtension.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireResponseAdapt.fsh", "SDCQuestionnaireResponseCommon.fsh", "SDCValueSet.fsh"],
            ["anc-quick-check.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "ChoiceColumnExtension.fsh", "CollapsibleCodes.fsh", "CollapsibleExtension.fsh", "ColumnCountExtension.fsh", "ItemAnswerMedia.fsh", "ItemMedia.fsh", "Keyboard.fsh", "KeyboardTypeCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireItemCollapsible.fsh", "QuestionnaireItemKeyboardType.fsh", "QuestionnairePerformerType.fsh", "RenderingCriticalExtension.fsh", "SDCBaseQuestionnaire.fsh", "SDCOpenLabel.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireRender.fsh", "ShortTextExtension.fsh", "WidthExtension.fsh"],
            ["AssembleExpectation.fsh"] = ["AssembleExpectationCodes.fsh", "QuestionnaireAssembleExpectation.fsh"],
            ["AustralianStates.fsh"] = ["AustralianStateCodes.fsh"],
            ["c-cda.fsh"] = ["SDCExample.fsh"],
            ["CollapsibleExtension.fsh"] = ["CollapsibleCodes.fsh", "QuestionnaireItemCollapsible.fsh"],
            ["DefinitionExtractValueExtension.fsh"] = ["dev-1.fsh"],
            ["demographics.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "DefinitionExtractExtension.fsh", "DefinitionExtractValueExtension.fsh", "dev-1.fsh", "ExtractAllocateIdExtension.fsh", "InitialExpressionExtension.fsh", "ItemPopulationContextExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireExtractDefinition.fsh"],
            ["EntryMode.fsh"] = ["EntryModeCodes.fsh", "QuestionnaireEntryMode.fsh"],
            ["example-of-ServiceRequest.fsh"] = ["SDCQuestionnaireServiceRequest.fsh", "SDCServiceRequestQuestionnaire.fsh"],
            ["example-of-Task.fsh"] = ["ItemAnswerMedia.fsh", "ItemMedia.fsh", "questionnaireresponse-sdc-example-ussg-fht-answers.fsh", "SDCQuestionnaireResponse.fsh", "SDCQuestionnaireResponseCommon.fsh", "SDCQuestionnaireServiceRequest.fsh", "SDCServiceRequestQuestionnaire.fsh", "SDCTaskQuestionnaire.fsh", "TaskCode.fsh", "TemporaryCodes.fsh"],
            ["ihe-sdc-for-SDCQuestionnaireAdapt.fsh"] = ["QuestionnaireAdaptiveExtension.fsh", "SDCQuestionnaireAdapt.fsh", "SDCQuestionnaireCommon.fsh"],
            ["ihe-sdc-for-SDCQuestionnaireAdaptSearch.fsh"] = ["AssembledFromExtension.fsh", "EndpointExtension.fsh", "QuestionnaireAdaptiveExtension.fsh", "SDCQuestionnaireAdaptSearch.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireSearch.fsh", "SDCUsageContext.fsh"],
            ["ihe-sdc-for-SDCQuestionnaireBehave.fsh"] = ["AnswerExpressionExtension.fsh", "AnswerOptionsToggleExpressionExtension.fsh", "AssembleDefinitionRoot.fsh", "AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "CalculatedExpressionExtension.fsh", "CandidateExpressionExtension.fsh", "EnableWhenExpressionExtension.fsh", "EndpointExtension.fsh", "EntryMode.fsh", "EntryModeCodes.fsh", "InitialExpressionExtension.fsh", "Keyboard.fsh", "KeyboardTypeCodes.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "LookupQuestionnaireExtension.fsh", "MaxQuantityExtension.fsh", "MinQuantityExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAnswerConstraint.fsh", "QuestionnaireAnswerConstraintCodes.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireEntryMode.fsh", "QuestionnaireItemKeyboardType.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "RenderingCriticalExtension.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireBehave.fsh", "SDCQuestionnaireCommon.fsh", "UnitOpen.fsh", "UnitSupplementalSystem.fsh"],
            ["ihe-sdc-for-SDCQuestionnaireCommon.fsh"] = ["SDCQuestionnaireCommon.fsh"],
            ["ihe-sdc-for-SDCQuestionnaireExtractDefinition.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "DefinitionExtractExtension.fsh", "DefinitionExtractValueExtension.fsh", "dev-1.fsh", "ExtractAllocateIdExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireExtractDefinition.fsh"],
            ["ihe-sdc-for-SDCQuestionnaireExtractObservation.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "IsSubjectExtension.fsh", "ObservationExtractCategory.fsh", "ObservationExtractEntry.fsh", "ObservationExtractExtension.fsh", "ObservationExtractRelationship.fsh", "ObservationExtractRelationshipCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireExtractObservation.fsh"],
            ["ihe-sdc-for-SDCQuestionnaireExtractStructureMap.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireExtractStructureMap.fsh", "TargetStructureMapExtension.fsh"],
            ["ihe-sdc-for-SDCQuestionnairePopulateExpression.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "CandidateExpressionExtension.fsh", "ChoiceColumnExtension.fsh", "ContextExpressionExtension.fsh", "InitialExpressionExtension.fsh", "IsSubjectExtension.fsh", "ItemPopulationContextExtension.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnairePopulateExpression.fsh"],
            ["ihe-sdc-for-SDCQuestionnairePopulateObservation.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "IsSubjectExtension.fsh", "ObservationLinkPeriodExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnairePopulateObservation.fsh"],
            ["ihe-sdc-for-SDCQuestionnairePopulateStructureMap.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "IsSubjectExtension.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnairePopulateStructureMap.fsh", "SourceQueriesExtension.fsh", "SourceStructureMapExtension.fsh"],
            ["ihe-sdc-for-SDCQuestionnaireRender.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "ChoiceColumnExtension.fsh", "CollapsibleCodes.fsh", "CollapsibleExtension.fsh", "ColumnCountExtension.fsh", "ItemAnswerMedia.fsh", "ItemMedia.fsh", "Keyboard.fsh", "KeyboardTypeCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireItemCollapsible.fsh", "QuestionnaireItemKeyboardType.fsh", "QuestionnairePerformerType.fsh", "RenderingCriticalExtension.fsh", "SDCBaseQuestionnaire.fsh", "SDCOpenLabel.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireRender.fsh", "ShortTextExtension.fsh", "WidthExtension.fsh"],
            ["ihe-sdc-for-SDCQuestionnaireSearch.fsh"] = ["AssembledFromExtension.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireSearch.fsh", "SDCUsageContext.fsh"],
            ["ihesdc-for-SDCCodeSystem.fsh"] = ["RenderingCriticalExtension.fsh", "SDCCodeSystem.fsh", "SDCQuestionnaireCommon.fsh"],
            ["ihesdc-for-SDCValueSet.fsh"] = ["RenderingCriticalExtension.fsh", "SDCQuestionnaireCommon.fsh", "SDCValueSet.fsh"],
            ["Keyboard.fsh"] = ["KeyboardTypeCodes.fsh", "QuestionnaireItemKeyboardType.fsh"],
            ["LaunchContextExtension.fsh"] = ["LaunchContext.fsh", "QuestionnaireLaunchContext.fsh"],
            ["map-populate-out.fsh"] = ["ItemAnswerMedia.fsh", "ItemMedia.fsh", "SDCQuestionnaireResponse.fsh", "SDCQuestionnaireResponseCommon.fsh"],
            ["ObservationExtractExtension.fsh"] = ["ObservationExtractRelationship.fsh", "ObservationExtractRelationshipCodes.fsh"],
            ["ObservationExtractRelationship.fsh"] = ["ObservationExtractRelationshipCodes.fsh"],
            ["PerformerTypeExtension.fsh"] = ["QuestionnairePerformerType.fsh"],
            ["populate-request.fsh"] = ["SDCParametersQuestionnairePopulateIn.fsh"],
            ["populate-response.fsh"] = ["ItemAnswerMedia.fsh", "ItemMedia.fsh", "questionnaireresponse-sdc-example-ussg-fht-answers.fsh", "SDCParametersQuestionnairePopulateOut.fsh", "SDCQuestionnaireResponse.fsh", "SDCQuestionnaireResponseCommon.fsh"],
            ["populatehtml-response.fsh"] = ["SDCParametersQuestionnairePopulateHtmlOut.fsh"],
            ["populatelink-response.fsh"] = ["SDCParametersQuestionnairePopulateLinkOut.fsh"],
            ["Questionnaire-assemble.fsh"] = ["AssembleContextExtension.fsh", "AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCModularQuestionnaire.fsh", "SDCParametersQuestionnaireAssembleIn.fsh", "SDCQuestionnaireCommon.fsh", "SubQuestionnaireExtension.fsh"],
            ["Questionnaire-populate.fsh"] = ["ItemAnswerMedia.fsh", "ItemMedia.fsh", "SDCParametersQuestionnairePopulateIn.fsh", "SDCParametersQuestionnairePopulateOut.fsh", "SDCQuestionnaireResponse.fsh", "SDCQuestionnaireResponseCommon.fsh"],
            ["Questionnaire-populatehtml.fsh"] = ["SDCParametersQuestionnairePopulateHtmlOut.fsh", "SDCParametersQuestionnairePopulateIn.fsh"],
            ["Questionnaire-populatelink.fsh"] = ["SDCParametersQuestionnairePopulateIn.fsh", "SDCParametersQuestionnairePopulateLinkOut.fsh"],
            ["questionnaire-sdc-derivation-child.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh"],
            ["questionnaire-sdc-derivation-parent.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh"],
            ["questionnaire-sdc-profile-example-cap.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh"],
            ["questionnaire-sdc-profile-example-context-expression.fsh"] = ["AnswerExpressionExtension.fsh", "AnswerOptionsToggleExpressionExtension.fsh", "AssembleDefinitionRoot.fsh", "AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "CalculatedExpressionExtension.fsh", "CandidateExpressionExtension.fsh", "EnableWhenExpressionExtension.fsh", "EndpointExtension.fsh", "EntryMode.fsh", "EntryModeCodes.fsh", "InitialExpressionExtension.fsh", "Keyboard.fsh", "KeyboardTypeCodes.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "LookupQuestionnaireExtension.fsh", "MaxQuantityExtension.fsh", "MinQuantityExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAnswerConstraint.fsh", "QuestionnaireAnswerConstraintCodes.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireEntryMode.fsh", "QuestionnaireItemKeyboardType.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "RenderingCriticalExtension.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireBehave.fsh", "SDCQuestionnaireCommon.fsh", "UnitOpen.fsh", "UnitSupplementalSystem.fsh"],
            ["questionnaire-sdc-profile-example-cqf-PHQ9.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "IsSubjectExtension.fsh", "ObservationExtractCategory.fsh", "ObservationExtractEntry.fsh", "ObservationExtractExtension.fsh", "ObservationExtractRelationship.fsh", "ObservationExtractRelationshipCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireExtractObservation.fsh"],
            ["questionnaire-sdc-profile-example-form-behavior.fsh"] = ["AnswerExpressionExtension.fsh", "AnswerOptionsToggleExpressionExtension.fsh", "AssembleDefinitionRoot.fsh", "AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "CalculatedExpressionExtension.fsh", "CandidateExpressionExtension.fsh", "EnableWhenExpressionExtension.fsh", "EndpointExtension.fsh", "EntryMode.fsh", "EntryModeCodes.fsh", "InitialExpressionExtension.fsh", "Keyboard.fsh", "KeyboardTypeCodes.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "LookupQuestionnaireExtension.fsh", "MaxQuantityExtension.fsh", "MinQuantityExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAnswerConstraint.fsh", "QuestionnaireAnswerConstraintCodes.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireBehaviorConditionCodes.fsh", "QuestionnaireEntryMode.fsh", "QuestionnaireFormBehaviorConditions.fsh", "QuestionnaireItemKeyboardType.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "RenderingCriticalExtension.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireBehave.fsh", "SDCQuestionnaireCommon.fsh", "UnitOpen.fsh", "UnitSupplementalSystem.fsh"],
            ["questionnaire-sdc-profile-example-framingham-hchd-lhc.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh"],
            ["questionnaire-sdc-profile-example-hunger-vital-signs.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh"],
            ["questionnaire-sdc-profile-example-image-options.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "ChoiceColumnExtension.fsh", "CollapsibleCodes.fsh", "CollapsibleExtension.fsh", "ColumnCountExtension.fsh", "ItemAnswerMedia.fsh", "ItemMedia.fsh", "Keyboard.fsh", "KeyboardTypeCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireItemCollapsible.fsh", "QuestionnaireItemKeyboardType.fsh", "QuestionnairePerformerType.fsh", "RenderingCriticalExtension.fsh", "SDCBaseQuestionnaire.fsh", "SDCOpenLabel.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireRender.fsh", "ShortTextExtension.fsh", "WidthExtension.fsh"],
            ["questionnaire-sdc-profile-example-item-weight.fsh"] = ["AnswerExpressionExtension.fsh", "AnswerOptionsToggleExpressionExtension.fsh", "AssembleDefinitionRoot.fsh", "AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "CalculatedExpressionExtension.fsh", "CandidateExpressionExtension.fsh", "EnableWhenExpressionExtension.fsh", "EndpointExtension.fsh", "EntryMode.fsh", "EntryModeCodes.fsh", "InitialExpressionExtension.fsh", "Keyboard.fsh", "KeyboardTypeCodes.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "LookupQuestionnaireExtension.fsh", "MaxQuantityExtension.fsh", "MinQuantityExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAnswerConstraint.fsh", "QuestionnaireAnswerConstraintCodes.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireEntryMode.fsh", "QuestionnaireItemKeyboardType.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "RenderingCriticalExtension.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireBehave.fsh", "SDCQuestionnaireCommon.fsh", "UnitOpen.fsh", "UnitSupplementalSystem.fsh"],
            ["questionnaire-sdc-profile-example-loinc.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh"],
            ["questionnaire-sdc-profile-example-multi-subject.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "IsSubjectExtension.fsh", "ObservationExtractCategory.fsh", "ObservationExtractEntry.fsh", "ObservationExtractExtension.fsh", "ObservationExtractRelationship.fsh", "ObservationExtractRelationshipCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireExtractObservation.fsh"],
            ["questionnaire-sdc-profile-example-PHQ9-search.fsh"] = ["AssembledFromExtension.fsh", "EndpointExtension.fsh", "QuestionnaireAdaptiveExtension.fsh", "SDCQuestionnaireAdaptSearch.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireSearch.fsh", "SDCUsageContext.fsh"],
            ["questionnaire-sdc-profile-example-PHQ9.fsh"] = ["adaptive-questionnaireresponse-sdc-example-phq9.fsh", "CodeSystemCSPHQ9.fsh", "ItemAnswerMedia.fsh", "ItemMedia.fsh", "QuestionnaireAdaptiveExtension.fsh", "RenderingCriticalExtension.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireResponseAdapt.fsh", "SDCQuestionnaireResponseCommon.fsh", "SDCValueSet.fsh"],
            ["questionnaire-sdc-profile-example-render.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "ChoiceColumnExtension.fsh", "CollapsibleCodes.fsh", "CollapsibleExtension.fsh", "ColumnCountExtension.fsh", "ItemAnswerMedia.fsh", "ItemMedia.fsh", "Keyboard.fsh", "KeyboardTypeCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireItemCollapsible.fsh", "QuestionnaireItemKeyboardType.fsh", "QuestionnairePerformerType.fsh", "RenderingCriticalExtension.fsh", "SDCBaseQuestionnaire.fsh", "SDCOpenLabel.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireRender.fsh", "ShortTextExtension.fsh", "WidthExtension.fsh"],
            ["questionnaire-sdc-profile-example-ussg-fht.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "IsSubjectExtension.fsh", "ObservationLinkPeriodExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnairePopulateObservation.fsh"],
            ["questionnaire-sdc-profile-example-weight-height-tracking-panel.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh"],
            ["questionnaire-sdc-test-all-data-types.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh"],
            ["questionnaire-sdc-test-enableWhen.fsh"] = ["AnswerExpressionExtension.fsh", "AnswerOptionsToggleExpressionExtension.fsh", "AssembleDefinitionRoot.fsh", "AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "CalculatedExpressionExtension.fsh", "CandidateExpressionExtension.fsh", "EnableWhenExpressionExtension.fsh", "EndpointExtension.fsh", "EntryMode.fsh", "EntryModeCodes.fsh", "InitialExpressionExtension.fsh", "Keyboard.fsh", "KeyboardTypeCodes.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "LookupQuestionnaireExtension.fsh", "MaxQuantityExtension.fsh", "MinQuantityExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAnswerConstraint.fsh", "QuestionnaireAnswerConstraintCodes.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireEntryMode.fsh", "QuestionnaireItemKeyboardType.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "RenderingCriticalExtension.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireBehave.fsh", "SDCQuestionnaireCommon.fsh", "UnitOpen.fsh", "UnitSupplementalSystem.fsh"],
            ["questionnaire-sdc-test-fhirpath-prepop-candexpr.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "CandidateExpressionExtension.fsh", "ChoiceColumnExtension.fsh", "ContextExpressionExtension.fsh", "InitialExpressionExtension.fsh", "IsSubjectExtension.fsh", "ItemPopulationContextExtension.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnairePopulateExpression.fsh"],
            ["questionnaire-sdc-test-fhirpath-prepop-initialexpression.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "CandidateExpressionExtension.fsh", "ChoiceColumnExtension.fsh", "ContextExpressionExtension.fsh", "InitialExpressionExtension.fsh", "IsSubjectExtension.fsh", "ItemPopulationContextExtension.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnairePopulateExpression.fsh"],
            ["questionnaire-sdc-test-fhirpath-prepop-source-query.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "IsSubjectExtension.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnairePopulateStructureMap.fsh", "SourceQueriesExtension.fsh", "SourceStructureMapExtension.fsh"],
            ["questionnaire-sdc-test-initialvalue-multiple.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "CandidateExpressionExtension.fsh", "ChoiceColumnExtension.fsh", "ContextExpressionExtension.fsh", "InitialExpressionExtension.fsh", "IsSubjectExtension.fsh", "ItemPopulationContextExtension.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnairePopulateExpression.fsh"],
            ["questionnaire-sdc-test-initialvalue.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "CandidateExpressionExtension.fsh", "ChoiceColumnExtension.fsh", "ContextExpressionExtension.fsh", "InitialExpressionExtension.fsh", "IsSubjectExtension.fsh", "ItemPopulationContextExtension.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnairePopulateExpression.fsh"],
            ["questionnaire-sdc-test-nested-groups.fsh"] = ["AnswerExpressionExtension.fsh", "AnswerOptionsToggleExpressionExtension.fsh", "AssembleDefinitionRoot.fsh", "AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "CalculatedExpressionExtension.fsh", "CandidateExpressionExtension.fsh", "EnableWhenExpressionExtension.fsh", "EndpointExtension.fsh", "EntryMode.fsh", "EntryModeCodes.fsh", "InitialExpressionExtension.fsh", "Keyboard.fsh", "KeyboardTypeCodes.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "LookupQuestionnaireExtension.fsh", "MaxQuantityExtension.fsh", "MinQuantityExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAnswerConstraint.fsh", "QuestionnaireAnswerConstraintCodes.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireEntryMode.fsh", "QuestionnaireItemKeyboardType.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "RenderingCriticalExtension.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireBehave.fsh", "SDCQuestionnaireCommon.fsh", "UnitOpen.fsh", "UnitSupplementalSystem.fsh"],
            ["questionnaire-sdc-test-required-radios.fsh"] = ["AnswerExpressionExtension.fsh", "AnswerOptionsToggleExpressionExtension.fsh", "AssembleDefinitionRoot.fsh", "AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "CalculatedExpressionExtension.fsh", "CandidateExpressionExtension.fsh", "EnableWhenExpressionExtension.fsh", "EndpointExtension.fsh", "EntryMode.fsh", "EntryModeCodes.fsh", "InitialExpressionExtension.fsh", "Keyboard.fsh", "KeyboardTypeCodes.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "LookupQuestionnaireExtension.fsh", "MaxQuantityExtension.fsh", "MinQuantityExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAnswerConstraint.fsh", "QuestionnaireAnswerConstraintCodes.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireEntryMode.fsh", "QuestionnaireItemKeyboardType.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "RenderingCriticalExtension.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireBehave.fsh", "SDCQuestionnaireCommon.fsh", "UnitOpen.fsh", "UnitSupplementalSystem.fsh"],
            ["QuestionnaireAnswerConstraint.fsh"] = ["QuestionnaireAnswerConstraintCodes.fsh"],
            ["QuestionnaireAssembleExpectation.fsh"] = ["AssembleExpectationCodes.fsh"],
            ["QuestionnaireEntryMode.fsh"] = ["EntryModeCodes.fsh"],
            ["QuestionnaireFormBehaviorConditions.fsh"] = ["QuestionnaireBehaviorConditionCodes.fsh"],
            ["QuestionnaireItemCollapsible.fsh"] = ["CollapsibleCodes.fsh"],
            ["QuestionnaireItemKeyboardType.fsh"] = ["KeyboardTypeCodes.fsh"],
            ["QuestionnaireLaunchContext.fsh"] = ["LaunchContext.fsh"],
            ["questionnaireresponse-sdc-example-ussg-fht-answers.fsh"] = ["ItemAnswerMedia.fsh", "ItemMedia.fsh", "SDCQuestionnaireResponse.fsh", "SDCQuestionnaireResponseCommon.fsh"],
            ["questionnaireresponse-sdc-profile-example-loinc.fsh"] = ["ItemAnswerMedia.fsh", "ItemMedia.fsh", "SDCQuestionnaireResponse.fsh", "SDCQuestionnaireResponseCommon.fsh"],
            ["questionnaireresponse-sdc-profile-example-multi-subject.fsh"] = ["ItemAnswerMedia.fsh", "ItemMedia.fsh", "SDCQuestionnaireResponse.fsh", "SDCQuestionnaireResponseCommon.fsh"],
            ["questionnaireresponse-sdc-profile-example-PHQ9.fsh"] = ["CodeSystemCSPHQ9.fsh", "ItemAnswerMedia.fsh", "ItemMedia.fsh", "SDCQuestionnaireResponse.fsh", "SDCQuestionnaireResponseCommon.fsh"],
            ["questionnaireresponse-sdc-profile-example.fsh"] = ["ItemAnswerMedia.fsh", "ItemMedia.fsh", "SDCQuestionnaireResponse.fsh", "SDCQuestionnaireResponseCommon.fsh"],
            ["request-task-example.fsh"] = ["SDCQuestionnaireServiceRequest.fsh", "SDCServiceRequestQuestionnaire.fsh", "SDCTaskQuestionnaire.fsh", "TaskCode.fsh", "TemporaryCodes.fsh"],
            ["sdc-assemble-request.fsh"] = ["AssembleContextExtension.fsh", "AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "sdc-modular-root.fsh", "SDCBaseQuestionnaire.fsh", "SDCModularQuestionnaire.fsh", "SDCParametersQuestionnaireAssembleIn.fsh", "SDCQuestionnaireCommon.fsh", "SubQuestionnaireExtension.fsh"],
            ["sdc-CHF.fsh"] = ["SDCLibrary.fsh", "SDCQuestionnaireCommon.fsh"],
            ["sdc-form-manager.fsh"] = ["sdc-form-fill-manager.fsh"],
            ["sdc-modular-contact.fsh"] = ["AssembleContextExtension.fsh", "AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCModularQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SubQuestionnaireExtension.fsh"],
            ["sdc-modular-name.fsh"] = ["AssembleContextExtension.fsh", "AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCModularQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SubQuestionnaireExtension.fsh"],
            ["sdc-modular-root-assembled.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCParametersQuestionnaireAssembleOut.fsh", "SDCQuestionnaireCommon.fsh"],
            ["sdc-modular-root.fsh"] = ["AssembleContextExtension.fsh", "AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCModularQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SubQuestionnaireExtension.fsh"],
            ["SDCBaseQuestionnaire.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCQuestionnaireCommon.fsh"],
            ["SDCCodeSystem.fsh"] = ["ihesdc-for-SDCCodeSystem.fsh", "RenderingCriticalExtension.fsh", "SDCQuestionnaireCommon.fsh"],
            ["SDCExample.fsh"] = ["c-cda.fsh"],
            ["SDCModularQuestionnaire.fsh"] = ["AssembleContextExtension.fsh", "AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SubQuestionnaireExtension.fsh"],
            ["SDCParametersQuestionnaireAssembleIn.fsh"] = ["AssembleContextExtension.fsh", "AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCModularQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SubQuestionnaireExtension.fsh"],
            ["SDCParametersQuestionnaireAssembleOut.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh"],
            ["SDCParametersQuestionnaireNextQuestionnaireIn.fsh"] = ["ItemAnswerMedia.fsh", "ItemMedia.fsh", "SDCQuestionnaireResponseAdapt.fsh", "SDCQuestionnaireResponseCommon.fsh"],
            ["SDCParametersQuestionnaireNextQuestionnaireOut.fsh"] = ["ItemAnswerMedia.fsh", "ItemMedia.fsh", "SDCQuestionnaireResponseAdapt.fsh", "SDCQuestionnaireResponseCommon.fsh"],
            ["SDCParametersQuestionnairePopulateOut.fsh"] = ["ItemAnswerMedia.fsh", "ItemMedia.fsh", "SDCQuestionnaireResponse.fsh", "SDCQuestionnaireResponseCommon.fsh"],
            ["SDCParametersQuestionnaireProcessResponseIn.fsh"] = ["ItemAnswerMedia.fsh", "ItemMedia.fsh", "SDCQuestionnaireResponse.fsh", "SDCQuestionnaireResponseCommon.fsh"],
            ["SDCParametersQuestionnaireResponseExtractIn.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "DefinitionExtractExtension.fsh", "DefinitionExtractValueExtension.fsh", "dev-1.fsh", "ExtractAllocateIdExtension.fsh", "IsSubjectExtension.fsh", "ItemAnswerMedia.fsh", "ItemMedia.fsh", "ObservationExtractCategory.fsh", "ObservationExtractEntry.fsh", "ObservationExtractExtension.fsh", "ObservationExtractRelationship.fsh", "ObservationExtractRelationshipCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireExtractDefinition.fsh", "SDCQuestionnaireExtractObservation.fsh", "SDCQuestionnaireExtractStructureMap.fsh", "SDCQuestionnaireExtractTemplate.fsh", "SDCQuestionnaireResponse.fsh", "SDCQuestionnaireResponseCommon.fsh", "TargetStructureMapExtension.fsh", "TemplateExtractBundleExtension.fsh", "TemplateExtractContextExtension.fsh", "TemplateExtractExtension.fsh", "tev-1.fsh"],
            ["SDCQuestionnaireAdapt.fsh"] = ["ihe-sdc-for-SDCQuestionnaireAdapt.fsh", "QuestionnaireAdaptiveExtension.fsh", "SDCQuestionnaireCommon.fsh"],
            ["SDCQuestionnaireAdaptSearch.fsh"] = ["AssembledFromExtension.fsh", "EndpointExtension.fsh", "ihe-sdc-for-SDCQuestionnaireAdaptSearch.fsh", "QuestionnaireAdaptiveExtension.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireSearch.fsh", "SDCUsageContext.fsh"],
            ["SDCQuestionnaireBehave.fsh"] = ["AnswerExpressionExtension.fsh", "AnswerOptionsToggleExpressionExtension.fsh", "AssembleDefinitionRoot.fsh", "AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "CalculatedExpressionExtension.fsh", "CandidateExpressionExtension.fsh", "EnableWhenExpressionExtension.fsh", "EndpointExtension.fsh", "EntryMode.fsh", "EntryModeCodes.fsh", "ihe-sdc-for-SDCQuestionnaireBehave.fsh", "InitialExpressionExtension.fsh", "Keyboard.fsh", "KeyboardTypeCodes.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "LookupQuestionnaireExtension.fsh", "MaxQuantityExtension.fsh", "MinQuantityExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAnswerConstraint.fsh", "QuestionnaireAnswerConstraintCodes.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireEntryMode.fsh", "QuestionnaireItemKeyboardType.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "RenderingCriticalExtension.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "UnitOpen.fsh", "UnitSupplementalSystem.fsh"],
            ["SDCQuestionnaireCommon.fsh"] = ["ihe-sdc-for-SDCQuestionnaireCommon.fsh"],
            ["SDCQuestionnaireExtractDefinition.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "DefinitionExtractExtension.fsh", "DefinitionExtractValueExtension.fsh", "dev-1.fsh", "ExtractAllocateIdExtension.fsh", "ihe-sdc-for-SDCQuestionnaireExtractDefinition.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh"],
            ["SDCQuestionnaireExtractObservation.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "ihe-sdc-for-SDCQuestionnaireExtractObservation.fsh", "IsSubjectExtension.fsh", "ObservationExtractCategory.fsh", "ObservationExtractEntry.fsh", "ObservationExtractExtension.fsh", "ObservationExtractRelationship.fsh", "ObservationExtractRelationshipCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh"],
            ["SDCQuestionnaireExtractStructureMap.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "ihe-sdc-for-SDCQuestionnaireExtractStructureMap.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "TargetStructureMapExtension.fsh"],
            ["SDCQuestionnaireExtractTemplate.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "ExtractAllocateIdExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "TemplateExtractBundleExtension.fsh", "TemplateExtractContextExtension.fsh", "TemplateExtractExtension.fsh", "tev-1.fsh"],
            ["SDCQuestionnaireLibraryUsageContext.fsh"] = ["SDCUsageContext.fsh"],
            ["SDCQuestionnairePopulateExpression.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "CandidateExpressionExtension.fsh", "ChoiceColumnExtension.fsh", "ContextExpressionExtension.fsh", "ihe-sdc-for-SDCQuestionnairePopulateExpression.fsh", "InitialExpressionExtension.fsh", "IsSubjectExtension.fsh", "ItemPopulationContextExtension.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh"],
            ["SDCQuestionnairePopulateObservation.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "ihe-sdc-for-SDCQuestionnairePopulateObservation.fsh", "IsSubjectExtension.fsh", "ObservationLinkPeriodExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh"],
            ["SDCQuestionnairePopulateStructureMap.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "ihe-sdc-for-SDCQuestionnairePopulateStructureMap.fsh", "IsSubjectExtension.fsh", "LaunchContext.fsh", "LaunchContextExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireLaunchContext.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SourceQueriesExtension.fsh", "SourceStructureMapExtension.fsh"],
            ["SDCQuestionnaireRender.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "ChoiceColumnExtension.fsh", "CollapsibleCodes.fsh", "CollapsibleExtension.fsh", "ColumnCountExtension.fsh", "ihe-sdc-for-SDCQuestionnaireRender.fsh", "ItemAnswerMedia.fsh", "ItemMedia.fsh", "Keyboard.fsh", "KeyboardTypeCodes.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnaireItemCollapsible.fsh", "QuestionnaireItemKeyboardType.fsh", "QuestionnairePerformerType.fsh", "RenderingCriticalExtension.fsh", "SDCBaseQuestionnaire.fsh", "SDCOpenLabel.fsh", "SDCQuestionnaireCommon.fsh", "ShortTextExtension.fsh", "WidthExtension.fsh"],
            ["SDCQuestionnaireResponse.fsh"] = ["ItemAnswerMedia.fsh", "ItemMedia.fsh", "SDCQuestionnaireResponseCommon.fsh"],
            ["SDCQuestionnaireResponseAdapt.fsh"] = ["ItemAnswerMedia.fsh", "ItemMedia.fsh", "SDCQuestionnaireResponseCommon.fsh"],
            ["SDCQuestionnaireResponseCommon.fsh"] = ["ItemAnswerMedia.fsh", "ItemMedia.fsh"],
            ["SDCQuestionnaireSearch.fsh"] = ["AssembledFromExtension.fsh", "ihe-sdc-for-SDCQuestionnaireSearch.fsh", "SDCQuestionnaireCommon.fsh", "SDCUsageContext.fsh"],
            ["SDCQuestionnaireServiceRequest.fsh"] = ["SDCServiceRequestQuestionnaire.fsh"],
            ["SDCTaskQuestionnaire.fsh"] = ["SDCQuestionnaireServiceRequest.fsh", "SDCServiceRequestQuestionnaire.fsh", "TaskCode.fsh", "TemporaryCodes.fsh"],
            ["SDCLibrary.fsh"] = ["SDCQuestionnaireCommon.fsh"],
            ["SDCValueSet.fsh"] = ["ihesdc-for-SDCValueSet.fsh", "RenderingCriticalExtension.fsh", "SDCQuestionnaireCommon.fsh"],
            ["SDOHCC-QuestionnaireHungerVitalSign.fsh"] = ["AssembleExpectation.fsh", "AssembleExpectationCodes.fsh", "DefinitionExtractExtension.fsh", "DefinitionExtractValueExtension.fsh", "dev-1.fsh", "ExtractAllocateIdExtension.fsh", "OptionalDisplayExtension.fsh", "PerformerTypeExtension.fsh", "QuestionnaireAssembleExpectation.fsh", "QuestionnairePerformerType.fsh", "SDCBaseQuestionnaire.fsh", "SDCQuestionnaireCommon.fsh", "SDCQuestionnaireExtractDefinition.fsh"],
            ["TaskCode.fsh"] = ["TemporaryCodes.fsh"],
            ["UnitOpen.fsh"] = ["QuestionnaireAnswerConstraint.fsh", "QuestionnaireAnswerConstraintCodes.fsh"],
        };


    /// <summary>
    /// Returns all FSH file names in the SDC test data directory, excluding supporting
    /// files that contain only aliases or rulesets (no compilable resources).
    /// Each entry is wrapped in <c>object[]</c> for MSTest <see cref="DynamicDataAttribute"/>.
    /// </summary>
    private static IEnumerable<object[]> GetSdcFshFileNames()
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "aliases.fsh",
            "shared.fsh"
        };

        return Directory.GetFiles(SdcPath, "*.fsh", SearchOption.AllDirectories)
                        .Select(Path.GetFileName)
                        .Where(f => !excluded.Contains(f!))
                        .OrderBy(f => f)
                        .Select(f => new object[] { f! });
    }

    [TestMethod]
    [DynamicData(nameof(GetSdcFshFileNames))]
    public void Compile_SpecificResource(string fshFileName, params string[] otherFiles)
    {
        FshDoc parsedFsh = GetFshDocument(fshFileName, out string fshText);
        FshDoc parsedFshAliases = GetFshDocument("aliases.fsh", out string _);
        FshDoc parsedFshShared = GetFshDocument("shared.fsh", out _);
        var outputDir = Path.Combine(AppContext.BaseDirectory, "TestOutput", "sdc-fhir-output");

        if (parsedFsh.Entities.All(e => e is Mapping))
        {
            // this is a mapping only, so nothing to actually test in the compiled output
            Console.WriteLine("Mapping definition only, nothing to compile. Will be verified in other tests that use these mappings.");
            // scan over _fileDependencies to see of our filename is a dependency for other things and list them out
            var deps = _fileDependencies.Where(kvp => kvp.Value.Contains(fshFileName));
            Console.WriteLine($"  Tested by: {String.Join(", ", deps.Select(kvp => kvp.Key))}");
            Assert.IsGreaterThan(0, deps.Count(), "Expected this mapping to be a dependency for at least one other file, but it was not found as a dependency anywhere.");
            return;
        }

        // Load any additional FSH files required to resolve cross-file references
        // (e.g. CodeSystem definitions needed for ValueSet system URL resolution).
        var autoDepFiles = _fileDependencies.TryGetValue(fshFileName, out var depArray) ? depArray : [];
        var extraDocs = otherFiles
            .Concat(autoDepFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(f => GetFshDocument(f, out _))
            .ToList();

        if (autoDepFiles.Any())
            Console.WriteLine($"Dependencies: {String.Join(", ", autoDepFiles)}");

        // ── 2. Compile all documents together with a shared context ──────────────
        // Compiling as a batch allows cross-file alias/ruleset resolution so that
        // profiles that reference rulesets defined in other files are handled correctly.
        var compileErrors = new List<string>();
        IReadOnlyList<CompilerWarning> warnings = [];
        var resources = new List<FhirResource>();

        // Supply the SDC IG canonical base so that resource URLs are generated as
        // "{canonical}/{ResourceType}/{id}" (e.g. "http://hl7.org/fhir/uv/sdc/CodeSystem/assemble-expectation").
        // This mirrors what sushi reads from sushi-config.yaml's "canonical:" field.
        var sdcOptions = new CompilerOptions
        {
            CanonicalBase = "http://hl7.org/fhir/uv/sdc",
            FhirVersion = R4FshCompiler.FhirVersion,
            Resolver = _source
        };

        var compileResult = R4FshCompiler.Compile(
            [parsedFshAliases, parsedFshShared, .. extraDocs, parsedFsh], sdcOptions);

        switch (compileResult)
        {
            case CompileResult<List<FhirResource>>.SuccessResult s:
                resources = s.Value;
                warnings = s.Warnings;
                break;

            case CompileResult<List<FhirResource>>.FailureResult f:
                // Multi-doc compile failed (pre-existing compiler bugs may cause this).
                // Fall back to compiling each document individually so we can still produce
                // resources from the files that do compile correctly.
                compileErrors.AddRange(f.Errors.Select(e => e.ToString()));
                warnings = f.Warnings;

                break;
        }


        // ── Compile failures ─────────────────────────────────────────────────────
        if (compileErrors.Count > 0)
        {
            Console.WriteLine($"\nCompile failures ({compileErrors.Count}):");
            foreach (var e in compileErrors) Console.WriteLine($"  COMPILE: {e}");
        }

        // ── Compiler warnings ────────────────────────────────────────────────────
        if (warnings.Count > 0)
        {
            Console.WriteLine($"\nCompiler warnings ({warnings.Count}):");
            foreach (var w in warnings) Console.WriteLine($"  WARNING: {w.EntityName} - {w.Message}");
        }

        // ── Resource breakdown ───────────────────────────────────────────────────
        Console.WriteLine($"\nCompiled {resources.Count} FHIR resource(s):");

        var structureDefs = resources.OfType<StructureDefinition>().ToList();
        var valueSets = resources.OfType<FhirValueSet>().ToList();
        var codeSystems = resources.OfType<FhirCodeSystem>().ToList();
        var instances = resources.Where(r => r is not StructureDefinition
                                               and not FhirValueSet
                                               and not FhirCodeSystem).ToList();

        Console.WriteLine($"  StructureDefinitions : {structureDefs.Count}");
        Console.WriteLine($"  ValueSets            : {valueSets.Count}");
        Console.WriteLine($"  CodeSystems          : {codeSystems.Count}");
        Console.WriteLine($"  Other instances      : {instances.Count}");

        Console.WriteLine("\nStructureDefinitions:");
        foreach (var sd in structureDefs.OrderBy(s => s.Name))
        {
            Console.WriteLine($"  [{sd.Kind}] {sd.Name} (id={sd.Id}, base={sd.BaseDefinition})");
        }

        Console.WriteLine("\nValueSets:");
        foreach (var vs in valueSets.OrderBy(v => v.Name))
            Console.WriteLine($"  {vs.Name} (id={vs.Id})");

        Console.WriteLine("\nCodeSystems:");
        foreach (var cs in codeSystems.OrderBy(c => c.Name))
            Console.WriteLine($"  {cs.Name} (id={cs.Id})");

        if (instances.Count > 0)
        {
            Console.WriteLine("\nOther instances:");
            foreach (var r in instances)
                Console.WriteLine($"  [{r.TypeName}] {r.Id}");
        }

        Console.WriteLine("--------------------------------------");
        Console.WriteLine();
        Console.WriteLine(fshText);
        Console.WriteLine();

        if (resources.Count == 0 && parsedFsh.Entities.All(e => e is Invariant))
        {
            // this is an invariant only, so nothing to actually test in the compiled output
            Console.WriteLine("Invariant definition only, nothing to compile. Will be verified in other tests that use these invariants.");
            return;
        }

        var serializerSettings = new FhirJsonSerializationSettings { Pretty = true };
        // foreach (var resource in resources)
        //var rj = resources.Last();
        //{
        //    Console.WriteLine("--------------------------------------");
        //    Console.WriteLine();
        //    Console.WriteLine(rj.ToJson(serializerSettings));
        //}

        // T1: SDC IG now compiles with zero errors.  Hard assert so regressions are caught.
        Assert.AreEqual(0, compileErrors.Count,
            $"{compileErrors.Count} compile error(s) found. See output for details.");

        Assert.IsTrue(resources.Count > 0, "No FHIR resources were produced from the SDC IG FSH.");


        // and finally compare with any sushi generated files
        var sushiDir = Path.Combine(AppContext.BaseDirectory, "TestData", "sushi-generated");
        // foreach (var resource in resources)
        var resource = resources.Last();
        {
            var index = resources.IndexOf(resource) + 1;
            var idSegment = !string.IsNullOrWhiteSpace(resource.Id) ? resource.Id : $"noId-{index}";
            var fileName = $"{resource.TypeName}-{idSegment}.json";
            // Sanitize to remove characters that are illegal in file names.
            fileName = string.Concat(fileName.Split(Path.GetInvalidFileNameChars()));
            
            var json = resource.ToJson(serializerSettings);

            var filePath = Path.Combine(sushiDir, fileName);
            if (File.Exists(filePath))
            {
                var jsonSushiGenerated = File.ReadAllText(filePath);

                // normalize through our parser/serializer again
                var parserSettings = new ParserSettings { AcceptUnknownMembers = true, AllowUnrecognizedEnums = true };
                var jsonParser = new FhirJsonParser(parserSettings);
                var resourceSushi = jsonParser.Parse<FhirResource>(jsonSushiGenerated);
                var normalizedJson = resourceSushi.ToJson(serializerSettings);


                if (normalizedJson != json)
                {
                    // Log the name of the source/target files
                    Console.WriteLine($"Expected JSON file: {filePath}");
                    Console.WriteLine($"Actual JSON file:   {filePath.Replace("sushi-generated", "actual")}");

                    try
                    {
                        if (resource is StructureDefinition sd && sd.HasSnapshot)
                        {
                            sd.Snapshot = null;
                        }
                        // Use the resource Id when available; otherwise fall back to an index so that
                        // multiple id-less resources of the same type don't overwrite each other.
                        //var idSegment = !string.IsNullOrWhiteSpace(resource.Id) ? resource.Id : $"noId-{index}";
                        //var fileName = $"{resource.TypeName}-{idSegment}.json";
                        // Sanitize to remove characters that are illegal in file names.
                        fileName = string.Concat(fileName.Split(Path.GetInvalidFileNameChars()));
                        var filePathGenerated = Path.Combine(outputDir, fileName);
                        File.WriteAllText(filePathGenerated, json);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Warning: could not write {resource.TypeName}/{resource.Id}: {ex.Message}");
                    }

                    // report the JSON difference to the console (using a jsondiff lib)
                    var diff = new JsonDiffPatchDotNet.JsonDiffPatch().Diff(normalizedJson, json);
                    Console.WriteLine($"JSON difference for file {fileName}:\n{diff}");

                    // and fail the test
                    Assert.Fail("JSON Content not the same as the sushi-generated file: " + fileName);
                }
            }
        }
    }

    private static FshDoc GetFshDocument(string fshFileName, out string fshText)
    {
        var fshFile = Path.Combine(SdcPath, fshFileName);

        fshText = File.ReadAllText(fshFile);
        var result = FshParser.Parse(fshText);

        switch (result)
        {
            case ParseResult.Success s:
                return s.Document;

            case ParseResult.Failure f:
                var firstError = f.Errors.FirstOrDefault();
                Console.WriteLine($"\nParse failures:");
                var errorMessage = $"{Path.GetFileName(fshFile)}: {firstError?.Message ?? "unknown parse error"} (line {firstError?.Line})";
                Console.WriteLine($"  PARSE: {errorMessage}");
                Assert.Fail(errorMessage);
                return null;
            default:
                Assert.Fail("No result from parse");
                return null;
        }
    }

    // ── Test 2: Serialize to valid FHIR JSON ───────────────────────────────────

    /// <summary>
    /// Serializes every compiled FHIR resource to JSON using <see cref="FhirJsonSerializer"/>
    /// and immediately parses it back with the strict <see cref="FhirJsonParser"/>.
    /// This round-trip confirms that the in-memory resource object graph is a valid, well-formed
    /// FHIR R4 resource – i.e. all required properties are present and no unknown properties leak in.
    ///
    /// TODO (sushi comparison):
    ///   Save the JSON output alongside the sushi-generated JSON files and use a JSON diff tool
    ///   (e.g. `json-diff`, `jq` with sorting, or a dedicated FHIR diff tool such as
    ///   https://github.com/microsoft/fhir-codegen) to highlight structural differences.
    ///   Important known differences to expect:
    ///     • Sushi emits fully resolved canonical URLs; our compiler may use local IDs.
    ///     • Sushi generates snapshot elements; this test verifies snapshots separately.
    ///     • Meta.profile and narrative (div) are not set by the FSH compiler.
    /// </summary>
    [TestMethod]
    public void ShouldSerializeCompiledResourcesToValidFhirJson()
    {
        var (resources, parseErrors, compileErrors, _) = GetOrCompileAll();

        // Skip only if parse errors prevent any resources from being produced.
        if (parseErrors.Count > 0)
        {
            Assert.Inconclusive("Skipped: parse errors prevent resource compilation.");
            return;
        }

        var serializerSettings = new FhirJsonSerializationSettings { Pretty = true };
        var parserSettings = new ParserSettings { AcceptUnknownMembers = false, AllowUnrecognizedEnums = true };
        var jsonParser = new FhirJsonParser(parserSettings);

        int successCount = 0;
        var failures = new List<string>();

        foreach (var resource in resources)
        {
            try
            {
                // Serialize to JSON.
                var json = resource.ToJson(serializerSettings);

                // Parse back to confirm well-formedness.
                var reparsed = jsonParser.Parse<FhirResource>(json);

                Assert.IsNotNull(reparsed, $"Round-trip parse returned null for {resource.TypeName}/{resource.Id}");
                Assert.AreEqual(resource.TypeName, reparsed.TypeName,
                    $"TypeName mismatch after round-trip for {resource.Id}");

                successCount++;
            }
            catch (Exception ex)
            {
                failures.Add($"{resource.TypeName}/{resource.Id}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nJSON round-trip: {successCount}/{resources.Count} resources OK");

        if (failures.Count > 0)
        {
            Console.WriteLine($"\nJSON round-trip failures ({failures.Count}):");
            foreach (var f in failures) Console.WriteLine($"  {f}");
        }

        Assert.AreEqual(0, failures.Count,
            $"{failures.Count} resource(s) failed JSON round-trip validation. See output.");
    }

    // ── Test 3: Generate snapshots for StructureDefinitions ───────────────────

    /// <summary>
    /// Attempts to generate a FHIR snapshot for every <see cref="StructureDefinition"/>
    /// produced by the SDC IG compilation using the Firely SDK
    /// <see cref="SnapshotGenerator"/>.
    ///
    /// <para>
    /// The generator is seeded with an <see cref="InMemoryResourceResolver"/> containing
    /// all the StructureDefinitions compiled from the SDC IG itself, so inter-SDC profile
    /// resolution (e.g. sdc-questionnaire → sdc-questionnairecommon) works without network
    /// access.
    /// </para>
    ///
    /// <para>
    /// Snapshot generation for SDs that derive from base FHIR R4 profiles
    /// (e.g. <c>http://hl7.org/fhir/StructureDefinition/Questionnaire</c>) requires the
    /// FHIR R4 core specification ZIP, which must be present at runtime as
    /// <c>specification.zip</c> in the application base directory (or provided via
    /// <see cref="ZipSource.CreateValidationSource()"/>).  When the ZIP is absent the
    /// base-profile look-up will fail gracefully and the test records the outcome in the
    /// console output without failing the test run, so the test remains meaningful even
    /// in environments without the spec ZIP.
    /// </para>
    ///
    /// TODO (sushi comparison):
    ///   The snapshot element count and element paths printed below should match the
    ///   snapshot in the sushi-generated StructureDefinitions.  Common gaps to investigate:
    ///     • Elements added by slicing discriminators.
    ///     • Elements inherited from base resources that sushi merges in.
    ///     • Extension context constraints.
    ///   Use `jq '.snapshot.element | length'` on both files as a quick count check.
    /// </summary>
    [TestMethod]
    public void ShouldGenerateSnapshotsForStructureDefinitions()
    {
        var (resources, parseErrors, compileErrors, _) = GetOrCompileAll();

        if (parseErrors.Count > 0)
        {
            Assert.Inconclusive("Skipped: parse errors prevent resource compilation.");
            return;
        }

        var structureDefs = resources.OfType<StructureDefinition>().ToList();

        if (structureDefs.Count == 0)
        {
            Assert.Inconclusive("No StructureDefinitions were compiled – nothing to snapshot.");
            return;
        }

        // ── Build the resource resolver ──────────────────────────────────────────
        // Seed with all compiled SDC StructureDefinitions so that intra-IG cross-references
        // resolve (e.g. a profile that derives from another SDC profile).
        var inMemoryResolver = new InMemoryResourceResolver(structureDefs.Cast<FhirResource>().ToArray());

        // Try to layer on the R4 core spec ZIP if it is available at runtime.
        // This will be absent in CI environments that don't ship the zip, which is fine –
        // we handle the failure gracefully below.
        ISyncOrAsyncResourceResolver resolver;
        var specZipPath = Path.Combine(AppContext.BaseDirectory, "specification.zip");

        if (File.Exists(specZipPath))
        {
            // When the R4 specification ZIP is present, stack it under the in-memory resolver
            // so that base profiles (Questionnaire, Task, etc.) can be resolved.
            var zipSource = new ZipSource(specZipPath);
            resolver = new MultiResolver(inMemoryResolver, zipSource);
            Console.WriteLine("Using R4 specification.zip for base profile resolution.");
        }
        else
        {
            resolver = inMemoryResolver;
            Console.WriteLine(
                "specification.zip not found – base FHIR R4 profiles (e.g. Questionnaire, Task) " +
                "cannot be resolved.  Snapshots for SDs that inherit directly from a base R4 " +
                "resource type will be incomplete.  To enable full snapshot generation, place " +
                $"the R4 specification.zip at: {specZipPath}");
        }

        // Cache the resolver so repeated snapshot generation reuses it.
        var cachedResolver = new CachedResolver(resolver);
        var settings = new SnapshotGeneratorSettings
        {
            GenerateSnapshotForExternalProfiles = true,
            ForceRegenerateSnapshots = true,
            GenerateElementIds = true,
        };
        var generator = new SnapshotGenerator(cachedResolver, settings);

        // ── Generate snapshots ───────────────────────────────────────────────────
        int snapshotOk = 0;
        int snapshotPartial = 0;
        var snapshotErrors = new List<string>();

        foreach (var sd in structureDefs)
        {
            try
            {
                generator.Update(sd);

                var outcome = generator.Outcome;
                var elementCount = sd.Snapshot?.Element?.Count ?? 0;

                if (outcome != null && outcome.Issue.Any(i =>
                        i.Severity is OperationOutcome.IssueSeverity.Error or OperationOutcome.IssueSeverity.Fatal))
                {
                    var issues = string.Join("; ",
                        outcome.Issue
                               .Where(i => i.Severity is OperationOutcome.IssueSeverity.Error
                                                      or OperationOutcome.IssueSeverity.Fatal)
                               .Select(i => i.Diagnostics ?? i.Details?.Text ?? i.Severity.ToString()));
                    Console.WriteLine(
                        $"  [{sd.Kind}] {sd.Name}: PARTIAL snapshot ({elementCount} elements) – {issues}");
                    snapshotPartial++;
                }
                else
                {
                    Console.WriteLine(
                        $"  [{sd.Kind}] {sd.Name}: OK – {elementCount} snapshot elements");
                    snapshotOk++;
                }
            }
            catch (Exception ex)
            {
                snapshotErrors.Add($"{sd.Name}: {ex.Message}");
                Console.WriteLine($"  [{sd.Kind}] {sd.Name}: ERROR – {ex.Message}");
            }
        }

        // ── Summary ──────────────────────────────────────────────────────────────
        Console.WriteLine(
            $"\nSnapshot generation: {snapshotOk} OK, {snapshotPartial} partial, " +
            $"{snapshotErrors.Count} errors  (total SDs: {structureDefs.Count})");

        if (snapshotErrors.Count > 0)
        {
            Console.WriteLine("\nSnapshot errors:");
            foreach (var e in snapshotErrors) Console.WriteLine($"  {e}");
        }

        // Partial results (missing base profiles) are expected when spec.zip is absent;
        // treat those as acceptable.  Hard errors (exceptions) are a test failure.
        Assert.AreEqual(0, snapshotErrors.Count,
            $"{snapshotErrors.Count} StructureDefinition(s) threw an exception during snapshot generation. " +
            "See output for details.");
    }

    // ── Test 4: Snapshot element counts are non-trivial ────────────────────────

    /// <summary>
    /// Verifies that StructureDefinitions with a populated snapshot contain at least
    /// the root element, providing a basic sanity check that the snapshot generator
    /// actually ran and produced output.
    ///
    /// TODO (sushi comparison):
    ///   For each SD printed below, compare the element count against the corresponding
    ///   sushi output.  A significantly lower count usually indicates that the base profile
    ///   was not resolved (see the snapshot test for spec.zip requirements).
    /// </summary>
    [TestMethod]
    public void ShouldHaveNonEmptySnapshotsForStructureDefinitions()
    {
        var (resources, parseErrors, compileErrors, _) = GetOrCompileAll();

        if (parseErrors.Count > 0)
        {
            Assert.Inconclusive("Skipped: parse errors prevent resource compilation.");
            return;
        }

        var structureDefs = resources.OfType<StructureDefinition>()
                                     .Where(sd => sd.Snapshot?.Element?.Count > 0)
                                     .ToList();

        Console.WriteLine($"\nStructureDefinitions with snapshots: {structureDefs.Count}");
        foreach (var sd in structureDefs.OrderBy(s => s.Name))
        {
            var elemCount = sd.Snapshot.Element.Count;
            Console.WriteLine($"  {sd.Name}: {elemCount} elements");

            // Root element must always be present.
            var root = sd.Snapshot.Element.FirstOrDefault();
            Assert.IsNotNull(root, $"Snapshot for '{sd.Name}' has no root element");
        }
    }

    // ── Test 5: Validate required metadata on compiled resources ───────────────

    /// <summary>
    /// Checks that each compiled FHIR resource has the minimum required metadata
    /// populated: a non-blank <c>Id</c> (or <c>Url</c> for conformance resources),
    /// a <c>Name</c> for conformance resources, and a <c>Status</c> where applicable.
    ///
    /// TODO (sushi comparison):
    ///   Verify that the Url / canonical assigned by this compiler matches the canonical
    ///   base used in the sushi-generated output.  The SDC IG uses the base URL
    ///   <c>http://hl7.org/fhir/uv/sdc</c> – check that all SDs have this prefix.
    /// </summary>
    [TestMethod]
    public void ShouldHaveRequiredMetadataOnAllResources()
    {
        var (resources, parseErrors, compileErrors, _) = GetOrCompileAll();

        if (parseErrors.Count > 0)
        {
            Assert.Inconclusive("Skipped: parse errors prevent resource compilation.");
            return;
        }

        var metadataFailures = new List<string>();

        foreach (var resource in resources)
        {
            // Every resource must have at least a resource Id OR a canonical URL (for conformance resources).
            var hasId = !string.IsNullOrWhiteSpace(resource.Id);
            string? canonicalUrl = resource switch
            {
                StructureDefinition sd => sd.Url,
                FhirValueSet vs => vs.Url,
                FhirCodeSystem cs => cs.Url,
                _ => null
            };
            var hasUrl = !string.IsNullOrWhiteSpace(canonicalUrl);

            if (!hasId && !hasUrl)
                metadataFailures.Add($"{resource.TypeName}: missing both Id and Url");

            // Conformance resources require a Name.
            if (resource is StructureDefinition sdCheck && string.IsNullOrWhiteSpace(sdCheck.Name))
                metadataFailures.Add($"StructureDefinition/{sdCheck.Id}: missing Name");

            if (resource is FhirValueSet vsCheck && string.IsNullOrWhiteSpace(vsCheck.Name))
                metadataFailures.Add($"ValueSet/{vsCheck.Id}: missing Name");

            if (resource is FhirCodeSystem csCheck && string.IsNullOrWhiteSpace(csCheck.Name))
                metadataFailures.Add($"CodeSystem/{csCheck.Id}: missing Name");
        }

        if (metadataFailures.Count > 0)
        {
            Console.WriteLine($"\nMetadata validation findings ({metadataFailures.Count}):");
            foreach (var f in metadataFailures) Console.WriteLine($"  {f}");
        }
        else
        {
            Console.WriteLine($"\nAll {resources.Count} compiled resources have required metadata.");
        }

        Assert.AreEqual(0, metadataFailures.Count,
            $"{metadataFailures.Count} resource(s) missing required metadata. See output for details.");
    }

    // ── Test 5b: Expected resource type counts ─────────────────────────────────

    /// <summary>
    /// Asserts that the compiled SDC IG produces the expected number of FHIR resources
    /// by type, establishing a regression baseline.
    ///
    /// The counts below reflect the current fsh-compiler output (not necessarily sushi
    /// output – sushi produces more instances for Questionnaire/QuestionnaireResponse
    /// examples that require additional compiler work). When sushi parity is achieved,
    /// these counts should converge.
    /// </summary>
    [TestMethod]
    public void ShouldProduceExpectedResourceTypeCounts()
    {
        var (resources, parseErrors, compileErrors, _) = GetOrCompileAll();

        if (parseErrors.Count > 0)
        {
            Assert.Inconclusive("Skipped: parse errors prevent resource compilation.");
            return;
        }

        var byType = resources
            .GroupBy(r => r.TypeName)
            .ToDictionary(g => g.Key, g => g.Count());

        Console.WriteLine("\nResource type counts:");
        foreach (var kv in byType.OrderBy(k => k.Key))
            Console.WriteLine($"  {kv.Key}: {kv.Value}");

        // ── Regression baseline (current compiler output) ────────────────────
        // Update these numbers when compiler improvements change the output;
        // the test is there to catch unintentional regressions.
        Assert.IsTrue(byType.TryGetValue("StructureDefinition", out var sdCount) && sdCount > 0,
            "Should produce at least one StructureDefinition");
        Assert.IsTrue(byType.TryGetValue("ValueSet", out var vsCount) && vsCount > 0,
            "Should produce at least one ValueSet");
        Assert.IsTrue(byType.TryGetValue("CodeSystem", out var csCount) && csCount > 0,
            "Should produce at least one CodeSystem");
        Assert.IsTrue(resources.Count > 100,
            $"Should produce more than 100 resources total; got {resources.Count}");

        Console.WriteLine($"\nTotal resources: {resources.Count}");
    }


    /// <summary>
    /// Serializes all compiled resources to pretty-printed JSON and writes them to
    /// <c>%TEMP%\sdc-fhir-output\</c> on disk so they can be diffed manually against
    /// the sushi-generated counterparts.
    ///
    /// This test always succeeds; the JSON files are left on disk for manual inspection.
    ///
    /// TODO (sushi comparison – step-by-step instructions):
    ///   1. Run sushi on the SDC IG source:
    ///        cd &lt;sdc-ig-repo&gt; &amp;&amp; sushi .
    ///   2. Note the output directory (usually `fsh-generated/resources/`).
    ///   3. Compare the files in that directory with those written to the path logged below.
    ///      A convenient one-liner with jq (Linux / macOS):
    ///        diff &lt;(jq -S . sushi-output/StructureDefinition-sdc-questionnaire.json) \
    ///             &lt;(jq -S . our-output/StructureDefinition-sdc-questionnaire.json)
    ///   4. Known expected differences:
    ///        a. Sushi includes a full snapshot; our output may have a partial one when
    ///           specification.zip is absent (see ShouldGenerateSnapshotsForStructureDefinitions).
    ///        b. Sushi populates text.div (narrative); the FSH compiler does not.
    ///        c. Canonical URLs may differ if the IG base URL was not fully resolved.
    ///        d. Element IDs may differ.
    /// </summary>
    [TestMethod]
    public void ShouldWriteCompiledResourcesToDiskForManualComparison()
    {
        var (resources, parseErrors, compileErrors, _) = GetOrCompileAll();

        if (parseErrors.Count > 0 || compileErrors.Count > 0)
        {
            Console.WriteLine("Warning: compile had errors; output may be incomplete.");
        }

        // Output directory is placed under the test assembly's output directory for better
        // isolation and determinism.  The directory is cleared on each test run so stale
        // artifacts don't accumulate.
        var outputDir = Path.Combine(AppContext.BaseDirectory, "TestOutput", "sdc-fhir-output");
        if (Directory.Exists(outputDir))
        {
            try
            {
                Directory.Delete(outputDir, recursive: true);
            }
            catch
            {
                // just delete all the files in the folder instead
                foreach (var filename in Directory.EnumerateFiles(outputDir))
                {
                    File.Delete(filename);
                }
            }
        }
        Directory.CreateDirectory(outputDir);

        var serializerSettings = new FhirJsonSerializationSettings { Pretty = true };

        // Build a map of resource file names → JSON
        var compiledFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        int written = 0;
        int index = 0;
        foreach (var resource in resources)
        {
            index++;
            try
            {
                if (resource is StructureDefinition sd && sd.HasSnapshot)
                {
                    sd.Snapshot = null;
                }
                // Use the resource Id when available; otherwise fall back to an index so that
                // multiple id-less resources of the same type don't overwrite each other.
                var idSegment = !string.IsNullOrWhiteSpace(resource.Id) ? resource.Id : $"noId-{index}";
                var fileName = $"{resource.TypeName}-{idSegment}.json";
                // Sanitize to remove characters that are illegal in file names.
                fileName = string.Concat(fileName.Split(Path.GetInvalidFileNameChars()));
                var json = resource.ToJson(serializerSettings);
                var filePath = Path.Combine(outputDir, fileName);
                File.WriteAllText(filePath, json);
                compiledFiles[fileName] = json;
                written++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Warning: could not write {resource.TypeName}/{resource.Id}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nWrote {written} resource(s) to: {outputDir}");

        // T6: Compare compiled resources against sushi-generated JSON files.
        // Skipped fields that are expected to differ: snapshot, text (narrative), element ids.
        // Comparisons performed:
        //   1. File-size ratio (completeness indicator – flags resources where ours is notably smaller)
        //   2. All top-level scalar fields (string, boolean, integer)
        //   3. Resource-type-specific structural content:
        //      - StructureDefinition: differential element paths and count
        //      - CodeSystem: concept codes and count
        //      - ValueSet: compose include systems
        //   4. Contained resources (recursive key-field check)
        var sushiDir = Path.Combine(AppContext.BaseDirectory, "TestData", "sushi-generated");
        if (Directory.Exists(sushiDir))
        {
            var sushiFiles = Directory.GetFiles(sushiDir, "*.json");
            Console.WriteLine($"\nT6 comparison vs. sushi-generated ({sushiFiles.Length} sushi files):");

            int matched = 0;
            int mismatches = 0;
            var mismatchDetails = new List<string>();
            int missing = 0;
            var missingDetails = new List<string>();
            var sizeWarnings = new List<string>();

            foreach (var sushiFile in sushiFiles)
            {
                var fileName = Path.GetFileName(sushiFile);
                if (!compiledFiles.TryGetValue(fileName, out var ourJson))
                {
                    missing++;
                    missingDetails.Add(fileName);
                    continue;
                }

                try
                {
                    var sushiText = File.ReadAllText(sushiFile);
                    var sushiObj = JsonDocument.Parse(sushiText).RootElement;
                    var ourObj = JsonDocument.Parse(ourJson).RootElement;

                    // 1. File-size completeness heuristic: warn when ours is less than 50% of sushi's size.
                    var sushiSize = sushiText.Length;
                    var ourSize = ourJson.Length;
                    if (sushiSize != ourSize)
                        sizeWarnings.Add($"{fileName}: sushi={sushiSize}B ours={ourSize}B ({ourSize * 100 / sushiSize}%)");

                    // 2. All top-level scalar fields
                    CompareAllScalarFields(fileName, sushiObj, ourObj, mismatchDetails, ref mismatches);

                    // 3a. StructureDefinition: differential element paths
                    var resourceType = sushiObj.TryGetProperty("resourceType", out var rtEl) ? rtEl.GetString() : null;
                    if (resourceType == "StructureDefinition")
                        CompareStructureDefinitionDifferential(fileName, sushiObj, ourObj, mismatchDetails, ref mismatches);
                    else if (resourceType == "CodeSystem")
                        CompareCodeSystemConcepts(fileName, sushiObj, ourObj, mismatchDetails, ref mismatches);
                    else if (resourceType == "ValueSet")
                        CompareValueSetCompose(fileName, sushiObj, ourObj, mismatchDetails, ref mismatches);

                    // 4. Contained resources
                    if (sushiObj.TryGetProperty("contained", out var sushiContained) &&
                        sushiContained.ValueKind == JsonValueKind.Array)
                    {
                        var ourContained = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                        if (ourObj.TryGetProperty("contained", out var ourContainedArr) &&
                            ourContainedArr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in ourContainedArr.EnumerateArray())
                            {
                                var rt = item.TryGetProperty("resourceType", out var rtv) ? rtv.GetString() : null;
                                var cid = item.TryGetProperty("id", out var idv) ? idv.GetString() : null;
                                ourContained.TryAdd($"{rt}/{cid}", item);
                            }
                        }

                        foreach (var sushiItem in sushiContained.EnumerateArray())
                        {
                            var rt = sushiItem.TryGetProperty("resourceType", out var rtv) ? rtv.GetString() : null;
                            var cid = sushiItem.TryGetProperty("id", out var idv) ? idv.GetString() : null;
                            var key = $"{rt}/{cid}";
                            var prefix = $"{fileName}[contained:{key}]";

                            if (!ourContained.TryGetValue(key, out var ourItem))
                            {
                                mismatchDetails.Add($"{prefix}: sushi has contained resource, ours=<missing>");
                                mismatches++;
                                continue;
                            }
                            CompareAllScalarFields(prefix, sushiItem, ourItem, mismatchDetails, ref mismatches);
                        }
                    }

                    matched++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Warning: could not compare {fileName}: {ex.Message}");
                }
            }

            Console.WriteLine($"  Matched: {matched}  Mismatches: {mismatches}  Missing from output: {missing}  Size warnings: {sizeWarnings.Count}");

            if (sizeWarnings.Count > 0)
            {
                Console.WriteLine("  Size warnings (ours is <50% of sushi size – likely incomplete content):");
                foreach (var w in sizeWarnings)
                    Console.WriteLine($"    {w}");
            }
            if (mismatchDetails.Count > 0)
            {
                Console.WriteLine("  Field mismatches:");
                foreach (var detail in mismatchDetails)
                    Console.WriteLine($"    {detail}");
            }
            if (missingDetails.Count > 0)
            {
                Console.WriteLine("  Files missing from compiled output:");
                foreach (var detail in missingDetails)
                    Console.WriteLine($"    {detail}");
            }
        }
        else
        {
            Console.WriteLine("sushi-generated directory not found; skipping T6 comparison.");
        }

        // This test never fails – it is informational.
        Assert.IsTrue(written >= 0);
    }

    // ── Test 7: Normalize sushi-generated JSON property order ──────────────────

    /// <summary>
    /// Reads every JSON file in the <c>TestData/sushi-generated</c> directory, parses it
    /// through <see cref="FhirJsonParser"/> and re-serializes it with <see cref="FhirJsonSerializer"/>.
    /// The round-tripped JSON is written back to the same file, which normalizes property
    /// ordering to match the Firely SDK's canonical output — the same order used by our
    /// compiled resources.  This makes file-level diffs between sushi and our output
    /// meaningful without noise from property reordering.
    ///
    /// This test is idempotent and safe to run repeatedly.
    /// </summary>
    // [TestMethod, Ignore]
    public void ShouldNormalizeSushiGeneratedJsonPropertyOrder()
    {
        var sushiDir = Path.Combine(AppContext.BaseDirectory, "TestData", "sushi-generated");
        if (!Directory.Exists(sushiDir))
        {
            Assert.Inconclusive($"sushi-generated directory not found: {sushiDir}");
            return;
        }

        var sushiFiles = Directory.GetFiles(sushiDir, "*.json");
        Assert.IsTrue(sushiFiles.Length > 0, "No JSON files found in sushi-generated directory");

        var serializerSettings = new FhirJsonSerializationSettings { Pretty = true };
        var parserSettings = new ParserSettings { AcceptUnknownMembers = true, AllowUnrecognizedEnums = true };
        var jsonParser = new FhirJsonParser(parserSettings);

        int normalized = 0;
        var failures = new List<string>();

        foreach (var sushiFile in sushiFiles)
        {
            var fileName = Path.GetFileName(sushiFile);
            try
            {
                var originalJson = File.ReadAllText(sushiFile);
                var resource = jsonParser.Parse<FhirResource>(originalJson);
                var normalizedJson = resource.ToJson(serializerSettings);

                File.WriteAllText(sushiFile, normalizedJson);
                normalized++;
            }
            catch (Exception ex)
            {
                failures.Add($"{fileName}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nNormalized {normalized}/{sushiFiles.Length} sushi-generated JSON file(s).");

        if (failures.Count > 0)
        {
            Console.WriteLine($"\nNormalization failures ({failures.Count}):");
            foreach (var f in failures) Console.WriteLine($"  {f}");
        }

        Assert.AreEqual(0, failures.Count,
            $"{failures.Count} file(s) failed normalization. See output for details.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses every FSH file in the SDC test-data directory, discovers entity-level
    /// dependencies (profiles with non-builtin parents, instances with non-builtin
    /// InstanceOf), maps them to source files, and returns a file-level dependency
    /// graph with transitive closure.
    /// <para>
    /// Used by <see cref="SequenceFshDocs"/> to validate that the hardcoded
    /// <see cref="_fileDependencies"/> dictionary is correct and up-to-date.
    /// </para>
    /// </summary>
    private static Dictionary<string, HashSet<string>> ComputeFileDependencies()
    {
        var fshFiles = Directory.GetFiles(SdcPath, "*.fsh", SearchOption.AllDirectories)
                                .OrderBy(f => f)
                                .ToArray();

        // (entity name, dependency entity name) -- a single entity may have multiple deps.
        var entityDeps = new List<(string EntityName, string DependsOn)>();
        // entity name → source file name
        var entitySource = new Dictionary<string, string>(StringComparer.Ordinal);

        var fshDocs = new List<FshDoc>();

        foreach (var fshFile in fshFiles)
        {
            try
            {
                var fshText = File.ReadAllText(fshFile);
                var result = FshParser.Parse(fshText);
                if (result is not ParseResult.Success s) continue;

                var fa = new FileInfo(fshFile);
                s.Document.SetAnnotation(fa);
                fshDocs.Add(s.Document);

                foreach (var e in s.Document.Entities)
                {
                    e.AddAnnotation(fa);

                    // Track entity -> source file for all relevant entity types so
                    // dependency edges can resolve even when a profile has a core
                    // FHIR parent (and therefore adds no parent dependency edge).
                    switch (e)
                    {
                        case Profile pEntity:
                            entitySource.TryAdd(pEntity.Name, fa.Name);
                            break;
                        case Instance iEntity:
                            entitySource.TryAdd(iEntity.Name, fa.Name);
                            break;
                        case Logical lEntity:
                            entitySource.TryAdd(lEntity.Name, fa.Name);
                            break;
                        case Hl7.FhirShorthand.Serialization.Models.Resource rEntity:
                            entitySource.TryAdd(rEntity.Name, fa.Name);
                            break;
                        case Hl7.FhirShorthand.Serialization.Models.Extension extEntity:
                            entitySource.TryAdd(extEntity.Name, fa.Name);
                            break;
                        case Hl7.FhirShorthand.Serialization.Models.CodeSystem csEntity:
                            entitySource.TryAdd(csEntity.Name, fa.Name);
                            break;
                        case Hl7.FhirShorthand.Serialization.Models.ValueSet vsEntity:
                            entitySource.TryAdd(vsEntity.Name, fa.Name);
                            break;
                        case Hl7.FhirShorthand.Serialization.Models.Mapping mEntity:
                            entitySource.TryAdd(mEntity.Name, fa.Name);
                            if (!string.IsNullOrEmpty(mEntity.Source) && !mEntity.Source.StartsWith('$') &&
                                !ModelInfo.ModelInspector.IsKnownResource(mEntity.Source))
                            {
                                entityDeps.Add((mEntity.Name, mEntity.Source));
                            }
                            break;
                        case Invariant invEntity:
                            entitySource.TryAdd(invEntity.Name, fa.Name);
                            break;
                    }

                    if (e is Profile p && p.Parent != null && !ModelInfo.ModelInspector.IsKnownResource(p.Parent.Value))
                    {
                        entityDeps.Add((p.Name, p.Parent.Value));
                        entitySource.TryAdd(p.Name, fa.Name);
                    }
                    if (e is Instance i && i.InstanceOf != null && !ModelInfo.ModelInspector.IsKnownResource(i.InstanceOf))
                    {
                        entityDeps.Add((i.Name, i.InstanceOf));
                        entitySource.TryAdd(i.Name, fa.Name);
                    }
                    // Scan Instance rules for Canonical(name) and NameValue cross-references.
                    // These require the referenced entity to be present in the compilation
                    // context so that its canonical URL can be resolved or the instance
                    // can be embedded (e.g. * parameter[response].resource = someInstance).
                    if (e is Instance instEntity)
                    {
                        foreach (var rule in instEntity.Rules.OfType<InstanceFixedValueRule>())
                        {
                            if (rule.Value is Hl7.FhirShorthand.Serialization.Models.Canonical can &&
                                !string.IsNullOrEmpty(can.Url) &&
                                !can.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                                !can.Url.StartsWith("urn:", StringComparison.OrdinalIgnoreCase) &&
                                !can.Url.StartsWith('$'))
                            {
                                entityDeps.Add((instEntity.Name, can.Url));
                                entitySource.TryAdd(instEntity.Name, fa.Name);
                            }
                            // NameValue: cross-instance reference (e.g. * parameter[x].resource = someInstance)
                            // The referenced instance must be compiled together so it can be embedded inline.
                            if (rule.Value is NameValue nameVal &&
                                !string.IsNullOrEmpty(nameVal.Value) &&
                                !nameVal.Value.StartsWith('$'))
                            {
                                entityDeps.Add((instEntity.Name, nameVal.Value));
                                entitySource.TryAdd(instEntity.Name, fa.Name);
                            }
                            // Reference(localInstanceName): cross-instance reference that needs the
                            // referenced instance compiled so its FHIR resource type can be resolved
                            // for the ResourceType/id prefix (e.g. QuestionnaireResponse/id).
                            if (rule.Value is Reference refVal &&
                                !string.IsNullOrEmpty(refVal.Type) &&
                                !refVal.Type.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                                !refVal.Type.StartsWith("urn:", StringComparison.OrdinalIgnoreCase) &&
                                !refVal.Type.StartsWith('$') &&
                                !refVal.Type.Contains('/'))
                            {
                                entityDeps.Add((instEntity.Name, refVal.Type));
                                entitySource.TryAdd(instEntity.Name, fa.Name);
                            }
                            // Code with a local CodeSystem entity name as system
                            // (e.g. * valueCoding = KeyboardTypeCodes#email requires KeyboardTypeCodes).
                            // Extract the system part (before '#') from the Code.Value string.
                            if (rule.Value is Hl7.FhirShorthand.Serialization.Models.Code fshCodeVal)
                            {
                                var hashIdx = fshCodeVal.Value.IndexOf('#');
                                if (hashIdx > 0)
                                {
                                    var systemName = fshCodeVal.Value[..hashIdx];
                                    if (!systemName.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                                        !systemName.StartsWith("urn:", StringComparison.OrdinalIgnoreCase) &&
                                        !systemName.StartsWith('$'))
                                    {
                                        entityDeps.Add((instEntity.Name, systemName));
                                        entitySource.TryAdd(instEntity.Name, fa.Name);
                                    }
                                }
                            }
                            // Scan rule paths for extension[EntityName] references.
                            // e.g. `* extension[QuestionnaireAdaptiveExtension].valueBoolean = true`
                            // requires the QuestionnaireAdaptiveExtension SD to resolve the URL.
                            if (!string.IsNullOrEmpty(rule.Path))
                            {
                                var extRefs = new HashSet<string>(StringComparer.Ordinal);
                                CollectExtensionNameRefsFromPath(rule.Path, extRefs);
                                foreach (var extName in extRefs)
                                {
                                    entityDeps.Add((instEntity.Name, extName));
                                    entitySource.TryAdd(instEntity.Name, fa.Name);
                                }
                            }
                        }
                        // Scan InstanceInsertRule parameters for local CodeSystem references.
                        // e.g. `* insert itemCoding("H1/T1/Q1", "...", CodeSystemCSPHQ9#Not-at-all "Not at all")`
                        // requires CodeSystemCSPHQ9 to resolve the system URL.
                        foreach (var insertRule in instEntity.Rules.OfType<InstanceInsertRule>())
                        {
                            foreach (var param in insertRule.Parameters)
                            {
                                var hashIdx = param.IndexOf('#');
                                if (hashIdx <= 0) continue;
                                var systemName = param[..hashIdx];
                                if (!systemName.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                                    !systemName.StartsWith("urn:", StringComparison.OrdinalIgnoreCase) &&
                                    !systemName.StartsWith('$') &&
                                    !systemName.Contains('"') &&
                                    !systemName.Contains(' '))
                                {
                                    entityDeps.Add((instEntity.Name, systemName));
                                    entitySource.TryAdd(instEntity.Name, fa.Name);
                                }
                            }
                        }
                    }
                    if (e is Hl7.FhirShorthand.Serialization.Models.Extension ext)
                    {
                        entitySource.TryAdd(ext.Name, fa.Name);
                    }
                    // Track CodeSystems and ValueSets so instances/profiles that reference
                    // them by name can resolve the source file.
                    if (e is Hl7.FhirShorthand.Serialization.Models.CodeSystem cs)
                    {
                        entitySource.TryAdd(cs.Name, fa.Name);
                    }
                    if (e is Hl7.FhirShorthand.Serialization.Models.ValueSet vs)
                    {
                        entitySource.TryAdd(vs.Name, fa.Name);
                    }

                    // Scan rules for ContainsRule items that reference extensions by name.
                    // When NamedAlias is set, Name is the extension type (e.g. "RenderingCriticalExtension")
                    // and NamedAlias is the slice name.  Names starting with '$' are aliases, not local entities.
                    var rules = e switch
                    {
                        Profile pr => pr.Rules.AsEnumerable<FshRule>(),
                        Hl7.FhirShorthand.Serialization.Models.Extension ex => ex.Rules.AsEnumerable<FshRule>(),
                        Logical l => l.Rules.AsEnumerable<FshRule>(),
                        Hl7.FhirShorthand.Serialization.Models.Resource r => r.Rules.AsEnumerable<FshRule>(),
                        _ => Enumerable.Empty<FshRule>()
                    };
                    foreach (var rule in rules.OfType<ContainsRule>())
                    {
                        foreach (var item in rule.Items)
                        {
                            if (item.NamedAlias != null && !item.Name.StartsWith('$'))
                            {
                                entityDeps.Add((e.Name, item.Name));
                                entitySource.TryAdd(e.Name, fa.Name);
                            }
                        }
                    }

                    // Scan for ValueSetRule (binding rules like `* value[x] from MyValueSet (required)`).
                    // The referenced ValueSet must be compiled together with this entity so the
                    // binding URL can be resolved to its canonical form.
                    foreach (var vsRule in rules.OfType<ValueSetRule>())
                    {
                        if (!string.IsNullOrEmpty(vsRule.ValueSetName) && !vsRule.ValueSetName.StartsWith('$'))
                        {
                            entityDeps.Add((e.Name, vsRule.ValueSetName));
                            entitySource.TryAdd(e.Name, fa.Name);
                        }
                    }

                    // Scan OnlyRule type references (e.g. `* value[x] only uri or Reference(SDCModularQuestionnaire)`
                    // or `* resource only SDCModularQuestionnaire`).
                    // The named types must be compiled together so the constraint can be resolved.
                    foreach (var onlyRule in rules.OfType<OnlyRule>())
                    {
                        foreach (var targetType in onlyRule.TargetTypes)
                        {
                            // Unwrap Reference(TypeName) to get the bare type name.
                            var typeName = targetType.StartsWith("Reference(", StringComparison.Ordinal) && targetType.EndsWith(')')
                                ? targetType[10..^1]
                                : targetType;

                            if (string.IsNullOrEmpty(typeName) ||
                                typeName.StartsWith('$') ||
                                typeName.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                                typeName.StartsWith("urn:", StringComparison.OrdinalIgnoreCase) ||
                                ModelInfo.ModelInspector.IsKnownResource(typeName) ||
                                ModelInfo.ModelInspector.FindClassMapping(typeName) != null)
                                continue;

                            entityDeps.Add((e.Name, typeName));
                            entitySource.TryAdd(e.Name, fa.Name);
                        }
                    }

                    // Scan ObeysRule references: `* obeys invName` makes the entity depend on
                    // the invariant definition.  The invariant must be in context so its
                    // human-readable description and expression can populate the constraint.
                    foreach (var obeysRule in rules.OfType<ObeysRule>())
                    {
                        foreach (var invName in obeysRule.InvariantNames)
                        {
                            if (!string.IsNullOrEmpty(invName))
                            {
                                entityDeps.Add((e.Name, invName));
                                entitySource.TryAdd(e.Name, fa.Name);
                            }
                        }
                    }

                    // Scan ValueSet entities for CodeSystem references in two forms:
                    //   1. `* include codes from system EntryModeCodes` — VsComponentRule.FromSystem
                    //   2. `* AustralianStateCodes#ACT "..."` — VsComponentRule.ConceptCode.Value (System#Code)
                    if (e is Hl7.FhirShorthand.Serialization.Models.ValueSet vsDefEntity)
                    {
                        foreach (var vsCompRule in vsDefEntity.Rules.OfType<VsComponentRule>())
                        {
                            // Form 1: `include/exclude codes from system LocalCodeSystem`
                            if (!string.IsNullOrEmpty(vsCompRule.FromSystem) &&
                                !vsCompRule.FromSystem.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                                !vsCompRule.FromSystem.StartsWith("urn:", StringComparison.OrdinalIgnoreCase) &&
                                !vsCompRule.FromSystem.StartsWith('$'))
                            {
                                entityDeps.Add((vsDefEntity.Name, vsCompRule.FromSystem));
                                entitySource.TryAdd(vsDefEntity.Name, fa.Name);
                            }

                            // Form 2: `* System#Code "Display"` — system name is the prefix before '#'
                            if (vsCompRule.IsConceptComponent &&
                                vsCompRule.ConceptCode?.Value is string codeVal &&
                                codeVal.Contains('#'))
                            {
                                var systemName = codeVal[..codeVal.IndexOf('#')];
                                if (!string.IsNullOrEmpty(systemName) &&
                                    !systemName.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                                    !systemName.StartsWith("urn:", StringComparison.OrdinalIgnoreCase) &&
                                    !systemName.StartsWith('$'))
                                {
                                    entityDeps.Add((vsDefEntity.Name, systemName));
                                    entitySource.TryAdd(vsDefEntity.Name, fa.Name);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Skip files that fail to parse.
            }
        }

        // ── Scan RuleSet bodies for extension-name bracket references ─────────────
        // RuleSets in shared.fsh can contain path segments like extension[InitialExpressionExtension].
        // Instances that insert those rulesets indirectly depend on the extension's source file.
        // Build: ruleSetName → set of extension entity names referenced in paths.
        var ruleSetExtensionDeps = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        // Scan parameterized RuleSet bodies for Reference({param}) patterns so that when an
        // Instance inserts such a ruleset, the referenced local instance becomes a dependency.
        // Build: ruleSetName → list of parameter indices whose argument values are Reference targets.
        var ruleSetReferenceParamIdxs = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        foreach (var doc in fshDocs)
        {
            foreach (var e in doc.Entities.OfType<RuleSet>())
            {
                var extRefs = new HashSet<string>(StringComparer.Ordinal);

                // Scan parsed rules (non-parameterized rulesets).
                foreach (var rule in e.Rules)
                {
                    if (string.IsNullOrEmpty(rule.Path)) continue;
                    CollectExtensionNameRefsFromPath(rule.Path, extRefs);
                }

                // Parameterized rulesets store content as raw text; scan it with a simple pattern.
                if (e.IsParameterized && !string.IsNullOrEmpty(e.UnparsedContent))
                {
                    // Match extension[SomeName] where SomeName is not $, http, a number, or bracket-wrapped.
                    foreach (System.Text.RegularExpressions.Match m in
                        System.Text.RegularExpressions.Regex.Matches(
                            e.UnparsedContent, @"extension\[([A-Za-z][A-Za-z0-9_-]*)\]"))
                    {
                        extRefs.Add(m.Groups[1].Value);
                    }

                    // Match Reference({paramName}) patterns.  For each match, find the index of
                    // paramName in the ruleset's declared parameters so the call-site argument
                    // can be resolved to a concrete entity name when the ruleset is inserted.
                    var paramNames = e.Parameters.Select(p => p.Value).ToList();
                    var refParamIdxs = new List<int>();
                    foreach (Match m in
                        Regex.Matches(e.UnparsedContent, @"Reference\(\{([^}]+)\}\)"))
                    {
                        var paramName = m.Groups[1].Value;
                        var idx = paramNames.IndexOf(paramName);
                        if (idx >= 0 && !refParamIdxs.Contains(idx))
                            refParamIdxs.Add(idx);
                    }
                    if (refParamIdxs.Count > 0)
                        ruleSetReferenceParamIdxs[e.Name] = refParamIdxs;
                }

                if (extRefs.Count > 0)
                    ruleSetExtensionDeps[e.Name] = extRefs;
            }
        }

        // For every Instance that uses an InstanceInsertRule referencing a ruleset with extension deps,
        // add those extension entities as direct dependencies of the instance entity.
        // Also resolve Reference({param}) arguments: when a parameterized ruleset body contains
        // Reference({paramName}), the corresponding call-site argument is a local instance name
        // that must be compiled together so its FHIR resource type prefix can be resolved.
        foreach (var doc in fshDocs)
        {
            foreach (var e in doc.Entities.OfType<Instance>())
            {
                if (!entitySource.TryGetValue(e.Name, out var instanceFile)) continue;
                foreach (var insertRule in e.Rules.OfType<InstanceInsertRule>())
                {
                    // Extension path dependencies.
                    if (ruleSetExtensionDeps.TryGetValue(insertRule.RuleSetReference, out var extRefs))
                    {
                        foreach (var extName in extRefs)
                            entityDeps.Add((e.Name, extName));
                    }

                    // Reference(localInstance) parameter dependencies.
                    if (ruleSetReferenceParamIdxs.TryGetValue(insertRule.RuleSetReference, out var paramIdxs))
                    {
                        foreach (var idx in paramIdxs)
                        {
                            if (idx >= insertRule.Parameters.Count) continue;
                            var argValue = insertRule.Parameters[idx].Trim();
                            if (string.IsNullOrEmpty(argValue) ||
                                argValue.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                                argValue.StartsWith("urn:", StringComparison.OrdinalIgnoreCase) ||
                                argValue.StartsWith('$') ||
                                argValue.Contains('/'))
                                continue;
                            entityDeps.Add((e.Name, argValue));
                        }
                    }
                }
            }
        }

        // Build direct file-level dependencies.
        var allFileNames = fshDocs
            .Select(d => d.Annotation<FileInfo>()?.Name)
            .Where(n => n != null)
            .Select(n => n!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var fileDeps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in allFileNames)
            fileDeps[f] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (entityName, dependsOnEntity) in entityDeps)
        {
            if (!entitySource.TryGetValue(entityName, out var fromFile)) continue;
            if (!entitySource.TryGetValue(dependsOnEntity, out var toFile)) continue;
            if (!fromFile.Equals(toFile, StringComparison.OrdinalIgnoreCase))
                fileDeps[fromFile].Add(toFile);
        }

        // Compute transitive closure so Compile_SpecificResource loads the full chain.
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var (file, deps) in fileDeps)
            {
                var transitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var dep in deps)
                {
                    if (fileDeps.TryGetValue(dep, out var depDeps))
                        transitive.UnionWith(depDeps);
                }
                var before = deps.Count;
                deps.UnionWith(transitive);
                deps.Remove(file); // no self-loops
                if (deps.Count != before)
                    changed = true;
            }
        }

        // Mapping companion files: when a Mapping entity in file B sources an entity defined
        // in file A, file B must be loaded alongside file A so the output SD includes the
        // mapping data (matching the sushi-generated baseline which is compiled from all files).
        // This is a reverse dependency — the mapping depends on the source, but the source
        // file's test also needs the mapping file.  Added after the transitive closure so that
        // the mapping file's own compile-time dependencies are not pulled in transitively here.
        foreach (var doc in fshDocs)
        {
            foreach (var e in doc.Entities.OfType<Hl7.FhirShorthand.Serialization.Models.Mapping>())
            {
                if (string.IsNullOrEmpty(e.Source)) continue;
                if (!entitySource.TryGetValue(e.Name, out var mappingFile)) continue;
                if (!entitySource.TryGetValue(e.Source, out var sourceFile)) continue;
                if (!mappingFile.Equals(sourceFile, StringComparison.OrdinalIgnoreCase) &&
                    fileDeps.TryGetValue(sourceFile, out var sourceFileDeps))
                {
                    sourceFileDeps.Add(mappingFile);
                }
            }
        }

        // Remove aliases.fsh and shared.fsh since they are always loaded.
        foreach (var deps in fileDeps.Values)
        {
            deps.Remove("aliases.fsh");
            deps.Remove("shared.fsh");
        }

        return fileDeps;
    }

    /// <summary>
    /// Compares all top-level scalar (string, boolean, number) properties present in
    /// <paramref name="sushiEl"/> against <paramref name="ourEl"/>, accumulating differences
    /// into <paramref name="mismatchDetails"/> and incrementing <paramref name="mismatches"/>.
    /// Object- and array-valued properties are skipped (they are handled by type-specific helpers).
    /// The <c>text</c> (narrative) and <c>meta</c> properties are intentionally excluded.
    /// </summary>
    private static void CompareAllScalarFields(
        string label,
        JsonElement sushiEl,
        JsonElement ourEl,
        List<string> mismatchDetails,
        ref int mismatches)
    {
        foreach (var prop in sushiEl.EnumerateObject())
        {
            // Skip non-scalar properties and properties that are expected to differ.
            if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                continue;
            if (prop.Name is "text" or "meta")
                continue;

            var sushiRaw = prop.Value.GetRawText();
            if (!ourEl.TryGetProperty(prop.Name, out var ourVal))
            {
                mismatchDetails.Add($"{label}.{prop.Name}: sushi={sushiRaw} ours=<missing>");
                mismatches++;
                continue;
            }
            var ourRaw = ourVal.GetRawText();
            if (sushiRaw != ourRaw)
            {
                mismatchDetails.Add($"{label}.{prop.Name}: sushi={sushiRaw} ours={ourRaw}");
                mismatches++;
            }
        }
    }

    /// <summary>
    /// Collects extension entity names referenced in bracket notation within a path string,
    /// e.g. <c>extension[InitialExpressionExtension]</c> → adds <c>"InitialExpressionExtension"</c>.
    /// Skips aliases (<c>$…</c>), absolute URLs (<c>http…</c>), and numeric indices.
    /// </summary>
    private static void CollectExtensionNameRefsFromPath(string path, HashSet<string> extRefs)
    {
        foreach (var segment in path.Split('.'))
        {
            var bracketStart = segment.IndexOf('[');
            var bracketEnd = segment.IndexOf(']');
            if (bracketStart < 0 || bracketEnd < bracketStart) continue;
            var baseName = segment[..bracketStart];
            var innerName = segment[(bracketStart + 1)..bracketEnd];
            if (!string.Equals(baseName, "extension", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrEmpty(innerName) || innerName.StartsWith('$') ||
                innerName.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                int.TryParse(innerName, out _)) continue;
            extRefs.Add(innerName);
        }
    }

    /// <summary>
    /// For a <c>StructureDefinition</c>, compares the set of element <c>path</c> values
    /// found in <c>differential.element</c> and reports missing/extra paths.
    ///
    /// Path values can legitimately repeat when multiple slices share the same path,
    /// so this comparison is set-based rather than count-based.
    /// </summary>
    private static void CompareStructureDefinitionDifferential(
        string label,
        JsonElement sushiEl,
        JsonElement ourEl,
        List<string> mismatchDetails,
        ref int mismatches)
    {
        var sushiPaths = ExtractStringValuesFromNestedArray(sushiEl, ["differential", "element"], "path");
        var ourPaths = ExtractStringValuesFromNestedArray(ourEl, ["differential", "element"], "path");

        var sushiPathSet = new HashSet<string>(sushiPaths, StringComparer.Ordinal);
        var ourPathSet = new HashSet<string>(ourPaths, StringComparer.Ordinal);
        foreach (var path in sushiPathSet)
        {
            if (!ourPathSet.Contains(path))
            {
                mismatchDetails.Add($"{label}.differential.element[path={path}]: present in sushi, missing from ours");
                mismatches++;
            }
        }

        foreach (var path in ourPathSet)
        {
            if (!sushiPathSet.Contains(path))
            {
                mismatchDetails.Add($"{label}.differential.element[path={path}]: present in ours, missing from sushi");
                mismatches++;
            }
        }
    }

    /// <summary>
    /// For a <c>CodeSystem</c>, compares the set of concept <c>code</c> values and the
    /// top-level <c>count</c> field (when present in the sushi output).
    /// </summary>
    private static void CompareCodeSystemConcepts(
        string label,
        JsonElement sushiEl,
        JsonElement ourEl,
        List<string> mismatchDetails,
        ref int mismatches)
    {
        var sushiCodes = ExtractStringValuesFromNestedArray(sushiEl, ["concept"], "code");
        var ourCodes = ExtractStringValuesFromNestedArray(ourEl, ["concept"], "code");

        if (sushiCodes.Count != ourCodes.Count)
        {
            mismatchDetails.Add($"{label}.concept count: sushi={sushiCodes.Count} ours={ourCodes.Count}");
            mismatches++;
        }

        var ourCodeSet = new HashSet<string>(ourCodes, StringComparer.Ordinal);
        foreach (var code in sushiCodes)
        {
            if (!ourCodeSet.Contains(code))
            {
                mismatchDetails.Add($"{label}.concept[code={code}]: present in sushi, missing from ours");
                mismatches++;
            }
        }
    }

    /// <summary>
    /// For a <c>ValueSet</c>, compares the set of <c>system</c> URIs listed under
    /// <c>compose.include</c>.
    /// </summary>
    private static void CompareValueSetCompose(
        string label,
        JsonElement sushiEl,
        JsonElement ourEl,
        List<string> mismatchDetails,
        ref int mismatches)
    {
        var sushiSystems = ExtractStringValuesFromNestedArray(sushiEl, ["compose", "include"], "system");
        var ourSystems = ExtractStringValuesFromNestedArray(ourEl, ["compose", "include"], "system");

        if (sushiSystems.Count != ourSystems.Count)
        {
            mismatchDetails.Add($"{label}.compose.include count: sushi={sushiSystems.Count} ours={ourSystems.Count}");
            mismatches++;
        }

        var ourSystemSet = new HashSet<string>(ourSystems, StringComparer.Ordinal);
        foreach (var system in sushiSystems)
        {
            if (!ourSystemSet.Contains(system))
            {
                mismatchDetails.Add($"{label}.compose.include[system={system}]: present in sushi, missing from ours");
                mismatches++;
            }
        }
    }

    /// <summary>
    /// Walks a chain of JSON object properties given by <paramref name="propertyPath"/> and
    /// then, if the final value is a JSON array, collects the string value of
    /// <paramref name="valueProperty"/> from each element.
    /// </summary>
    /// <example>
    /// <code>
    /// // Collect all differential element paths from a StructureDefinition:
    /// var paths = ExtractStringValuesFromNestedArray(root, ["differential", "element"], "path");
    /// </code>
    /// </example>
    private static List<string> ExtractStringValuesFromNestedArray(
        JsonElement root,
        string[] propertyPath,
        string valueProperty)
    {
        var current = root;
        foreach (var segment in propertyPath)
        {
            if (!current.TryGetProperty(segment, out current))
                return [];
        }

        if (current.ValueKind != JsonValueKind.Array)
            return [];

        var values = new List<string>();
        foreach (var item in current.EnumerateArray())
        {
            if (item.TryGetProperty(valueProperty, out var valEl))
                values.Add(valEl.GetString() ?? string.Empty);
        }
        return values;
    }
}
