using Hl7.Fhir.Model;
using FhirValueSet = Hl7.Fhir.Model.ValueSet;

namespace fsh_compiler_tester_r4;

/// <summary>
/// Tests compiling FSH ValueSet entities to FHIR R4 ValueSet resources.
/// </summary>
[TestClass]
public class R4ValueSetCompilerTests
{
    [TestMethod]
    public void ShouldCompileSimpleValueSet()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            ValueSet: MyValueSet
            Title: ""My Value Set""
            Description: ""A simple value set""
        ");
        var vs = CompilerTestHelper.GetValueSet(resources, "MyValueSet");
        Assert.AreEqual("MyValueSet", vs.Name);
        Assert.AreEqual("My Value Set", vs.Title);
        Assert.AreEqual("A simple value set", vs.Description);
        Assert.AreEqual(PublicationStatus.Active, vs.Status);
    }

    [TestMethod]
    public void ShouldCompileValueSetWithInclude()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            ValueSet: MyValueSet
            * include codes from system http://loinc.org
        ");
        var vs = CompilerTestHelper.GetValueSet(resources, "MyValueSet");
        Assert.IsNotNull(vs.Compose);
        Assert.AreEqual(1, vs.Compose.Include.Count);
        Assert.AreEqual("http://loinc.org", vs.Compose.Include[0].System);
    }

    [TestMethod]
    public void ShouldCompileValueSetWithExclude()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            ValueSet: MyValueSet
            * include codes from system http://loinc.org
            * exclude codes from system http://snomed.info/sct
        ");
        var vs = CompilerTestHelper.GetValueSet(resources, "MyValueSet");
        Assert.IsNotNull(vs.Compose);
        Assert.AreEqual(1, vs.Compose.Include.Count);
        Assert.AreEqual(1, vs.Compose.Exclude.Count);
        Assert.AreEqual("http://snomed.info/sct", vs.Compose.Exclude[0].System);
    }

    [TestMethod]
    public void ShouldCompileValueSetWithCaretValueTitle()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            ValueSet: MyValueSet
            * ^title = ""Override Title""
        ");
        var vs = CompilerTestHelper.GetValueSet(resources, "MyValueSet");
        Assert.AreEqual("Override Title", vs.Title);
    }
}
