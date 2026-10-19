using System;
using Wingmate.Core;
using Xunit;

public class FeedbackTests
{
    [Fact] public void Url_IsNewIssueWithEncodedBody()
    {
        var url = Feedback.IssueUrl("0.1.0", "2026-10-12", "10.0.22631.0");
        Assert.StartsWith("https://github.com/BeeSpecial3D/Wingmate/issues/new?body=", url);
        var body = Uri.UnescapeDataString(url.Substring(url.IndexOf("?body=") + 6));
        Assert.Contains("**Wingmate:** 0.1.0", body);
        Assert.Contains("**Schedule:** 2026-10-12", body);
        Assert.Contains("**Windows:** 10.0.22631.0", body);
        Assert.Contains("### What happened", body);
        Assert.Contains("### What did you expect", body);
        Assert.DoesNotContain(" ", url); Assert.DoesNotContain("\n", url); Assert.DoesNotContain("#", url);
        Assert.True(url.Length < 400, url.Length.ToString());
    }

    [Fact] public void MissingOrOddVersions_StayShortAndSafe()
    {
        var body = Feedback.Body(null, "", "x\ny" + new string('9', 100));
        Assert.Contains("**Wingmate:** unknown", body);
        Assert.Contains("**Schedule:** unknown", body);
        Assert.Contains("**Windows:** xy" + new string('9', 38) + "\n", body);
        Assert.True(Feedback.IssueUrl(new string('a', 500), new string('b', 500), new string('c', 500)).Length < 600);
    }
}
