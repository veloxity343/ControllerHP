using System.Text.RegularExpressions;

namespace WattsLeft;

/// <summary>A newer release: its version, and its page on GitHub.</summary>
internal sealed record UpdateInfo(Version Version, string Url);

/// <summary>
/// Asks GitHub which release is WattsLeft's latest, and says whether it's newer than this one. It only ever
/// tells the user: nothing is downloaded or run.
/// </summary>
/// <remarks>
/// GitHub answers <c>/releases/latest</c> with a redirect to that release's page, which names its tag, so one
/// HTTPS request carrying nothing about the user or their devices is enough. Only that address is read, and
/// only if it's exactly a release page of this repository with a plain version tag; the reply's body is never
/// read, and the page opened is built from the version, not taken from the reply. This also avoids GitHub's
/// API, whose limit of 60 requests an hour is shared by everyone on the same connection.
/// </remarks>
internal static partial class UpdateCheck
{
    private const string Repository = "veloxity343/WattsLeft";
    private static readonly Uri LatestRelease = new($"https://github.com/{Repository}/releases/latest");
    private const string ReleasePage = $"https://github.com/{Repository}/releases/tag/";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>A full release's page: this repository, then v and three numbers. Anything else is ignored.</summary>
    [GeneratedRegex(@"^https://github\.com/veloxity343/WattsLeft/releases/tag/v(\d{1,5})\.(\d{1,5})\.(\d{1,5})$")]
    private static partial Regex ReleasePagePattern();

    /// <summary>This app's version, without any pre-release or build suffix.</summary>
    public static Version Current { get; } = ParseVersion(AppInfo.Version.Split('-', '+')[0]) ?? new Version(0, 0, 0);

    /// <returns>The latest release if it's newer than this one; null if this is the latest.</returns>
    /// <exception cref="Exception">GitHub couldn't be reached, or didn't point to a release.</exception>
    public static async Task<UpdateInfo?> FindNewerAsync()
    {
        // The redirect is the answer, so it isn't followed.
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var http = new HttpClient(handler) { Timeout = Timeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"WattsLeft/{Current.ToString(3)}");
        using var request = new HttpRequestMessage(HttpMethod.Head, LatestRelease);
        using var reply = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if ((int)reply.StatusCode is < 300 or >= 400 || reply.Headers.Location is not { } location)
            throw new HttpRequestException($"GitHub didn't point to a release ({(int)reply.StatusCode} {reply.ReasonPhrase})");
        string address = (location.IsAbsoluteUri ? location : new Uri(LatestRelease, location)).AbsoluteUri;
        if (ReleasePagePattern().Match(address) is not { Success: true } match)
            throw new InvalidDataException($"GitHub pointed somewhere unexpected: {address}");
        var latest = new Version(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));
        return latest > Current ? new UpdateInfo(latest, ReleasePageFor(latest)) : null;
    }

    /// <summary>The release page for a version, as WattsLeft builds it.</summary>
    public static string ReleasePageFor(Version version) => $"{ReleasePage}v{version.ToString(3)}";

    /// <summary>A version as WattsLeft stores it, "1.2.3"; null if it isn't one.</summary>
    public static Version? ParseVersion(string? text) =>
        Version.TryParse(text, out var version) && version.Build >= 0 && version.Revision < 0 ? version : null;
}
