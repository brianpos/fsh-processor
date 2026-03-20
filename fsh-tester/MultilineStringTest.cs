using Microsoft.VisualStudio.TestTools.UnitTesting;
using fsh_processor;
using fsh_processor.Models;

namespace fsh_tester;

[TestClass]
public class MultilineStringTest
{
    [TestMethod]
    public void TestMultilineStringParsing()
    {
        // Test that triple-quoted strings have their delimiters properly removed
        var fsh = @"
Profile: TestProfile
Description: """"""This is a
multiline
description""""""
";
        
        var result = FshParser.Parse(fsh);
        Assert.IsInstanceOfType<ParseResult.Success>(result, "Parse should succeed");
        
        var doc = ((ParseResult.Success)result).Document;
        Assert.AreEqual(1, doc.Entities.Count, "Should have one entity");
        
        var profile = doc.Entities[0] as Profile;
        Assert.IsNotNull(profile, "Entity should be a Profile");
        
        // The description should NOT contain the triple quotes
        Assert.IsNotNull(profile.Description, "Description should not be null");
        Assert.IsFalse(profile.Description.Value.StartsWith("\"\"\""), "Description should not start with triple quotes");
        Assert.IsFalse(profile.Description.Value.EndsWith("\"\"\""), "Description should not end with triple quotes");
        
        // Normalize line endings for comparison (Windows uses \r\n)
        var normalizedDescription = profile.Description.Value.Replace("\r\n", "\n");
        Assert.AreEqual("This is a\nmultiline\ndescription", normalizedDescription, "Description should be extracted correctly");
    }

    [TestMethod]
    public void TestSingleQuoteStringParsing()
    {
        // Test that single-quoted strings still work correctly
        var fsh = @"
Profile: TestProfile
Description: ""This is a single line description""
";
        
        var result = FshParser.Parse(fsh);
        Assert.IsInstanceOfType<ParseResult.Success>(result, "Parse should succeed");
        
        var doc = ((ParseResult.Success)result).Document;
        Assert.AreEqual(1, doc.Entities.Count, "Should have one entity");
        
        var profile = doc.Entities[0] as Profile;
        Assert.IsNotNull(profile, "Entity should be a Profile");
        
        // The description should NOT contain the quotes
        Assert.IsNotNull(profile.Description, "Description should not be null");
        Assert.IsFalse(profile.Description.Value.StartsWith("\""), "Description should not start with quote");
        Assert.IsFalse(profile.Description.Value.EndsWith("\""), "Description should not end with quote");
        Assert.AreEqual("This is a single line description", profile.Description.Value, "Description should be extracted correctly");
    }

    [TestMethod]
    public void TestRoundTripMultilineString()
    {
        // Test that multiline strings can be parsed, serialized, and re-parsed
        var fsh = @"
Profile: TestProfile
Description: """"""This is a
multiline
description""""""
";
        
        // First parse
        var parseResult = FshParser.Parse(fsh);
        Assert.IsInstanceOfType<ParseResult.Success>(parseResult, "Initial parse should succeed");
        var doc = ((ParseResult.Success)parseResult).Document;
        
        // Serialize
        var serialized = FshSerializer.Serialize(doc);
        
        // Re-parse
        var reParseResult = FshParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult, "Re-parse should succeed");
        
        var reDoc = ((ParseResult.Success)reParseResult).Document;
        var originalProfile = doc.Entities[0] as Profile;
        var reParsedProfile = reDoc.Entities[0] as Profile;
        
        Assert.IsNotNull(originalProfile, "Original should be a Profile");
        Assert.IsNotNull(reParsedProfile, "Re-parsed should be a Profile");
        Assert.AreEqual(originalProfile.Description.Value, reParsedProfile.Description.Value, "Descriptions should match after round-trip");
    }

    [TestMethod]
    public void TestMultilineStringFormatPreservation()
    {
        // Verify that a triple-quoted description is serialized back as triple-quoted
        var fsh = "Profile: TestProfile\nDescription: \"\"\"This is a\nmultiline\ndescription\"\"\"";

        var parseResult = FshParser.Parse(fsh);
        Assert.IsInstanceOfType<ParseResult.Success>(parseResult, "Initial parse should succeed");
        var doc = ((ParseResult.Success)parseResult).Document;

        var profile = doc.Entities[0] as Profile;
        Assert.IsNotNull(profile);
        Assert.IsTrue(profile.Description!.IsMultiline, "IsMultiline should be set to true after parsing a triple-quoted string");

        var serialized = FshSerializer.Serialize(doc);
        Console.WriteLine("=== SERIALIZED ===");
        Console.WriteLine(serialized);

        // The serialized output should contain triple-quoted string, not escaped single-quoted
        Assert.IsTrue(serialized.Contains("\"\"\"This is a"), "Serialized output should preserve triple-quote format");
        Assert.IsFalse(serialized.Contains("\\n"), "Serialized multiline string should not contain escaped newlines");

        // Round-trip: re-parse and verify
        var reParseResult = FshParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult, "Re-parse should succeed");
        var reDoc = ((ParseResult.Success)reParseResult).Document;
        var reProfile = reDoc.Entities[0] as Profile;
        Assert.IsNotNull(reProfile);
        Assert.AreEqual(profile.Description.Value, reProfile!.Description!.Value, "Content should survive round-trip");
        Assert.IsTrue(reProfile.Description.IsMultiline, "IsMultiline should be preserved after round-trip");
    }

    [TestMethod]
    public void TestSingleLineStringStaysSingleLine()
    {
        // Verify that a regular quoted description stays as regular quoted
        var fsh = "Profile: TestProfile\nDescription: \"A simple description\"";

        var parseResult = FshParser.Parse(fsh);
        Assert.IsInstanceOfType<ParseResult.Success>(parseResult, "Parse should succeed");
        var doc = ((ParseResult.Success)parseResult).Document;

        var profile = doc.Entities[0] as Profile;
        Assert.IsNotNull(profile);
        Assert.IsFalse(profile.Description!.IsMultiline, "IsMultiline should be false for single-line string");

        var serialized = FshSerializer.Serialize(doc);

        // Should NOT contain triple quotes
        Assert.IsFalse(serialized.Contains("\"\"\""), "Single-line string should not become triple-quoted");
        Assert.IsTrue(serialized.Contains("\"A simple description\""), "Should contain regular quoted string");
    }

    [TestMethod]
    public void TestStringValueMultilineFormatPreservation()
    {
        // Verify that a multiline string value in a fixed-value rule preserves format
        var fsh = "Profile: TestProfile\nParent: Patient\n\n* extension[0].valueString = \"\"\"Line one\nLine two\nLine three\"\"\"";

        var parseResult = FshParser.Parse(fsh);
        Assert.IsInstanceOfType<ParseResult.Success>(parseResult, "Parse should succeed");
        var doc = ((ParseResult.Success)parseResult).Document;

        var serialized = FshSerializer.Serialize(doc);
        Console.WriteLine("=== SERIALIZED ===");
        Console.WriteLine(serialized);

        // Should contain triple quotes in the output
        Assert.IsTrue(serialized.Contains("\"\"\"Line one"), "Should preserve multiline value format");

        // Round-trip re-parse
        var reParseResult = FshParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult, "Re-parse should succeed");
    }
}
