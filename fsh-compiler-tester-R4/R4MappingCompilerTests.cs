using Hl7.Fhir.Model;
using FhirResource = Hl7.Fhir.Model.Resource;

namespace fsh_compiler_tester_r4;

/// <summary>
/// Tests compiling FSH Mapping entities into StructureDefinition.mapping[] and
/// ElementDefinition.mapping[] entries.
/// </summary>
[TestClass]
public class R4MappingCompilerTests
{
    [TestMethod]
    public void ShouldAddMappingDeclarationToSD()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation

            Mapping: ObsMapping
            Id: obs-map
            Source: MyObservation
            Target: ""http://hl7.org/v2""
            Title: ""HL7 v2 Mapping""
            Description: ""Maps to v2 segments""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyObservation");
        Assert.IsNotNull(sd.Mapping, "StructureDefinition.mapping should not be null");
        Assert.AreEqual(1, sd.Mapping.Count);
        var m = sd.Mapping[0];
        Assert.AreEqual("obs-map", m.Identity);
        Assert.AreEqual("http://hl7.org/v2", m.Uri);
        Assert.AreEqual("HL7 v2 Mapping", m.Name);
        Assert.AreEqual("Maps to v2 segments", m.Comment);
    }

    [TestMethod]
    public void ShouldAddElementMappingForRootPath()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation

            Mapping: ObsMapping
            Id: obs-map
            Source: MyObservation
            Target: ""http://hl7.org/v2""
            * -> ""OBX Segment""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyObservation");
        Assert.IsNotNull(sd.Mapping);
        var rootEd = sd.Differential.Element[0];
        Assert.IsNotNull(rootEd.Mapping, "Root element should have mapping entries");
        Assert.AreEqual(1, rootEd.Mapping.Count);
        Assert.AreEqual("obs-map", rootEd.Mapping[0].Identity);
        Assert.AreEqual("OBX Segment", rootEd.Mapping[0].Map);
    }

    [TestMethod]
    public void ShouldAddElementMappingForSpecificPath()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation

            Mapping: ObsMapping
            Id: obs-map
            Source: MyObservation
            Target: ""http://hl7.org/v2""
            * status -> ""OBX-11""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyObservation");
        var statusEd = CompilerTestHelper.GetElement(sd, "status");
        Assert.IsNotNull(statusEd.Mapping);
        Assert.AreEqual(1, statusEd.Mapping.Count);
        Assert.AreEqual("obs-map", statusEd.Mapping[0].Identity);
        Assert.AreEqual("OBX-11", statusEd.Mapping[0].Map);
    }

    [TestMethod]
    public void ShouldUseMappingNameWhenIdIsAbsent()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation

            Mapping: MyMapping
            Source: MyObservation
            Target: ""http://hl7.org/v2""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyObservation");
        Assert.IsNotNull(sd.Mapping);
        Assert.AreEqual("MyMapping", sd.Mapping[0].Identity);
    }

    [TestMethod]
    public void ShouldEmitWarningForMappingWithUnknownSource()
    {
        var fsh = CompilerTestHelper.LeftAlign(@"
            Mapping: OrphanMapping
            Id: orphan
            Source: NoSuchProfile
            Target: ""http://example.org""
        ");
        var doc = fsh_processor.FshParser.Parse(fsh);
        Assert.IsInstanceOfType<fsh_processor.Models.ParseResult.Success>(doc);
        var fshDoc = ((fsh_processor.Models.ParseResult.Success)doc).Document;

        var result = fsh_compiler_r4.R4FshCompiler.Compile(fshDoc);
        Assert.IsInstanceOfType<fsh_compiler.CompileResult<List<FhirResource>>.SuccessResult>(result,
            "Should succeed (no hard errors)");
        var successResult = (fsh_compiler.CompileResult<List<FhirResource>>.SuccessResult)result;
        Assert.IsTrue(successResult.Warnings.Any(w =>
                w.Message.Contains("NoSuchProfile") || w.Message.Contains("OrphanMapping")),
            "Should emit a warning about the unresolved source");
    }

    [TestMethod]
    public void ShouldAddMappingLanguageWhenPresent()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation

            Mapping: ObsMapping
            Id: obs-map
            Source: MyObservation
            Target: ""http://hl7.org/v2""
            * status -> ""OBX-11"" ""HL7v2 2.6""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyObservation");
        var statusEd = CompilerTestHelper.GetElement(sd, "status");
        Assert.IsNotNull(statusEd.Mapping);
        Assert.AreEqual("HL7v2 2.6", statusEd.Mapping[0].Language);
    }

    [TestMethod]
    public void ShouldExpandInsertRuleInMapping()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation

            RuleSet: ObsFieldMapping
            * status -> ""OBX-11""

            Mapping: ObsMapping
            Id: obs-map
            Source: MyObservation
            Target: ""http://hl7.org/v2""
            * insert ObsFieldMapping
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyObservation");
        var statusEd = CompilerTestHelper.GetElement(sd, "status");
        Assert.IsNotNull(statusEd.Mapping, "InsertRule should have expanded MappingRule onto status element");
        Assert.AreEqual(1, statusEd.Mapping.Count);
        Assert.AreEqual("obs-map", statusEd.Mapping[0].Identity);
        Assert.AreEqual("OBX-11", statusEd.Mapping[0].Map);
    }
}
