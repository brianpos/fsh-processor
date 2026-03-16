using Hl7.Fhir.Model;

namespace fsh_compiler_tester_r4;

/// <summary>
/// Tests compiling FSH Logical and Resource entities to FHIR R4 StructureDefinitions,
/// covering LrCardRule, LrFlagRule, AddElementRule, and AddCRElementRule.
/// </summary>
[TestClass]
public class R4LogicalCompilerTests
{
    // ─── Logical model metadata ───────────────────────────────────────────────

    [TestMethod]
    public void ShouldCompileSimpleLogical()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            Title: ""My Logical Model""
            Description: ""A simple logical model""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyModel");
        Assert.AreEqual("MyModel", sd.Name);
        Assert.AreEqual(StructureDefinition.StructureDefinitionKind.Logical, sd.Kind);
        Assert.AreEqual(StructureDefinition.TypeDerivationRule.Specialization, sd.Derivation);
        Assert.AreEqual("My Logical Model", sd.Title);
    }

    // ─── AddElementRule ───────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyAddElementRule()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            * name 0..1 string ""Patient name""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyModel");
        var ed = CompilerTestHelper.GetElement(sd, "name");
        Assert.AreEqual(0, ed.Min);
        Assert.AreEqual("1", ed.Max);
        Assert.AreEqual("Patient name", ed.Short);
        Assert.IsNotNull(ed.Type);
        Assert.AreEqual("string", ed.Type[0].Code);
    }

    [TestMethod]
    public void ShouldApplyAddElementRuleWithDefinition()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            * birthDate 0..1 date ""Birth Date"" ""The date of birth of the subject.""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyModel");
        var ed = CompilerTestHelper.GetElement(sd, "birthDate");
        Assert.AreEqual("Birth Date", ed.Short);
        Assert.AreEqual("The date of birth of the subject.", ed.Definition);
    }

    // ─── LrCardRule ──────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyLrCardRuleOnLogical()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            * name 0..1 string ""Patient name""
            * name 1..1
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyModel");
        var ed = CompilerTestHelper.GetElement(sd, "name");
        Assert.AreEqual(1, ed.Min);
        Assert.AreEqual("1", ed.Max);
    }

    // ─── LrFlagRule ──────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyLrFlagRuleOnLogical()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            * name 0..1 string ""Patient name""
            * name MS
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyModel");
        var ed = CompilerTestHelper.GetElement(sd, "name");
        Assert.IsTrue(ed.MustSupport);
    }

    // ─── AddCRElementRule ────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyAddCRElementRule()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            * relatedItem 0..* contentReference #MyModel.name ""Related item""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyModel");
        var ed = CompilerTestHelper.GetElement(sd, "relatedItem");
        Assert.AreEqual(0, ed.Min);
        Assert.AreEqual("*", ed.Max);
        Assert.AreEqual("#MyModel.name", ed.ContentReference);
        Assert.AreEqual("Related item", ed.Short);
    }
}
