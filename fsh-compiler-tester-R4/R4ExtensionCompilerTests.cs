using Hl7.Fhir.Model;

namespace fsh_compiler_tester_r4;

/// <summary>
/// Tests compiling FSH Extension entities to FHIR R4 StructureDefinitions.
/// </summary>
[TestClass]
public class R4ExtensionCompilerTests
{
    [TestMethod]
    public void ShouldCompileSimpleExtension()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Title: ""My Extension""
            Description: ""A simple extension""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyExtension");
        Assert.AreEqual("MyExtension", sd.Name);
        Assert.AreEqual("Extension", sd.Type);
        Assert.AreEqual(StructureDefinition.StructureDefinitionKind.ComplexType, sd.Kind);
        Assert.AreEqual(StructureDefinition.TypeDerivationRule.Constraint, sd.Derivation);
    }

    [TestMethod]
    public void ShouldCompileExtensionWithParent()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Parent: Extension
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyExtension");
        Assert.AreEqual("Extension", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldCompileExtensionWithContext()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: Patient
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyExtension");
        Assert.IsNotNull(sd.Context, "Extension should have a Context");
        Assert.AreEqual(1, sd.Context.Count);
        Assert.AreEqual("Patient", sd.Context[0].Expression);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
    }

    [TestMethod]
    public void ShouldCompileExtensionWithCardinalityRule()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            * value[x] 0..1
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyExtension");
        var ed = CompilerTestHelper.GetElement(sd, "value[x]");
        Assert.AreEqual(0, ed.Min);
        Assert.AreEqual("1", ed.Max);
    }
}
