using FloatingTools.App.Diagnostics;

namespace FloatingTools.Tests.Diagnostics;

public sealed class DebugAiRequestTimingTests
{
    [Fact]
    public void Complete_WritesOrderedPhasesAndTotalOnce()
    {
        var timestamps = new Queue<long>([0, 10, 35, 50, 75]);
        var output = new List<string>();
        using var timing = DebugAiRequestTiming.CreateForTest(
            "quick_chat",
            "openai_stream",
            timestamps.Dequeue,
            output.Add,
            frequency: 1_000);

        timing.SetModel("gpt-5.6-sol");
        timing.SetHasImages(true);
        timing.Mark("http_request_started");
        timing.Mark("first_text_delta_received");
        timing.Mark("stream_completed");
        timing.Complete("success");
        timing.Complete("failure");

#if DEBUG
        var line = Assert.Single(output);
        Assert.Contains("tool=quick_chat", line);
        Assert.Contains("operation=openai_stream", line);
        Assert.Contains("outcome=success", line);
        Assert.Contains("model=gpt-5.6-sol", line);
        Assert.Contains("hasImages=true", line);
        Assert.Contains("totalMs=75.0", line);
        Assert.Contains(
            "phases=http_request_started@10.0(+10.0),"
            + "first_text_delta_received@35.0(+25.0),"
            + "stream_completed@50.0(+15.0)",
            line);
#else
        // [Conditional("DEBUG")] erases the Mark/Complete calls above from
        // this Release-compiled test assembly regardless of the enabled:true
        // bypass constructor — this is the real, correct Release behavior:
        // DEBUG-only instrumentation has zero footprint when the CALLER
        // (not just DebugAiRequestTiming itself) is built without DEBUG.
        Assert.Empty(output);
#endif
    }

    [Fact]
    public void Output_SanitizesMetadataAndNeverContainsRawContent()
    {
        var timestamps = new Queue<long>([0, 1]);
        var output = new List<string>();
        using var timing = DebugAiRequestTiming.CreateForTest(
            "translation",
            "request",
            timestamps.Dequeue,
            output.Add);

        timing.SetModel("model; secret user text\nשלום");
        timing.Complete("success");

#if DEBUG
        var line = Assert.Single(output);
        Assert.DoesNotContain("secret user text", line);
        Assert.DoesNotContain("שלום", line);
        Assert.DoesNotContain('\n', line);
        Assert.Contains("model=model__secret_user_text_____", line);
#else
        // Same Release-configuration reality as above: the SetModel/Complete
        // calls are erased at this call site, so no output — and therefore
        // no risk of the raw text ever appearing anywhere — is produced.
        Assert.Empty(output);
#endif
    }

    [Fact]
    public void DisabledTiming_IsANoOp()
    {
        var output = new List<string>();
        using var timing = DebugAiRequestTiming.CreateForTest(
            "translation",
            "request",
            static () => throw new InvalidOperationException("Clock was read."),
            output.Add,
            enabled: false);

        timing.SetModel("gpt-5.6-sol");
        timing.Mark("http_request_started");
        timing.Complete("success");

        Assert.Empty(output);
    }
}
