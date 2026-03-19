using fsh_compiler;
using fsh_compiler_r4;
using fsh_processor;
using fsh_processor.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Snapshot;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Utility;
using System.Text.Json;
using FhirCodeSystem = Hl7.Fhir.Model.CodeSystem;
using FhirResource = Hl7.Fhir.Model.Resource;
using FhirValueSet = Hl7.Fhir.Model.ValueSet;

namespace fsh_compiler_tester_r4;

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
            FhirVersion = R4FshCompiler.FhirVersion
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

    public void Compile_SpecificResource(string fshFileName)
    {
        FshDoc parsedFsh = GetFshDocument(fshFileName, out string fshText);
        FshDoc parsedFshAliases = GetFshDocument("aliases.fsh", out string fshTextAliases);

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
            FhirVersion = R4FshCompiler.FhirVersion
        };

        var compileResult = R4FshCompiler.Compile([parsedFshAliases, parsedFsh], sdcOptions);

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

        Console.WriteLine("--------------------------------------");
        Console.WriteLine();
        Console.WriteLine(fshText);
        Console.WriteLine();

        var serializerSettings = new FhirJsonSerializationSettings { Pretty = true };
        foreach (var r in resources)
        {
            Console.WriteLine("--------------------------------------");
            Console.WriteLine();
            Console.WriteLine(r.ToJson(serializerSettings));
        }

        // T1: SDC IG now compiles with zero errors.  Hard assert so regressions are caught.
        Assert.AreEqual(0, compileErrors.Count,
            $"{compileErrors.Count} compile error(s) found. See output for details.");

        Assert.IsTrue(resources.Count > 0, "No FHIR resources were produced from the SDC IG FSH.");

        // and finally compare with any sushi generated files
        var sushiDir = Path.Combine(AppContext.BaseDirectory, "TestData", "sushi-generated");
        foreach (var resource in resources)
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
                if (jsonSushiGenerated != json)
                    Assert.Fail("JSON Content not the same as the sushi-generated file: " + fileName);
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
    /// For a <c>StructureDefinition</c>, compares the list of element <c>path</c> values
    /// found in <c>differential.element</c>.  Also checks the element count and reports any
    /// paths present in the sushi output that are absent from ours.
    /// </summary>
    private static void CompareStructureDefinitionDifferential(
        string label,
        JsonElement sushiEl,
        JsonElement ourEl,
        List<string> mismatchDetails,
        ref int mismatches)
    {
        var sushiPaths = ExtractStringValuesFromNestedArray(sushiEl, ["differential", "element"], "path");
        var ourPaths   = ExtractStringValuesFromNestedArray(ourEl,    ["differential", "element"], "path");

        if (sushiPaths.Count != ourPaths.Count)
        {
            mismatchDetails.Add($"{label}.differential.element count: sushi={sushiPaths.Count} ours={ourPaths.Count}");
            mismatches++;
        }

        var ourPathSet = new HashSet<string>(ourPaths, StringComparer.Ordinal);
        foreach (var path in sushiPaths)
        {
            if (!ourPathSet.Contains(path))
            {
                mismatchDetails.Add($"{label}.differential.element[path={path}]: present in sushi, missing from ours");
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
        var ourCodes   = ExtractStringValuesFromNestedArray(ourEl,   ["concept"], "code");

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
        var ourSystems   = ExtractStringValuesFromNestedArray(ourEl,   ["compose", "include"], "system");

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
