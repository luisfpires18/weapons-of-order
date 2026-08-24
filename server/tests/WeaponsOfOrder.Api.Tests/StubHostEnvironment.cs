using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// Just enough <see cref="IHostEnvironment"/> to ask an options validator what it does in a
/// given environment, without standing up a host to find out.
/// </summary>
internal sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "WeaponsOfOrder.Api.Tests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } =
        new PhysicalFileProvider(AppContext.BaseDirectory);
}
