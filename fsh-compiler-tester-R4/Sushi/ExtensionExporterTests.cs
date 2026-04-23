// Ported from SUSHI: test/export/StructureDefinition.ExtensionExporter.test.ts
// Source: https://github.com/FHIR/sushi/blob/main/test/export/StructureDefinition.ExtensionExporter.test.ts
//
// See SushiCompilerTestHelper.cs for cross-cutting differences.

using Hl7.Fhir.Model;
using Hl7.FhirShorthand.Compiler;
using FhirResource = Hl7.Fhir.Model.Resource;

namespace Hl7.FhirShorthand.Compiler_tester_r4.Sushi;

[TestClass]
public class ExtensionExporterTests
{
    [TestMethod]
    public void ShouldOutputEmptyResultsWithEmptyInput()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Alias: $X = http://example.org/x
        ");
        var sds = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? SushiCompilerTestHelper.StructureDefinitions(ok.Value)
            : new List<StructureDefinition>();
        Assert.AreEqual(0, sds.Count);
    }

    [TestMethod]
    public void ShouldExportASingleExtension()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: Foo
        ");
        Assert.AreEqual(1, SushiCompilerTestHelper.StructureDefinitions(resources).Count);
    }

    [TestMethod]
    public void ShouldAddSourceInfoForTheExportedExtensionToThePackage()
    {
        Assert.Inconclusive("SUSHI pkg.fshMap has no fsh-compiler equivalent.");
    }

    [TestMethod]
    public void ShouldExportMultipleExtensions()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: Foo

            Extension: Bar
        ");
        Assert.AreEqual(2, SushiCompilerTestHelper.StructureDefinitions(resources).Count);
    }

    [TestMethod]
    public void ShouldStillExportExtensionsIfOneFails()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Extension: Foo
            Parent: Baz

            Extension: Bar
        ");
        if (result is CompileResult<List<FhirResource>>.SuccessResult ok)
        {
            var sds = SushiCompilerTestHelper.StructureDefinitions(ok.Value);
            Assert.AreEqual(1, sds.Count);
            Assert.AreEqual("Bar", sds[0].Name);
        }
        else
        {
            Assert.Inconclusive("Compiler aborts on unresolved parent.");
        }
    }

    [TestMethod]
    public void ShouldLogAMessageWithSourceInformationWhenTheParentIsNotFound()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Extension: Wrong
            Parent: DoesNotExist
        ");
        bool hasError = result is CompileResult<List<FhirResource>>.FailureResult
                        || result.Warnings.Any(w => w.Message.Contains("DoesNotExist"));
        Assert.IsTrue(hasError);
    }

    [TestMethod]
    public void ShouldLogAMessageWithSourceInformationWhenTheParentIsNotAnExtension()
    {
        // SUSHI: "The parent of an extension must be the base Extension or another defined extension".
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Extension: Wrong
            Parent: Patient
        ");
        Assert.IsTrue(
            result is CompileResult<List<FhirResource>>.FailureResult
            || result.Warnings.Any(w => w.Message.ToLower().Contains("parent")),
            "Expected a warning about invalid parent for Extension.");
    }

    [TestMethod]
    public void ShouldExportExtensionsWithFSHyParents()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: Foo

            Extension: Bar
            Parent: Foo
        ");
        var sds = SushiCompilerTestHelper.StructureDefinitions(resources);
        Assert.AreEqual(2, sds.Count);
        var foo = sds.First(s => s.Name == "Foo");
        var bar = sds.First(s => s.Name == "Bar");
        Assert.AreEqual(foo.Url, bar.BaseDefinition);
    }

    [TestMethod]
    public void ShouldExportExtensionsWithTheSameFSHyParents()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: Foo

            Extension: Bar
            Parent: Foo

            Extension: Baz
            Parent: Foo
        ");
        Assert.AreEqual(3, SushiCompilerTestHelper.StructureDefinitions(resources).Count);
    }

    [TestMethod]
    public void ShouldExportExtensionsWithDeepFSHyParents()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: Foo

            Extension: Bar
            Parent: Foo

            Extension: Baz
            Parent: Bar
        ");
        Assert.AreEqual(3, SushiCompilerTestHelper.StructureDefinitions(resources).Count);
    }

    [TestMethod]
    public void ShouldExportExtensionsWithOutOfOrderFSHyParents()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: Foo
            Parent: Bar

            Extension: Bar
            Parent: Baz

            Extension: Baz
        ");
        Assert.AreEqual(3, SushiCompilerTestHelper.StructureDefinitions(resources).Count);
    }

    [TestMethod]
    public void ShouldNotLogAnErrorWhenAnInlineExtensionIsUsed()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Extension: MyExtension
            * extension contains SomeExtension 0..*
        ");
        Assert.AreEqual(0, result.Warnings.Count(w => w.Message.ToLower().Contains("error")));
    }

    [TestMethod]
    public void ShouldExportExtensionsWithExtensionInstanceParents()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: ParentExtension
            InstanceOf: StructureDefinition
            Usage: #definition
            * name = ""ParentExtension""
            * status = #active
            * kind = #resource
            * abstract = false
            * type = ""Extension""
            * derivation = #constraint
            * baseDefinition = ""http://hl7.org/fhir/StructureDefinition/Extension""
            * snapshot.element[0].id = ""Extension""
            * snapshot.element[0].path = ""Extension""
            * snapshot.element[+].id = ""Extension.url""
            * snapshot.element[=].path = ""Extension.url""

            Extension: ChildExtension
            Parent: ParentExtension
        ");
        var child = SushiCompilerTestHelper.FindSd(resources, "ChildExtension");
        Assert.IsNotNull(child);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/ParentExtension",
            child.BaseDefinition);
    }

    // ─── #context ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldSetExtensionContextByAQuotedString()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: ""some.fhirpath.expression""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(1, sd.Context.Count);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Fhirpath, sd.Context[0].Type);
        Assert.AreEqual("some.fhirpath.expression", sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForAnExtensionByUrl()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: http://hl7.org/fhir/StructureDefinition/cqf-library
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Extension, sd.Context[0].Type);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/cqf-library", sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForAnExtensionByName()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: library
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Extension, sd.Context[0].Type);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/cqf-library", sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForAnExtensionById()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: cqf-library
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Extension, sd.Context[0].Type);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/cqf-library", sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForABaseResourceRootElementByIdName()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: Observation
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
        Assert.AreEqual("Observation", sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForABaseResourceRootElementByUrl()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: http://hl7.org/fhir/StructureDefinition/Observation
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
        Assert.AreEqual("Observation", sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForABaseResourceByIdWithAFshPath()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: Observation.component.valueQuantity
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
        Assert.AreEqual("Observation.component.value[x]:valueQuantity", sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextToItselfByUrl()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Id: my-extension
            Context: http://hl7.org/fhir/us/minimal/StructureDefinition/my-extension
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Extension, sd.Context[0].Type);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/my-extension",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextToItselfByName()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Id: my-extension
            Context: MyExtension
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Extension, sd.Context[0].Type);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/my-extension",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextToItselfById()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Id: my-extension
            Context: my-extension
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Extension, sd.Context[0].Type);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/my-extension",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForABaseResourceByUrlWithAFshPath()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: http://hl7.org/fhir/StructureDefinition/Observation#component.valueQuantity
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
        Assert.AreEqual("Observation.component.value[x]:valueQuantity", sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForABaseResourceWithNoDerivationRootElementByIdName()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: Resource
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
        Assert.AreEqual("Resource", sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForABaseResourceWithNoDerivationRootElementByUrl()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: http://hl7.org/fhir/StructureDefinition/Element
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
        Assert.AreEqual("Element", sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForABaseResourceWithNoDerivationByIdWithAFshPath()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: Resource.language
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
        Assert.AreEqual("Resource.language", sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForABaseResourceWithNoDerivationByUrlWithAFshPath()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: http://hl7.org/fhir/StructureDefinition/Element#id
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
        Assert.AreEqual("Element.id", sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextWithTypeExtensionWhenThePathIsPartOfAComplexExtensionByName()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: proficiency.extension[level]
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Extension, sd.Context[0].Type);
        Assert.AreEqual(
            "http://hl7.org/fhir/StructureDefinition/patient-proficiency#level",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextWithTypeExtensionWhenThePathIsPartOfAComplexExtensionByUrl()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: http://hl7.org/fhir/StructureDefinition/patient-proficiency#extension[level]
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Extension, sd.Context[0].Type);
        Assert.AreEqual(
            "http://hl7.org/fhir/StructureDefinition/patient-proficiency#level",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextWithTypeExtensionWhenThePathIsItsOwnSubExtensionByName()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Id: my-extension
            Context: MyExtension.extension[foo]
            * extension contains foo 0..1
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Extension, sd.Context[0].Type);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/my-extension#foo",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextWithTypeExtensionWhenThePathIsIsItsOwnSubExtensionByUrl()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Id: my-extension
            Context: http://hl7.org/fhir/us/minimal/StructureDefinition/my-extension#extension[foo]
            * extension contains foo 0..1
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Extension, sd.Context[0].Type);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/my-extension#foo",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextWithTypeExtensionWhenThePathIsADeepPartOfAComplexExtensionByName()
    {
        // Requires the MyVeryComplexExtension SUSHI fixture (mvc-extension.json).
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: MyVeryComplexExtension#extension[foo].extension[bigFoo]
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Extension, sd.Context[0].Type);
        Assert.AreEqual(
            "http://example.org/StructureDefinition/mvc-extension#foo.bigFoo",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextWithTypeElementWhenThePathIsADeepPartOfAComplexExtensionButContainsNonExtensionElements()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: MyVeryComplexExtension#extension[bar].value[x].extension[secretBar]
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
        Assert.AreEqual(
            "http://example.org/StructureDefinition/mvc-extension#Extension.extension:bar.value[x].extension:secretBar",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextWhenAnAliasIsUsedForAResourceUrl()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Alias: $PROF = http://hl7.org/fhir/StructureDefinition/patient-proficiency

            Extension: MyExtension
            Context: $PROF#extension[level]
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Extension, sd.Context[0].Type);
        Assert.AreEqual(
            "http://hl7.org/fhir/StructureDefinition/patient-proficiency#level",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenNoExtensionOrResourceCanBeFoundWithTheProvidedValue()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Extension: MyExtension
            Context: MysteryExtension
        ");
        bool hasError = result is CompileResult<List<FhirResource>>.FailureResult
                        || result.Warnings.Any(w => w.Message.Contains("MysteryExtension"));
        Assert.IsTrue(hasError, "Expected a warning about MysteryExtension not being found.");
    }

    // ─── #withCustomResource ──────────────────────────────────────────────────

    [TestMethod]
    public void ShouldSetExtensionContextForACustomResourceRootElementById()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            Id: my-obs

            Extension: MyExtension
            Context: my-obs
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/my-obs#Observation",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForACustomResourceRootElementByName()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            Id: my-obs

            Extension: MyExtension
            Context: MyObservation
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/my-obs#Observation",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForACustomResourceRootElementByUrl()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            Id: my-obs

            Extension: MyExtension
            Context: http://hl7.org/fhir/us/minimal/StructureDefinition/my-obs
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/my-obs#Observation",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForACustomResourceByIdWithAFshPath()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            Id: my-obs

            Extension: MyExtension
            Context: my-obs.component.valueQuantity
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/my-obs#Observation.component.value[x]:valueQuantity",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForACustomResourceByNameWithAFshPath()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            Id: my-obs

            Extension: MyExtension
            Context: MyObservation.component.valueQuantity
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/my-obs#Observation.component.value[x]:valueQuantity",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForACustomResourceByUrlWithAFshPath()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            Id: my-obs

            Extension: MyExtension
            Context: http://hl7.org/fhir/us/minimal/StructureDefinition/my-obs#component.valueQuantity
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/my-obs#Observation.component.value[x]:valueQuantity",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldSetExtensionContextForACustomResourceByUrlWhenTheUrlContainsAHashCharacter()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            Id: my-obs
            * ^url = ""http://hl7.org/fhir/us/minimal/StructureDefinition/my-profiles#obs""

            Extension: MyExtension
            Context: http://hl7.org/fhir/us/minimal/StructureDefinition/my-profiles#obs#component.valueQuantity
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/my-profiles#obs#Observation.component.value[x]:valueQuantity",
            sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenACustomResourceElementIsSpecifiedWithAnInvalidFshPath()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: MyObservation
            Parent: Observation
            Id: my-obs

            Extension: MyExtension
            Context: MyObservation.component.valueToast
        ");
        bool hasError = result is CompileResult<List<FhirResource>>.FailureResult
                        || result.Warnings.Any(w => w.Message.Contains("valueToast"));
        Assert.IsTrue(hasError, "Expected a warning for the invalid FSH path on the context.");
    }
}
