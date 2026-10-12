namespace Sanctuary.WebAPI.Options;

public class WebAPIOptions
{
    public const string Section = "WebAPI";

    public string? LaunchArguments { get; set; }

    public bool? MemberByDefault { get; set; }

    /// <summary>
    /// Shared secret used to verify that a request to /login or /register
    /// actually came from an official Clover launcher (or a trusted
    /// server-to-server caller, like the website) rather than a script
    /// calling this API directly. Set via the WebAPI__LauncherSharedSecret
    /// environment variable (see src/Docker/.env) - never put a real
    /// value here, since this file is tracked in git.
    /// </summary>
    public string? LauncherSharedSecret { get; set; }
}
