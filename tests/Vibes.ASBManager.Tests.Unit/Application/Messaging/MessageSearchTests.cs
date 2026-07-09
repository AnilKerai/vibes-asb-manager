using Vibes.ASBManager.Application.Messaging;
using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Tests.Unit.Application.Messaging;

public class MessageSearchTests
{
    private static MessagePreview Msg(
        string? correlationId = null, string? subject = null,
        string? deadLetterReason = null, string? messageId = null, string? body = null)
        => new()
        {
            SequenceNumber = 1,
            EnqueuedTime = DateTimeOffset.UnixEpoch,
            CorrelationId = correlationId,
            Subject = subject,
            DeadLetterReason = deadLetterReason,
            MessageId = messageId,
            Body = body
        };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_term_matches_everything(string? term)
        => Assert.True(MessageSearch.Matches(Msg(subject: "anything"), term));

    [Fact]
    public void Matches_on_body_content()
        => Assert.True(MessageSearch.Matches(Msg(body: "{\"orderId\":\"abc-123\"}"), "abc-123"));

    [Fact]
    public void Matches_on_dead_letter_reason()
        => Assert.True(MessageSearch.Matches(Msg(deadLetterReason: "MaxDeliveryCountExceeded"), "maxdelivery"));

    [Fact]
    public void Matches_on_correlation_id()
        => Assert.True(MessageSearch.Matches(Msg(correlationId: "corr-99"), "corr-99"));

    [Fact]
    public void Matches_on_subject()
        => Assert.True(MessageSearch.Matches(Msg(subject: "OrderPlaced"), "orderplaced"));

    [Fact]
    public void Matches_on_message_id()
        => Assert.True(MessageSearch.Matches(Msg(messageId: "msg-77"), "msg-77"));

    [Fact]
    public void Is_case_insensitive()
        => Assert.True(MessageSearch.Matches(Msg(subject: "TimeoutError"), "TIMEOUT"));

    [Fact]
    public void Trims_surrounding_whitespace_from_the_term()
        => Assert.True(MessageSearch.Matches(Msg(correlationId: "abc"), "  abc  "));

    [Fact]
    public void Returns_false_when_no_field_contains_the_term()
        => Assert.False(MessageSearch.Matches(Msg(subject: "hello", body: "world"), "zzz"));

    [Fact]
    public void Null_fields_do_not_throw()
        => Assert.False(MessageSearch.Matches(Msg(), "anything"));
}
