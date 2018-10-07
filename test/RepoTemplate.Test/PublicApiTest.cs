extern alias RepoTemplate;
using NUnit.Framework;
using PublicApiGenerator;
using Shouldly;
using System.Diagnostics.CodeAnalysis;

namespace RepoTemplate.Test
{
    [ExcludeFromCodeCoverage]
    public sealed class PublicApiTest
    {
        [Test]
        public void IsApproved() =>
            ApiGenerator
                .GeneratePublicApi(
                    typeof(RepoTemplate::ThisAssembly).Assembly,
                    shouldIncludeAssemblyAttributes: false)
                .ShouldMatchApproved(c => c
                    .WithFilenameGenerator((_, __, fileType, extension) =>
                        $"PublicApi.{fileType}.{extension}"));
    }
}
