// Ported from SUSHI: test/export/StructureDefinition.LogicalExporter.test.ts
// Source: https://github.com/FHIR/sushi/blob/main/test/export/StructureDefinition.LogicalExporter.test.ts
//
// See SushiCompilerTestHelper.cs for cross-cutting differences.

using Hl7.Fhir.Model;
using Hl7.FhirShorthand.Compiler;
using FhirResource = Hl7.Fhir.Model.Resource;

namespace Hl7.FhirShorthand.Compiler_tester_r4.Sushi;

[TestClass]
public class LogicalExporterTests
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
    public void ShouldExportASingleLogicalModel()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
        ");
        Assert.AreEqual(1, SushiCompilerTestHelper.StructureDefinitions(resources).Count);
    }

    [TestMethod]
    public void ShouldAddSourceInfoForTheExportedLogicalModelToThePackage()
    {
        Assert.Inconclusive("SUSHI pkg.fshMap has no fsh-compiler equivalent.");
    }

    [TestMethod]
    public void ShouldExportMultipleLogicalModels()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo

            Logical: Bar
        ");
        Assert.AreEqual(2, SushiCompilerTestHelper.StructureDefinitions(resources).Count);
    }

    [TestMethod]
    public void ShouldStillExportLogicalModelsIfOneFails()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: Foo
            Parent: Baz

            Logical: Bar
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
    public void ShouldExportASingleLogicalModelWithBaseParentWhenParentNotDefined()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Base", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldExportASingleLogicalModelWithBaseParentById()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            Parent: Base
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Base", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldExportASingleLogicalModelWithBaseParentByUrl()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            Parent: http://hl7.org/fhir/StructureDefinition/Base
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Base", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldExportASingleLogicalModelWithElementParentById()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            Parent: Element
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Element", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldExportASingleLogicalModelWithElementParentByUrl()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            Parent: http://hl7.org/fhir/StructureDefinition/Element
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Element", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldExportASingleLogicalModelWithAnotherLogicalModelParentById()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            Parent: AlternateIdentification
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual(
            "http://hl7.org/fhir/cda/StructureDefinition/AlternateIdentification",
            sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldExportASingleLogicalModelWithAnotherLogicalModelParentByUrl()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            Parent: http://hl7.org/fhir/cda/StructureDefinition/AlternateIdentification
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual(
            "http://hl7.org/fhir/cda/StructureDefinition/AlternateIdentification",
            sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldExportASingleLogicalModelWithAComplexTypeParentById()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            Parent: Address
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Address", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldExportASingleLogicalModelWithAComplexTypeParentByUrl()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            Parent: http://hl7.org/fhir/StructureDefinition/Address
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Address", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldExportASingleLogicalModelWithAResourceParentById()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            Parent: Appointment
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Appointment", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldExportASingleLogicalModelWithAResourceParentByUrl()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            Parent: http://hl7.org/fhir/StructureDefinition/Appointment
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Appointment", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldLogAnErrorWithSourceInformationWhenTheParentIsInvalid()
    {
        // 'actualgroup' is a Profile in SUSHI test fixtures; port uses an arbitrary
        // Profile-as-parent which is not legal for a Logical model.
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: BadParent
            Parent: us-core-patient
        ");
        Assert.IsTrue(
            result is CompileResult<List<FhirResource>>.FailureResult
            || result.Warnings.Any(w => w.Message.ToLower().Contains("parent")),
            "Expected an error about a logical model with an invalid parent.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWithSourceInformationWhenTheParentIsNotFound()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: Bogus
            Parent: BogusParent
        ");
        bool hasError = result is CompileResult<List<FhirResource>>.FailureResult
                        || result.Warnings.Any(w => w.Message.Contains("BogusParent"));
        Assert.IsTrue(hasError);
    }

    [TestMethod]
    public void ShouldExportLogicalModelsWithFSHyParents()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo

            Logical: Bar
            Parent: Foo
        ");
        var sds = SushiCompilerTestHelper.StructureDefinitions(resources);
        Assert.AreEqual(2, sds.Count);
        var foo = sds.First(s => s.Name == "Foo");
        var bar = sds.First(s => s.Name == "Bar");
        Assert.AreEqual(foo.Url, bar.BaseDefinition);
    }

    [TestMethod]
    public void ShouldExportLogicalModelsWithTheSameFSHyParents()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo

            Logical: Bar
            Parent: Foo

            Logical: Baz
            Parent: Foo
        ");
        Assert.AreEqual(3, SushiCompilerTestHelper.StructureDefinitions(resources).Count);
    }

    [TestMethod]
    public void ShouldExportLogicalModelsWithDeepFSHyParents()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo

            Logical: Bar
            Parent: Foo

            Logical: Baz
            Parent: Bar
        ");
        Assert.AreEqual(3, SushiCompilerTestHelper.StructureDefinitions(resources).Count);
    }

    [TestMethod]
    public void ShouldExportLogicalModelsWithOutOfOrderFSHyParents()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            Parent: Bar

            Logical: Bar
            Parent: Baz

            Logical: Baz
        ");
        Assert.AreEqual(3, SushiCompilerTestHelper.StructureDefinitions(resources).Count);
    }

    [TestMethod]
    public void ShouldIncludeAddedElementHavingLogicalModelAsDatatypeWhenParentIsBaseWithoutRegardToDefinitionOrderFooThenBar()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            * bars 0..1 Bar ""short of property bars""

            Logical: Bar
            * length 0..1 Quantity ""short of property length""
            * width 0..1 Quantity ""short of property width""
        ");
        var foo = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(foo);
        var bars = foo.Differential?.Element.FirstOrDefault(e => e.Path == "Foo.bars");
        Assert.IsNotNull(bars);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/Bar",
            bars.Type?.FirstOrDefault()?.Code);
    }

    [TestMethod]
    public void ShouldIncludeAddedElementHavingLogicalModelAsDatatypeWhenParentIsBaseWithoutRegardToDefinitionOrderBarThenFoo()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Bar
            * length 0..1 Quantity ""short of property length""
            * width 0..1 Quantity ""short of property width""

            Logical: Foo
            * bars 0..1 Bar ""short of property bars""
        ");
        var foo = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(foo);
        var bars = foo.Differential?.Element.FirstOrDefault(e => e.Path == "Foo.bars");
        Assert.IsNotNull(bars);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/Bar",
            bars.Type?.FirstOrDefault()?.Code);
    }

    [TestMethod]
    public void ShouldIncludeAddedElementHavingLogicalModelAsDatatypeWhenParentIsElement()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            * bars 0..1 Bar ""short of property bars""

            Logical: Bar
            Parent: Element
            * length 0..1 Quantity ""short of property length""
            * width 0..1 Quantity ""short of property width""
        ");
        var foo = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(foo);
        var bars = foo.Differential?.Element.FirstOrDefault(e => e.Path == "Foo.bars");
        Assert.IsNotNull(bars);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/Bar",
            bars.Type?.FirstOrDefault()?.Code);
    }

    [TestMethod]
    public void ShouldIncludeAddedElementHavingLogicalModelAsDatatypeWhenParentIsAnotherLogicalModel()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            * bars 0..1 Bar ""short of property bars""

            Logical: Bar
            Parent: AlternateIdentification
            * length 0..1 Quantity ""short of property length""
            * width 0..1 Quantity ""short of property width""
        ");
        var foo = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(foo);
        var bars = foo.Differential?.Element.FirstOrDefault(e => e.Path == "Foo.bars");
        Assert.IsNotNull(bars);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/minimal/StructureDefinition/Bar",
            bars.Type?.FirstOrDefault()?.Code);
    }

    [TestMethod]
    public void ShouldNotReAddElementsThatAreDefinedOnTheParentLogicalModel()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: FishLogical
            * fins 0..1 boolean ""notes about fins""

            Logical: SharkLogical
            Parent: FishLogical
            * fins 0..1 boolean ""notes about shark fins""
            * name 0..1 string ""some sharks have names""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("fins") || w.Message.ToLower().Contains("already")),
            "Expected a warning about redefining fins on SharkLogical.");
    }

    [TestMethod]
    public void ShouldNotReAddElementsThatAreDefinedOnTheParentLogicalModelEvenWhenTheParentTypeIsOverwrittenWithACaretValueRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: FishLogical
            * fins 0..1 boolean ""notes about fins""

            Logical: SharkLogical
            Parent: FishLogical
            * ^type = ""http://hl7.org/fhir/us/minimal/StructureDefinition/FishLogical""
            * fins 0..1 boolean ""notes about shark fins""
            * name 0..1 string ""some sharks have names""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("fins") || w.Message.ToLower().Contains("already")),
            "Expected a warning about redefining fins.");
    }

    [TestMethod]
    public void ShouldHaveCorrectBaseAndTypesForEachNestedLogicalModel()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Other
            * thing 1..1 boolean ""Is it a thing?""

            Logical: FooFromOther
            Parent: Other
            * bars 0..* BarFromOther ""The bars of the foo""

            Logical: BarFromOther
            Parent: Other
            * height 1..1 Quantity ""The height of the bar""
        ");
        var other = SushiCompilerTestHelper.FindSd(resources, "Other");
        var foo = SushiCompilerTestHelper.FindSd(resources, "FooFromOther");
        var bar = SushiCompilerTestHelper.FindSd(resources, "BarFromOther");
        Assert.IsNotNull(other);
        Assert.IsNotNull(foo);
        Assert.IsNotNull(bar);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Base", other.BaseDefinition);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/StructureDefinition/Other", foo.BaseDefinition);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/StructureDefinition/Other", bar.BaseDefinition);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenAnInlineExtensionIsUsed()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: MyModel
            Parent: Element
            * extension contains SomeExtension 0..*
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("contains")
                || w.Message.ToLower().Contains("logical")
                || w.Message.ToLower().Contains("extension")),
            "Expected a warning about ContainsRule not permitted on Logical.");
    }

    [TestMethod]
    public void ShouldAllowConstraintsOnNewlyAddedElementsAndSubElements()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: ExampleModel
            Id: ExampleModel
            * name 0..* HumanName ""A person's full name""
            * name 1..1
            * name.given 1..1
        ");
        Assert.AreEqual(0, result.Warnings.Count(w => w.Message.ToLower().Contains("error")));
    }

    [TestMethod]
    public void ShouldAllowConstraintsOnRootElements()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: ExampleModel
            Id: ExampleModel
            * . ^alias = ""ExampleAlias""
        ");
        Assert.AreEqual(0, result.Warnings.Count(w => w.Message.ToLower().Contains("error")));
    }

    [TestMethod]
    public void ShouldAllowConstraintsOnInheritedElements()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            ValueSet: MyValueSet

            Invariant: MyInvariant
            Description: ""Is after 1900""
            Severity: #error

            Profile: MyStringProfile
            Parent: string

            Logical: MyTestModel
            Parent: ELTSSServiceModel
            Id: MyModel
            * backboneProp 0..* BackboneElement ""short of backboneProp""
            * backboneProp.name 1..1 HumanName ""short of backboneProp.name""
            * backboneProp.address 0..* Address ""short of backboneProp.address""
            * fundingSource MS
            * fundingSource 1..1
            * deliveryAddress only MyStringProfile
            * unitType from MyValueSet (required)
            * name = ""pet-sitting""
            * startDate obeys MyInvariant
            * startDate ^comment = ""Approximate is OK""
            * backboneProp.address MS
            * backboneProp.address 1..100
        ");
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void ShouldAddNewElementsAfterInheritedElements()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: MyTestModel
            Parent: ELTSSServiceModel
            Id: MyModel
            * backboneProp 0..* BackboneElement ""short of backboneProp""
            * backboneProp.name 1..1 HumanName ""short of backboneProp.name""
            * backboneProp.address 0..* Address ""short of backboneProp.address""
        ");
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenSlicingAnInheritedElement()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: MyModel
            Parent: ELTSSServiceModel
            * deliveryAddress contains home 0..*
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("contains") || w.Message.ToLower().Contains("logical")),
            "Expected a warning about slicing in a Logical.");
    }

    [TestMethod]
    public void ShouldExportALogicalModelWithCharacteristicsAndWarnThatTheyAreNotVerified()
    {
        Assert.Inconclusive(
            "Logical characteristics (structuredefinition-type-characteristics extension) " +
            "are not yet supported by the FSH grammar. " +
            "See https://build.fhir.org/ig/HL7/fhir-shorthand/ for `Characteristics:` syntax.");
    }

    [TestMethod]
    public void ShouldCreateLogicalRootElementWithShortEqualToTitleIfShortNotAvailableAndDefinitionEqualToDescriptionIfDefinitionNotAvailable()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyTestModel
            Id: MyModel
            Title: ""MyTestModel title is here""
            Description: ""MyTestModel description is here""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyTestModel");
        Assert.IsNotNull(sd);
        var root = sd.Differential?.Element.FirstOrDefault();
        Assert.IsNotNull(root);
        Assert.AreEqual("MyTestModel title is here", root.Short);
        Assert.AreEqual("MyTestModel description is here", root.Definition);
    }

    [TestMethod]
    public void ShouldCreateLogicalRootElementWithShortEqualToNameIfShortAndTitleNotAvailableAndDefinitionEqualToNameIfDescriptionAndDefinitionNotAvailable()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyTestModel
            Id: MyModel
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyTestModel");
        Assert.IsNotNull(sd);
        var root = sd.Differential?.Element.FirstOrDefault();
        Assert.IsNotNull(root);
        Assert.AreEqual("MyTestModel", root.Short);
        Assert.AreEqual("MyTestModel", root.Definition);
    }

    [TestMethod]
    public void ShouldCreateLogicalRootElementWithShortEqualToTitleIfShortNotAvailableAndDefinitionEqualToShortIfDescriptionAndDefinitionNotAvailable()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyTestModel
            Id: MyModel
            Title: ""MyTestModel title is here""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyTestModel");
        Assert.IsNotNull(sd);
        var root = sd.Differential?.Element.FirstOrDefault();
        Assert.IsNotNull(root);
        Assert.AreEqual("MyTestModel title is here", root.Short);
        Assert.AreEqual(root.Short, root.Definition);
    }

    [TestMethod]
    public void ShouldCreateLogicalRootElementWithShortEqualShortCaretRuleAndDefinitionEqualToDefinitionCaretRule()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyTestModel
            Id: MyModel
            Title: ""MyTestModel title is here""
            Description: ""MyTestModel description is here""
            * . ^short = ""Caret short value is here""
            * . ^definition = ""Caret definition value is here""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyTestModel");
        Assert.IsNotNull(sd);
        var root = sd.Differential?.Element.FirstOrDefault();
        Assert.IsNotNull(root);
        Assert.AreEqual("Caret short value is here", root.Short);
        Assert.AreEqual("Caret definition value is here", root.Definition);
    }

    [TestMethod]
    public void ShouldExportALogicalModelWithCharacteristics()
    {
        Assert.Inconclusive("See ShouldExportALogicalModelWithCharacteristicsAndWarnThatTheyAreNotVerified.");
    }

    [TestMethod]
    public void ShouldExportALogicalModelWithCharacteristicsAndWarnWhenACharacteristicIsNotFoundInTheCodeSystem()
    {
        Assert.Inconclusive("See ShouldExportALogicalModelWithCharacteristicsAndWarnThatTheyAreNotVerified.");
    }
}
