using System.Diagnostics;
using OpenTelemetry;

namespace WeaponsOfOrder.Api.Telemetry;

/// <summary>
/// Removes query strings from exported spans.
/// </summary>
/// <remarks>
/// <para>
/// Not a precaution — a concrete leak. A confirmation or reset link is
/// <c>/confirm-email?userId=…&amp;token=…</c>, a client route the server answers with the
/// React document. The token is a single-use bearer credential, and following that link is
/// an ordinary navigation this application handles, so without this every confirmation and
/// every password reset would write its token into Application Insights, where it is
/// retained, queryable and exportable.
/// </para>
/// <para>
/// AUTH_SECURITY.md already forbids logging tokens; telemetry is a log with a different
/// name. Nothing here needs the query string, so the whole thing goes rather than a list of
/// parameter names somebody has to remember to extend.
/// </para>
/// </remarks>
internal sealed class QueryStringRedactingProcessor : BaseProcessor<Activity>
{
    /// <summary>The OpenTelemetry semantic-convention tag carrying the query on its own.</summary>
    private const string UrlQueryTag = "url.query";

    /// <summary>
    /// Tags that carry a whole URL. Present on outgoing HTTP spans, and written by older
    /// instrumentation for incoming ones.
    /// </summary>
    private static readonly string[] FullUrlTags = ["url.full", "http.url"];

    public override void OnEnd(Activity activity)
    {
        activity.SetTag(UrlQueryTag, null);

        foreach (var tag in FullUrlTags)
        {
            if (activity.GetTagItem(tag) is string url && TryStripQuery(url, out var stripped))
            {
                activity.SetTag(tag, stripped);
            }
        }
    }

    /// <summary>
    /// Trims at the first <c>?</c> or <c>#</c>. Deliberately textual: a value that does not
    /// parse as a URL still must not be exported with its query intact.
    /// </summary>
    private static bool TryStripQuery(string url, out string stripped)
    {
        var cut = url.AsSpan().IndexOfAny('?', '#');

        if (cut < 0)
        {
            stripped = url;
            return false;
        }

        stripped = url[..cut];
        return true;
    }
}
