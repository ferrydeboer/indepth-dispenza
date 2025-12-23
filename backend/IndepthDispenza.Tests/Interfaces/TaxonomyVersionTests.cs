using System.Text.Json;
using InDepthDispenza.Functions.Interfaces;
using NUnit.Framework;

namespace IndepthDispenza.Tests.Interfaces;

public class TaxonomyVersionTests
{
    [TestCase("v1.0", 1, 0)]
    [TestCase("1.2", 1, 2)]
    [TestCase("V10.20", 10, 20)]
    public void Parse_ValidStrings_YieldExpectedMajorMinor(string input, int major, int minor)
    {
        var ok = TaxonomyVersion.TryParse(input, out var v);
        Assert.That(ok, Is.True);
        Assert.That(v.Major, Is.EqualTo(major));
        Assert.That(v.Minor, Is.EqualTo(minor));
        Assert.That(v.ToString(), Is.EqualTo($"v{major}.{minor}"));
    }

    [TestCase("")]
    [TestCase("v")]
    [TestCase("1")]
    [TestCase("1.0.0")]
    [TestCase("v1.")]
    [TestCase(".1")]
    [TestCase("1.a")]
    [TestCase("a.b")]
    public void Parse_InvalidStrings_ReturnsFalse(string input)
    {
        var ok = TaxonomyVersion.TryParse(input, out _);
        Assert.That(ok, Is.False);
        Assert.Throws<FormatException>(() => TaxonomyVersion.Parse(input));
    }

    [Test]
    public void Comparison_Works_AsExpected()
    {
        TaxonomyVersion v10 = "v1.0";
        TaxonomyVersion v11 = "1.1";
        TaxonomyVersion v20 = "v2.0";
        TaxonomyVersion v109 = "v1.9";

        Assert.That(v11, Is.GreaterThan(v10));
        Assert.That(v20, Is.GreaterThan(v109));
        Assert.That(v10, Is.LessThan(v11));
        Assert.That(v10, Is.LessThanOrEqualTo(v11));
        Assert.That(v11, Is.GreaterThanOrEqualTo(v10));
        Assert.That(v10 == (TaxonomyVersion)"1.0", Is.True);
        Assert.That(v10 != v11, Is.True);
    }

    [Test]
    public void ImplicitConversions_Work()
    {
        TaxonomyVersion v = "1.3"; // from string
        string s = v; // to string
        Assert.That(s, Is.EqualTo("v1.3"));
    }

    [Test]
    public void SystemTextJson_Roundtrip_Works()
    {
        var v = (TaxonomyVersion)"v3.4";
        var opts = new JsonSerializerOptions();
        opts.Converters.Add(new SystemTextJsonTaxonomyVersionConverter());
        var json = JsonSerializer.Serialize(v, opts);
        Assert.That(json, Is.EqualTo("\"v3.4\""));
        var back = JsonSerializer.Deserialize<TaxonomyVersion>(json, opts);
        Assert.That(back, Is.EqualTo(v));
    }

    [Test]
    public void NewtonsoftJson_Roundtrip_Works()
    {
        var v = (TaxonomyVersion)"2.5";
        var settings = new Newtonsoft.Json.JsonSerializerSettings();
        settings.Converters.Add(new NewtonsoftTaxonomyVersionConverter());
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(v, settings);
        Assert.That(json, Is.EqualTo("\"v2.5\""));
        var back = Newtonsoft.Json.JsonConvert.DeserializeObject<TaxonomyVersion>(json, settings);
        Assert.That(back, Is.EqualTo(v));
    }

    [Test]
    public void IncrementMinor_ReturnsNew_WithMinorIncremented_AndOriginalUnchanged()
    {
        var original = new TaxonomyVersion(1, 0);
        var incremented = original.IncrementMinor();
        Assert.That(incremented.ToString(), Is.EqualTo("v1.1"));
        // immutability
        Assert.That(original.ToString(), Is.EqualTo("v1.0"));
        // another example
        TaxonomyVersion v = "v2.9";
        var inc = v.IncrementMinor();
        Assert.That(inc.ToString(), Is.EqualTo("v2.10"));
    }
}
