using System.Net;
using System.Text;
using DzhusShelter.MainBot.Birthdays;
using FluentAssertions;

namespace DzhusShelter.MainBot.Tests;

public class BirthdayNotificationsApiClientTests
{
    private sealed class StubHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static BirthdayNotificationsApiClient Client(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://api.test") });

    [Fact]
    public async Task GetPendingAsync_DeserializesStringEnumAndDateOnly()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """[{"kind":"Today","forDate":"2026-11-12","people":[{"name":"Саша Пупкін","age":25},{"name":"Оля Коваль","age":null}]}]""");

        var result = await Client(handler).GetPendingAsync(CancellationToken.None);

        handler.Request!.Method.Should().Be(HttpMethod.Get);
        handler.Request.RequestUri!.AbsolutePath.Should().Be("/api/birthdays/notifications/pending");
        var n = result.Should().ContainSingle().Subject;
        n.Kind.Should().Be(BirthdayNotificationKind.Today);
        n.ForDate.Should().Be(new DateOnly(2026, 11, 12));
        n.People.Select(p => p.Age).Should().Equal(25, null);
        n.People[0].Name.Should().Be("Саша Пупкін");
    }

    [Fact]
    public async Task GetPendingAsync_NonSuccess_Throws()
    {
        var act = () => Client(new StubHandler(HttpStatusCode.Unauthorized)).GetPendingAsync(CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task MarkSentAsync_PostsKindAndDate()
    {
        var handler = new StubHandler(HttpStatusCode.NoContent);

        await Client(handler).MarkSentAsync(BirthdayNotificationKind.Today, new DateOnly(2026, 11, 12), CancellationToken.None);

        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.AbsolutePath.Should().Be("/api/birthdays/notifications/sent");
        handler.RequestBody.Should().Be("""{"kind":"Today","forDate":"2026-11-12"}""");
    }

    [Fact]
    public async Task MarkSentAsync_NonSuccess_Throws()
    {
        var act = () => Client(new StubHandler(HttpStatusCode.BadRequest))
            .MarkSentAsync(BirthdayNotificationKind.Today, new DateOnly(2026, 11, 12), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
